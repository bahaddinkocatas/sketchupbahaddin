using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools.Drawing;

/// <summary>
/// Circle (C): click the centre, click to set the radius. The circle lies on the face under the centre,
/// otherwise horizontal; arrow keys choose the orientation. Type a radius, or "24s" for the segment count.
/// </summary>
public sealed class CircleTool : InferenceToolBase
{
    public const int DefaultSegments = 24;

    private Vector3d? _center;
    private Vector3d _normal = Vector3d.UnitZ;
    private Vector3d? _forcedNormal;
    private int _segments = DefaultSegments;

    public override string Id => "draw/circle";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => _center is null ? "Sides" : "Radius";

    public override string StatusText => _center is null
        ? "Select center point. Arrows lock the orientation (↑ blue, → red, ← green). Type e.g. 24s for the number of sides."
        : "Select radius point or enter value.";

    public override string MeasurementValue => _center is { } c && Current is { } cur
        ? Units.Default.Format(Radius(cur.Position))
        : _segments.ToString();

    protected override Vector3d? From => _center;
    protected override Plane3d? DrawingPlane => _center is { } c ? new Plane3d(c, _normal) : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _center = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (_center is null)
        {
            // Arrow keys follow the drawing axes (moved by the Axes tool), like the other tools.
            var axes = view.Model.Axes;
            Vector3d? n = key switch
            {
                ViewKey.Up => axes.Z,
                ViewKey.Right => axes.X,
                ViewKey.Left => axes.Y,
                _ => null,
            };
            if (n is not null)
            {
                _forcedNormal = _forcedNormal is { } f && f.IsEqual(n.Value) ? null : n;
                view.Invalidate();
                return true;
            }
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    private double Radius(Vector3d p) => _center is { } c ? Vector3d.Distance(c, new Plane3d(c, _normal).Project(p)) : 0;

    private List<Vector3d> Points(double radius, Vector3d toward)
    {
        var c = _center!.Value;
        var start = new Plane3d(c, _normal).Project(toward) - c;
        var u = start.IsZero(1e-9) ? _normal.AnyPerpendicular() : start.Normalized();
        var v = Vector3d.Cross(_normal, u);
        var pts = new List<Vector3d>(_segments);
        for (var i = 0; i < _segments; i++)
        {
            var a = 2 * Math.PI * i / _segments;
            pts.Add(c + (u * Math.Cos(a) + v * Math.Sin(a)) * radius);
        }
        return pts;
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
        if (_center is null)
        {
            _center = r.Position;
            _normal = _forcedNormal ?? r.Face?.Normal ?? r.Image?.Normal ?? Sketch.Inference.InferenceEngine.ViewPlaneNormal(view.Camera, view.Model.Axes);
        }
        else
        {
            Create(view, Radius(r.Position), r.Position);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void Create(IView view, double radius, Vector3d toward)
    {
        if (radius <= Tolerance.Length) return;
        var pts = Points(radius, toward);
        view.Model.Run("Circle", () => view.Model.ActiveEntities.AddPolyline(pts, closed: true));
        _center = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (MeasurementInput.TryParseSegments(text, out var segments))
        {
            _segments = segments;
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }
        if (_center is not { } c || !Units.Default.TryParseLength(text, out var radius) || radius <= 0) return false;
        Create(view, radius, Current?.Position ?? c + _normal.AnyPerpendicular());
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_center is { } c && Current is { } cur)
        {
            var radius = Radius(cur.Position);
            if (radius > Tolerance.Length)
            {
                var pts = Points(radius, cur.Position);
                for (var i = 0; i < pts.Count; i++) g.Line(pts[i], pts[(i + 1) % pts.Count], DrawColor.Preview, 1.5f);
                g.Line(c, pts[0], DrawColor.Preview, 1, dashed: true);
            }
            g.Point(c, DrawColor.Black, 7, PointShape.Cross);
        }
        DrawInference(g);
    }
}
