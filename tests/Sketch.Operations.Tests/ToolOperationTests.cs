using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Operations.Tests;

public class ToolOperationTests
{
    private static Vector3d P(double x, double y, double z = 0) => new(x, y, z);

    private static Vector3d[] Rect(double x0, double y0, double w, double h, double z = 0) =>
        new[] { P(x0, y0, z), P(x0 + w, y0, z), P(x0 + w, y0 + h, z), P(x0, y0 + h, z) };

    [Fact]
    public void Offset_inward_creates_a_ring_and_an_inner_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, 10, 8), true);
        var face = ents.Faces.Single();
        Offset.Execute(ents, face, 1);
        Assert.Equal(2, ents.Faces.Count);
        var inner = ents.Faces.Single(f => f.InnerLoops.Count == 0);
        Assert.Equal(8 * 6, Math.Abs(inner.Area), 6);
    }

    [Fact]
    public void Offset_outward_grows_the_loop()
    {
        var loop = Offset.OffsetLoop(Rect(0, 0, 10, 8), Vector3d.UnitZ, -2)!;
        var basis = new PlaneBasis(Vector3d.Zero, Vector3d.UnitZ);
        Assert.Equal(14 * 12, Math.Abs(Polygon2d.SignedArea(loop.Select(basis.ToLocal).ToList())), 6);
    }

    [Fact]
    public void Offset_of_an_L_shape_keeps_mitred_corners()
    {
        var l = new[] { P(0, 0), P(10, 0), P(10, 4), P(4, 4), P(4, 10), P(0, 10) };
        var loop = Offset.OffsetLoop(l, Vector3d.UnitZ, 1)!;
        Assert.Equal(6, loop.Count);
        Assert.Contains(loop, p => p.IsEqual(P(3, 3), 1e-9)); // reflex corner moves diagonally
    }

    [Fact]
    public void Offset_that_collapses_returns_null()
    {
        Assert.Null(Offset.OffsetLoop(Rect(0, 0, 4, 4), Vector3d.UnitZ, 3));
    }

    [Fact]
    public void Rotating_a_box_keeps_it_closed_and_outward()
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, 10, 6), true);
        PushPull.Execute(ents, ents.Faces.Single(), -4);
        var all = ents.Faces.Cast<Entity>().ToList();
        MoveCopy.Transform(ents, all, Matrix4d.Translation(P(5, 3, 2)) * Matrix4d.Rotation(Vector3d.UnitZ, Math.PI / 6) * Matrix4d.Translation(P(-5, -3, -2)));
        Assert.All(ents.Faces, f => Assert.True(f.Area > 0));
        Assert.True(Volume(ents) > 0);
    }

    [Fact]
    public void Mirroring_a_box_keeps_faces_outward()
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, 10, 6), true);
        PushPull.Execute(ents, ents.Faces.Single(), -4);
        MoveCopy.Transform(ents, ents.Faces.Cast<Entity>().ToList(), Matrix4d.Scaling(-1, 1, 1));
        Assert.True(Volume(ents) > 0, "mirrored solid must not be inside out");
        Assert.Equal(-10, ents.Vertices.Min(v => v.Position.X), 9);
    }

    [Fact]
    public void Polar_array_copy_with_rotation()
    {
        var ents = new Entities();
        ents.AddEdge(P(5, 0), P(8, 0));
        MoveCopy.CopyTransformed(ents, ents.Edges.ToList(), Matrix4d.Rotation(Vector3d.UnitZ, Math.PI / 2), 3);
        Assert.Equal(4, ents.Edges.Count);
        Assert.Contains(ents.Edges, e => e.Start.Position.IsEqual(P(0, -5), 1e-9) || e.End.Position.IsEqual(P(0, -5), 1e-9));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(5.0)]  // half circle
    [InlineData(8.0)]  // more than half
    [InlineData(-3.0)]
    public void Arc_passes_through_its_bulge_point(double bulge)
    {
        var start = P(0, 0);
        var end = P(10, 0);
        var pts = Shapes.ArcByBulge(start, end, Vector3d.UnitY, bulge, 64);
        Assert.True(pts[0].IsEqual(start, 1e-9) && pts[^1].IsEqual(end, 1e-9));
        var top = P(5, bulge);
        Assert.True(pts.Min(p => Vector3d.Distance(p, top)) < 0.2, "arc must pass near the bulge point");
        // All points on one circle.
        var h = 5.0;
        var c = (bulge * bulge - h * h) / (2 * bulge);
        var center = P(5, c);
        var r = Math.Abs(bulge - c);
        Assert.All(pts, p => Assert.Equal(r, Vector3d.Distance(p, center), 6));
    }

    [Fact]
    public void Follow_me_along_an_L_path_makes_a_closed_outward_solid()
    {
        var ents = new Entities();
        // Path on the ground.
        ents.AddEdge(P(0, 0, 0), P(20, 0, 0));
        ents.AddEdge(P(20, 0, 0), P(20, 15, 0));
        var path = ents.Edges.ToList();
        // Square profile standing at the path start, perpendicular to the first segment, just above the path.
        ents.AddPolyline(new[] { P(0, -1, 0.5), P(0, 1, 0.5), P(0, 1, 2.5), P(0, -1, 2.5) }, true);
        var profile = ents.Faces.Single();

        var ordered = FollowMe.OrderPath(path, out var closed)!;
        Assert.False(closed);
        Assert.True(FollowMe.Execute(ents, profile, ordered, closed));

        foreach (var e in ents.Edges.Where(e => e.Faces.Count > 0))
            Assert.True(e.Faces.Count == 2,
                $"{e} {e.Start.Position}-{e.End.Position} faces: {string.Join(" | ", e.Faces.Select(f => $"{f} n={f.Normal}"))}");
        Assert.True(Volume(ents) > 0, "swept solid must face outward");
    }

    [Fact]
    public void Follow_me_around_a_closed_rectangle_makes_a_ring()
    {
        var ents = new Entities();
        var path = new List<Vector3d> { P(0, 0), P(20, 0), P(20, 10), P(0, 10) };
        var profileGroup = new Entities();
        // Profile in a separate container so path and profile don't interact.
        ents.AddPolyline(new[] { P(-1, 5, 0), P(1, 5, 0), P(1, 5, 2), P(-1, 5, 2) }, true);
        var profile = ents.Faces.Single();
        Assert.True(FollowMe.Execute(ents, profile, new[] { P(0, 5), P(0, 0), P(20, 0), P(20, 10), P(0, 10) }.ToList(), closed: true));
        Assert.True(Volume(ents) > 0);
        _ = path;
        _ = profileGroup;
    }

    private static double Volume(Entities ents) =>
        ents.Faces.Sum(f => Vector3d.Dot(f.OuterLoop.Vertices[0].Position, f.Normal) * f.Area / 3);
}
