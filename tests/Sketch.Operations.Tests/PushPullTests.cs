using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Operations.Tests;

public class PushPullTests
{
    private static Vector3d P(double x, double y, double z = 0) => new(x, y, z);

    private static Vector3d[] Rect(double x0, double y0, double w, double h, double z = 0) =>
        new[] { P(x0, y0, z), P(x0 + w, y0, z), P(x0 + w, y0 + h, z), P(x0, y0 + h, z) };

    private static void AssertClosedAndOutward(Entities ents)
    {
        // Every edge is shared by exactly two faces, walked in opposite directions.
        foreach (var e in ents.Edges)
        {
            Assert.Equal(2, e.Faces.Count);
            var a = e.Faces[0].Loops.Any(l => l.Traverses(e.Start, e.End));
            var b = e.Faces[1].Loops.Any(l => l.Traverses(e.Start, e.End));
            Assert.NotEqual(a, b);
        }
    }

    private static Face Ground(Entities ents, double z = 0) =>
        ents.Faces.Single(f => Math.Abs(f.Normal.Z) > 0.99 && Math.Abs(f.OuterLoop.Vertices[0].Position.Z - z) < 1e-9
                               && f.InnerLoops.Count == 0 && Math.Abs(f.Area) > 0);

    private static Entities Box(double size = 10)
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, size, size), closed: true);
        var floor = ents.Faces.Single();
        // The floor faces down, so pulling "up" is a negative distance along its normal.
        PushPull.Execute(ents, floor, -size);
        return ents;
    }

    [Fact]
    public void Pulling_a_ground_rectangle_up_makes_a_closed_box()
    {
        var ents = Box();
        Assert.Equal(6, ents.Faces.Count);
        Assert.Equal(12, ents.Edges.Count);
        Assert.Equal(8, ents.Vertices.Count);
        AssertClosedAndOutward(ents);
        var center = P(5, 5, 5);
        Assert.All(ents.Faces, f => Assert.True(Vector3d.Dot(f.Normal, f.OuterLoop.Vertices[0].Position - center) > 0));
    }

    [Fact]
    public void Pulling_a_free_face_toward_its_front_keeps_it_as_outward_cap()
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, 10, 10, z: 5), closed: true); // faces up
        var top = ents.Faces.Single();
        PushPull.Execute(ents, top, 4);
        Assert.Equal(6, ents.Faces.Count);
        AssertClosedAndOutward(ents);
        Assert.Contains(ents.Faces, f => f.Normal.Z > 0.99 && Math.Abs(f.OuterLoop.Vertices[0].Position.Z - 9) < 1e-9);
        Assert.Contains(ents.Faces, f => f.Normal.Z < -0.99 && Math.Abs(f.OuterLoop.Vertices[0].Position.Z - 5) < 1e-9);
    }

    [Fact]
    public void Pulling_the_top_of_a_box_stretches_it()
    {
        var ents = Box();
        var top = ents.Faces.Single(f => f.Normal.Z > 0.99);
        Assert.True(PushPull.ShouldStretch(top));
        PushPull.Execute(ents, top, 5);
        Assert.Equal(6, ents.Faces.Count);
        Assert.Equal(12, ents.Edges.Count);
        Assert.Equal(15, ents.Vertices.Max(v => v.Position.Z), 9);
        AssertClosedAndOutward(ents);
    }

    [Fact]
    public void Pulling_a_rectangle_drawn_on_a_box_top_makes_a_bump()
    {
        var ents = Box();
        ents.AddPolyline(Rect(3, 3, 4, 4, z: 10), closed: true);
        var inner = ents.Faces.Single(f => f.Normal.Z > 0.99 && f.InnerLoops.Count == 0 && Math.Abs(f.Area - 16) < 1e-6);
        PushPull.Execute(ents, inner, 3);

        Assert.Equal(13, ents.Vertices.Max(v => v.Position.Z), 9);
        // Box (6) with a hole in the top + bump sides (4) + bump top (1) = 11 faces.
        Assert.Equal(11, ents.Faces.Count);
        AssertClosedAndOutward(ents);
        var bumpTop = ents.Faces.Single(f => f.Vertices.All(v => Math.Abs(v.Position.Z - 13) < 1e-9));
        Assert.True(bumpTop.Normal.Z > 0.99);
    }

    [Fact]
    public void Pushing_a_rectangle_into_a_box_top_makes_a_pocket()
    {
        var ents = Box();
        ents.AddPolyline(Rect(3, 3, 4, 4, z: 10), closed: true);
        var inner = ents.Faces.Single(f => f.Normal.Z > 0.99 && f.InnerLoops.Count == 0 && Math.Abs(f.Area - 16) < 1e-6);
        PushPull.Execute(ents, inner, -4);

        Assert.Equal(11, ents.Faces.Count);
        AssertClosedAndOutward(ents);
        var pocketFloor = ents.Faces.Single(f => f.Vertices.All(v => Math.Abs(v.Position.Z - 6) < 1e-9));
        Assert.True(pocketFloor.Normal.Z > 0.99, "the pocket floor faces up, out of the solid");
    }

    [Fact]
    public void Undo_restores_geometry_before_push_pull()
    {
        var model = new SketchModel();
        model.Run("Rect", () => model.Entities.AddPolyline(Rect(0, 0, 10, 10), closed: true));
        var floor = model.Entities.Faces.Single();
        model.Run("Push/Pull", () => PushPull.Execute(model.Entities, floor, -10));
        Assert.Equal(6, model.Entities.Faces.Count);
        model.Undo();
        Assert.Single(model.Entities.Faces);
    }

    [Fact]
    public void Moving_a_box_top_edge_sideways_folds_non_planar_faces()
    {
        var ents = Box();
        var topEdge = ents.Edges.Single(e => e.Start.Position.Z == 10 && e.End.Position.Z == 10
                                            && e.Start.Position.Y == 0 && e.End.Position.Y == 0);
        MoveCopy.Move(ents, new[] { topEdge }, P(0, 0, 3));
        Assert.All(ents.Faces, f =>
            Assert.All(f.Vertices, v => Assert.True(Math.Abs(f.Plane.SignedDistance(v.Position)) < 1e-6)));
    }

    [Fact]
    public void Copying_a_face_creates_a_matching_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Rect(0, 0, 10, 10), closed: true);
        var face = ents.Faces.Single();
        MoveCopy.Copy(ents, new Entity[] { face }, P(20, 0));
        Assert.Equal(2, ents.Faces.Count);
        Assert.All(ents.Faces, f => Assert.True(f.Normal.Z < 0));
    }

    [Fact]
    public void Array_copy_makes_several_copies()
    {
        var ents = new Entities();
        ents.AddEdge(P(0, 0), P(0, 10));
        MoveCopy.Copy(ents, ents.Edges.ToList(), P(5, 0), count: 4);
        Assert.Equal(5, ents.Edges.Count);
    }
}
