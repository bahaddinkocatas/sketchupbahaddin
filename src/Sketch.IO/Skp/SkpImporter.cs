using Sketch.Geometry;
using Sketch.Model;
using Sketch.Model.Templates;
using static Sketch.IO.Skp.SkpApi;

namespace Sketch.IO.Skp;

/// <summary>Result of reading a .skp file.</summary>
public sealed record SkpImportResult(int Faces, int Edges, int Groups, IReadOnlyList<string> Tags, IReadOnlyList<string> Materials,
    int SkippedFaces = 0, int Images = 0);

/// <summary>
/// Reads SketchUp (.skp) files: faces (with holes), edges (soft/smooth), groups and components (components become
/// groups with their geometry copied), images (e.g. floor plans to trace over), tags with visibility, face/group
/// materials and hidden flags. Geometry is converted to world coordinates. 2021+ files are read directly by
/// <see cref="SkpNativeReader"/>; the SketchUp C API below is the fallback for older files.
/// </summary>
public static class SkpImporter
{
    /// <summary>Where SketchUpAPI.dll is looked for: next to the app (SDK copy), then installed SketchUp versions.</summary>
    public static string? FindApiLibrary()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "SketchUpSDK", "SketchUpAPI.dll"),
            Path.Combine(AppContext.BaseDirectory, "SketchUpAPI.dll"),
        };
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var root = Path.Combine(programFiles, "SketchUp");
        if (Directory.Exists(root))
        {
            candidates.AddRange(Directory.GetDirectories(root, "SketchUp 20*")
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => Path.Combine(d, "SketchUp", "SketchUpAPI.dll")));
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Imports the file's content into <paramref name="model"/>'s root (tags and materials are added as needed).</summary>
    // The C API is initialised and terminated around each read and is not meant for concurrent use: one read at a time.
    private static readonly object Gate = new();

    /// <summary>
    /// Reads a .skp file into the model. SketchUp 2021 and newer files are read by Sketch's own reader
    /// (<see cref="SkpNativeReader"/>, no SketchUp needed); older files go through the SketchUp C API when SketchUp
    /// (or its SDK) is installed. <paramref name="apiLibrary"/> forces the C API with that library.
    /// </summary>
    public static SkpImportResult Import(string path, SketchModel model, string? apiLibrary = null)
    {
        lock (Gate)
        {
            if (apiLibrary is null && SkpNativeReader.CanRead(path)) return SkpNativeReader.Read(path, model);
            return ImportLocked(path, model, apiLibrary);
        }
    }

    private static SkpImportResult ImportLocked(string path, SketchModel model, string? apiLibrary)
    {
        var dll = apiLibrary ?? FindApiLibrary()
                  ?? throw new FileNotFoundException(
                      "This file was saved by SketchUp 2020 or older. Such files can only be read through SketchUp's own library " +
                      "(SketchUpAPI.dll), which was not found. Open and save the file with SketchUp 2021 or newer, or install SketchUp.");
        UseLibrary(dll);
        SUInitialize();
        var suModel = IntPtr.Zero;
        try
        {
            var result = SUModelCreateFromFile(ref suModel, path);
            if (result != Ok) throw new InvalidDataException($"The SketchUp file could not be read (code {result}).");
            var reader = new Reader(model);
            reader.ReadLayers(suModel);
            var state = reader.ReadEntities(Get(suModel, SUModelGetEntities), Matrix4d.Identity);
            model.Entities.Import(state);
            return new SkpImportResult(reader.FaceCount, reader.EdgeCount, reader.GroupCount, reader.TagNames.ToList(), reader.MaterialNames.ToList(),
                reader.SkippedFaces, reader.ImageCount);
        }
        finally
        {
            if (suModel != IntPtr.Zero) SUModelRelease(ref suModel);
            SUTerminate();
        }
    }

    private sealed class Reader
    {
        private readonly SketchModel _model;
        private int _nextId = 1;
        public int FaceCount, EdgeCount, GroupCount, SkippedFaces, ImageCount;
        public readonly HashSet<string> TagNames = new();
        public readonly HashSet<string> MaterialNames = new();

        public Reader(SketchModel model) => _model = model;

        public void ReadLayers(IntPtr suModel)
        {
            foreach (var layer in GetList(suModel, SUModelGetNumLayers, SUModelGetLayers))
            {
                var tag = TagFor(layer);
                if (tag is not null && SULayerGetVisibility(layer, out var visible) == Ok) tag.Visible = visible;
            }
            _model.Tags.RaiseChanged();
        }

        private Tag? TagFor(IntPtr layer)
        {
            if (layer == IntPtr.Zero) return null;
            var name = GetString(layer, SULayerGetName);
            if (string.IsNullOrEmpty(name) || name is "Layer0" or "Untagged" or "Etiketsiz") return null;
            TagNames.Add(name);
            return _model.Tags.Find(name)
                   ?? _model.Tags.Add(name, Sketch.Model.Templates.ThermalTemplate.DefaultColor(name, _model.Tags.Items.Count));
        }

        private string? TagName(IntPtr element) => TagFor(Get(element, SUDrawingElementGetLayer))?.Name;

        private string? MaterialName(IntPtr material)
        {
            if (material == IntPtr.Zero) return null;
            var name = GetString(material, SUMaterialGetName);
            if (string.IsNullOrEmpty(name)) return null;
            // Glass and wood/door materials become the template's Glass and Door (windows and doors for DEAP).
            if (ThermalTemplate.OpeningMaterial(name) is { } opening)
            {
                ThermalTemplate.OpeningMaterial(_model, opening);
                MaterialNames.Add(opening);
                return opening;
            }
            if (_model.Materials.Find(name) is null)
            {
                var color = SUMaterialGetColor(material, out var c) == Ok ? new Rgb(c.Red, c.Green, c.Blue) : new Rgb(200, 200, 200);
                var alpha = SUMaterialGetOpacity(material, out var a) == Ok ? a : 1;
                _model.Materials.Add(name, color, alpha);
            }
            MaterialNames.Add(name);
            return name;
        }

        private static bool Hidden(IntPtr element) => SUDrawingElementGetHidden(element, out var h) == Ok && h;

        /// <summary>SUTransformation to our matrix (handles both column- and row-major storage).</summary>
        private static Matrix4d ToMatrix(Transformation t)
        {
            var v = t.Values;
            var columnMajor = Math.Abs(v[3]) + Math.Abs(v[7]) + Math.Abs(v[11]) < 1e-9;
            return columnMajor
                ? Matrix4d.FromRows(v[0], v[4], v[8], v[12], v[1], v[5], v[9], v[13], v[2], v[6], v[10], v[14], v[3], v[7], v[11], v[15])
                : Matrix4d.FromRows(v);
        }

        /// <summary>Converts one entities collection (and nested groups/components) into a snapshot in world coordinates.</summary>
        public EntitiesState ReadEntities(IntPtr entities, Matrix4d transform)
        {
            var vertexIds = new Dictionary<IntPtr, int>();
            var vertices = new List<EntitiesState.VertexData>();
            int Vertex(IntPtr v)
            {
                if (vertexIds.TryGetValue(v, out var id)) return id;
                SUVertexGetPosition(v, out var p);
                id = _nextId++;
                vertexIds[v] = id;
                vertices.Add(new EntitiesState.VertexData(id, transform.TransformPoint(new Vector3d(p.X, p.Y, p.Z))));
                return id;
            }

            var edges = new List<EntitiesState.EdgeData>();
            foreach (var e in GetList(entities, (IntPtr h, out nuint n) => SUEntitiesGetNumEdges(h, false, out n),
                         (IntPtr h, nuint len, IntPtr[] items, out nuint n) => SUEntitiesGetEdges(h, false, len, items, out n)))
            {
                var a = Get(e, SUEdgeGetStartVertex);
                var b = Get(e, SUEdgeGetEndVertex);
                if (a == IntPtr.Zero || b == IntPtr.Zero) continue;
                SUEdgeGetSoft(e, out var soft);
                SUEdgeGetSmooth(e, out var smooth);
                var element = SUEdgeToDrawingElement(e);
                edges.Add(new EntitiesState.EdgeData(_nextId++, Vertex(a), Vertex(b), soft, smooth, Hidden(element), TagName(element)));
                EdgeCount++;
            }

            var faces = new List<EntitiesState.FaceData>();
            var mirror = Determinant(transform) < 0;
            foreach (var f in GetList(entities, SUEntitiesGetNumFaces, SUEntitiesGetFaces))
            {
                var outerLoop = Get(f, SUFaceGetOuterLoop);
                if (outerLoop == IntPtr.Zero)
                {
                    SkippedFaces++;
                    continue;
                }
                var outer = GetList(outerLoop, SULoopGetNumVertices, SULoopGetVertices).Select(Vertex).ToList();
                var inner = GetList(f, SUFaceGetNumInnerLoops, SUFaceGetInnerLoops)
                    .Select(l => (IReadOnlyList<int>)GetList(l, SULoopGetNumVertices, SULoopGetVertices).Select(Vertex).ToList())
                    .ToList();
                if (outer.Count < 3)
                {
                    SkippedFaces++;
                    continue;
                }
                SUFaceGetNormal(f, out var n);
                var normal = transform.TransformVector(new Vector3d(n.X, n.Y, n.Z)).Normalized();
                if (mirror)
                {
                    // A mirrored copy runs its loops the other way around.
                    outer.Reverse();
                    inner = inner.Select(l => (IReadOnlyList<int>)l.Reverse().ToList()).ToList();
                }
                var element = SUFaceToDrawingElement(f);
                // Faces carry one material here: the front one, else the back one (DEAP evidence reads either).
                var material = Get(f, SUFaceGetFrontMaterial);
                if (material == IntPtr.Zero) material = Get(f, SUFaceGetBackMaterial);
                faces.Add(new EntitiesState.FaceData(_nextId++, outer, inner, normal, TagName(element),
                    MaterialName(material), Hidden(element)));
                FaceCount++;
            }

            var images = new List<EntitiesState.ImageData>();
            foreach (var im in GetList(entities, SUEntitiesGetNumImages, SUEntitiesGetImages))
            {
                if (ReadImage(im, transform) is not { } image) continue;
                images.Add(image);
                ImageCount++;
            }

            var groups = new List<EntitiesState.GroupData>();
            foreach (var g in GetList(entities, SUEntitiesGetNumGroups, SUEntitiesGetGroups))
            {
                SUGroupGetTransform(g, out var t);
                var element = SUGroupToDrawingElement(g);
                var material = Get(element, SUDrawingElementGetMaterial);
                var nested = ReadEntities(Get(g, SUGroupGetEntities), transform * ToMatrix(t));
                groups.Add(new EntitiesState.GroupData(_nextId++, GetString(g, SUGroupGetName), TagName(element),
                    MaterialName(material), Hidden(element), nested));
                GroupCount++;
            }
            foreach (var inst in GetList(entities, SUEntitiesGetNumInstances, SUEntitiesGetInstances))
            {
                SUComponentInstanceGetTransform(inst, out var t);
                var definition = Get(inst, SUComponentInstanceGetDefinition);
                var content = Get(definition, SUComponentDefinitionGetEntities);
                var element = SUComponentInstanceToDrawingElement(inst);
                var material = Get(element, SUDrawingElementGetMaterial);
                var name = GetString(inst, SUComponentInstanceGetName);
                if (string.IsNullOrEmpty(name)) name = GetString(definition, SUComponentDefinitionGetName);
                var nested = ReadEntities(content, transform * ToMatrix(t));
                groups.Add(new EntitiesState.GroupData(_nextId++, name, TagName(element), MaterialName(material), Hidden(element), nested));
                GroupCount++;
            }

            return new EntitiesState(vertices, edges, faces, _nextId, groups, Images: images);
        }

        /// <summary>Larger images are reduced to this many pixels on their long side (as they are drawn).</summary>
        private const int MaxImagePixels = 4096;

        /// <summary>
        /// An image in world coordinates with its pixels (as PNG) in the model's image store. Its own transformation
        /// maps a rectangle of its pixel size onto the model; if it does not, the reported size is used.
        /// </summary>
        private EntitiesState.ImageData? ReadImage(IntPtr image, Matrix4d transform)
        {
            if (SUImageGetTransform(image, out var t) != Ok || SUImageGetPixelDimensions(image, out var pw, out var ph) != Ok
                || pw == 0 || ph == 0)
                return null;
            var bytes = ImagePng(image);
            if (bytes is null) return null;
            var local = ToMatrix(t);
            var m = transform * local;
            var u = new Vector3d(pw, 0, 0);
            var v = new Vector3d(0, ph, 0);
            if (SUImageGetDimensions(image, out var width, out var height) == Ok && width > 0 && height > 0)
            {
                var lu = local.TransformVector(u).Length;
                var lv = local.TransformVector(v).Length;
                if (lu > 0 && Math.Abs(lu - width) > width * 0.05) u *= width / lu;
                if (lv > 0 && Math.Abs(lv - height) > height * 0.05) v *= height / lv;
            }
            var element = SUImageToDrawingElement(image);
            var key = _model.Context.Images.Add(bytes);
            return new EntitiesState.ImageData(_nextId++, key, m.TransformPoint(Vector3d.Zero), m.TransformVector(u), m.TransformVector(v),
                GetString(image, SUImageGetName), TagName(element), Hidden(element));
        }

        /// <summary>The image's pixels as a PNG file's bytes (reduced to <see cref="MaxImagePixels"/>), or null.</summary>
        private static byte[]? ImagePng(IntPtr image)
        {
            var rep = IntPtr.Zero;
            if (SUImageRepCreate(ref rep) != Ok) return null;
            var file = Path.Combine(Path.GetTempPath(), $"sketch-skp-image-{Guid.NewGuid():N}.png");
            try
            {
                if (SUImageGetImageRep(image, ref rep) != Ok) return null;
                if (SUImageRepGetPixelDimensions(rep, out var w, out var h) == Ok && Math.Max(w, h) > MaxImagePixels)
                {
                    var scale = (double)MaxImagePixels / Math.Max(w, h);
                    SUImageRepResize(rep, (nuint)Math.Max(1, Math.Round(w * scale)), (nuint)Math.Max(1, Math.Round(h * scale)));
                }
                return SUImageRepSaveToFile(rep, file) == Ok && File.Exists(file) ? File.ReadAllBytes(file) : null;
            }
            finally
            {
                SUImageRepRelease(ref rep);
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // a leftover temporary file is harmless
                }
            }
        }

        private static double Determinant(Matrix4d m) =>
            m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
            - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
            + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
    }
}
