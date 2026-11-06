using Sketch.Geometry;

namespace Sketch.Tools.Navigation;

/// <summary>Zoom Window: drag a rectangle to fill the view with it. Returns to the previous tool afterwards.</summary>
public sealed class ZoomWindowTool : Tool
{
    private bool _dragging;
    private double _x0, _y0, _x1, _y1;

    public override string Id => "camera/zoom window";
    public override ToolCursor Cursor => ToolCursor.Zoom;
    public override string StatusText => "Drag a window around the area to zoom to.";

    /// <summary>Raised when the zoom is done, so the host can switch back to the previous tool.</summary>
    public event EventHandler? Finished;

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _dragging = true;
        _x0 = _x1 = e.X;
        _y0 = _y1 = e.Y;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (!_dragging) return;
        _x1 = e.X;
        _y1 = e.Y;
        view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (!_dragging) return;
        _dragging = false;
        var w = Math.Abs(_x1 - _x0);
        var h = Math.Abs(_y1 - _y0);
        if (w < 4 || h < 4) return;
        view.SaveCameraForPrevious();
        var cx = (_x0 + _x1) / 2;
        var cy = (_y0 + _y1) / 2;
        var cam = view.Camera;
        // Move the target to the window centre (on the surface if there is one), then zoom by the window ratio.
        var center = NavigationMath.ZoomPoint(view, cx, cy);
        var anchor = NavigationMath.PanAnchor(view, cx, cy);
        NavigationMath.PanTo(view, anchor, view.Width / 2.0, view.Height / 2.0);
        var factor = Math.Max(w / view.Width, h / view.Height);
        var ray = cam.RayThroughPixel(view.Width / 2.0, view.Height / 2.0, view.Width, view.Height);
        var depth = Vector3d.Dot(center - ray.Origin, ray.Direction);
        cam.ZoomAbout(ray.PointAt(depth > 0 ? depth : cam.Distance), factor);
        view.Invalidate();
        Finished?.Invoke(this, EventArgs.Empty);
    }

    public override void Reset(IView view) => _dragging = false;

    public override void Draw(IView view, IViewDrawing g)
    {
        if (!_dragging) return;
        var cam = view.Camera;
        Vector3d W(double x, double y) => cam.RayThroughPixel(x, y, view.Width, view.Height).PointAt(cam.Distance);
        var pts = new[] { W(_x0, _y0), W(_x1, _y0), W(_x1, _y1), W(_x0, _y1) };
        for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], DrawColor.Preview, 1, dashed: true);
    }
}
