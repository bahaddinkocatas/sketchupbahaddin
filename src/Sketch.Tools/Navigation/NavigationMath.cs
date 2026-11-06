using Sketch.Geometry;

namespace Sketch.Tools.Navigation;

/// <summary>Shared camera-navigation helpers used by the Orbit/Pan/Zoom tools and middle-mouse navigation.</summary>
public static class NavigationMath
{
    /// <summary>Radians of rotation per pixel of mouse travel while orbiting.</summary>
    public const double OrbitRadiansPerPixel = 0.0075;

    /// <summary>Zoom factor per wheel notch (120 units). Values below 1 move closer.</summary>
    public const double WheelZoomFactor = 0.8;

    /// <summary>
    /// Orbit pivot: the surface at the centre of the view; otherwise the point on the view's centre line at the
    /// depth of the model (so the model turns in place instead of swinging around a far-away point).
    /// </summary>
    public static Vector3d OrbitPivot(IView view)
    {
        var hit = view.PickSurfacePoint(view.Width / 2.0, view.Height / 2.0);
        if (hit is { } h) return h;
        var cam = view.Camera;
        var (center, radius) = view.ModelBounds;
        var depth = Vector3d.Dot(center - cam.Eye, cam.Direction);
        // Model behind the camera or very close: keep the target.
        if (depth <= radius * 0.05) return cam.Target;
        return cam.Eye + cam.Direction * depth;
    }

    /// <summary>
    /// Point to zoom toward: the surface under the cursor, else the ground plane, else a point at target depth.
    /// </summary>
    public static Vector3d ZoomPoint(IView view, double x, double y)
    {
        var hit = view.PickSurfacePoint(x, y);
        if (hit is { } h) return h;

        var cam = view.Camera;
        var ray = cam.RayThroughPixel(x, y, view.Width, view.Height);
        var t = Plane3d.Ground.Intersect(ray);
        // Near the horizon the ground hit is very far away; zooming toward it would jump, so cap the distance.
        if (t is > 0 && t.Value < cam.Distance * 3) return ray.PointAt(t.Value);

        var targetPlane = new Plane3d(cam.Target, cam.Direction);
        var tt = targetPlane.Intersect(ray);
        return tt is > 0 ? ray.PointAt(tt.Value) : cam.Target;
    }

    /// <summary>Depth anchor for panning: surface under the cursor, else the target-depth plane.</summary>
    public static Vector3d PanAnchor(IView view, double x, double y)
    {
        var hit = view.PickSurfacePoint(x, y);
        if (hit is { } h) return h;
        var cam = view.Camera;
        var ray = cam.RayThroughPixel(x, y, view.Width, view.Height);
        var t = new Plane3d(cam.Target, cam.Direction).Intersect(ray);
        return t is > 0 ? ray.PointAt(t.Value) : cam.Target;
    }

    /// <summary>Moves the camera so <paramref name="anchor"/> stays under pixel (x, y).</summary>
    public static void PanTo(IView view, Vector3d anchor, double x, double y)
    {
        var cam = view.Camera;
        var plane = new Plane3d(anchor, cam.Direction);
        var ray = cam.RayThroughPixel(x, y, view.Width, view.Height);
        var t = plane.Intersect(ray);
        if (t is null) return;
        cam.Translate(anchor - ray.PointAt(t.Value));
    }

    public static void WheelZoom(IView view, double x, double y, int wheelDelta)
    {
        if (wheelDelta == 0) return;
        // A burst of wheel steps counts as one camera change for Camera > Previous.
        if ((DateTime.UtcNow - _lastWheel).TotalMilliseconds > 700) view.SaveCameraForPrevious();
        _lastWheel = DateTime.UtcNow;
        var point = ZoomPoint(view, x, y);
        var notches = wheelDelta / 120.0;
        view.Camera.ZoomAbout(point, Math.Pow(WheelZoomFactor, notches));
        view.Invalidate();
    }

    private static DateTime _lastWheel = DateTime.MinValue;

    public static void ZoomExtents(IView view)
    {
        view.SaveCameraForPrevious();
        var (center, radius) = view.ModelBounds;
        view.Camera.ZoomExtents(center, radius, view.Width / (double)Math.Max(view.Height, 1));
        view.Invalidate();
    }
}
