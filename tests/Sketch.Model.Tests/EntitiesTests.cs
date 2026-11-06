using Sketch.Geometry;

namespace Sketch.Model.Tests;

public class EntitiesTests
{
    private static Vector3d P(double x, double y, double z = 0) => new(x, y, z);

    private static Vector3d[] Square(double x0, double y0, double size, double z = 0) =>
        new[] { P(x0, y0, z), P(x0 + size, y0, z), P(x0 + size, y0 + size, z), P(x0, y0 + size, z) };

    [Fact]
    public void Single_line_creates_two_vertices_and_one_edge_no_face()
    {
        var ents = new Entities();
        ents.AddEdge(P(0, 0), P(10, 0));
        Assert.Equal(2, ents.Vertices.Count);
        Assert.Single(ents.Edges);
        Assert.Empty(ents.Faces);
    }

    [Fact]
    public void Closed_square_drawn_edge_by_edge_creates_a_face()
    {
        var ents = new Entities();
        var s = Square(0, 0, 10);
        for (var i = 0; i < 4; i++) ents.AddEdge(s[i], s[(i + 1) % 4]);
        var face = Assert.Single(ents.Faces);
        Assert.Equal(100, Math.Abs(face.Area), 6);
        Assert.Equal(4, face.Edges.Count);
    }

    [Fact]
    public void Face_on_ground_faces_down()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        var face = Assert.Single(ents.Faces);
        Assert.True(face.Normal.Z < -0.99, $"normal {face.Normal}");
    }

    [Fact]
    public void Face_above_ground_faces_up()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10, z: 5), closed: true);
        Assert.True(Assert.Single(ents.Faces).Normal.Z > 0.99);
    }

    [Fact]
    public void Outer_loop_is_counter_clockwise_around_normal()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10, z: 5), closed: true);
        Assert.True(Assert.Single(ents.Faces).Area > 0);
    }

    [Fact]
    public void Diagonal_splits_face_into_two()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.AddEdge(P(0, 0), P(10, 10));
        Assert.Equal(2, ents.Faces.Count);
        Assert.All(ents.Faces, f => Assert.Equal(50, Math.Abs(f.Area), 6));
        Assert.All(ents.Faces, f => Assert.True(f.Normal.Z < 0)); // orientation inherited
    }

    [Fact]
    public void Line_across_face_between_edge_midpoints_splits_edges_and_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.AddEdge(P(5, 0), P(5, 10));
        Assert.Equal(2, ents.Faces.Count);
        Assert.Equal(7, ents.Edges.Count); // 4 sides, 2 of them split, plus the new edge
        Assert.Equal(6, ents.Vertices.Count);
    }

    [Fact]
    public void Crossing_lines_split_each_other()
    {
        var ents = new Entities();
        ents.AddEdge(P(0, 5), P(10, 5));
        ents.AddEdge(P(5, 0), P(5, 10));
        Assert.Equal(4, ents.Edges.Count);
        Assert.Equal(5, ents.Vertices.Count);
        Assert.NotNull(ents.FindVertex(P(5, 5)));
    }

    [Fact]
    public void Line_ending_on_edge_splits_that_edge()
    {
        var ents = new Entities();
        ents.AddEdge(P(0, 0), P(10, 0));
        ents.AddEdge(P(4, 0), P(4, 6));
        Assert.Equal(3, ents.Edges.Count);
    }

    [Fact]
    public void Erasing_a_boundary_edge_deletes_the_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        var edge = ents.Edges[0];
        ents.Erase(new[] { edge });
        Assert.Empty(ents.Faces);
        Assert.Equal(3, ents.Edges.Count);
    }

    [Fact]
    public void Erasing_the_diagonal_heals_the_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        var diagonal = ents.AddEdge(P(0, 0), P(10, 10)).Single();
        Assert.Equal(2, ents.Faces.Count);

        ents.Erase(new[] { diagonal });

        var face = Assert.Single(ents.Faces);
        Assert.Equal(100, Math.Abs(face.Area), 6);
        Assert.True(face.Normal.Z < 0);
    }

    [Fact]
    public void Erasing_a_midline_heals_face_and_merges_collinear_edges()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        var mid = ents.AddEdge(P(5, 0), P(5, 10)).Single();
        ents.Erase(new[] { mid });
        Assert.Single(ents.Faces);
        Assert.Equal(4, ents.Edges.Count);
        Assert.Equal(4, ents.Vertices.Count);
    }

    [Fact]
    public void Erasing_only_a_face_keeps_edges()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.Erase(new[] { ents.Faces[0] });
        Assert.Empty(ents.Faces);
        Assert.Equal(4, ents.Edges.Count);
    }

    [Fact]
    public void Redrawing_an_edge_restores_a_deleted_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.Erase(new[] { ents.Faces[0] });
        ents.AddEdge(P(0, 0), P(10, 0));
        Assert.Single(ents.Faces);
    }

    [Fact]
    public void Rectangle_drawn_inside_a_face_makes_a_hole_and_an_inner_face()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.AddPolyline(Square(3, 3, 4), closed: true);

        Assert.Equal(2, ents.Faces.Count);
        var outer = ents.Faces.Single(f => f.InnerLoops.Count == 1);
        var inner = ents.Faces.Single(f => f.InnerLoops.Count == 0);
        Assert.Equal(100 - 16, Math.Abs(outer.Area), 6);
        Assert.Equal(16, Math.Abs(inner.Area), 6);
        Assert.True(outer.Normal.Z < 0 && inner.Normal.Z < 0, "the inner face keeps the outer face's orientation");
    }

    [Fact]
    public void Rectangle_drawn_around_a_face_gets_a_hole()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(3, 3, 4), closed: true);
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        Assert.Equal(2, ents.Faces.Count);
        Assert.Contains(ents.Faces, f => f.InnerLoops.Count == 1);
    }

    [Fact]
    public void Dangling_edge_inside_face_does_not_split_it()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.AddEdge(P(0, 0), P(4, 4));
        var face = Assert.Single(ents.Faces);
        Assert.Equal(100, Math.Abs(face.Area), 6);
    }

    [Fact]
    public void Open_box_sides_orient_consistently_with_the_floor()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true); // floor faces down
        ents.AddPolyline(Square(0, 0, 10, z: 10), closed: true);
        // Four vertical walls.
        ents.AddEdge(P(0, 0, 0), P(0, 0, 10));
        ents.AddEdge(P(10, 0, 0), P(10, 0, 10));
        ents.AddEdge(P(10, 10, 0), P(10, 10, 10));
        ents.AddEdge(P(0, 10, 0), P(0, 10, 10));

        Assert.Equal(6, ents.Faces.Count);
        var center = P(5, 5, 5);
        foreach (var f in ents.Faces)
        {
            var toFace = f.OuterLoop.Vertices[0].Position - center;
            Assert.True(Vector3d.Dot(f.Normal, toFace) > 0, $"{f} normal {f.Normal} should point outward");
        }
    }

    [Fact]
    public void State_round_trip_restores_geometry()
    {
        var ents = new Entities();
        ents.AddPolyline(Square(0, 0, 10), closed: true);
        ents.AddEdge(P(0, 0), P(10, 10));
        var state = ents.CaptureState();
        ents.Clear();
        Assert.Empty(ents.Edges);
        ents.RestoreState(state);
        Assert.Equal(2, ents.Faces.Count);
        Assert.Equal(5, ents.Edges.Count);
        Assert.All(ents.Faces, f => Assert.Equal(3, f.Edges.Count));
    }

    [Fact]
    public void Undo_and_redo_through_the_model()
    {
        var model = new SketchModel();
        model.Run("Dikdörtgen", () => model.Entities.AddPolyline(Square(0, 0, 10), closed: true));
        model.Run("Çizgi", () => model.Entities.AddEdge(P(0, 0), P(10, 10)));
        Assert.Equal(2, model.Entities.Faces.Count);

        Assert.True(model.Undo());
        Assert.Single(model.Entities.Faces);
        Assert.True(model.Undo());
        Assert.Empty(model.Entities.Edges);
        Assert.False(model.Undo());

        Assert.True(model.Redo());
        Assert.Single(model.Entities.Faces);
    }

    [Fact]
    public void Triangle_closed_by_third_edge_creates_face()
    {
        var ents = new Entities();
        ents.AddEdge(P(0, 0, 3), P(10, 0, 3));
        ents.AddEdge(P(10, 0, 3), P(0, 10, 3));
        Assert.Empty(ents.Faces);
        ents.AddEdge(P(0, 10, 3), P(0, 0, 3));
        Assert.Single(ents.Faces);
    }

    [Fact]
    public void Non_planar_loop_creates_no_face()
    {
        var ents = new Entities();
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 10, 5), P(0, 10, 0) }, closed: true);
        Assert.Empty(ents.Faces);
    }
}
