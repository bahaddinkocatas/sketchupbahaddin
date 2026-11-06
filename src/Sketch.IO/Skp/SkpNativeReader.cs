using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Model.Templates;

namespace Sketch.IO.Skp;

/// <summary>
/// Reads SketchUp 2021 and newer .skp files without SketchUp: such a file is a short header followed by a zip
/// archive whose <c>model.dat</c> is a tag-length-value tree (<see cref="SkpRecord"/>), with materials as XML and
/// images as plain PNG/JPEG entries. Imports faces (with holes), edges, groups and components (as groups in world
/// coordinates), images, text notes, tags (layers) with visibility and materials with colour. The structure was
/// worked out from the user's own files only; see <c>docs/skp-bicimi.md</c>.
/// </summary>
public static class SkpNativeReader
{
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>True when the file has the 2021+ zip container this reader understands.</summary>
    public static bool CanRead(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var head = new byte[4096];
            var n = fs.Read(head, 0, head.Length);
            return IndexOf(head.AsSpan(0, n), ZipMagic) >= 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>The SketchUp version that wrote the file ("{26.1.256}" in the header gives 26), or null.</summary>
    public static int? FileVersion(string path)
    {
        using var fs = File.OpenRead(path);
        var head = new byte[256];
        var n = fs.Read(head, 0, head.Length);
        var text = Encoding.Unicode.GetString(head, 0, n - n % 2);
        var m = Regex.Match(text, @"\{(\d+)\.\d+\.\d+\}");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    public static SkpImportResult Read(string path, SketchModel model)
    {
        var bytes = File.ReadAllBytes(path);
        var zipStart = IndexOf(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)), ZipMagic);
        if (zipStart < 0) throw new InvalidDataException("The file is not a SketchUp 2021 or newer model (no zip container).");
        using var zip = new ZipArchive(new MemoryStream(bytes, zipStart, bytes.Length - zipStart, false), ZipArchiveMode.Read);
        var entry = zip.GetEntry("model.dat") ?? throw new InvalidDataException("The SketchUp file has no model.dat entry.");
        var root = SkpRecord.Root(ReadEntry(entry));
        if (root.Tag != SkpTag.Root) throw new InvalidDataException($"Unexpected model.dat root record {root.Tag:x4}.");
        return new Reader(model, zip).Read(root);
    }

    private static int IndexOf(ReadOnlySpan<byte> hay, ReadOnlySpan<byte> needle) => hay.IndexOf(needle);

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        using var ms = new MemoryStream(entry.Length > 0 && entry.Length < int.MaxValue ? (int)entry.Length : 0);
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private sealed record MaterialInfo(string Name, Rgb Color, double Alpha, string? TexturePath);

    private sealed record DefinitionInfo(string Name, int Type, SkpRecord Entities);

    private sealed class Reader
    {
        private const int MaxDepth = 64;
        private readonly SketchModel _model;
        private readonly ZipArchive _zip;
        private readonly Dictionary<long, Tag?> _layers = new();
        private readonly Dictionary<long, MaterialInfo> _materials = new();
        private readonly Dictionary<long, DefinitionInfo> _definitions = new();
        private readonly Dictionary<string, string> _imageKeys = new();
        private int _nextId = 1;
        private int _faces, _edges, _groups, _skipped, _images;
        private readonly HashSet<string> _tagNames = new();
        private readonly HashSet<string> _materialNames = new();

        public Reader(SketchModel model, ZipArchive zip)
        {
            _model = model;
            _zip = zip;
        }

        public SkpImportResult Read(SkpRecord root)
        {
            ReadUnits(root.Child(SkpTag.Options));
            ReadLayers(root.Path(SkpTag.LayerList, 0x3a98, 0x3a99));
            ReadMaterials(root.Path(SkpTag.MaterialList, 0x30d4, 0x30d5));
            ReadDefinitions(root.Path(SkpTag.DefinitionList, 0x1770, 0x1771));
            var entities = root.Path(SkpTag.RootEntities, SkpTag.Entities)
                           ?? throw new InvalidDataException("The SketchUp file has no model entities.");
            var state = ReadEntities(entities, Matrix4d.Identity, 0);
            _model.Entities.Import(state);
            ReadScenes(root.Path(SkpTag.SceneList, 0x6d60, 0x6d61));
            _model.Tags.RaiseChanged();
            return new SkpImportResult(_faces, _edges, _groups, _tagNames.ToList(), _materialNames.ToList(), _skipped, _images);
        }

        /// <summary>
        /// The file's scenes replace the template's: name, camera, hidden tags, and what each one saves. Styles are not
        /// read from the file, so a scene named like one of the template's (e.g. "(Thermal Elements)") keeps the
        /// template scene's style: colour by tag for the thermal-element scenes.
        /// </summary>
        private void ReadScenes(SkpRecord? list)
        {
            if (list is null) return;
            var scenes = new List<Scene>();
            foreach (var rec in list.Value.Children(SkpTag.Scene))
            {
                var name = rec.Child(SkpTag.SceneName)?.Text();
                if (string.IsNullOrEmpty(name)) continue;
                var template = _model.Scenes.Items.FirstOrDefault(s => s.Name == name && s.Style is not null);
                var scene = new Scene
                {
                    Name = name,
                    SavesStyle = template is not null || rec.Child(SkpTag.SceneStyleId) is not null,
                    Style = template?.Style ?? _model.Style,
                };
                var cam = rec.Path(SkpTag.SceneCameraWrap, SkpTag.CameraRecord);
                scene.SavesCamera = cam is not null;
                if (cam is { } c) scene.Camera = ReadCamera(c);
                var hidden = rec.Child(SkpTag.SceneHiddenLayers);
                scene.SavesTags = hidden is not null;
                if (hidden is { } h)
                {
                    var hiddenIds = IdList(h.Bytes);
                    scene.TagVisibility = _layers
                        .Where(kv => kv.Value is not null)
                        .ToDictionary(kv => kv.Value!.Name, kv => !hiddenIds.Contains(kv.Key));
                }
                scenes.Add(scene);
            }
            if (scenes.Count == 0) return;
            _model.Scenes.Clear();
            foreach (var s in scenes) _model.Scenes.Add(s);
        }

        /// <summary>
        /// A face normal through a transformation: like the inverse transpose, so it stays perpendicular to the face
        /// when an instance is scaled differently along its axes (a plain direction transform would tilt it). Built
        /// from two directions in the face's plane and kept on the same side under mirroring.
        /// </summary>
        private static Vector3d TransformNormal(Matrix4d t, Vector3d n)
        {
            if (n.IsZero()) return Vector3d.Zero;
            n = n.Normalized();
            var u = n.AnyPerpendicular().Normalized();
            var v = Vector3d.Cross(n, u);
            var m = Vector3d.Cross(t.TransformVector(u), t.TransformVector(v));
            var det = Vector3d.Dot(t.TransformVector(Vector3d.UnitX),
                Vector3d.Cross(t.TransformVector(Vector3d.UnitY), t.TransformVector(Vector3d.UnitZ)));
            return (det < 0 ? -m : m).Normalized();
        }

        /// <summary>A list of ids, each written as a length byte followed by that many little-endian bytes.</summary>
        private static HashSet<long> IdList(ReadOnlySpan<byte> bytes)
        {
            var ids = new HashSet<long>();
            var p = 0;
            while (p < bytes.Length)
            {
                int len = bytes[p++];
                if (len <= 0 || len > 8 || p + len > bytes.Length) break;
                long id = 0;
                for (var i = len - 1; i >= 0; i--) id = (id << 8) | bytes[p + i];
                ids.Add(id);
                p += len;
            }
            return ids;
        }

        private static Camera? ReadCamera(SkpRecord c)
        {
            var eye = c.Child(SkpTag.CameraEye);
            var target = c.Child(SkpTag.CameraTarget);
            var up = c.Child(SkpTag.CameraUp);
            if (eye is null || target is null || up is null) return null;
            var camera = new Camera();
            camera.Set(eye.Value.Point(), target.Value.Point(), up.Value.Point());
            camera.IsPerspective = (c.Child(SkpTag.CameraPerspective)?.Byte() ?? 1) != 0;
            var fov = c.Child(SkpTag.CameraFov)?.Double() ?? 0;
            if (fov is > 1 and < 170) camera.FovYDegrees = fov;
            var ortho = c.Child(SkpTag.CameraOrthoHeight)?.Double() ?? 0;
            if (ortho > 0) camera.OrthoHeight = ortho;
            return camera;
        }

        // ------------------------------------------------------------------ options, layers, materials, definitions

        private static void ReadUnits(SkpRecord? options)
        {
            var groups = options?.Path(0x61a8, 0x61a9);
            if (groups is null) return;
            foreach (var group in groups.Value.Children(SkpTag.OptionGroup))
            {
                if (group.Child(SkpTag.OptionGroupName)?.Text() != "UnitsOptions") continue;
                var values = group.Child(SkpTag.OptionValues);
                if (values is null) return;
                var expectUnit = false;
                foreach (var item in values.Value.Children())
                {
                    if (item.Tag == SkpTag.OptionName) expectUnit = item.Text() == "LengthUnit";
                    else if (item.Tag == SkpTag.OptionValue && expectUnit)
                    {
                        var unit = item.Child(SkpTag.OptionInt)?.Int32() ?? -1;
                        // SketchUp's length unit codes: 0 inches, 1 feet, 2 millimetres, 3 centimetres, 4 metres.
                        Units.Default.Unit = unit switch
                        {
                            0 => LengthUnit.Inches,
                            1 => LengthUnit.Feet,
                            3 => LengthUnit.Centimeters,
                            4 => LengthUnit.Meters,
                            _ => LengthUnit.Millimeters,
                        };
                        return;
                    }
                }
            }
        }

        private void ReadLayers(SkpRecord? list)
        {
            if (list is null) return;
            foreach (var layer in list.Value.Children(SkpTag.Layer))
            {
                var id = layer.Path(SkpTag.Base, SkpTag.Id)?.Id();
                var name = layer.Child(SkpTag.LayerName)?.Text();
                if (id is null || name is null) continue;
                var tag = TagFor(name, MaterialXml("Layer_" + name)?.Color);
                if (tag is not null) tag.Visible = (layer.Child(SkpTag.LayerHidden)?.Byte() ?? 0) == 0;
                _layers[id.Value] = tag;
            }
        }

        private Tag? TagFor(string name, Rgb? color)
        {
            if (string.IsNullOrEmpty(name) || name is "Layer0" or "Untagged" or "Etiketsiz") return null;
            _tagNames.Add(name);
            return _model.Tags.Find(name)
                   ?? _model.Tags.Add(name, color ?? ThermalTemplate.DefaultColor(name, _model.Tags.Items.Count));
        }

        private void ReadMaterials(SkpRecord? list)
        {
            if (list is null) return;
            foreach (var material in list.Value.Children(SkpTag.Material))
            {
                var id = material.Path(SkpTag.Base, SkpTag.Id)?.Id();
                var name = material.Child(SkpTag.MaterialName)?.Text();
                if (id is null || string.IsNullOrEmpty(name)) continue;
                _materials[id.Value] = MaterialXml(name) ?? new MaterialInfo(name, new Rgb(200, 200, 200), 1, null);
            }
        }

        /// <summary>Colour, opacity and texture file of materials/&lt;name&gt;/material.xml, or null when absent.</summary>
        private MaterialInfo? MaterialXml(string name)
        {
            var entry = _zip.GetEntry($"materials/{name}/material.xml");
            if (entry is null) return null;
            try
            {
                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                var m = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "material");
                if (m is null) return null;
                byte Channel(string attr) => (byte)Math.Clamp(int.TryParse((string?)m.Attribute(attr), out var v) ? v : 200, 0, 255);
                var color = new Rgb(Channel("colorRed"), Channel("colorGreen"), Channel("colorBlue"));
                var useTrans = (string?)m.Attribute("useTrans") == "1";
                var alpha = useTrans && double.TryParse((string?)m.Attribute("trans"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var t)
                    ? Math.Clamp(1 - t, 0, 1)
                    : 1;
                var image = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "image");
                var file = (string?)image?.Attribute("path");
                string? texture = null;
                if (!string.IsNullOrEmpty(file))
                {
                    file = file.Replace('\\', '/');
                    if (file.StartsWith("./", StringComparison.Ordinal)) file = file[2..];
                    texture = $"materials/{name}/{file}";
                }
                return new MaterialInfo(name, color, alpha, texture);
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException or InvalidDataException)
            {
                return null;
            }
        }

        private void ReadDefinitions(SkpRecord? list)
        {
            if (list is null) return;
            foreach (var def in list.Value.Children(SkpTag.Definition))
            {
                var entities = def.Child(SkpTag.Entities);
                var id = entities?.Path(SkpTag.Header, SkpTag.Base, SkpTag.Id)?.Id();
                if (entities is null || id is null) continue;
                var name = def.Child(SkpTag.DefinitionName)?.Text() ?? string.Empty;
                var type = def.Child(SkpTag.DefinitionType)?.Int32() ?? 1;
                _definitions[id.Value] = new DefinitionInfo(name, type, entities.Value);
            }
        }

        // ------------------------------------------------------------------ entities

        private static bool Hidden(SkpRecord? header) => ((header?.Child(SkpTag.HeaderFlags)?.Byte() ?? 0) & 0x40) != 0;

        private string? TagName(SkpRecord? header)
        {
            var id = header?.Child(SkpTag.HeaderLayer)?.Id();
            return id is not null && _layers.TryGetValue(id.Value, out var tag) ? tag?.Name : null;
        }

        private string? MaterialName(long? id)
        {
            if (id is null || !_materials.TryGetValue(id.Value, out var info) || string.IsNullOrEmpty(info.Name)) return null;
            // Glass and wood/door materials become the template's Glass and Door (windows and doors for DEAP).
            if (ThermalTemplate.OpeningMaterial(info.Name) is { } opening)
            {
                ThermalTemplate.OpeningMaterial(_model, opening);
                _materialNames.Add(opening);
                return opening;
            }
            if (_model.Materials.Find(info.Name) is null) _model.Materials.Add(info.Name, info.Color, info.Alpha);
            _materialNames.Add(info.Name);
            return info.Name;
        }

        /// <summary>
        /// The instance's placement (13 doubles): the 3×3 matrix row by row, then the origin, then a scale divisor.
        /// Row order was settled with the Block 4 file: only then do the floor-plan images cover the dwellings and the
        /// DEAP orientations match the labels the SketchUp extension wrote into the file.
        /// </summary>
        private static Matrix4d InstanceMatrix(SkpRecord instance)
        {
            var d = instance.Child(SkpTag.InstanceTransform)?.Doubles();
            if (d is null || d.Length < 12) return Matrix4d.Identity;
            var s = d.Length >= 13 && Math.Abs(d[12]) > 1e-12 ? 1 / d[12] : 1;
            return Matrix4d.FromRows(
                d[0] * s, d[1] * s, d[2] * s, d[9] * s,
                d[3] * s, d[4] * s, d[5] * s, d[10] * s,
                d[6] * s, d[7] * s, d[8] * s, d[11] * s,
                0, 0, 0, 1);
        }

        private EntitiesState ReadEntities(SkpRecord entities, Matrix4d transform, int depth)
        {
            var vertexIds = new Dictionary<long, int>();
            var positions = new Dictionary<int, Vector3d>();
            var vertices = new List<EntitiesState.VertexData>();
            if (entities.Child(SkpTag.VertexList) is { } vertexList)
            {
                foreach (var v in vertexList.Children(SkpTag.Vertex))
                {
                    var id = v.Path(SkpTag.Base, SkpTag.Id)?.Id();
                    var pos = v.Child(SkpTag.VertexPosition);
                    if (id is null || pos is null || pos.Value.Length < 24) continue;
                    var newId = _nextId++;
                    var p = transform.TransformPoint(pos.Value.Point());
                    vertexIds[id.Value] = newId;
                    positions[newId] = p;
                    vertices.Add(new EntitiesState.VertexData(newId, p));
                }
            }

            var edgeVertices = new Dictionary<long, (int Start, int End)>();
            var edges = new List<EntitiesState.EdgeData>();
            if (entities.Child(SkpTag.EdgeList) is { } edgeList)
            {
                foreach (var e in edgeList.Children(SkpTag.Edge))
                {
                    var header = e.Child(SkpTag.Header);
                    var id = header?.Path(SkpTag.Base, SkpTag.Id)?.Id();
                    var s = e.Child(SkpTag.EdgeStart)?.Id();
                    var t = e.Child(SkpTag.EdgeEnd)?.Id();
                    if (id is null || s is null || t is null
                        || !vertexIds.TryGetValue(s.Value, out var vs) || !vertexIds.TryGetValue(t.Value, out var vt) || vs == vt)
                        continue;
                    var flags = header?.Child(SkpTag.HeaderFlags)?.Byte() ?? 0;
                    var soft = (flags & 1) != 0;
                    edges.Add(new EntitiesState.EdgeData(_nextId++, vs, vt, soft, soft, Hidden(header), TagName(header)));
                    edgeVertices[id.Value] = (vs, vt);
                    _edges++;
                }
            }

            var faces = new List<EntitiesState.FaceData>();
            if (entities.Child(SkpTag.FaceList) is { } faceList)
            {
                foreach (var f in faceList.Children(SkpTag.Face))
                {
                    var header = f.Child(SkpTag.Header);
                    var loops = new List<List<int>>();
                    foreach (var loop in f.Child(SkpTag.FaceLoops)?.Children(SkpTag.Loop) ?? Enumerable.Empty<SkpRecord>())
                    {
                        var seq = LoopVertices(loop, edgeVertices);
                        if (seq is { Count: >= 3 }) loops.Add(seq);
                    }
                    if (loops.Count == 0)
                    {
                        _skipped++;
                        continue;
                    }
                    var outer = loops[0];
                    var inner = loops.Skip(1).Cast<IReadOnlyList<int>>().ToList();
                    var plane = f.Child(SkpTag.FacePlane)?.Doubles();
                    var normal = plane is { Length: >= 3 } ? TransformNormal(transform, new Vector3d(plane[0], plane[1], plane[2])) : Vector3d.Zero;
                    var newell = NewellNormal(outer, positions);
                    if (normal.IsZero()) normal = newell.Normalized();
                    if (normal.IsZero())
                    {
                        _skipped++;
                        continue;
                    }
                    if (Vector3d.Dot(newell, normal) < 0)
                    {
                        // The outer loop must run counter-clockwise around the normal (a mirrored placement flips it).
                        outer.Reverse();
                        inner = inner.Select(l => (IReadOnlyList<int>)l.Reverse().ToList()).ToList();
                    }
                    var materialId = header?.Child(SkpTag.HeaderMaterial)?.Id() ?? f.Child(SkpTag.FaceFrontMaterial)?.Id();
                    faces.Add(new EntitiesState.FaceData(_nextId++, outer, inner, normal, TagName(header), MaterialName(materialId), Hidden(header)));
                    _faces++;
                }
            }

            var images = new List<EntitiesState.ImageData>();
            var groups = new List<EntitiesState.GroupData>();
            // Groups and pictures come wrapped (1d4c / 1f40 > 1964); component instances are 1964 records directly.
            var instanceLists = new (SkpRecord? List, ushort WrapTag)[]
            {
                (entities.Child(SkpTag.ImageInstanceList), SkpTag.ImageInstanceWrap),
                (entities.Child(SkpTag.InstanceList), SkpTag.InstanceWrap),
                (entities.Child(SkpTag.ComponentInstanceList), SkpTag.Instance),
            };
            foreach (var (list, wrapTag) in instanceLists)
            {
                if (list is null) continue;
                foreach (var wrap in list.Value.Children(wrapTag))
                {
                    var instance = wrapTag == SkpTag.Instance ? wrap : wrap.Child(SkpTag.Instance);
                    var defId = instance?.Child(SkpTag.InstanceDefinition)?.Id();
                    if (instance is null || defId is null || !_definitions.TryGetValue(defId.Value, out var def)) continue;
                    var header = instance.Value.Child(SkpTag.Header);
                    var name = instance.Value.Child(SkpTag.InstanceName)?.Text();
                    if (string.IsNullOrEmpty(name)) name = def.Name;
                    var placement = transform * InstanceMatrix(instance.Value);
                    if (def.Type == 2)
                    {
                        if (ReadImage(def, placement, name, header) is { } image)
                        {
                            images.Add(image);
                            _images++;
                        }
                        continue;
                    }
                    if (depth >= MaxDepth) continue;
                    var nested = ReadEntities(def.Entities, placement, depth + 1);
                    groups.Add(new EntitiesState.GroupData(_nextId++, name, TagName(header),
                        MaterialName(header?.Child(SkpTag.HeaderMaterial)?.Id()), Hidden(header), nested));
                    _groups++;
                }
            }

            var notes = new List<EntitiesState.NoteData>();
            if (entities.Child(SkpTag.TextList) is { } textList)
            {
                foreach (var t in textList.Children(SkpTag.Text))
                {
                    var header = t.Child(SkpTag.Header);
                    var text = t.Child(SkpTag.TextString)?.Text();
                    var point = Attachment(t.Path(SkpTag.TextAttachment, SkpTag.TextAttachmentInner), vertexIds, positions, transform);
                    if (string.IsNullOrEmpty(text) || point is null) continue;
                    notes.Add(new EntitiesState.NoteData(_nextId++, point.Value, Vector3d.Zero, text, false, TagName(header), Hidden(header)));
                }
            }

            var guides = new List<EntitiesState.GuideData>();
            if (entities.Child(SkpTag.GuideList) is { } guideList)
            {
                foreach (var g in guideList.Children(SkpTag.Guide))
                {
                    var header = g.Path(SkpTag.GuideHeaderWrap, SkpTag.Header);
                    var a = g.Child(SkpTag.GuideStart);
                    var b = g.Child(SkpTag.GuideEnd);
                    if (a is null || a.Value.Length < 24) continue;
                    var start = transform.TransformPoint(a.Value.Point());
                    var isLine = (g.Child(SkpTag.GuideIsLine)?.Byte() ?? 1) != 0 && b is { Length: >= 24 };
                    var direction = isLine ? (transform.TransformPoint(b!.Value.Point()) - start).Normalized() : Vector3d.Zero;
                    guides.Add(new EntitiesState.GuideData(_nextId++, start, direction, TagName(header), Hidden(header)));
                }
            }

            var dimensions = new List<EntitiesState.DimensionData>();
            if (entities.Child(SkpTag.DimensionList) is { } dimensionList)
            {
                foreach (var d in dimensionList.Children(SkpTag.Dimension))
                {
                    var header = d.Path(SkpTag.DimensionHeaderWrap, SkpTag.Header);
                    var start = Attachment(d.Path(SkpTag.DimensionStart, SkpTag.TextAttachmentInner), vertexIds, positions, transform);
                    var end = Attachment(d.Path(SkpTag.DimensionEnd, SkpTag.TextAttachmentInner), vertexIds, positions, transform);
                    if (start is null || end is null || start.Value.IsEqual(end.Value)) continue;
                    var normal = d.Child(SkpTag.DimensionNormal) is { Length: >= 24 } n ? transform.TransformVector(n.Point()).Normalized() : Vector3d.UnitZ;
                    var distance = d.Child(SkpTag.DimensionOffset)?.Double() ?? 0;
                    var along = (end.Value - start.Value).Normalized();
                    var side = Vector3d.Cross(normal, along);
                    var offset = side.IsZero() ? Vector3d.Zero : side.Normalized() * distance;
                    dimensions.Add(new EntitiesState.DimensionData(_nextId++, start.Value, end.Value, offset, TagName(header), Hidden(header)));
                }
            }

            var sections = new List<EntitiesState.SectionData>();
            if (entities.Child(SkpTag.SectionList) is { } sectionList)
            {
                var activeId = entities.Child(SkpTag.ActiveSection)?.Id();
                var bounds = entities.Child(SkpTag.Bounds)?.Doubles();
                var size = bounds is { Length: >= 6 }
                    ? Math.Max(10, Math.Max(bounds[3] - bounds[0], Math.Max(bounds[4] - bounds[1], bounds[5] - bounds[2])) * 0.6)
                    : 100;
                foreach (var s in sectionList.Children(SkpTag.Section))
                {
                    var header = s.Child(SkpTag.Header);
                    var id = header?.Path(SkpTag.Base, SkpTag.Id)?.Id();
                    var plane = s.Child(SkpTag.SectionPlaneEq)?.Doubles();
                    if (plane is not { Length: >= 4 }) continue;
                    var localNormal = new Vector3d(plane[0], plane[1], plane[2]);
                    if (localNormal.IsZero()) continue;
                    var localPoint = localNormal.Normalized() * (-plane[3] / localNormal.Length);
                    var normal = transform.TransformVector(localNormal).Normalized();
                    var point = transform.TransformPoint(localPoint);
                    var name = s.Child(SkpTag.SectionName)?.Text() ?? "Section";
                    sections.Add(new EntitiesState.SectionData(_nextId++, point, normal, size, name, id is not null && id == activeId,
                        TagName(header), Hidden(header)));
                }
            }

            return new EntitiesState(vertices, edges, faces, _nextId, groups, guides, dimensions, notes, images, sections);
        }

        /// <summary>
        /// Where a text or dimension is attached: a point of its own (kind 1), or a vertex of the same container (kind 2).
        /// </summary>
        private static Vector3d? Attachment(SkpRecord? attachment, Dictionary<long, int> vertexIds, Dictionary<int, Vector3d> positions,
            Matrix4d transform)
        {
            if (attachment is null) return null;
            var kind = attachment.Value.Child(SkpTag.AttachmentKind)?.Int32() ?? 1;
            var point = attachment.Value.Child(SkpTag.TextPoint);
            if (kind == 2)
            {
                var vertexId = attachment.Value.Path(SkpTag.AttachmentVertexWrap, SkpTag.AttachmentVertexInner, SkpTag.AttachmentVertexId)?.Id();
                if (vertexId is not null && vertexIds.TryGetValue(vertexId.Value, out var vid) && positions.TryGetValue(vid, out var p))
                    return p + (point is { Length: >= 24 } ? transform.TransformVector(point.Value.Point()) : Vector3d.Zero);
            }
            return point is { Length: >= 24 } ? transform.TransformPoint(point.Value.Point()) : null;
        }

        /// <summary>A loop's vertices in order: each edge use names an edge and whether it is walked backwards.</summary>
        private static List<int>? LoopVertices(SkpRecord loop, Dictionary<long, (int Start, int End)> edgeVertices)
        {
            var uses = loop.Child(SkpTag.LoopEdgeUses);
            if (uses is null) return null;
            var seq = new List<int>();
            foreach (var use in uses.Value.Children(SkpTag.EdgeUse))
            {
                var edgeId = use.Child(SkpTag.EdgeUseEdge)?.Id();
                if (edgeId is null || !edgeVertices.TryGetValue(edgeId.Value, out var ev)) return null;
                var reversed = (use.Child(SkpTag.EdgeUseReversed)?.Byte() ?? 0) != 0;
                var v = reversed ? ev.End : ev.Start;
                if (seq.Count == 0 || seq[^1] != v) seq.Add(v);
            }
            if (seq.Count > 1 && seq[0] == seq[^1]) seq.RemoveAt(seq.Count - 1);
            return seq;
        }

        private static Vector3d NewellNormal(List<int> loop, Dictionary<int, Vector3d> positions)
        {
            double x = 0, y = 0, z = 0;
            for (var i = 0; i < loop.Count; i++)
            {
                var a = positions[loop[i]];
                var b = positions[loop[(i + 1) % loop.Count]];
                x += (a.Y - b.Y) * (a.Z + b.Z);
                y += (a.Z - b.Z) * (a.X + b.X);
                z += (a.X - b.X) * (a.Y + b.Y);
            }
            return new Vector3d(x, y, z);
        }

        /// <summary>
        /// An image: its definition holds a rectangle of its pixel size (bounds) and one face whose material carries
        /// the picture file; the instance placement maps that rectangle into the model.
        /// </summary>
        private EntitiesState.ImageData? ReadImage(DefinitionInfo def, Matrix4d placement, string name, SkpRecord? header)
        {
            var bounds = def.Entities.Child(SkpTag.Bounds)?.Doubles();
            if (bounds is null || bounds.Length < 6) return null;
            var pw = bounds[3] - bounds[0];
            var ph = bounds[4] - bounds[1];
            if (pw <= 0 || ph <= 0) return null;
            string? texture = null;
            foreach (var f in def.Entities.Child(SkpTag.FaceList)?.Children(SkpTag.Face) ?? Enumerable.Empty<SkpRecord>())
            {
                var materialId = f.Child(SkpTag.Header)?.Child(SkpTag.HeaderMaterial)?.Id() ?? f.Child(SkpTag.FaceFrontMaterial)?.Id();
                if (materialId is not null && _materials.TryGetValue(materialId.Value, out var info) && info.TexturePath is not null)
                {
                    texture = info.TexturePath;
                    break;
                }
            }
            if (texture is null) return null;
            if (!_imageKeys.TryGetValue(texture, out var key))
            {
                var entry = _zip.GetEntry(texture);
                if (entry is null) return null;
                key = _model.Context.Images.Add(ReadEntry(entry));
                _imageKeys[texture] = key;
            }
            var origin = placement.TransformPoint(new Vector3d(bounds[0], bounds[1], bounds[2]));
            var u = placement.TransformVector(new Vector3d(pw, 0, 0));
            var v = placement.TransformVector(new Vector3d(0, ph, 0));
            return new EntitiesState.ImageData(_nextId++, key, origin, u, v, name, TagName(header), Hidden(header));
        }
    }
}
