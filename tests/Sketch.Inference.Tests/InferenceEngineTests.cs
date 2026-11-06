using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Inference.Tests;

public class InferenceEngineTests
{
    private const int W = 1200, H = 800;

    private static Vector3d P(double x, double y, double z) => new(x, y, z);

    /// <summary>A 100x60x50 box seen from the front-left-above, like the default view.</summary>
    private static (Entities Ents, Camera Cam) Scene()
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(100, 0, 0), P(100, 60, 0), P(0, 60, 0) }, true);
        PushPull.Execute(ents, ents.Faces.Single(), -50);
        var cam = new Camera();
        cam.Set(P(-150, -300, 220), P(50, 30, 25), Vector3d.UnitZ);
        return (ents, cam);
    }

    private static (double X, double Y) Screen(Camera cam, Vector3d p)
    {
        var s = cam.WorldToScreen(p, W, H)!.Value;
        return (s.X, s.Y);
    }

    private static Face Front(Entities ents) => ents.Faces.Single(f => f.Normal.Y < -0.99);

    [Fact]
    public void Cursor_on_a_face_reports_that_face()
    {
        var (ents, cam) = Scene();
        var (x, y) = Screen(cam, P(40, 0, 20));
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x, y));
        Assert.Equal(InferenceKind.OnFace, r.Kind);
        Assert.Same(Front(ents), r.Face);
    }

    [Fact]
    public void Snapping_to_an_endpoint_still_reports_the_face_under_the_cursor()
    {
        var (ents, cam) = Scene();
        // Corner of the front face, nudged inside the face so the face is under the cursor.
        var (x, y) = Screen(cam, P(100, 0, 50));
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x - 2, y + 2));
        Assert.Equal(InferenceKind.Endpoint, r.Kind);
        Assert.NotNull(r.Face);
        Assert.True(Math.Abs(r.Face!.Plane.SignedDistance(r.Position)) < 1e-6);
    }

    [Fact]
    public void Snapping_to_an_edge_reports_an_adjacent_face()
    {
        var (ents, cam) = Scene();
        var (x, y) = Screen(cam, P(50, 0, 50)); // middle of the front-top edge (midpoint inference)
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x + 30, y));
        Assert.NotNull(r.Face);
    }

    [Fact]
    public void On_a_drawing_plane_axes_leaving_the_plane_are_ignored()
    {
        var (ents, cam) = Scene();
        var front = Front(ents);
        var corner = P(30, 0, 20);
        // Put the cursor exactly on the screen projection of the green axis through the corner
        // (perpendicular to the front face).
        var (x, y) = Screen(cam, corner + P(0, -40, 0));
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x, y)
        {
            From = corner,
            DrawingPlane = front.Plane,
        });
        Assert.NotEqual(InferenceAxis.Green, r.Axis);
        Assert.True(Math.Abs(front.Plane.SignedDistance(r.Position)) < 1e-6, "result must stay on the drawing plane");
        Assert.False(r.Position.IsEqual(corner, 1), "result must not collapse onto the start point");
    }

    [Fact]
    public void Axis_inference_from_start_point()
    {
        var (ents, cam) = Scene();
        var start = P(0, -50, 0);
        var (x, y) = Screen(cam, start + P(80, 0, 0)); // along red
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x, y) { From = start });
        Assert.Equal(InferenceKind.OnLine, r.Kind);
        Assert.Equal(InferenceAxis.Red, r.Axis);
        Assert.Equal(-50, r.Position.Y, 6);
        Assert.Equal(0, r.Position.Z, 6);
    }

    [Fact]
    public void Hidden_corner_is_not_snapped()
    {
        var (ents, cam) = Scene();
        // The back-bottom corner (0,60,0) is hidden behind the box from this view.
        var hidden = P(100, 60, 0);
        var (x, y) = Screen(cam, hidden);
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x, y));
        Assert.False(r.Kind == InferenceKind.Endpoint && r.Position.IsEqual(hidden, 1e-6));
    }

    [Fact]
    public void Free_point_without_geometry_lies_on_the_ground()
    {
        var cam = new Camera();
        var r = new InferenceEngine(new Entities()).Infer(new InferenceQuery(cam, W, H, 700, 600));
        Assert.Equal(0, r.Position.Z, 6);
    }

    [Fact]
    public void Drawing_along_an_axis_snaps_where_it_crosses_an_edge()
    {
        // Top of the box; start on the top face and move along red toward the far edge x = 100.
        var (ents, cam) = Scene();
        var start = P(30, 20, 50);
        var crossing = P(100, 20, 50);
        var (x, y) = Screen(cam, crossing);
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x + 3, y + 2) { From = start });
        Assert.Equal(InferenceKind.OnEdge, r.Kind);
        Assert.Equal(InferenceAxis.Red, r.Axis);
        Assert.True(r.Position.IsEqual(crossing, 1e-6), r.Position.ToString());
    }

    [Fact]
    public void Parallel_view_picks_the_front_face_even_when_it_is_behind_the_eye_position()
    {
        // A big box; the parallel camera's eye point sits inside it (the view still shows the front face).
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10000, 0, 0), P(10000, 6000, 0), P(0, 6000, 0) }, true);
        PushPull.Execute(ents, ents.Faces.Single(), -5000);
        var cam = new Camera { IsPerspective = false, OrthoHeight = 20000 };
        cam.Set(P(5000, 3000, 2500), P(5000, 3001, 2500), Vector3d.UnitZ); // looking along +Y from the box centre
        var (x, y) = Screen(cam, P(3000, 0, 2000));
        var r = new InferenceEngine(ents).Infer(new InferenceQuery(cam, W, H, x, y));
        Assert.Equal(InferenceKind.OnFace, r.Kind);
        Assert.Same(Front(ents), r.Face);
    }

    [Fact]
    public void Empty_space_drawing_plane_follows_the_view()
    {
        var cam = new Camera();
        Assert.Equal(Vector3d.UnitZ, InferenceEngine.ViewPlaneNormal(cam, ModelAxes.World)); // default view from above
        cam.SetStandardView(StandardView.Front, Vector3d.Zero, 100);
        Assert.Equal(Vector3d.UnitY, InferenceEngine.ViewPlaneNormal(cam, ModelAxes.World)); // red-blue plane
        cam.SetStandardView(StandardView.Right, Vector3d.Zero, 100);
        Assert.Equal(Vector3d.UnitX, InferenceEngine.ViewPlaneNormal(cam, ModelAxes.World)); // green-blue plane

        // A free point in a front view lies on the vertical plane through the origin, not on the far-away ground.
        var r = new InferenceEngine(new Entities()).Infer(new InferenceQuery(cam, W, H, W / 2.0 + 40, H / 2.0 - 30));
        Assert.Equal(0, r.Position.X, 6);
    }

    [Fact]
    public void Alignment_with_a_point_at_the_back_stays_on_the_face_under_the_cursor()
    {
        // A point at the back of the top face; then the cursor on the front face, vertically below it on screen.
        var (ents, cam) = Scene();
        ents.AddEdge(P(30, 60, 50), P(30, 40, 50));
        var engine = new InferenceEngine(ents);
        var (rx, ry) = Screen(cam, P(30, 60, 50));
        Assert.Equal(InferenceKind.Endpoint, engine.Infer(new InferenceQuery(cam, W, H, rx, ry)).Kind);

        var (x, y) = Screen(cam, P(30, 0, 20));
        var r = engine.Infer(new InferenceQuery(cam, W, H, x + 2, y));
        Assert.Equal(InferenceKind.FromPoint, r.Kind);
        Assert.Equal(0, r.Position.Y, 6);          // on the front face, not at the depth of the back point
        Assert.Equal(30, r.Position.X, 6);         // aligned with it along blue
        Assert.Same(Front(ents), r.Face);
    }
}
