using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;
using Xunit.Abstractions;

namespace Sketch.Operations.Tests;

/// <summary>Broad push/pull scenarios: every result must stay a closed solid with outward-facing faces.</summary>
public class PushPullStressTests(ITestOutputHelper output)
{
    private static Vector3d P(double x, double y, double z) => new(x, y, z);

    private static Entities Box(double x, double y, double z, double z0 = 0)
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, z0), P(x, 0, z0), P(x, y, z0), P(0, y, z0) }, true);
        var f = ents.Faces.Single();
        PushPull.Execute(ents, f, f.Normal.Z < 0 ? -z : z);
        return ents;
    }

    /// <summary>Signed volume using face normals; positive when all faces point outward.</summary>
    private static double SignedVolume(Entities ents)
    {
        double v = 0;
        foreach (var f in ents.Faces)
        {
            var area = f.Area; // positive area with outer loop CCW around the normal
            var c = f.OuterLoop.Vertices[0].Position;
            v += Vector3d.Dot(c, f.Normal) * area / 3;
        }
        return v;
    }

    private void AssertSolid(Entities ents, string what)
    {
        foreach (var e in ents.Edges)
        {
            Assert.True(e.Faces.Count == 2, $"{what}: edge {e} has {e.Faces.Count} faces");
            var a = e.Faces[0].Loops.Any(l => l.Traverses(e.Start, e.End));
            var b = e.Faces[1].Loops.Any(l => l.Traverses(e.Start, e.End));
            Assert.True(a != b, $"{what}: faces {e.Faces[0]} and {e.Faces[1]} disagree on edge {e}");
        }
        foreach (var f in ents.Faces) Assert.True(f.Area > 0, $"{what}: face {f} loop is clockwise around its normal");
        Assert.True(SignedVolume(ents) > 0, $"{what}: faces point inward (volume {SignedVolume(ents):0.###})");
    }

    public static IEnumerable<object[]> Directions() =>
        new[] { "+X", "-X", "+Y", "-Y", "+Z", "-Z" }.SelectMany(d => new[] { new object[] { d, 3.0 }, new object[] { d, -3.0 } });

    private static Face FaceToward(Entities ents, string dir)
    {
        var n = dir switch
        {
            "+X" => Vector3d.UnitX, "-X" => -Vector3d.UnitX, "+Y" => Vector3d.UnitY,
            "-Y" => -Vector3d.UnitY, "+Z" => Vector3d.UnitZ, _ => -Vector3d.UnitZ,
        };
        return ents.Faces.Where(f => f.Normal.IsEqual(n, 1e-9)).OrderByDescending(f => Math.Abs(f.Area)).First();
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public void Pushing_or_pulling_any_box_face_keeps_a_valid_solid(string dir, double distance)
    {
        var ents = Box(10, 8, 6);
        AssertSolid(ents, "box");
        PushPull.Execute(ents, FaceToward(ents, dir), distance);
        AssertSolid(ents, $"{dir} {distance}");
        Assert.Equal(6, ents.Faces.Count);
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public void Bump_or_pocket_on_any_box_face_keeps_a_valid_solid(string dir, double distance)
    {
        var ents = Box(10, 10, 10);
        var face = FaceToward(ents, dir);
        // A 4x4 rectangle centred on the face, drawn in its plane.
        var n = face.Normal;
        var center = face.Vertices.Aggregate(Vector3d.Zero, (s, v) => s + v.Position) / face.Vertices.Count();
        var u = Math.Abs(n.Z) > 0.9 ? Vector3d.UnitX : Vector3d.UnitZ;
        var w = Vector3d.Cross(n, u);
        ents.AddPolyline(new[] { center - u * 2 - w * 2, center + u * 2 - w * 2, center + u * 2 + w * 2, center - u * 2 + w * 2 }, true);
        var inner = ents.Faces.Single(f => f.ContainsPoint(center) && f.InnerLoops.Count == 0);
        Assert.True(inner.Normal.IsEqual(n, 1e-9), $"rectangle on {dir} face must face like the face");

        PushPull.Execute(ents, inner, distance);
        foreach (var f in ents.Faces) output.WriteLine($"{f} n={f.Normal} area={f.Area:0.##}");
        AssertSolid(ents, $"{dir} {distance}");
        Assert.Equal(11, ents.Faces.Count);
    }

    [Fact]
    public void Repeated_push_pulls_keep_a_valid_solid()
    {
        var ents = Box(10, 10, 10);
        PushPull.Execute(ents, FaceToward(ents, "+Z"), 5);
        PushPull.Execute(ents, FaceToward(ents, "+X"), -2);
        PushPull.Execute(ents, FaceToward(ents, "-Y"), 4);
        AssertSolid(ents, "sequence");

        var top = FaceToward(ents, "+Z");
        var c = top.Vertices.Aggregate(Vector3d.Zero, (s, v) => s + v.Position) / top.Vertices.Count();
        ents.AddPolyline(new[] { c + P(-1, -1, 0), c + P(1, -1, 0), c + P(1, 1, 0), c + P(-1, 1, 0) }, true);
        var small = ents.Faces.Single(f => f.ContainsPoint(c) && f.InnerLoops.Count == 0);
        PushPull.Execute(ents, small, 3);
        AssertSolid(ents, "bump on stretched top");
        var bumpTop = ents.Faces.Single(f => f.ContainsPoint(c + P(0, 0, 3)));
        PushPull.Execute(ents, bumpTop, 2); // stretch the bump
        AssertSolid(ents, "stretched bump");
    }

    [Theory]
    [InlineData(0.0, 6.0)]
    [InlineData(0.0, -6.0)]
    [InlineData(4.0, 6.0)]
    [InlineData(4.0, -6.0)]
    public void Free_rectangle_at_any_height_extrudes_outward(double z0, double distance)
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, z0), P(10, 0, z0), P(10, 8, z0), P(0, 8, z0) }, true);
        PushPull.Execute(ents, ents.Faces.Single(), distance);
        AssertSolid(ents, $"z0={z0} d={distance}");
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(-5.0)]
    public void Ground_square_touching_a_wall_pulls_up_with_top_facing_up(double wallSign)
    {
        // Floor square with a vertical wall face standing on one of its edges (an open surface).
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 10, 0), P(0, 10, 0) }, true);
        ents.AddPolyline(new[] { P(0, 10, 0), P(10, 10, 0), P(10, 10, wallSign), P(0, 10, wallSign) }, true);
        var floor = ents.Faces.Single(f => Math.Abs(f.Normal.Z) > 0.99);
        PushPull.Execute(ents, floor, floor.Normal.Z < 0 ? -6 : 6);

        var top = ents.Faces.Single(f => f.Vertices.All(v => Math.Abs(v.Position.Z - 6) < 1e-9));
        Assert.True(top.Normal.Z > 0.99, $"top normal {top.Normal}");
        var bottom = ents.Faces.Single(f => f.Vertices.All(v => Math.Abs(v.Position.Z) < 1e-9));
        Assert.True(bottom.Normal.Z < -0.99, $"bottom normal {bottom.Normal}");
    }

    [Fact]
    public void Square_with_a_dangling_line_pulls_up_like_a_free_face()
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 10, 0), P(0, 10, 0) }, true);
        ents.AddEdge(P(10, 10, 0), P(15, 15, 0));
        var floor = ents.Faces.Single();
        PushPull.Execute(ents, floor, -6);
        Assert.Contains(ents.Faces, f => f.Normal.Z > 0.99 && f.Vertices.All(v => Math.Abs(v.Position.Z - 6) < 1e-9));
        Assert.Contains(ents.Faces, f => f.Normal.Z < -0.99 && f.Vertices.All(v => Math.Abs(v.Position.Z) < 1e-9));
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(-1.0)]
    public void Sloped_face_of_a_wedge_extrudes_into_a_valid_solid(double distance)
    {
        // Triangular prism: profile in the XZ plane, extruded along Y.
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(0, 0, 6) }, true);
        var profile = ents.Faces.Single();
        PushPull.Execute(ents, profile, profile.Normal.Y < 0 ? -8 : 8);
        AssertSolid(ents, "wedge");

        var slope = ents.Faces.Single(f => f.Normal.X > 0.1 && f.Normal.Z > 0.1);
        Assert.False(PushPull.ShouldStretch(slope));
        PushPull.Execute(ents, slope, distance);
        AssertSolid(ents, $"wedge slope {distance}");
    }

    [Fact]
    public void Free_vertical_rectangle_extrudes_outward()
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 6), P(0, 0, 6) }, true);
        PushPull.Execute(ents, ents.Faces.Single(), 4);
        AssertSolid(ents, "vertical");
    }
}
