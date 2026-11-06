using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Modify;

/// <summary>
/// Offset (F): click a face, move to set the distance (inside = smaller copy, outside = larger), click to finish.
/// Type a distance; double-click a face to repeat the last offset.
/// </summary>
public sealed class OffsetTool : Tool
{
    private Face? _face;
    private Face? _hover;
    private double _distance;
    private double? _last;
    private List<Vector3d>? _preview;

    public override string Id => "tools/offset";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Distance";
    public override string StatusText => _face is null
        ? "Pick a face to offset. Double-click repeats the last distance."
        : "Pick the offset distance or enter a value (inward/outward). Esc = cancel.";
    public override string MeasurementValue => _face is null ? string.Empty : Units.Default.Format(Math.Abs(_distance));

    private static Face? FaceUnder(IView view, double x, double y)
    {
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        var f = view.Inference.FaceInference(ray)?.Face;
        return f is not null && ReferenceEquals(f.Parent, view.Model.ActiveEntities) ? f : null;
    }

    private Vector3d? OnFacePlane(IView view, double x, double y)
    {
        if (_face is null) return null;
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        var t = _face.Plane.Intersect(ray);
        return t is > 0 ? ray.PointAt(t.Value) : null;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_face is null)
        {
            _hover = FaceUnder(view, e.X, e.Y);
        }
        else if (OnFacePlane(view, e.X, e.Y) is { } p)
        {
            _distance = Offset.DistanceFromBoundary(_face, p);
            _preview = Offset.OffsetLoop(_face.OuterLoop.Vertices.Select(v => v.Position).ToList(), _face.Normal, _distance);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_face is null)
        {
            _face = FaceUnder(view, e.X, e.Y);
            _distance = 0;
            _preview = null;
            if (_face is null && view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y)) is null)
            {
                RequestExit();
                return;
            }
        }
        else
        {
            Create(view, _distance);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        if (_last is not { } last) return;
        _face = FaceUnder(view, e.X, e.Y);
        if (_face is not null) Create(view, last);
    }

    private void Create(IView view, double distance)
    {
        if (_face is { IsDeleted: false } face && Math.Abs(distance) > Tolerance.Length)
        {
            view.Model.Run("Offset", () => Offset.Execute(view.Model.ActiveEntities, face, distance));
            _last = distance;
        }
        _face = null;
        _preview = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_face is null || !Units.Default.TryParseLength(text, out var d)) return false;
        // The typed value follows the side the mouse is on.
        Create(view, (_distance < 0 ? -1 : 1) * d);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Reset(IView view)
    {
        _face = null;
        _preview = null;
        NotifyStateChanged();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        var face = _face ?? _hover;
        if (face is { IsDeleted: false })
            foreach (var (a, b) in face.OuterLoop.Segments())
                g.Line(a.Position, b.Position, DrawColor.Selection.WithAlpha(150), 2, depthTested: true);
        if (_preview is { } loop)
            for (var i = 0; i < loop.Count; i++) g.Line(loop[i], loop[(i + 1) % loop.Count], DrawColor.Preview, 1.5f);
    }
}

/// <summary>
/// Follow Me: select the path edges first, then click the profile face. Without a selection the tool asks for one.
/// </summary>
public sealed class FollowMeTool : Tool
{
    private Face? _hover;

    public override string Id => "tools/followme";
    public override ToolCursor Cursor => ToolCursor.PushPull;

    public override string StatusText =>
        "Select the path edges first (with Select), then click the profile face with this tool.";

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
        var f = view.Inference.FaceInference(ray)?.Face;
        _hover = f is not null && ReferenceEquals(f.Parent, view.Model.ActiveEntities) ? f : null;
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_hover is not { } profile)
        {
            if (view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y)) is null) RequestExit();
            return;
        }
        var pathEdges = view.Selection.Items.OfType<Edge>().Where(x => !x.IsDeleted && !profile.Edges.Contains(x)).ToList();
        if (pathEdges.Count == 0)
        {
            // A selected face's boundary can serve as the path too (e.g. sweeping around a roof).
            pathEdges = view.Selection.Items.OfType<Face>().Where(f => f != profile).SelectMany(f => f.OuterLoop.Segments())
                .Select(s => view.Model.ActiveEntities.FindEdge(s.A, s.B)).OfType<Edge>().Distinct().ToList();
        }
        var path = FollowMe.OrderPath(pathEdges, out var closed);
        if (path is null)
        {
            view.ShowTooltip("Select connected path edges first", e.X, e.Y);
            return;
        }
        view.Model.Run("Follow Me", () => FollowMe.Execute(view.Model.ActiveEntities, profile, path, closed));
        view.Selection.Clear();
    }

    public override void Deactivate(IView view)
    {
        view.ShowTooltip(null, 0, 0);
        base.Deactivate(view);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        foreach (var e in view.Selection.Items.OfType<Edge>().Where(x => !x.IsDeleted))
            g.Line(e.Start.Position, e.End.Position, DrawColor.Selection, 3, depthTested: true);
        if (_hover is { IsDeleted: false } f)
            foreach (var (a, b) in f.OuterLoop.Segments())
                g.Line(a.Position, b.Position, DrawColor.Magenta, 2, depthTested: true);
    }
}
