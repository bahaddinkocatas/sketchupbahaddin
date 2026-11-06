using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools.Drawing;

/// <summary>
/// Rectangle (R): click one corner, click the opposite corner. Started inside a face, the rectangle lies on it.
/// Started at a corner, on an edge or in empty space, its plane follows the cursor as in SketchUp: the plane that
/// holds the way to the snapped point (e.g. another corner at the same height gives a horizontal rectangle), or
/// the plane facing the view in empty space. Arrow keys lock the plane (Up = horizontal, Right = red/vertical,
/// Left = green/vertical; again = unlock). The sides follow the drawing axes, turned to line up with the edges of
/// the faces at the start that lie in the rectangle's plane, so a rectangle started on a building that is not
/// square to the axes lines up with it (loose lines such as the north arrow do not turn it). Type "width,height" in the Measurements box.
/// </summary>
public sealed class RectangleTool : InferenceToolBase
{
    private Vector3d? _corner;
    private Plane3d _plane = Plane3d.Ground;
    private Vector3d _u = Vector3d.UnitX, _v = Vector3d.UnitY;
    private Vector3d? _forcedNormal;
    private Vector3d? _fixedNormal; // the face the rectangle was started inside
    private List<Vector3d> _planes = new();
    // Edges of faces at the start (direction, normal of the face it bounds) the sides may line up with.
    private List<(Vector3d Direction, Vector3d FaceNormal)> _faceEdges = new();
    private bool _probing;
    private bool _isSquare;

    public override string Id => "draw/rectangle";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => "Dimensions";

    public override string StatusText => _corner is null
        ? "Select first corner. Arrows choose the plane (↑ horizontal, → red, ← green)."
        : "Select opposite corner or enter value (\"width;height\"). Arrows lock the plane.";

    public override string MeasurementValue
    {
        get
        {
            if (_corner is null || Current is null) return string.Empty;
            var (a, b) = Dimensions(Current.Position);
            return $"{Units.Default.Format(Math.Abs(a))}; {Units.Default.Format(Math.Abs(b))}";
        }
    }

    // No axis lines from the first corner: they only lead to zero-height rectangles. Points, edges and
    // alignments with other points still snap.
    protected override Vector3d? From => null;
    protected override Plane3d? DrawingPlane => _corner is null || _probing ? null : _plane;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _corner = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (DrawingPlanes.FromArrow(key, view.Model.Axes) is { } n)
        {
            _forcedNormal = _forcedNormal is { } f && f.IsEqual(n) ? null : n;
            if (_corner is { } c)
            {
                SetupPlane(c, _forcedNormal ?? _fixedNormal ?? _plane.Normal, view.Model.Axes);
                Track(view, MouseX, MouseY);
                NotifyStateChanged();
            }
            view.Invalidate();
            return true;
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    /// <summary>Inference for the cursor; after the first corner the plane first follows the cursor (unless locked).</summary>
    private InferenceResult Track(IView view, double x, double y)
    {
        if (_corner is { } c && (_forcedNormal ?? _fixedNormal) is null)
        {
            _probing = true;
            var probe = UpdateInference(view, x, y);
            _probing = false;
            var n = DrawingPlanes.FollowCursor(c, probe, _planes, view.Camera, view.Model.Axes);
            if (!Segment3d.AreParallel(n, _plane.Normal)) SetupPlane(c, n, view.Model.Axes);
        }
        return UpdateInference(view, x, y);
    }

    private void SetupPlane(Vector3d origin, Vector3d normal, ModelAxes axes)
    {
        normal = normal.Normalized();
        _plane = new Plane3d(origin, normal);
        // The drawing axes projected onto the plane...
        var u = axes.X - normal * Vector3d.Dot(axes.X, normal);
        if (u.Length < 0.1) u = axes.Y - normal * Vector3d.Dot(axes.Y, normal);
        u = u.Normalized();
        // ...turned (by less than 45°) onto the start's edge that is closest to them, if one lies in the plane.
        var side = Vector3d.Cross(normal, u);
        var turns = _faceEdges.Where(e => Segment3d.AreParallel(e.FaceNormal, normal)).Select(e => e.Direction)
            .Select(d =>
            {
                var angle = Math.Atan2(Vector3d.Dot(d, side), Vector3d.Dot(d, u));
                return angle - Math.Round(angle / (Math.PI / 2)) * (Math.PI / 2);
            })
            .ToList();
        if (turns.Count > 0)
        {
            var turn = turns.MinBy(Math.Abs);
            u = (u * Math.Cos(turn) + side * Math.Sin(turn)).Normalized();
        }
        _u = u;
        _v = Vector3d.Cross(normal, _u).Normalized();
    }

    /// <summary>
    /// Edges at the start point that bound a face (the corner's or edge's faces, or the face started in), with that
    /// face's normal. Only these line the rectangle up: a loose line through the start (e.g. the north arrow at the
    /// origin) says nothing about how the rectangle should sit, and a face's edge only counts in that face's plane.
    /// </summary>
    private static List<(Vector3d Direction, Vector3d FaceNormal)> FaceEdges(InferenceResult r)
    {
        var list = new List<(Vector3d, Vector3d)>();
        void Add(Vector3d a, Vector3d b, Vector3d normal)
        {
            var d = b - a;
            if (!d.IsZero(1e-9)) list.Add((d.Normalized(), normal));
        }
        if (r.Kind == InferenceKind.OnFace && r.Face is { } face)
        {
            var loop = face.OuterLoop.Vertices;
            for (var i = 0; i < loop.Count; i++) Add(loop[i].Position, loop[(i + 1) % loop.Count].Position, face.Normal);
            return list;
        }
        var edges = r.Vertex?.Edges ?? (r.Edge is { } e ? new[] { e } : Array.Empty<Edge>());
        foreach (var edge in edges)
            foreach (var f in edge.Faces)
                Add(edge.Start.Position, edge.End.Position, f.Normal);
        return list;
    }

    private (double A, double B) Dimensions(Vector3d p)
    {
        var d = p - _corner!.Value;
        return (Vector3d.Dot(d, _u), Vector3d.Dot(d, _v));
    }

    private Vector3d[] Corners(double a, double b)
    {
        var c = _corner!.Value;
        return new[] { c, c + _u * a, c + _u * a + _v * b, c + _v * b };
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var r = Track(view, e.X, e.Y);
        if (_corner is not null)
        {
            // Snap to a square when the sides are within a few pixels of each other.
            var (a, b) = Dimensions(_plane.Project(r.Position));
            var upp = view.Camera.UnitsPerPixel(_corner.Value, view.Height);
            _isSquare = Math.Abs(Math.Abs(a) - Math.Abs(b)) < 6 * upp && Math.Abs(a) > 10 * upp;
            if (_isSquare) view.ShowTooltip("Square",e.X, e.Y);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = Track(view, e.X, e.Y);
        if (_corner is null)
        {
            _fixedNormal = DrawingPlanes.FixedBy(r);
            _planes = DrawingPlanes.PlanesAt(r, view.Model.Axes);
            _faceEdges = FaceEdges(r);
            var normal = _forcedNormal ?? _fixedNormal ?? Sketch.Inference.InferenceEngine.ViewPlaneNormal(view.Camera, view.Model.Axes);
            _corner = r.Position;
            SetupPlane(r.Position, normal, view.Model.Axes);
        }
        else
        {
            var (a, b) = Dimensions(_plane.Project(r.Position));
            if (_isSquare) b = Math.Sign(b == 0 ? 1 : b) * Math.Abs(a);
            Create(view, a, b);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void Create(IView view, double a, double b)
    {
        if (Math.Abs(a) <= Tolerance.Length || Math.Abs(b) <= Tolerance.Length) return;
        var corners = Corners(a, b);
        view.Model.Run("Rectangle", () => view.Model.ActiveEntities.AddPolyline(corners, closed: true));
        _corner = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_corner is null || Current is null) return false;
        var parts = Units.SplitList(text);
        if (parts.Length != 2) return false;
        if (!Units.Default.TryParseLength(parts[0], out var a) || !Units.Default.TryParseLength(parts[1], out var b)) return false;
        // Typed sizes follow the direction the mouse was dragged.
        var (ca, cb) = Dimensions(_plane.Project(Current.Position));
        Create(view, (ca < 0 ? -1 : 1) * Math.Abs(a), (cb < 0 ? -1 : 1) * Math.Abs(b));
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_corner is not null && Current is { } c)
        {
            var (a, b) = Dimensions(_plane.Project(c.Position));
            if (_isSquare) b = Math.Sign(b == 0 ? 1 : b) * Math.Abs(a);
            var pts = Corners(a, b);
            g.Polygon(pts, new DrawColor(80, 130, 220, 40));
            for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], DrawColor.Preview, 1.5f);
            if (_isSquare) g.Line(pts[0], pts[2], DrawColor.Preview, 1, dashed: true);
        }
        DrawInference(g);
    }
}
