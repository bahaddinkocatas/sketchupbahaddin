using Sketch.Geometry;

namespace Sketch.Tools.Navigation;

/// <summary>Orbit (O): drag to rotate around the centre of the view. Hold Shift to pan instead.</summary>
public sealed class OrbitTool : Tool
{
    private bool _dragging;
    private double _lastX, _lastY;
    private Vector3d _pivot;
    private Vector3d _panAnchor;

    public override string Id => "camera/orbit";
    public override string StatusText => "Drag to orbit. Shift = pan.";
    public override ToolCursor Cursor => ToolCursor.Orbit;

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _dragging = true;
        view.SaveCameraForPrevious();
        _lastX = e.X;
        _lastY = e.Y;
        _pivot = NavigationMath.OrbitPivot(view);
        _panAnchor = NavigationMath.PanAnchor(view, e.X, e.Y);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (!_dragging) return;
        if (e.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            NavigationMath.PanTo(view, _panAnchor, e.X, e.Y);
        }
        else
        {
            view.Camera.Orbit(_pivot,
                -(e.X - _lastX) * NavigationMath.OrbitRadiansPerPixel,
                -(e.Y - _lastY) * NavigationMath.OrbitRadiansPerPixel);
            _panAnchor = NavigationMath.PanAnchor(view, e.X, e.Y);
        }
        _lastX = e.X;
        _lastY = e.Y;
        view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e) => _dragging = false;

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        // Double-click centres the view on the clicked point.
        var p = view.PickSurfacePoint(e.X, e.Y);
        if (p is null) return;
        var cam = view.Camera;
        cam.Translate(p.Value - cam.Target);
        view.Invalidate();
    }

    public override void Reset(IView view) => _dragging = false;
}

/// <summary>Pan (H): drag to slide the view; the grabbed point stays under the cursor.</summary>
public sealed class PanTool : Tool
{
    private bool _dragging;
    private Vector3d _anchor;

    public override string Id => "camera/pan";
    public override string StatusText => "Drag to pan the view.";
    public override ToolCursor Cursor => ToolCursor.Pan;

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _dragging = true;
        view.SaveCameraForPrevious();
        _anchor = NavigationMath.PanAnchor(view, e.X, e.Y);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (!_dragging) return;
        NavigationMath.PanTo(view, _anchor, e.X, e.Y);
        view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e) => _dragging = false;
    public override void Reset(IView view) => _dragging = false;
}

/// <summary>
/// Zoom (Z): drag up to zoom in, down to zoom out, about the centre of the view.
/// Shift+drag changes the field of view. Typing a number sets the field of view in degrees ("50" or "50deg") or
/// as a lens length ("35mm").
/// </summary>
public sealed class ZoomTool : Tool
{
    private bool _dragging;
    private double _lastY;
    private Vector3d _center;

    public override string Id => "camera/zoom";
    public override string StatusText => "Drag up to zoom in, down to zoom out.";
    public override ToolCursor Cursor => ToolCursor.Zoom;

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _dragging = true;
        view.SaveCameraForPrevious();
        _lastY = e.Y;
        _center = NavigationMath.ZoomPoint(view, view.Width / 2.0, view.Height / 2.0);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (!_dragging) return;
        var dy = e.Y - _lastY;
        _lastY = e.Y;
        var cam = view.Camera;
        if (e.Modifiers.HasFlag(KeyModifiers.Shift) && cam.IsPerspective)
            cam.FovYDegrees = Math.Clamp(cam.FovYDegrees * Math.Exp(dy * 0.005), 1, 120);
        else
            cam.ZoomAbout(_center, Math.Exp(dy * 0.01));
        view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e) => _dragging = false;
    public override void Reset(IView view) => _dragging = false;

    public override bool OnUserText(IView view, string text)
    {
        var cam = view.Camera;
        if (!cam.IsPerspective) return false;
        var t = text.Trim().ToLowerInvariant();
        const double filmWidthMm = 36; // SketchUp API: default film width for focal-length conversions
        if (t.EndsWith("mm") && double.TryParse(t[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var mm) && mm > 0)
        {
            // Lens length → horizontal FOV over a 36 mm film, converted to our vertical FOV.
            var hfov = 2 * Math.Atan(filmWidthMm / (2 * mm));
            var aspect = view.Width / (double)Math.Max(view.Height, 1);
            var vfov = 2 * Math.Atan(Math.Tan(hfov / 2) / aspect);
            cam.FovYDegrees = Math.Clamp(vfov * 180 / Math.PI, 1, 120);
        }
        else
        {
            var num = t.EndsWith("deg") ? t[..^3] : t;
            if (!double.TryParse(num.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var deg) || deg <= 0) return false;
            cam.FovYDegrees = Math.Clamp(deg, 1, 120);
        }
        view.Invalidate();
        return true;
    }
}
