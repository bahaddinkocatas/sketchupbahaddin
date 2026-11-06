using Sketch.Geometry;

namespace Sketch.Tools;

/// <summary>
/// Lasso Select (Shift+Space). Drag a freehand loop, or click points to draw a polygonal loop and double-click
/// (or press Enter) to close it. Drawn counter-clockwise the lasso selects everything it touches (crossing);
/// clockwise only what lies fully inside. Ctrl/Alt adds, Shift toggles, Ctrl+Shift removes.
/// </summary>
public sealed class LassoTool : Tool
{
    private readonly List<Vector2d> _points = new();
    private bool _dragging;
    private bool _polygonal;
    private Vector2d _cursor;
    private KeyModifiers _modifiers;

    public override string Id => "tools/lasso";
    public override ToolCursor Cursor => ToolCursor.Select;

    public override string StatusText => _polygonal
        ? "Click to add a corner; double-click or press Enter to close. Esc = cancel."
        : "Drag to draw a freehand lasso, or click to start a polygon lasso. Counterclockwise = select touching, clockwise = select fully inside.";

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var p = new Vector2d(e.X, e.Y);
        _cursor = p;
        if (_polygonal)
        {
            _points.Add(p);
            view.Invalidate();
            return;
        }
        _points.Clear();
        _points.Add(p);
        _dragging = true;
        _modifiers = e.Modifiers;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        _cursor = new Vector2d(e.X, e.Y);
        if (_dragging && (_cursor - _points[^1]).Length > 3) _points.Add(_cursor);
        if (_dragging || _polygonal) view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (!_dragging || e.Button != MouseButton.Left) return;
        _dragging = false;
        if (PathLength() > 12 && _points.Count > 2)
        {
            Finish(view);
            return;
        }
        // A click (no drag) starts a polygonal lasso.
        _points.Clear();
        _points.Add(new Vector2d(e.X, e.Y));
        _polygonal = true;
        NotifyStateChanged();
    }

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        if (_polygonal && _points.Count >= 3) Finish(view);
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key == ViewKey.Enter && _polygonal && _points.Count >= 3)
        {
            Finish(view);
            return true;
        }
        return false;
    }

    private double PathLength()
    {
        double sum = 0;
        for (var i = 1; i < _points.Count; i++) sum += (_points[i] - _points[i - 1]).Length;
        return sum;
    }

    private void Finish(IView view)
    {
        // Double-clicking adds the same point twice; drop duplicates.
        var poly = new List<Vector2d>();
        foreach (var p in _points)
            if (poly.Count == 0 || (p - poly[^1]).Length > 1) poly.Add(p);
        if (poly.Count >= 3)
        {
            // Screen y points down, so a positive signed area is a clockwise loop on screen.
            var crossing = Polygon2d.SignedArea(poly) < 0;
            var items = new ScreenRegion(poly).Pick(view, crossing);
            var m = _modifiers;
            if (m.HasFlag(KeyModifiers.Alt)) m = (m & ~KeyModifiers.Alt) | KeyModifiers.Ctrl;
            SelectTool.Apply(view, items, m);
        }
        Clear(view);
    }

    private void Clear(IView view)
    {
        _points.Clear();
        _dragging = false;
        _polygonal = false;
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void Reset(IView view) => Clear(view);

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_points.Count == 0 || (!_dragging && !_polygonal)) return;
        var cam = view.Camera;
        var depth = cam.Distance;
        Vector3d W(Vector2d p) => cam.RayThroughPixel(p.X, p.Y, view.Width, view.Height).PointAt(depth);
        var pts = _points.Append(_cursor).ToList();
        var crossing = pts.Count >= 3 && Polygon2d.SignedArea(pts) < 0;
        for (var i = 1; i < pts.Count; i++) g.Line(W(pts[i - 1]), W(pts[i]), DrawColor.Black, 1, dashed: crossing);
        if (pts.Count >= 3) g.Line(W(pts[^1]), W(pts[0]), DrawColor.Black.WithAlpha(110), 1, dashed: true);
    }
}
