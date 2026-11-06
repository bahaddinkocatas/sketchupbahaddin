using Silk.NET.OpenGL;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Tools;

namespace Sketch.Rendering;

/// <summary>What to draw in one frame.</summary>
public sealed class RenderScene
{
    public Entities? Entities { get; init; }
    public ModelAxes Axes { get; init; } = ModelAxes.World;
    public IReadOnlySet<Entity> Selection { get; init; } = new HashSet<Entity>();

    public RenderStyle Style { get; init; } = new();

    /// <summary>Pixels of imported images.</summary>
    public ImageStore? Images { get; init; }

    /// <summary>Container being edited; geometry outside it is drawn faded (when not hidden). Null = model root.</summary>
    public Entities? ActiveEntities { get; init; }

    /// <summary>Changes whenever geometry or selection changes; the cached mesh is rebuilt when it differs.</summary>
    public long Version { get; init; }

    /// <summary>Changes when tag visibility/colours, materials or the style change (all cached geometry is redrawn).</summary>
    public long DisplayVersion { get; init; }

    public Action<IViewDrawing>? Overlay { get; init; }
}

/// <summary>Draws the scene with OpenGL 3.3 core. Must be used on the thread that owns the GL context.</summary>
public sealed class Renderer : IDisposable, IViewDrawing
{
    private readonly GL _gl;
    private readonly ShaderProgram _lineShader;
    private readonly ShaderProgram _pointShader;
    private readonly ShaderProgram _faceShader;
    private readonly ShaderProgram _flatShader;

    private readonly VertexBatch _faces;      // pos3 normal3 front4 back4 selected1
    private readonly VertexBatch _transparentFaces;
    private readonly VertexBatch _modelLines; // pos3 color4 width1 dashed1
    private readonly VertexBatch _backEdges;
    private readonly VertexBatch _sectionLines; // section plane outlines and cut outlines (never clipped)
    private (float X, float Y, float Z, float W) _clip = NoClip;
    private static readonly (float, float, float, float) NoClip = (0, 0, 0, 1);
    private RenderStyle _style = new();
    private readonly TextLabels _text;
    private readonly VertexBatch _profileLines;
    /// <summary>Edges that may be drawn as profiles: end points and the normals of their visible faces.</summary>
    private readonly List<(Vector3d A, Vector3d B, Vector3d N1, Vector3d? N2)> _profileCandidates = new();
    private (Vector3d Eye, Vector3d Dir, bool Persp, long Mesh)? _profileKey;
    private long _meshBuilds;
    private readonly ImageQuads _images;

    public IImageDecoder? ImageDecoder
    {
        get => _images.Decoder;
        set => _images.Decoder = value;
    }
    private readonly List<(Vector3d Anchor, string Text, DrawColor Color)> _modelLabels = new();

    /// <summary>Font rendering for labels (dimensions, notes, tool feedback); set by the UI layer.</summary>
    public ITextRasterizer? TextRasterizer
    {
        get => _text.Rasterizer;
        set => _text.Rasterizer = value;
    }
    private readonly VertexBatch _axisLines;
    private readonly VertexBatch _overlayLines;
    private readonly VertexBatch _overlayDepthLines;
    private readonly VertexBatch _overlayPoints; // pos3 color4 size1 shape1
    private readonly VertexBatch _overlayPolys;  // pos3 color4

    private long _meshVersion = -1;
    private Matrix4d _viewProj = Matrix4d.Identity;
    private int _width = 1, _height = 1;

    /// <summary>Default background: the warm grey-beige of the user's SketchUp template.</summary>
    public static readonly DrawColor Background = new(211, 208, 186);

    private DrawColor _background = Background;
    public static readonly DrawColor FrontFace = new(255, 255, 255);
    public static readonly DrawColor BackFace = new(164, 178, 192);
    /// <summary>Annotation (dimension, note) colour.</summary>
    public static readonly DrawColor EdgeColor = new(0, 0, 0);

    /// <summary>Default edge colour: orange, readable on top of imported plans.</summary>
    public static readonly DrawColor DefaultEdgeColor = new(230, 110, 15);

    private DrawColor _edgeColor = DefaultEdgeColor;

    /// <summary>Direction toward the light (world space).</summary>
    private static readonly Vector3d LightDir = new Vector3d(-0.35, -0.55, 0.76).Normalized();

    public Renderer(GL gl)
    {
        _gl = gl;
        _lineShader = new ShaderProgram(gl, Shaders.LineVertex, Shaders.LineFragment, Shaders.LineGeometry);
        _pointShader = new ShaderProgram(gl, Shaders.PointVertex, Shaders.PointFragment);
        _faceShader = new ShaderProgram(gl, Shaders.FaceVertex, Shaders.FaceFragment);
        _flatShader = new ShaderProgram(gl, Shaders.FlatVertex, Shaders.FlatFragment);

        _faces = new VertexBatch(gl, PrimitiveType.Triangles, 3, 3, 4, 4, 1);
        _transparentFaces = new VertexBatch(gl, PrimitiveType.Triangles, 3, 3, 4, 4, 1);
        _modelLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _backEdges = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _sectionLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _text = new TextLabels(gl);
        _profileLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _images = new ImageQuads(gl);
        _axisLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _overlayLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _overlayDepthLines = new VertexBatch(gl, PrimitiveType.Lines, 3, 4, 1, 1);
        _overlayPoints = new VertexBatch(gl, PrimitiveType.Points, 3, 4, 1, 1);
        _overlayPolys = new VertexBatch(gl, PrimitiveType.Triangles, 3, 4);
    }

    /// <summary>Waits until the GPU has finished (for timing).</summary>
    public void Finish() => _gl.Finish();

    public string GlVersion => _gl.GetStringS(StringName.Version) ?? "?";
    public string GlRenderer => _gl.GetStringS(StringName.Renderer) ?? "?";

    public void Render(Camera camera, int width, int height, Vector3d sceneCenter, double sceneRadius, RenderScene scene)
    {
        _width = Math.Max(width, 1);
        _height = Math.Max(height, 1);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _background = scene.Style.Background is { } bg ? new DrawColor(bg.R, bg.G, bg.B) : Background;
        _edgeColor = scene.Style.EdgeColor is { } ec ? new DrawColor(ec.R, ec.G, ec.B) : DefaultEdgeColor;
        _gl.ClearColor(_background.R / 255f, _background.G / 255f, _background.B / 255f, 1f);
        _gl.ClearDepth(1.0);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.Multisample);
        // Nothing is cut away by the near/far planes (axes, guides and tool previews reach across the whole view).
        _gl.Enable(EnableCap.DepthClamp);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.Disable(EnableCap.CullFace);

        var aspect = _width / (double)_height;
        var (near, far) = camera.ClipRange(sceneCenter, sceneRadius);
        var proj = camera.ProjectionMatrix(aspect, near, far);
        _viewProj = proj * camera.ViewMatrix;
        var viewProj = _viewProj.ToColumnMajorFloat();

        if (scene.Version != _meshVersion || !Equals(scene.Style, _style) || scene.DisplayVersion != _displayVersion)
        {
            if (!Equals(scene.Style, _style) || scene.DisplayVersion != _displayVersion) _chunks.Clear();
            _displayVersion = scene.DisplayVersion;
            _style = scene.Style;
            BuildModelMesh(scene.Entities, scene.Selection, scene.ActiveEntities);
            _meshVersion = scene.Version;
        }

        // Faces, pushed slightly back so coincident edges win the depth test. The active section plane
        // clips faces and edges (gl_ClipDistance); axes, section outlines and tool overlays are not clipped.
        _gl.Enable(EnableCap.ClipDistance0);
        _gl.Enable(EnableCap.PolygonOffsetFill);
        _gl.PolygonOffset(1f, 1f);
        _faceShader.Use();
        _faceShader.Set("uViewProj", viewProj);
        SetClip(_faceShader, _clip);
        _faceShader.Set("uEye", (float)camera.Eye.X, (float)camera.Eye.Y, (float)camera.Eye.Z);
        var vd = camera.Direction;
        _faceShader.Set("uViewDir", (float)vd.X, (float)vd.Y, (float)vd.Z);
        _faceShader.Set("uPerspective", camera.IsPerspective ? 1 : 0);
        _faceShader.Set("uLightDir", (float)LightDir.X, (float)LightDir.Y, (float)LightDir.Z);
        _faceShader.Set("uLit", _style.FaceStyle == FaceStyle.HiddenLine ? 0 : 1);
        _faces.Draw();
        // Images sit a little further back so faces traced on top of a plan always win.
        _gl.PolygonOffset(4f, 16f);
        _images.Draw(viewProj, scene.Images);
        _gl.PolygonOffset(1f, 1f);

        // Axes and edges.
        _axisLines.Clear();
        AddAxes(_axisLines, camera, near, far, aspect, scene.Axes);
        _lineShader.Use();
        _lineShader.Set("uViewProj", viewProj);
        _lineShader.Set("uViewport", _width, _height);
        SetClip(_lineShader, NoClip);
        _axisLines.Draw();
        _sectionLines.Draw();
        SetClip(_lineShader, _clip);
        _modelLines.Draw();
        BuildProfiles(camera);
        _profileLines.Draw();
        if (_style.BackEdges)
        {
            // Edges hidden behind faces, dashed.
            _gl.DepthFunc(DepthFunction.Greater);
            _gl.DepthMask(false);
            _backEdges.Draw();
            _gl.DepthMask(true);
            _gl.DepthFunc(DepthFunction.Lequal);
        }

        // Translucent faces (glass, X-ray) last, without writing depth.
        _faceShader.Use();
        _gl.DepthMask(false);
        _transparentFaces.Draw();
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.PolygonOffsetFill);
        _lineShader.Use();
        SetClip(_lineShader, NoClip);
        _gl.Disable(EnableCap.ClipDistance0);

        // Tool overlay.
        _overlayLines.Clear();
        _overlayDepthLines.Clear();
        _overlayPoints.Clear();
        _overlayPolys.Clear();
        _text.Clear();
        // Model labels (texts, dimensions) have no background card: halo text reads better over the model. Like the
        // geometry they belong to, they are hidden behind faces in front of them.
        foreach (var (anchor, text, color) in _modelLabels) _text.Add(anchor, text, color, boxed: false, depthTested: true);
        scene.Overlay?.Invoke(this);

        _flatShader.Use();
        _flatShader.Set("uViewProj", viewProj);
        _gl.DepthMask(false);
        _overlayPolys.Draw();
        _gl.DepthMask(true);

        _lineShader.Use();
        _overlayDepthLines.Draw();
        _gl.Disable(EnableCap.DepthTest);
        _overlayLines.Draw();
        _pointShader.Use();
        _pointShader.Set("uViewProj", viewProj);
        _overlayPoints.Draw();
        _gl.Enable(EnableCap.DepthTest);
        _text.Draw(viewProj, _width, _height);
    }

    /// <summary>Thick outline where the section plane cuts visible faces (the "section cut").</summary>
    private void AddSectionCut(Entities entities, Plane3d plane)
    {
        var cutColor = new DrawColor(20, 20, 20);
        foreach (var face in entities.AllFaces().Where(Entities.IsEffectivelyVisible))
        {
            var dir = Vector3d.Cross(face.Normal, plane.Normal);
            if (dir.IsZero(1e-9)) continue;
            dir = dir.Normalized();
            var hits = new List<double>();
            var origin = Vector3d.Zero;
            var haveOrigin = false;
            foreach (var loop in face.Loops)
            {
                foreach (var (va, vb) in loop.Segments())
                {
                    var a = va.Position;
                    var b = vb.Position;
                    var da = plane.SignedDistance(a);
                    var db = plane.SignedDistance(b);
                    // Half-open test so a vertex exactly on the plane counts once.
                    if ((da > 0) == (db > 0)) continue;
                    var p = a + (b - a) * (da / (da - db));
                    if (!haveOrigin)
                    {
                        origin = p;
                        haveOrigin = true;
                    }
                    hits.Add(Vector3d.Dot(p - origin, dir));
                }
            }
            if (hits.Count < 2) continue;
            hits.Sort();
            for (var i = 0; i + 1 < hits.Count; i += 2)
                AddLine(_sectionLines, origin + dir * hits[i], origin + dir * hits[i + 1], cutColor, 3f, false);
        }
    }

    private static void SetClip(ShaderProgram shader, (float X, float Y, float Z, float W) c) => shader.Set("uClip", c.X, c.Y, c.Z, c.W);

    private static void AddLine(VertexBatch batch, Vector3d a, Vector3d b, DrawColor c, float width, bool dashed) =>
        AddLine(batch, a, b, c, width, dashed ? 1f : 0f);

    private static void AddLine(VertexBatch batch, Vector3d a, Vector3d b, DrawColor c, float width, float pattern)
    {
        float r = c.R / 255f, g = c.G / 255f, bl = c.B / 255f, al = c.A / 255f;
        Span<float> v = stackalloc float[18]
        {
            (float)a.X, (float)a.Y, (float)a.Z, r, g, bl, al, width, pattern,
            (float)b.X, (float)b.Y, (float)b.Z, r, g, bl, al, width, pattern,
        };
        batch.Data.AddRange(v);
        batch.MarkDirty();
    }

    private static void AddAxes(VertexBatch batch, Camera camera, double near, double far, double aspect, ModelAxes axes)
    {
        // "Infinite" axes: long enough to cross the whole view at any zoom (depth clamping keeps them from being cut
        // by the near/far planes, which made them look scaled with the model).
        var o = axes.Origin;
        var viewSize = (camera.IsPerspective ? camera.Distance : camera.OrthoHeight) * Math.Max(aspect, 1);
        var lateral = (o - camera.Target).Length + viewSize * 2 + 1;

        // Along the view direction the screen length shrinks, so the reach grows to still cross the screen.
        double Reach(Vector3d d)
        {
            var sideways = Math.Sqrt(Math.Max(1 - Math.Pow(Vector3d.Dot(d, camera.Direction), 2), 1e-4));
            return lateral / sideways;
        }

        foreach (var (dir, color) in new[] { (axes.X, DrawColor.AxisRed), (axes.Y, DrawColor.AxisGreen), (axes.Z, DrawColor.AxisBlue) })
        {
            AddLine(batch, o, o + dir * Reach(dir), color, 1, false);
            AddLine(batch, o, o - dir * Reach(-dir), color, 1, 2f);
        }
    }

    private readonly record struct Rgba(float R, float G, float B, float A)
    {
        public static Rgba Of(DrawColor c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        public static Rgba Of(Rgb c, double alpha = 1) => new(c.R / 255f, c.G / 255f, c.B / 255f, (float)alpha);

        /// <summary>Blends toward white (used to fade geometry outside the group being edited).</summary>
        public Rgba Faded() => new(R + (1 - R) * 0.65f, G + (1 - G) * 0.65f, B + (1 - B) * 0.65f, A);
    }

    /// <summary>
    /// Cached drawing data of one top-level group (a dwelling, say): reused while the group, its selection and the
    /// editing context are unchanged, so an edit only rebuilds the group it touched.
    /// </summary>
    private sealed class Chunk
    {
        public required long Version;
        public required Tag? Tag;
        public required Material? Material;
        public required int Selection;
        public required Entities? Active;
        public required bool Faded;
        public required float[] Faces, Transparent, Lines, BackEdges, Sections;
        public required (Vector3d A, Vector3d B, Vector3d N1, Vector3d? N2)[] Profiles;
        public required (Vector3d Anchor, string Text, DrawColor Color)[] Labels;
    }

    private Dictionary<Group, Chunk> _chunks = new();
    private Dictionary<Group, Chunk> _nextChunks = new();
    private Dictionary<Group, int> _selectionByGroup = new();
    private long _displayVersion;
    private Entities? _root;

    /// <summary>Per top-level group, a hash of the selected entities inside it (so selecting inside one group only rebuilds that one).</summary>
    private static Dictionary<Group, int> SelectionByTopGroup(Entities root, IReadOnlySet<Entity> selection)
    {
        var ids = new Dictionary<Group, List<int>>();
        foreach (var e in selection)
        {
            Group? top = null;
            for (var g = e.Parent?.Owner; g is not null; g = g.Parent?.Owner)
                if (ReferenceEquals(g.Parent, root)) top = g;
            if (top is null) continue;
            if (!ids.TryGetValue(top, out var list)) ids[top] = list = new List<int>();
            list.Add(e.Id);
        }
        return ids.ToDictionary(kv => kv.Key, kv =>
        {
            kv.Value.Sort();
            var h = new HashCode();
            foreach (var id in kv.Value) h.Add(id);
            return h.ToHashCode();
        });
    }

    private void CollectTopGroup(Group g, IReadOnlySet<Entity> selection, Entities active)
    {
        // Selection inside the group, and whether the group itself is selected (its edges then turn blue).
        var groupSelected = selection.Contains(g);
        var sel = HashCode.Combine(_selectionByGroup.GetValueOrDefault(g), groupSelected);
        // Editing inside this group: the exact edit context matters; otherwise only whether the group is faded.
        var activeKey = Entities.IsWithin(active, g.Entities) ? active : null;
        var faded = activeKey is null && !Entities.IsWithin(g.Entities, active);
        if (_chunks.TryGetValue(g, out var c) && c.Version == g.Entities.Version && c.Tag == g.Tag && c.Material == g.Material
            && c.Selection == sel && ReferenceEquals(c.Active, activeKey) && c.Faded == faded)
        {
            _faces.Data.AddRange(c.Faces);
            _transparentFaces.Data.AddRange(c.Transparent);
            _modelLines.Data.AddRange(c.Lines);
            _backEdges.Data.AddRange(c.BackEdges);
            _sectionLines.Data.AddRange(c.Sections);
            _profileCandidates.AddRange(c.Profiles);
            _modelLabels.AddRange(c.Labels);
            _nextChunks[g] = c;
            return;
        }

        int f0 = _faces.Data.Count, t0 = _transparentFaces.Data.Count, l0 = _modelLines.Data.Count, b0 = _backEdges.Data.Count,
            s0 = _sectionLines.Data.Count, p0 = _profileCandidates.Count, m0 = _modelLabels.Count;
        Collect(g.Entities, selection, active, groupSelected);
        // Images live in a separate structure; groups containing them are simply not cached.
        if (g.Entities.AllImages().Any()) return;
        float[] Slice(List<float> list, int from) => list.GetRange(from, list.Count - from).ToArray();
        _nextChunks[g] = new Chunk
        {
            Version = g.Entities.Version, Tag = g.Tag, Material = g.Material, Selection = sel, Active = activeKey, Faded = faded,
            Faces = Slice(_faces.Data, f0), Transparent = Slice(_transparentFaces.Data, t0), Lines = Slice(_modelLines.Data, l0),
            BackEdges = Slice(_backEdges.Data, b0), Sections = Slice(_sectionLines.Data, s0),
            Profiles = _profileCandidates.GetRange(p0, _profileCandidates.Count - p0).ToArray(),
            Labels = _modelLabels.GetRange(m0, _modelLabels.Count - m0).ToArray(),
        };
    }

    private void BuildModelMesh(Entities? entities, IReadOnlySet<Entity> selection, Entities? active)
    {
        _meshBuilds++;
        if (!ReferenceEquals(entities, _root)) _chunks.Clear();
        _root = entities;
        _nextChunks = new Dictionary<Group, Chunk>();
        _selectionByGroup = entities is null ? new() : SelectionByTopGroup(entities, selection);
        _faces.Clear();
        _transparentFaces.Clear();
        _modelLines.Clear();
        _backEdges.Clear();
        _sectionLines.Clear();
        _clip = NoClip;
        _modelLabels.Clear();
        _images.Clear();
        _profileCandidates.Clear();
        if (entities is null) return;
        _images.Prune(entities.AllImages().Select(i => i.ImageKey));
        Collect(entities, selection, active ?? entities);
        // Keep the chunks of groups that still exist, including hidden ones (shown again after leaving a group).
        foreach (var (g, c) in _chunks)
            if (!_nextChunks.ContainsKey(g) && !g.IsDeleted && ReferenceEquals(g.Parent, entities)) _nextChunks[g] = c;
        _chunks = _nextChunks;
        if (!_style.HideSectionCuts && entities.ActiveSectionPlane() is { } cut)
        {
            var n = cut.Normal;
            _clip = ((float)n.X, (float)n.Y, (float)n.Z, (float)-Vector3d.Dot(n, cut.Point));
            AddSectionCut(entities, cut.Plane);
        }
        _sectionLines.MarkDirty();
        _faces.MarkDirty();
        _transparentFaces.MarkDirty();
        _modelLines.MarkDirty();
        _backEdges.MarkDirty();
    }

    /// <param name="selectedAncestor">True inside a selected group: its edges and picture outlines draw in the selection colour.</param>
    private void Collect(Entities container, IReadOnlySet<Entity> selection, Entities active, bool selectedAncestor = false)
    {
        var faded = !Entities.IsWithin(container, active);
        var style = _style;

        if (style.FaceStyle != FaceStyle.Wireframe)
        {
            foreach (var face in container.Faces)
            {
                if (!Entities.IsEffectivelyVisible(face)) continue;
                List<Vector3d> tris;
                try
                {
                    tris = FaceTriangulator.Triangulate(face);
                }
                catch
                {
                    continue; // a degenerate face must not break drawing of the rest
                }

                var (front, back) = FaceColors(face, style, _background);
                if (faded)
                {
                    front = front.Faded();
                    back = back.Faded();
                }
                var transparent = front.A < 0.999f || back.A < 0.999f;
                var batch = transparent ? _transparentFaces : _faces;
                var n = face.Normal;
                var sel = selection.Contains(face) ? 1f : 0f;
                foreach (var p in tris)
                {
                    batch.Data.AddRange(new[]
                    {
                        (float)p.X, (float)p.Y, (float)p.Z, (float)n.X, (float)n.Y, (float)n.Z,
                        front.R, front.G, front.B, front.A, back.R, back.G, back.B, back.A, sel,
                    });
                }
            }
        }

        foreach (var e in container.Edges)
        {
            if (!Entities.IsEffectivelyVisible(e)) continue;
            var selected = selectedAncestor || selection.Contains(e);
            if (e.Soft && !selected) continue;
            var color = selected ? DrawColor.Selection : faded ? new DrawColor(185, 185, 185) : _edgeColor;
            AddLine(_modelLines, e.Start.Position, e.End.Position, color, selected ? 2f : 1f, false);
            if (!faded && !selected && _style.FaceStyle != FaceStyle.Wireframe && e.Faces.Count > 0)
            {
                // Profiles: edges with one visible face, or two whose facing differs (decided per camera).
                var visible = e.Faces.Where(Entities.IsEffectivelyVisible).ToList();
                if (visible.Count is 1 or 2)
                    _profileCandidates.Add((e.Start.Position, e.End.Position, visible[0].Normal, visible.Count == 2 ? visible[1].Normal : null));
            }
            if (_style.BackEdges && !faded)
                AddLine(_backEdges, e.Start.Position, e.End.Position, new DrawColor(90, 90, 90), 1, true);
        }

        foreach (var guide in container.Guides)
        {
            if (!Entities.IsEffectivelyVisible(guide)) continue;
            var selected = selection.Contains(guide);
            var color = selected ? DrawColor.Selection : faded ? new DrawColor(190, 190, 190) : new DrawColor(70, 70, 70);
            if (guide.IsPoint)
            {
                const double s = 4; // ~100 mm cross
                foreach (var axis in new[] { Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ })
                    AddLine(_modelLines, guide.Point - axis * s, guide.Point + axis * s, color, selected ? 2f : 1f, false);
            }
            else
            {
                const double reach = 1e5; // "infinite": clipped by the view
                AddLine(_modelLines, guide.Point - guide.Direction * reach, guide.Point + guide.Direction * reach, color,
                    selected ? 2f : 1f, true);
            }
        }

        CollectAnnotations(container, selection, faded);

        if (!_style.HideSectionPlanes)
        {
            foreach (var sp in container.Sections)
            {
                if (!Entities.IsEffectivelyVisible(sp)) continue;
                var selected = selection.Contains(sp);
                var color = selected ? DrawColor.Selection : sp.IsActive ? new DrawColor(230, 120, 30) : new DrawColor(140, 140, 140);
                var c = sp.Corners;
                for (var i = 0; i < 4; i++) AddLine(_sectionLines, c[i], c[(i + 1) % 4], color, selected ? 2.5f : 1.5f, false);
                // Arrows at the middle of each side point toward the visible side.
                var arrow = sp.Normal * sp.Size * 0.18;
                for (var i = 0; i < 4; i++)
                {
                    var mid = (c[i] + c[(i + 1) % 4]) / 2;
                    var along = (c[(i + 1) % 4] - c[i]).Normalized() * sp.Size * 0.06;
                    AddLine(_sectionLines, mid, mid + arrow, color, 1.5f, false);
                    AddLine(_sectionLines, mid + arrow, mid + arrow * 0.6 + along, color, 1.5f, false);
                    AddLine(_sectionLines, mid + arrow, mid + arrow * 0.6 - along, color, 1.5f, false);
                }
            }
        }

        foreach (var img in container.Images)
        {
            if (!Entities.IsEffectivelyVisible(img)) continue;
            _images.Add(img, faded);
            var c = img.Corners;
            var selected = selectedAncestor || selection.Contains(img);
            for (var i = 0; i < 4; i++)
                AddLine(_modelLines, c[i], c[(i + 1) % 4], selected ? DrawColor.Selection : new DrawColor(120, 120, 120),
                    selected ? 2f : 1f, false);
        }

        foreach (var g in container.Groups)
        {
            if (!Entities.IsEffectivelyVisible(g)) continue;
            var isSelected = selection.Contains(g);
            if (container.Owner is null) CollectTopGroup(g, selection, active);
            else Collect(g.Entities, selection, active, selectedAncestor || isSelected);
            if (isSelected) AddBox(g, DrawColor.Selection, dashed: false);
            else if (ReferenceEquals(g.Entities, active)) AddBox(g, new DrawColor(120, 120, 120), dashed: true);
        }
    }

    private void CollectAnnotations(Entities container, IReadOnlySet<Entity> selection, bool faded)
    {
        foreach (var d in container.Dimensions)
        {
            if (!Entities.IsEffectivelyVisible(d)) continue;
            var color = selection.Contains(d) ? DrawColor.Selection : faded ? new DrawColor(185, 185, 185) : EdgeColor;
            var a = d.Start + d.Offset;
            var b = d.End + d.Offset;
            var overshoot = d.Offset.IsZero() ? Vector3d.Zero : d.Offset.Normalized() * Math.Min(d.Length * 0.03, 4);
            AddLine(_modelLines, d.Start, a + overshoot, color, 1, false);
            AddLine(_modelLines, d.End, b + overshoot, color, 1, false);
            AddLine(_modelLines, a, b, color, 1, false);
            // Oblique ticks at both ends.
            var along = (b - a).Normalized();
            var side = d.Offset.IsZero() ? along.AnyPerpendicular() : d.Offset.Normalized();
            var tick = (along + side).Normalized() * Math.Min(d.Length * 0.025, 3);
            AddLine(_modelLines, a - tick, a + tick, color, 2, false);
            AddLine(_modelLines, b - tick, b + tick, color, 2, false);
            _modelLabels.Add(((a + b) / 2, Units.Default.Format(d.Length), color));
        }
        // Notes in a group with a material take its colour (e.g. the red DEAP opening labels).
        var noteColor = container.Owner?.Material is { } m ? new DrawColor(m.Color.R, m.Color.G, m.Color.B) : EdgeColor;
        foreach (var n in container.Notes)
        {
            if (!Entities.IsEffectivelyVisible(n)) continue;
            var color = selection.Contains(n) ? DrawColor.Selection : faded ? new DrawColor(185, 185, 185) : noteColor;
            if (n.HasLeader) AddLine(_modelLines, n.Anchor, n.LabelPosition, color, 1, false);
            _modelLabels.Add((n.LabelPosition, n.Text, color));
        }
    }

    /// <summary>Front/back colours of a face for the current style.</summary>
    private static (Rgba Front, Rgba Back) FaceColors(Face face, RenderStyle style, DrawColor background)
    {
        Rgba front, back;
        switch (style.FaceStyle)
        {
            case FaceStyle.HiddenLine:
                front = back = Rgba.Of(background);
                break;
            case FaceStyle.Monochrome:
                front = Rgba.Of(FrontFace);
                back = Rgba.Of(BackFace);
                break;
            default:
                if (style.ColorByTag)
                {
                    var tag = Entities.EffectiveTag(face);
                    front = back = Rgba.Of(tag?.Color ?? TagCollection.UntaggedColor);
                    var mat = Entities.EffectiveMaterial(face);
                    if (mat is { Alpha: < 0.999 }) front = back = front with { A = (float)mat.Alpha };
                }
                else
                {
                    var mat = Entities.EffectiveMaterial(face);
                    front = mat is null ? Rgba.Of(FrontFace) : Rgba.Of(mat.Color, mat.Alpha);
                    // Translucent materials (glass) look the same from both sides.
                    back = mat is { Alpha: < 0.999 } ? front : Rgba.Of(BackFace);
                }
                break;
        }
        if (style.XRay)
        {
            front = front with { A = Math.Min(front.A, 0.5f) };
            back = back with { A = Math.Min(back.A, 0.5f) };
        }
        return (front, back);
    }

    /// <summary>
    /// Profiles: edges on the outline of the shape (a face's open border, or where a face turned toward the
    /// camera meets one turned away) are drawn thicker, like SketchUp's default style.
    /// </summary>
    private void BuildProfiles(Camera camera)
    {
        // Profiles depend only on the camera and the mesh: skip the work on redraws that change neither.
        var key = (camera.Eye, camera.Direction, camera.IsPerspective, _meshBuilds);
        if (_profileKey == key) return;
        _profileKey = key;
        _profileLines.Clear();
        var dir = camera.Direction;
        var eye = camera.Eye;
        var perspective = camera.IsPerspective;
        foreach (var (a, b, n1, n2) in _profileCandidates)
        {
            bool outline;
            if (n2 is not { } second) outline = true;
            else
            {
                var toEye = perspective ? eye - (a + b) / 2 : -dir;
                outline = Vector3d.Dot(n1, toEye) * Vector3d.Dot(second, toEye) < 0;
            }
            if (outline) AddLine(_profileLines, a, b, _edgeColor, 2.6f, false);
        }
        _profileLines.MarkDirty();
    }

    private void AddBox(Group g, DrawColor color, bool dashed)
    {
        if (Entities.Bounds(g) is not { } b) return;
        var (n, x) = (b.Min, b.Max);
        var c = new[]
        {
            new Vector3d(n.X, n.Y, n.Z), new Vector3d(x.X, n.Y, n.Z), new Vector3d(x.X, x.Y, n.Z), new Vector3d(n.X, x.Y, n.Z),
            new Vector3d(n.X, n.Y, x.Z), new Vector3d(x.X, n.Y, x.Z), new Vector3d(x.X, x.Y, x.Z), new Vector3d(n.X, x.Y, x.Z),
        };
        int[][] edges = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 },
            new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
        foreach (var e in edges) AddLine(_modelLines, c[e[0]], c[e[1]], color, 1, dashed);
    }

    // ------------------------------------------------------------------ IViewDrawing (tool overlay)

    public void Line(Vector3d a, Vector3d b, DrawColor color, float widthPx = 1, bool dashed = false, bool depthTested = false) =>
        AddLine(depthTested ? _overlayDepthLines : _overlayLines, a, b, color, widthPx, dashed);

    public void Point(Vector3d p, DrawColor color, float sizePx = 9, PointShape shape = PointShape.Square, bool filled = true)
    {
        _overlayPoints.Data.AddRange(new[]
        {
            (float)p.X, (float)p.Y, (float)p.Z, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f,
            sizePx, (float)shape + (filled ? 4f : 0f),
        });
        _overlayPoints.MarkDirty();
    }

    public void Text(Vector3d anchor, string text, DrawColor color, double dx = 0, double dy = 0) =>
        _text.Add(anchor, text, color, dx, dy, boxed: true);

    public void Polygon(IReadOnlyList<Vector3d> points, DrawColor color)
    {
        if (points.Count < 3) return;
        // Fan triangulation is enough for the convex previews tools draw.
        for (var i = 1; i + 1 < points.Count; i++)
        {
            foreach (var p in new[] { points[0], points[i], points[i + 1] })
                _overlayPolys.Data.AddRange(new[]
                {
                    (float)p.X, (float)p.Y, (float)p.Z, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f,
                });
        }
        _overlayPolys.MarkDirty();
    }

    // ------------------------------------------------------------------ image export

    /// <summary>
    /// Renders the scene into an off-screen framebuffer and returns the pixels as top-down BGRA rows.
    /// Works even when the window is covered by other windows.
    /// </summary>
    public unsafe byte[] RenderToImage(Camera camera, int width, int height, Vector3d sceneCenter, double sceneRadius, RenderScene scene)
    {
        var fbo = _gl.GenFramebuffer();
        var color = _gl.GenRenderbuffer();
        var depth = _gl.GenRenderbuffer();
        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, color);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)width, (uint)height);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, color);
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depth);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)width, (uint)height);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depth);
            if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                throw new InvalidOperationException("Off-screen framebuffer is incomplete.");

            Render(camera, width, height, sceneCenter, sceneRadius, scene);

            var rgba = new byte[width * height * 4];
            fixed (byte* p = rgba)
                _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, p);

            // Flip vertically and convert RGBA -> BGRA.
            var bgra = new byte[rgba.Length];
            var stride = width * 4;
            for (var y = 0; y < height; y++)
            {
                var src = (height - 1 - y) * stride;
                var dst = y * stride;
                for (var x = 0; x < stride; x += 4)
                {
                    bgra[dst + x] = rgba[src + x + 2];
                    bgra[dst + x + 1] = rgba[src + x + 1];
                    bgra[dst + x + 2] = rgba[src + x];
                    bgra[dst + x + 3] = 255;
                }
            }
            return bgra;
        }
        finally
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.DeleteRenderbuffer(color);
            _gl.DeleteRenderbuffer(depth);
            _gl.DeleteFramebuffer(fbo);
        }
    }

    // ------------------------------------------------------------------ picking

    /// <summary>
    /// Reads the depth buffer at a pixel (origin top-left) of the last rendered frame and returns the world point,
    /// or null when only background is there. Call right after <see cref="Render"/> without swapping buffers.
    /// </summary>
    public unsafe Vector3d? ReadSurfacePoint(double x, double y)
    {
        var ix = (int)Math.Round(x);
        var iy = _height - 1 - (int)Math.Round(y);
        if (ix < 0 || iy < 0 || ix >= _width || iy >= _height) return null;
        float depth = 1;
        _gl.ReadPixels(ix, iy, 1, 1, PixelFormat.DepthComponent, PixelType.Float, &depth);
        if (depth >= 0.999999f) return null;
        var ndc = new Vector3d(2 * (ix + 0.5) / _width - 1, 2 * (iy + 0.5) / _height - 1, 2 * depth - 1);
        return _viewProj.Inverse().TransformPoint(ndc);
    }

    public void Dispose()
    {
        foreach (var b in new[] { _faces, _transparentFaces, _modelLines, _backEdges, _sectionLines, _axisLines, _overlayLines, _overlayDepthLines, _overlayPoints, _overlayPolys })
            b.Dispose();
        foreach (var s in new[] { _lineShader, _pointShader, _faceShader, _flatShader })
            s.Dispose();
        _text.Dispose();
        _profileLines.Dispose();
        _images.Dispose();
    }
}
