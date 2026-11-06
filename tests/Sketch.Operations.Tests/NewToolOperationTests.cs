using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Operations.Tests;

/// <summary>Geometry behind Flip, the arc tools, Axes, Section Plane and 3D Text.</summary>
public class NewToolOperationTests
{
    private static Vector3d P(double x, double y, double z = 0) => new(x, y, z);

    private static Entities Box(double x0, double w, double d, double h)
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(x0, 0), P(x0 + w, 0), P(x0 + w, d), P(x0, d) }, true);
        var floor = ents.Faces.Single();
        PushPull.Execute(ents, floor, Vector3d.Dot(floor.Normal, Vector3d.UnitZ) > 0 ? h : -h);
        return ents;
    }

    private static double Volume(Entities ents) =>
        ents.Faces.Sum(f => Vector3d.Dot(f.OuterLoop.Vertices[0].Position, f.Normal) * Math.Abs(f.Area)) / 3;

    [Fact]
    public void Reflection_mirrors_points_about_the_plane()
    {
        var m = Matrix4d.Reflection(P(5, 0, 0), Vector3d.UnitX);
        Assert.True(m.TransformPoint(P(8, 2, 3)).IsEqual(P(2, 2, 3), 1e-9));
        Assert.True(m.TransformPoint(P(5, 7, 1)).IsEqual(P(5, 7, 1), 1e-9));
    }

    [Fact]
    public void Flip_mirrors_a_box_and_keeps_it_outward()
    {
        var ents = Box(10, 4, 3, 2);
        var before = Volume(ents);
        MoveCopy.Transform(ents, ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), Matrix4d.Reflection(Vector3d.Zero, Vector3d.UnitX));
        Assert.All(ents.Vertices, v => Assert.True(v.Position.X <= -10 + 1e-9));
        Assert.Equal(before, Volume(ents), 6);
    }

    [Fact]
    public void Flip_copy_adds_a_mirrored_box()
    {
        var ents = Box(10, 4, 3, 2);
        var copy = MoveCopy.CopyTransformed(ents, ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), Matrix4d.Reflection(Vector3d.Zero, Vector3d.UnitX));
        Assert.Equal(12, ents.Faces.Count);
        Assert.True(Volume(ents) > 0, "both boxes must face outward");
        Assert.Contains(copy.OfType<Face>(), f => f.Vertices.All(v => v.Position.X < 0));
    }

    [Fact]
    public void Arc_through_three_points_passes_through_the_middle_one()
    {
        var pts = Shapes.ArcThrough(P(-10, 0), P(0, 10), P(10, 0), 24)!;
        Assert.True(pts[0].IsEqual(P(-10, 0)));
        Assert.True(pts[^1].IsEqual(P(10, 0)));
        Assert.All(pts, p => Assert.Equal(10, p.Length, 6));
        Assert.Contains(pts, p => p.IsEqual(P(0, 10), 1e-6));
        Assert.Null(Shapes.ArcThrough(P(0, 0), P(1, 0), P(2, 0), 12));
    }

    [Fact]
    public void Centre_arc_sweeps_the_given_angle()
    {
        var pts = Shapes.ArcAround(Vector3d.Zero, Vector3d.UnitZ, P(5, 0), Math.PI / 2, 12);
        Assert.Equal(13, pts.Count);
        Assert.True(pts[^1].IsEqual(P(0, 5), 1e-9));
        Assert.Equal(Math.PI / 2, Shapes.SignedAngle(P(1, 0), P(0, 1), Vector3d.UnitZ), 9);
        Assert.Equal(-Math.PI / 2, Shapes.SignedAngle(P(1, 0), P(0, -1), Vector3d.UnitZ), 9);
    }

    [Fact]
    public void Axes_from_points_are_orthonormal()
    {
        var axes = ModelAxes.FromPoints(P(1, 1), P(2, 2), P(0, 3))!;
        Assert.Equal(0, Vector3d.Dot(axes.X, axes.Y), 9);
        Assert.True(axes.Z.IsEqual(Vector3d.UnitZ, 1e-9));
        Assert.Null(ModelAxes.FromPoints(P(0, 0), P(0, 0), P(1, 0)));
    }

    [Fact]
    public void Section_planes_survive_undo_snapshots_and_move_with_transforms()
    {
        var ents = new Entities();
        var sp = ents.AddSectionPlane(P(0, 0, 5), -Vector3d.UnitZ, 100, "Kesit 1");
        Assert.Same(sp, ents.ActiveSectionPlane());
        ents.TransformAnnotations(new Entity[] { sp }, Matrix4d.Translation(P(0, 0, 2)));
        Assert.Equal(7, sp.Point.Z, 9);

        var state = ents.CaptureState();
        ents.RestoreState(EntitiesState.Empty);
        Assert.Null(ents.ActiveSectionPlane());
        ents.RestoreState(state);
        var restored = Assert.Single(ents.Sections);
        Assert.True(restored.IsActive);
        Assert.Equal("Kesit 1", restored.Name);

        ents.SetActiveSection(restored, false);
        Assert.Null(ents.ActiveSectionPlane());
        ents.ReverseSection(restored);
        Assert.True(restored.Normal.IsEqual(Vector3d.UnitZ));
    }

    [Fact]
    public void Text3D_keeps_letter_counters_open_and_extrudes()
    {
        // An "O": outer square with a square counter.
        var outer = new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) };
        var inner = new[] { P(3, 3), P(7, 3), P(7, 7), P(3, 7) };
        var ents = new Entities();
        var group = Text3D.Create(ents, new IReadOnlyList<Vector3d>[] { outer, inner }, Vector3d.UnitZ, filled: true, extrude: 2, "O");
        var content = group.Entities;
        Assert.DoesNotContain(content.Faces, f => Math.Abs(Math.Abs(f.Area) - 16) < 1e-6 && f.InnerLoops.Count == 0 && Math.Abs(f.Normal.Z) > 0.99);
        Assert.Equal(2 * (100 - 16) + 4 * 10 * 2 + 4 * 4 * 2, content.Faces.Sum(f => Math.Abs(f.Area)), 6);
        Assert.Empty(ents.Faces);
    }

    [Fact]
    public void Flipping_a_ground_face_keeps_its_back_side_up()
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0), P(4, 0), P(4, 3), P(0, 3) }, true);
        Assert.Equal(-1, ents.Faces.Single().Normal.Z, 9);
        MoveCopy.Transform(ents, ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), Matrix4d.Reflection(P(2, 1.5), Vector3d.UnitZ));
        Assert.Equal(-1, ents.Faces.Single().Normal.Z, 9);
        var copies = MoveCopy.CopyTransformed(ents, ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), Matrix4d.Rotation(Vector3d.UnitX, Math.PI));
        Assert.All(ents.Faces, f => Assert.Equal(-1, f.Normal.Z, 9));
    }
}
