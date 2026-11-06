using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Drawing;

/// <summary>Shared plane choice for drawing tools: arrow key before the first click, else the face clicked, else the ground.</summary>
internal static class DrawingPlanes
{
    public static Vector3d? FromArrow(ViewKey key, ModelAxes axes) => key switch
    {
        ViewKey.Up => axes.Z,
        ViewKey.Right => axes.Y, // vertical plane along the red axis
        ViewKey.Left => axes.X,  // vertical plane along the green axis
        _ => null,
    };

    public static Vector3d NormalFor(Vector3d? forced, InferenceResult r, ModelAxes axes, Camera camera) =>
        forced ?? r.Face?.Normal ?? r.Image?.Normal ?? InferenceEngine.ViewPlaneNormal(camera, axes);

    /// <summary>The plane a click inside a face (or an image) fixes; corners, edges and empty space fix none.</summary>
    public static Vector3d? FixedBy(InferenceResult r) => r.Kind switch
    {
        InferenceKind.OnFace => r.Face?.Normal,
        InferenceKind.OnImage => r.Image?.Normal,
        _ => null,
    };

    /// <summary>The cursor snapped to model geometry (a point that can decide the drawing plane).</summary>
    public static bool OnGeometry(InferenceResult r) => r.Kind is InferenceKind.Endpoint
        or InferenceKind.Midpoint or InferenceKind.Intersection or InferenceKind.Origin
        or InferenceKind.OnEdge or InferenceKind.OnFace or InferenceKind.OnGuide
        or InferenceKind.GuidePoint or InferenceKind.OnImage;

    /// <summary>Normals of the planes a shape started at a point may lie on: the drawing axes' planes and the faces there.</summary>
    public static List<Vector3d> PlanesAt(InferenceResult r, ModelAxes axes)
    {
        var list = new List<Vector3d> { axes.Z, axes.X, axes.Y };
        AddFacePlanes(list, r);
        return list;
    }

    public static void AddFacePlanes(List<Vector3d> list, InferenceResult r)
    {
        var faces = r.Vertex?.Edges.SelectMany(e => e.Faces) ?? r.Edge?.Faces ?? (r.Face is { } f ? new[] { f } : Enumerable.Empty<Face>());
        foreach (var face in faces)
            if (!list.Any(n => Segment3d.AreParallel(n, face.Normal))) list.Add(face.Normal);
    }

    /// <summary>
    /// Plane for a shape (rectangle) started at a corner, an edge or in empty space: the plane that holds the way
    /// from the start to the cursor, as in SketchUp, so the same corner of a box can start a horizontal or a
    /// vertical rectangle depending on where the cursor goes. When several planes hold it (the cursor runs along an
    /// axis) the one facing the view wins; with the cursor in empty space it is the plane facing the view.
    /// </summary>
    public static Vector3d FollowCursor(Vector3d start, InferenceResult cursor, IReadOnlyList<Vector3d> planes,
        Camera camera, ModelAxes axes)
    {
        var view = InferenceEngine.ViewPlaneNormal(camera, axes);
        var d = cursor.Position - start;
        if (!OnGeometry(cursor) || d.Length <= Tolerance.Length * 10) return view;
        d = d.Normalized();
        var best = planes.Min(n => Math.Abs(Vector3d.Dot(d, n)));
        var fits = planes.Where(n => Math.Abs(Vector3d.Dot(d, n)) <= best + 0.02).ToList();
        if (fits.Any(n => Segment3d.AreParallel(n, view))) return view;
        return fits.MaxBy(n => Math.Abs(Vector3d.Dot(n, camera.Direction)))!;
    }

    /// <summary>
    /// Plane of an arc whose chord runs from a to b, through a cursor point on model geometry (snapped to one of
    /// the given planes when within a few degrees). Null when the cursor is in empty space or on the chord's line.
    /// </summary>
    public static Vector3d? ThroughCursor(Vector3d a, Vector3d b, InferenceResult cursor, IReadOnlyList<Vector3d> planes)
    {
        if (!OnGeometry(cursor)) return null;
        var chord = b - a;
        var toCursor = cursor.Position - a;
        var n = Vector3d.Cross(chord, toCursor);
        if (n.Length <= 1e-3 * chord.Length * toCursor.Length) return null;
        n = n.Normalized();
        var c = chord.Normalized();
        foreach (var p in planes)
            if (Math.Abs(Vector3d.Dot(p, c)) < 1e-6 && Math.Abs(Vector3d.Dot(p, n)) > Math.Cos(3 * Math.PI / 180)) return p;
        return n;
    }

    /// <summary>
    /// Plane of an arc over a chord when the cursor gives none: the face the arc was started inside, else the plane
    /// facing the view if it holds the chord, else horizontal for a level chord, front-facing for a vertical one and
    /// the vertical plane through a sloped one.
    /// </summary>
    public static Vector3d ChordDefault(Vector3d chord, Vector3d? startFace, Camera camera, ModelAxes axes)
    {
        chord = chord.Normalized();
        if (startFace is { } f && Math.Abs(Vector3d.Dot(f, chord)) < 1e-6) return f;
        var view = InferenceEngine.ViewPlaneNormal(camera, axes);
        if (Math.Abs(Vector3d.Dot(view, chord)) < 1e-6) return view;
        if (Math.Abs(Vector3d.Dot(chord, axes.Z)) < 1e-9) return axes.Z;
        if (Segment3d.AreParallel(chord, axes.Z)) return axes.Y;
        return Vector3d.Cross(chord, axes.Z).Normalized();
    }
}

/// <summary>
/// Freehand: press and drag to draw a curve, release to finish. Ending at the start point closes the shape (and
/// makes a face). The curve lies on the face under the press, otherwise horizontal; arrow keys pick the plane.
/// </summary>
public sealed class FreehandTool : InferenceToolBase
{
    private readonly List<Vector3d> _points = new();
    private Plane3d _plane = Plane3d.Ground;
    private Vector3d? _forced;
    private bool _drawing;

    public override string Id => "draw/freehand";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string StatusText => _drawing
        ? "Drag to draw; release to finish the curve. Return to the start point to close the shape."
        : "Click and drag to draw a freehand curve. Arrows choose the plane (↑ horizontal, → red, ← green).";

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _points.Clear();
        _drawing = false;
        ClearInference(view);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (!_drawing && DrawingPlanes.FromArrow(key, view.Model.Axes) is { } n)
        {
            _forced = _forced is { } f && f.IsEqual(n) ? null : n;
            return true;
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        _plane = new Plane3d(r.Position, DrawingPlanes.NormalFor(_forced, r, view.Model.Axes, view.Camera));
        _points.Clear();
        _points.Add(r.Position);
        _drawing = true;
        NotifyStateChanged();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (!_drawing)
        {
            UpdateInference(view, e.X, e.Y);
            view.Invalidate();
            return;
        }
        var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
        if (_plane.Intersect(ray) is not { } t) return;
        var p = ray.PointAt(t);
        var upp = view.Camera.UnitsPerPixel(p, view.Height);
        if (Vector3d.Distance(p, _points[^1]) > 6 * upp)
        {
            _points.Add(p);
            view.Invalidate();
        }
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (!_drawing || e.Button != MouseButton.Left) return;
        _drawing = false;
        if (_points.Count >= 2)
        {
            var upp = view.Camera.UnitsPerPixel(_points[0], view.Height);
            var closed = _points.Count >= 4 && Vector3d.Distance(_points[0], _points[^1]) < 10 * upp;
            var pts = closed ? _points.Take(_points.Count - 1).ToList() : _points.ToList();
            view.Model.Run("Freehand", () => view.Model.ActiveEntities.AddPolyline(pts, closed));
        }
        _points.Clear();
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        for (var i = 1; i < _points.Count; i++) g.Line(_points[i - 1], _points[i], DrawColor.Preview, 1.5f);
        if (!_drawing) DrawInference(g);
    }
}

/// <summary>
/// Rotated Rectangle: click the first corner, click the second corner (or type the length), then move to set the
/// width and tilt (or type "width" or "width;angle") and click. The rectangle may lie at any angle.
/// </summary>
public sealed class RotatedRectangleTool : InferenceToolBase
{
    private Vector3d? _a;
    private Vector3d? _b;
    private Vector3d _baseNormal = Vector3d.UnitZ;
    private Vector3d? _forced;

    public override string Id => "draw/rotated rectangle";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => _b is null ? "Length" : "Width; Angle";

    public override string StatusText => _a is null ? "Select first corner. Arrows choose the plane."
        : _b is null ? "Select second corner or enter length."
        : "Select width and angle or enter \"width;angle\".";

    public override string MeasurementValue
    {
        get
        {
            if (_a is not { } a || Current is not { } c) return string.Empty;
            if (_b is null) return Units.Default.Format(Vector3d.Distance(a, c.Position));
            var (w, angle) = WidthAndAngle(c.Position);
            return $"{Units.Default.Format(Math.Abs(w))}; {angle:0.#}°";
        }
    }

    protected override Vector3d? From => _b ?? _a;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _a = null;
        _b = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (_a is null && DrawingPlanes.FromArrow(key, view.Model.Axes) is { } n)
        {
            _forced = _forced is { } f && f.IsEqual(n) ? null : n;
            return true;
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    private Vector3d EdgeDir => (_b!.Value - _a!.Value).Normalized();

    /// <summary>Side direction in the base plane (perpendicular to the first edge).</summary>
    private Vector3d BaseSide => Vector3d.Cross(_baseNormal, EdgeDir).Normalized();

    /// <summary>Width (signed along the base side) and tilt angle out of the base plane for a cursor point.</summary>
    private (double Width, double Angle) WidthAndAngle(Vector3d p)
    {
        var rel = p - _a!.Value;
        var perp = rel - EdgeDir * Vector3d.Dot(rel, EdgeDir);
        var width = perp.Length;
        if (width < Tolerance.Length) return (0, 0);
        var angle = Shapes.SignedAngle(BaseSide, perp, EdgeDir) * 180 / Math.PI;
        return (width, angle);
    }

    private Vector3d[] Corners(double width, double angleDeg)
    {
        var side = Matrix4d.Rotation(EdgeDir, angleDeg * Math.PI / 180).TransformVector(BaseSide) * width;
        return new[] { _a!.Value, _b!.Value, _b.Value + side, _a.Value + side };
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_a is null)
        {
            _a = r.Position;
            _baseNormal = DrawingPlanes.NormalFor(_forced, r, view.Model.Axes, view.Camera);
        }
        else if (_b is null) SetSecond(r.Position);
        else
        {
            var (w, angle) = WidthAndAngle(r.Position);
            Create(view, w, angle);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void SetSecond(Vector3d b)
    {
        if (_a is not { } a || Vector3d.Distance(a, b) <= Tolerance.Length) return;
        _b = b;
        // The base plane must contain the first edge.
        var dir = (b - a).Normalized();
        if (Math.Abs(Vector3d.Dot(dir, _baseNormal)) > 1e-6)
        {
            var n = Vector3d.Cross(Vector3d.Cross(dir, _baseNormal), dir);
            _baseNormal = n.IsZero(1e-9) ? dir.AnyPerpendicular() : n.Normalized();
        }
        Lock = null;
    }

    private void Create(IView view, double width, double angle)
    {
        if (width <= Tolerance.Length) return;
        var pts = Corners(width, angle);
        view.Model.Run("Rotated Rectangle", () => view.Model.ActiveEntities.AddPolyline(pts, closed: true));
        _a = null;
        _b = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_a is not { } a) return false;
        if (_b is null)
        {
            if (!Units.Default.TryParseLength(text, out var length) || Current is not { } c) return false;
            var dir = (c.Position - a).Normalized();
            if (dir.IsZero()) return false;
            SetSecond(a + dir * length);
        }
        else
        {
            var parts = Units.SplitList(text);
            if (parts.Length is < 1 or > 2 || !Units.Default.TryParseLength(parts[0], out var width)) return false;
            var current = Current is { } cur ? WidthAndAngle(cur.Position).Angle : 0;
            var angle = current;
            if (parts.Length == 2 && !Units.TryParseNumber(parts[1], out angle)) return false;
            Create(view, Math.Abs(width), angle);
        }
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_a is { } a && Current is { } c)
        {
            if (_b is null) g.Line(a, c.Position, RubberBandColor, 1.5f);
            else
            {
                var (w, angle) = WidthAndAngle(c.Position);
                if (w > Tolerance.Length)
                {
                    var pts = Corners(w, angle);
                    g.Polygon(pts, new DrawColor(80, 130, 220, 40));
                    for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], DrawColor.Preview, 1.5f);
                }
                else g.Line(a, _b.Value, DrawColor.Preview, 1.5f);
            }
        }
        DrawInference(g);
    }
}

/// <summary>
/// Arc (centre) and Pie: click the centre, click the start point (or type the radius), then click the end point
/// (or type the angle). Pie also draws the two radii and makes a face. Type "12s" for the segment count.
/// </summary>
public class CenterArcTool : InferenceToolBase
{
    private Vector3d? _center;
    private Vector3d? _start;
    private Vector3d _normal = Vector3d.UnitZ;
    private Vector3d? _forced;
    private double _sweep;
    private int _segments = ArcTool.DefaultSegments;

    protected virtual bool IsPie => false;

    public override string Id => "draw/arc center";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => _start is null ? "Radius" : "Angle";

    public override string StatusText => _center is null ? "Select center point. Arrows choose the plane."
        : _start is null ? "Select start point or enter radius."
        : "Select end point or enter angle. Type e.g. 12s for the number of segments.";

    public override string MeasurementValue => _center is { } c && Current is { } cur
        ? _start is null ? Units.Default.Format(Vector3d.Distance(c, Plane.Project(cur.Position))) : $"{Math.Abs(_sweep) * 180 / Math.PI:0.#}°"
        : string.Empty;

    private Plane3d Plane => new(_center ?? Vector3d.Zero, _normal);

    protected override Vector3d? From => _start is null ? _center : null;
    protected override Plane3d? DrawingPlane => _center is null ? null : Plane;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _center = null;
        _start = null;
        _sweep = 0;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (_center is null && DrawingPlanes.FromArrow(key, view.Model.Axes) is { } n)
        {
            _forced = _forced is { } f && f.IsEqual(n) ? null : n;
            return true;
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    private void UpdateSweep()
    {
        if (_center is not { } c || _start is not { } s || Current is null) return;
        var p = Plane.Project(Current.Position);
        if (Vector3d.Distance(p, c) < Tolerance.Length) return;
        var a = Shapes.SignedAngle(s - c, p - c, _normal);
        // Keep the sweep continuous so arcs can go past half a turn.
        var best = a;
        foreach (var k in new[] { -1, 1 })
            if (Math.Abs(a + k * 2 * Math.PI - _sweep) < Math.Abs(best - _sweep)) best = a + k * 2 * Math.PI;
        _sweep = Math.Clamp(best, -2 * Math.PI, 2 * Math.PI);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        UpdateSweep();
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_center is null)
        {
            _center = r.Position;
            _normal = DrawingPlanes.NormalFor(_forced, r, view.Model.Axes, view.Camera).Normalized();
        }
        else if (_start is null)
        {
            var p = Plane.Project(r.Position);
            if (Vector3d.Distance(p, _center.Value) > Tolerance.Length)
            {
                _start = p;
                _sweep = 0;
            }
        }
        else
        {
            UpdateSweep();
            Create(view, _sweep);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private List<Vector3d> Points(double sweep) => Shapes.ArcAround(_center!.Value, _normal, _start!.Value, sweep, _segments);

    private void Create(IView view, double sweep)
    {
        if (_center is not { } c || _start is null || Math.Abs(sweep) < 1e-6) return;
        var pts = Points(sweep);
        var full = Math.Abs(Math.Abs(sweep) - 2 * Math.PI) < 1e-9;
        if (full) pts.RemoveAt(pts.Count - 1);
        view.Model.Run(IsPie ? "Pie" : "Arc", () =>
        {
            if (IsPie && !full) view.Model.ActiveEntities.AddPolyline(pts.Prepend(c).ToList(), closed: true);
            else view.Model.ActiveEntities.AddPolyline(pts, closed: full);
        });
        _center = null;
        _start = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (MeasurementInput.TryParseSegments(text, out var segments))
        {
            _segments = segments;
            view.Invalidate();
            return true;
        }
        if (_center is not { } c) return false;
        if (_start is null)
        {
            if (!Units.Default.TryParseLength(text, out var radius) || radius <= Tolerance.Length) return false;
            var toward = Current is { } cur ? Plane.Project(cur.Position) - c : Vector3d.Zero;
            if (toward.IsZero(1e-9)) toward = _normal.AnyPerpendicular();
            _start = c + toward.Normalized() * radius;
            _sweep = 0;
        }
        else
        {
            var t = text.Trim().TrimEnd('°');
            if (!Units.TryParseNumber(t, out var degrees) || degrees == 0) return false;
            var sign = _sweep < 0 ? -1 : 1;
            Create(view, sign * Math.Clamp(Math.Abs(degrees), 0, 360) * Math.PI / 180);
        }
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_center is { } c && Current is { } cur)
        {
            g.Point(c, DrawColor.Black, 7, PointShape.Cross);
            if (_start is { } s)
            {
                g.Line(c, s, DrawColor.Preview, 1, dashed: true);
                if (Math.Abs(_sweep) > 1e-6)
                {
                    var pts = Points(_sweep);
                    for (var i = 0; i + 1 < pts.Count; i++) g.Line(pts[i], pts[i + 1], DrawColor.Preview, 1.5f);
                    if (IsPie)
                    {
                        g.Line(c, pts[0], DrawColor.Preview, 1.5f);
                        g.Line(c, pts[^1], DrawColor.Preview, 1.5f);
                    }
                }
            }
            else g.Line(c, Plane.Project(cur.Position), RubberBandColor, 1, dashed: true);
        }
        DrawInference(g);
    }
}

/// <summary>Pie: a centre arc closed with its two radii (a face).</summary>
public sealed class PieTool : CenterArcTool
{
    protected override bool IsPie => true;
    public override string Id => "draw/pie";
}

/// <summary>
/// 3 Point Arc: click the start point, click a second point the arc passes through, then click the end point.
/// The arc lies on the face under the start, otherwise in the plane chosen like the 2 Point Arc.
/// </summary>
public sealed class ThreePointArcTool : InferenceToolBase
{
    private Vector3d? _a;
    private Vector3d? _b;
    private Face? _startFace;
    private Vector3d? _fixedNormal;
    private List<Vector3d> _planes = new();
    private Vector3d _normal = Vector3d.UnitZ;
    private Vector3d _defaultNormal = Vector3d.UnitZ;
    private bool _probing;
    private int _segments = ArcTool.DefaultSegments;

    public override string Id => "draw/arc 3 point";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => "Length";

    public override string StatusText => _a is null ? "Select start point."
        : _b is null ? "Select second point on the arc."
        : "Select end point. Type e.g. 12s for the number of segments.";

    public override string MeasurementValue => _a is { } a && Current is { } c ? Units.Default.Format(Vector3d.Distance(_b ?? a, c.Position)) : string.Empty;

    protected override Vector3d? From => _b ?? _a;
    protected override Face? FromFace => _startFace;
    protected override Plane3d? DrawingPlane => _b is { } b && !_probing ? new Plane3d(b, _normal) : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _a = null;
        _b = null;
        _startFace = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    /// <summary>
    /// Inference for the cursor. For the end point the arc's plane first follows the cursor: through the three
    /// points when it is on geometry, else the default for the chord (unless the arc started inside a face).
    /// </summary>
    private InferenceResult Track(IView view, double x, double y)
    {
        if (_a is { } a && _b is { } b && _fixedNormal is null)
        {
            _probing = true;
            var probe = UpdateInference(view, x, y);
            _probing = false;
            _normal = DrawingPlanes.ThroughCursor(a, b, probe, _planes) ?? _defaultNormal;
        }
        return UpdateInference(view, x, y);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        Track(view, e.X, e.Y);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = Track(view, e.X, e.Y);
        if (_a is null)
        {
            _a = r.Position;
            _startFace = r.Face;
            _planes = DrawingPlanes.PlanesAt(r, view.Model.Axes);
        }
        else if (_b is null)
        {
            if (Vector3d.Distance(_a.Value, r.Position) <= Tolerance.Length) return;
            _b = r.Position;
            DrawingPlanes.AddFacePlanes(_planes, r);
            var chord = _b.Value - _a.Value;
            var inside = _startFace is { } sf && Math.Abs(Vector3d.Dot(sf.Normal, chord.Normalized())) < 1e-6 ? sf.Normal : (Vector3d?)null;
            _fixedNormal = inside is not null && DrawingPlanes.FixedBy(r) is not null ? inside : null;
            _defaultNormal = DrawingPlanes.ChordDefault(chord, inside, view.Camera, view.Model.Axes);
            _normal = _fixedNormal ?? _defaultNormal;
            Lock = null;
        }
        else if (Shapes.ArcThrough(_a.Value, _b.Value, new Plane3d(_b.Value, _normal).Project(r.Position), _segments) is { } pts)
        {
            view.Model.Run("Arc", () => view.Model.ActiveEntities.AddPolyline(pts, closed: false));
            _a = null;
            _b = null;
            Lock = null;
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override bool OnUserText(IView view, string text)
    {
        if (!MeasurementInput.TryParseSegments(text, out var segments)) return false;
        _segments = segments;
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_a is { } a && Current is { } c)
        {
            if (_b is { } b)
            {
                var end = new Plane3d(b, _normal).Project(c.Position);
                if (Shapes.ArcThrough(a, b, end, _segments) is { } pts)
                    for (var i = 0; i + 1 < pts.Count; i++) g.Line(pts[i], pts[i + 1], DrawColor.Preview, 1.5f);
                g.Point(b, DrawColor.Black, 7, PointShape.Circle, filled: false);
            }
            else g.Line(a, c.Position, RubberBandColor, 1, dashed: true);
        }
        DrawInference(g);
    }
}
