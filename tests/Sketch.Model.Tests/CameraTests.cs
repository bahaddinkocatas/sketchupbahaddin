using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Model.Tests;

public class CameraTests
{
    private static void AssertClose(Vector3d expected, Vector3d actual, double tol = 1e-6) =>
        Assert.True(expected.IsEqual(actual, tol), $"expected {expected}, got {actual}");

    [Fact]
    public void Up_vector_is_perpendicular_to_direction()
    {
        var cam = new Camera();
        Assert.True(Math.Abs(Vector3d.Dot(cam.Up, cam.Direction)) < 1e-9);
        Assert.True(cam.Up.Z > 0, "camera should be upright");
    }

    [Fact]
    public void Center_pixel_ray_points_at_target()
    {
        var cam = new Camera();
        var ray = cam.RayThroughPixel(400, 300, 800, 600);
        AssertClose(cam.Direction, ray.Direction);
    }

    [Fact]
    public void Orbit_keeps_distance_to_pivot()
    {
        var cam = new Camera();
        var pivot = cam.Target;
        var before = Vector3d.Distance(cam.Eye, pivot);
        cam.Orbit(pivot, 0.7, -0.3);
        Assert.Equal(before, Vector3d.Distance(cam.Eye, pivot), 6);
        Assert.True(cam.Up.Z > 0, "orbit must not roll the camera upside down");
    }

    [Fact]
    public void Orbit_positive_pitch_lowers_the_eye()
    {
        var cam = new Camera();
        var z = cam.Eye.Z;
        cam.Orbit(cam.Target, 0, 0.1);
        Assert.True(cam.Eye.Z < z);
    }

    [Fact]
    public void Orbit_is_clamped_at_the_pole()
    {
        var cam = new Camera();
        cam.Orbit(cam.Target, 0, -10); // far past straight-down
        Assert.True(cam.Direction.Z > -1 + 1e-6);
        Assert.True(cam.Up.Z > 0);
    }

    [Fact]
    public void Perspective_zoom_keeps_point_under_cursor()
    {
        var cam = new Camera();
        const int w = 800, h = 600;
        var ray = cam.RayThroughPixel(600, 200, w, h);
        var point = ray.PointAt(300);

        cam.ZoomAbout(point, 0.5);

        var newRay = cam.RayThroughPixel(600, 200, w, h);
        var toPoint = (point - newRay.Origin).Normalized();
        AssertClose(newRay.Direction, toPoint, 1e-6);
    }

    [Fact]
    public void Parallel_zoom_keeps_point_under_cursor()
    {
        var cam = new Camera { IsPerspective = false, OrthoHeight = 400 };
        const int w = 800, h = 600;
        var ray = cam.RayThroughPixel(650, 120, w, h);
        var point = ray.PointAt(Vector3d.Distance(cam.Eye, cam.Target));

        cam.ZoomAbout(point, 0.5);

        var newRay = cam.RayThroughPixel(650, 120, w, h);
        var offset = point - newRay.Origin;
        var lateral = offset - newRay.Direction * Vector3d.Dot(offset, newRay.Direction);
        Assert.True(lateral.Length < 1e-6, $"lateral error {lateral.Length}");
        Assert.Equal(200, cam.OrthoHeight, 9);
    }

    [Theory]
    [InlineData(StandardView.Top, 0, 0, -1)]
    [InlineData(StandardView.Front, 0, 1, 0)]
    [InlineData(StandardView.Right, -1, 0, 0)]
    public void Standard_views_look_along_axes(StandardView view, double x, double y, double z)
    {
        var cam = new Camera();
        cam.SetStandardView(view, Vector3d.Zero, 100);
        AssertClose(new Vector3d(x, y, z), cam.Direction);
    }

    [Fact]
    public void Zoom_extents_fits_sphere()
    {
        var cam = new Camera();
        cam.ZoomExtents(new Vector3d(10, 20, 30), 50, 16.0 / 9);
        AssertClose(new Vector3d(10, 20, 30), cam.Target);
        var halfFov = cam.FovYDegrees * Math.PI / 360;
        Assert.True(cam.Distance * Math.Sin(halfFov) >= 50, "sphere must fit vertically");
    }
}
