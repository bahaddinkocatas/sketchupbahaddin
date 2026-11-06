using Sketch.Geometry;
using Sketch.Model.Templates;
using Sketch.Operations;

namespace Sketch.Model.Tests;

public class GroupTagTests
{
    private static Vector3d P(double x, double y, double z = 0) => new(x, y, z);

    private static Vector3d[] Square(double x0, double y0, double s, double z = 0) =>
        new[] { P(x0, y0, z), P(x0 + s, y0, z), P(x0 + s, y0 + s, z), P(x0, y0 + s, z) };

    private static SketchModel BoxModel()
    {
        var model = new SketchModel();
        model.Entities.AddPolyline(Square(0, 0, 10), true);
        PushPull.Execute(model.Entities, model.Entities.Faces.Single(), -10);
        return model;
    }

    [Fact]
    public void Making_a_group_moves_geometry_into_it()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var group = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "1F");
        Assert.Empty(ents.Faces);
        Assert.Empty(ents.Edges);
        Assert.Single(ents.Groups);
        Assert.Equal(6, group.Entities.Faces.Count);
        Assert.Equal(12, group.Entities.Edges.Count);
        Assert.Equal("1F", group.Name);
        Assert.Same(group, group.Entities.Owner);
    }

    [Fact]
    public void Geometry_outside_a_group_does_not_stick_to_it()
    {
        var model = BoxModel();
        var ents = model.Entities;
        ents.MakeGroup(ents.Faces.Cast<Entity>().ToList());
        // A line through the grouped box must not split the group's edges.
        ents.AddEdge(P(-5, 5, 10), P(15, 5, 10));
        var group = ents.Groups.Single();
        Assert.Equal(12, group.Entities.Edges.Count);
        Assert.Single(ents.Edges);
    }

    [Fact]
    public void Group_survives_undo_and_redo_with_its_name()
    {
        var model = BoxModel();
        var ents = model.Entities;
        model.Run("Grup", () => ents.MakeGroup(ents.Faces.Cast<Entity>().ToList(), "2F"));
        model.Run("Çizgi", () => ents.AddEdge(P(20, 0), P(30, 0)));
        model.Undo();
        Assert.Equal("2F", ents.Groups.Single().Name);
        model.Undo();
        Assert.Empty(ents.Groups);
        Assert.Equal(6, ents.Faces.Count);
        model.Redo();
        Assert.Equal(6, ents.Groups.Single().Entities.Faces.Count);
    }

    [Fact]
    public void Explode_returns_geometry_to_the_parent()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var group = ents.MakeGroup(ents.Faces.Cast<Entity>().ToList());
        ents.Explode(group);
        Assert.Empty(ents.Groups);
        Assert.Equal(6, ents.Faces.Count);
        var center = P(5, 5, 5);
        Assert.All(ents.Faces, f => Assert.True(Vector3d.Dot(f.Normal, f.OuterLoop.Vertices[0].Position - center) > 0));
    }

    [Fact]
    public void Editing_context_puts_new_geometry_in_the_group()
    {
        var model = BoxModel();
        var group = model.Entities.MakeGroup(model.Entities.Faces.Cast<Entity>().ToList());
        model.EnterGroup(group);
        Assert.Same(group.Entities, model.ActiveEntities);
        model.ActiveEntities.AddEdge(P(0, 0, 10), P(10, 10, 10)); // diagonal on the top face
        Assert.Equal(7, group.Entities.Faces.Count);
        model.ExitGroup();
        Assert.Same(model.Entities, model.ActiveEntities);
    }

    [Fact]
    public void Edit_path_survives_undo()
    {
        var model = BoxModel();
        var group = model.Entities.MakeGroup(model.Entities.Faces.Cast<Entity>().ToList());
        model.EnterGroup(group);
        model.Run("Çizgi", () => model.ActiveEntities.AddEdge(P(0, 0, 10), P(10, 10, 10)));
        model.Undo();
        Assert.Single(model.EditPath);
        Assert.Equal(6, model.ActiveEntities.Faces.Count);
    }

    [Fact]
    public void Tags_and_materials_are_kept_when_faces_split()
    {
        var model = new SketchModel();
        var wall = model.Tags.Add("100 External Wall 1", new Rgb(255, 0, 0));
        var glass = model.Materials.Add("Glass", new Rgb(100, 150, 200), 0.5);
        model.Entities.AddPolyline(Square(0, 0, 10), true);
        var face = model.Entities.Faces.Single();
        face.Tag = wall;
        face.Material = glass;
        model.Entities.AddEdge(P(0, 0), P(10, 10));
        Assert.All(model.Entities.Faces, f =>
        {
            Assert.Same(wall, f.Tag);
            Assert.Same(glass, f.Material);
        });
    }

    [Fact]
    public void New_geometry_gets_the_active_tag()
    {
        var model = new SketchModel();
        var tag = model.Tags.Add("120 Internal Wall 1", new Rgb(0, 0, 255));
        model.ActiveTag = tag;
        model.Entities.AddPolyline(Square(0, 0, 10), true);
        Assert.Same(tag, model.Entities.Faces.Single().Tag);
        Assert.All(model.Entities.Edges, e => Assert.Same(tag, e.Tag));
    }

    [Fact]
    public void Tag_assignments_survive_undo()
    {
        var model = new SketchModel();
        var tag = model.Tags.Add("001 Exposed Floor 1", new Rgb(0, 128, 128));
        model.Run("Kare", () => model.Entities.AddPolyline(Square(0, 0, 10), true));
        model.Run("Etiket", () => model.Entities.Faces.Single().Tag = tag);
        model.Run("Çizgi", () => model.Entities.AddEdge(P(20, 0), P(30, 0)));
        model.Undo();
        Assert.Same(tag, model.Entities.Faces.Single().Tag);
    }

    [Fact]
    public void Hidden_tag_hides_group_content()
    {
        var model = BoxModel();
        var tag = model.Tags.Add("998 Heated space", new Rgb(1, 2, 3));
        var group = model.Entities.MakeGroup(model.Entities.Faces.Cast<Entity>().ToList());
        group.Tag = tag;
        var face = group.Entities.Faces[0];
        Assert.True(Entities.IsEffectivelyVisible(face));
        model.Tags.SetVisible(tag, false);
        Assert.False(Entities.IsEffectivelyVisible(face));
        Assert.Same(tag, Entities.EffectiveTag(face));
    }

    [Fact]
    public void Copying_a_group_duplicates_its_content()
    {
        var model = BoxModel();
        var group = model.Entities.MakeGroup(model.Entities.Faces.Cast<Entity>().ToList(), "Kutu");
        var copies = MoveCopy.Copy(model.Entities, new Entity[] { group }, P(20, 0, 0), count: 2);
        Assert.Equal(3, model.Entities.Groups.Count);
        var last = copies.OfType<Group>().Single();
        Assert.Equal("Kutu", last.Name);
        Assert.Equal(40, last.Entities.Vertices.Min(v => v.Position.X), 9);
        Assert.Equal(model.Entities.AllEdges().Count(), model.Entities.AllEdges().Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Guides_dimensions_and_notes_survive_undo_and_file_round_trip()
    {
        var model = BoxModel();
        var ents = model.Entities;
        model.Run("Notlar", () =>
        {
            ents.AddGuideLine(P(0, 0, 0), Vector3d.UnitX);
            ents.AddGuidePoint(P(5, 5, 5));
            ents.AddDimension(P(0, 0, 0), P(10, 0, 0), P(0, -3, 0));
            ents.AddNote(P(5, 5, 10), P(3, 3, 3), "Çatı", leader: true);
        });
        model.Run("Çizgi", () => ents.AddEdge(P(20, 0), P(30, 0)));
        model.Undo();
        Assert.Equal(2, ents.Guides.Count);
        Assert.Single(ents.Dimensions);
        Assert.Equal("Çatı", ents.Notes.Single().Text);

        var path = Path.Combine(Path.GetTempPath(), $"sketch-ann-{Guid.NewGuid():N}.sketch");
        try
        {
            Sketch.IO.SketchFile.Save(path, Sketch.IO.SketchDocument.From(model, new Camera()));
            var other = new SketchModel();
            Sketch.IO.SketchFile.Load(path).ApplyTo(other, new Camera());
            Assert.Equal(2, other.Entities.Guides.Count);
            Assert.Equal(10, other.Entities.Dimensions.Single().Length, 9);
            Assert.True(other.Entities.Notes.Single().HasLeader);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Template_has_the_users_tags_materials_and_scenes()
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        Assert.Equal(ThermalTemplate.TagNames.Length, model.Tags.Items.Count);
        Assert.NotNull(model.Tags.Find("100 External Wall 1"));
        // No duplicates (same name, or the same element under two numbers).
        var elements = model.Tags.Items.Select(t => t.Name[4..].ToLowerInvariant().Replace("-", " ")).ToList();
        Assert.Equal(elements.Count, elements.Distinct().Count());
        Assert.NotNull(model.Materials.Find("Glass"));
        Assert.NotNull(model.Materials.Find("Door"));
        Assert.Equal(2, model.Materials.Items.Count); // only Glass and Door
        Assert.Contains(model.Scenes.Items, s => s.Name == "(Wall TE)");
    }

    [Fact]
    public void Scene_applies_tag_visibility()
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        model.ApplyScene(model.Scenes.Items.Single(s => s.Name == "(Floor TE)"));
        Assert.True(model.Tags.Find("001 Exposed Floor 1")!.Visible);
        Assert.False(model.Tags.Find("100 External Wall 1")!.Visible);
        Assert.True(model.Style.ColorByTag);
    }

    [Fact]
    public void Editing_a_group_hides_the_rest_of_the_model()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var outer = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "Unit 1");
        ents.AddPolyline(Square(20, 0, 5), true);
        var other = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "Unit 2");
        var loose = ents.AddEdge(P(0, 30), P(5, 30)).Single();
        var inner = outer.Entities.MakeGroup(outer.Entities.Faces.Take(1).Cast<Entity>().ToList(), "Window");
        var sibling = outer.Entities.Faces.First();

        model.EnterGroup(outer);
        model.EnterGroup(inner);
        Assert.True(Entities.IsEffectivelyVisible(inner.Entities.Faces.Single()));
        Assert.True(Entities.IsEffectivelyVisible(outer)); // the groups around the edited one stay "visible"
        Assert.True(Entities.IsEffectivelyVisible(inner));
        Assert.False(Entities.IsEffectivelyVisible(sibling));
        Assert.False(Entities.IsEffectivelyVisible(other));
        Assert.False(Entities.IsEffectivelyVisible(other.Entities.Faces.Single()));
        Assert.False(Entities.IsEffectivelyVisible(loose));

        model.HideRestOfModel = false; // faded instead of hidden
        Assert.True(Entities.IsEffectivelyVisible(other));
        model.HideRestOfModel = true;

        model.ExitAllGroups();
        Assert.True(Entities.IsEffectivelyVisible(other));
        Assert.True(Entities.IsEffectivelyVisible(loose));
    }

    [Fact]
    public void Undo_while_editing_a_group_keeps_the_isolation_on_the_new_group_objects()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var unit = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "Unit 1");
        ents.AddEdge(P(0, 30), P(5, 30));
        model.EnterGroup(unit);
        model.Run("Line", () => unit.Entities.AddEdge(P(0, 0, 20), P(5, 0, 20)));
        model.Undo(); // recreates the group objects
        var restored = model.Entities.Groups.Single();
        Assert.True(Entities.IsEffectivelyVisible(restored.Entities.Faces.First()));
        Assert.False(Entities.IsEffectivelyVisible(model.Entities.Edges.Single()));
    }

    [Fact]
    public void Grouping_and_exploding_keep_images_texts_and_dimensions()
    {
        var model = new SketchModel();
        var ents = model.Entities;
        var key = model.Context.Images.Add(new byte[] { 1, 2, 3 });
        var image = ents.AddImage(key, P(0, 0), P(100, 0), P(0, 80), "plan.png");
        var note = ents.AddNote(P(10, 10), Vector3d.Zero, "Kitchen", leader: false);
        var dim = ents.AddDimension(P(0, 0), P(100, 0), P(0, -10));

        var group = ents.MakeGroup(new Entity[] { image, note, dim }, "1. Floor");
        Assert.Empty(ents.Images);
        Assert.Empty(ents.Notes);
        Assert.Empty(ents.Dimensions);
        Assert.Same(image, Assert.Single(group.Entities.Images));
        Assert.Same(group.Entities, image.Parent);
        Assert.Single(group.Entities.Notes);
        Assert.Single(group.Entities.Dimensions);

        var result = ents.Explode(group);
        Assert.Empty(ents.Groups);
        var back = Assert.Single(ents.Images);
        Assert.Equal(key, back.ImageKey);
        Assert.Equal(P(100, 0), back.U);
        Assert.Single(ents.Notes, n => n.Text == "Kitchen");
        Assert.Single(ents.Dimensions);
        Assert.Contains(back, result);
    }

    [Fact]
    public void Preview_restores_keep_untouched_groups_and_rebuild_changed_ones()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var a = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "A");
        ents.AddPolyline(Square(20, 0, 5), true);
        var b = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "B");
        var snapshot = ents.CaptureState();

        // Change B's content and some loose geometry, then go back.
        Operations.MoveCopy.Transform(b.Entities, b.Entities.Edges.Cast<Entity>().ToList(), Matrix4d.Translation(new Vector3d(0, 0, 7)));
        ents.AddEdge(P(0, 50), P(5, 50));
        ents.RestoreState(snapshot, keepUnchangedGroups: true);

        Assert.Same(a, ents.Groups.Single(g => g.Name == "A"));       // untouched: the very same object
        Assert.False(a.IsDeleted);
        var restoredB = ents.Groups.Single(g => g.Name == "B");
        Assert.NotSame(b, restoredB);                                  // changed: rebuilt from the snapshot
        Assert.True(b.IsDeleted);
        Assert.All(restoredB.Entities.Vertices, v => Assert.Equal(0, v.Position.Z, 9));
        Assert.Empty(ents.Edges);
    }

    [Fact]
    public void Undo_restores_a_moved_group()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var a = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "A");
        ents.AddPolyline(Square(20, 0, 5), true);
        ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "B");
        model.ResetHistory();

        model.Run("Move", () => Operations.MoveCopy.Transform(ents, new Entity[] { ents.Groups.Single(g => g.Name == "B") },
            Matrix4d.Translation(new Vector3d(100, 0, 0))));
        Assert.All(ents.Groups.Single(g => g.Name == "B").Entities.Vertices, v => Assert.True(v.Position.X >= 100));
        model.Undo();
        Assert.All(ents.Groups.Single(g => g.Name == "B").Entities.Vertices, v => Assert.True(v.Position.X < 30));
        Assert.Contains(ents.Groups, g => g.Name == "A");
        model.Redo();
        Assert.All(ents.Groups.Single(g => g.Name == "B").Entities.Vertices, v => Assert.True(v.Position.X >= 100));
    }

    /// <summary>Two floors side by side (different tags), a wall with a window hole and the glass in it.</summary>
    private static (SketchModel Model, Group Group) FloorsAndWall()
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        var ents = model.Entities;
        ents.Erase(ents.Groups.Cast<Entity>().ToList()); // no north arrow
        ents.AddPolyline(new[] { P(0, 0), P(10, 0), P(10, 5), P(0, 5) }, true);
        ents.Faces.Single().Tag = model.Tags.Find("020 Non-heat Loss Floor 1");
        ents.AddPolyline(new[] { P(0, 5), P(10, 5), P(10, 8), P(0, 8) }, true);
        ents.Faces.Single(f => f.ContainsPoint(P(5, 6.5))).Tag = model.Tags.Find("024 Non-heat Loss Floor Living Area");
        ents.AddPolyline(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 3), P(0, 0, 3) }, true);
        ents.AddPolyline(new[] { P(2, 0, 1), P(4, 0, 1), P(4, 0, 2), P(2, 0, 2) }, true);
        ents.Faces.Single(f => f.ContainsPoint(P(3, 0, 1.5))).Material = model.Materials.Find("Glass");
        ents.Faces.Single(f => f.ContainsPoint(P(8, 0, 1.5))).Tag = model.Tags.Find("100 External Wall 1");
        var group = ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "Unit 1");
        return (model, group);
    }

    private static List<string> Summary(IEnumerable<Face> faces) => faces
        .Select(f => $"{Entities.EffectiveTag(f)?.Name ?? "-"}|{f.Material?.Name ?? "-"}|{Math.Abs(f.Area):0.###}|{f.InnerLoops.Count}|{f.Normal}")
        .OrderBy(x => x, StringComparer.Ordinal).ToList();

    [Fact]
    public void Exploding_keeps_every_face_with_its_tag_material_and_hole()
    {
        var (model, group) = FloorsAndWall();
        var before = Summary(group.Entities.Faces);
        Assert.Equal(4, before.Count);
        model.Entities.Explode(group);
        Assert.Equal(before, Summary(model.Entities.Faces));
    }

    [Fact]
    public void Exploding_next_to_existing_geometry_keeps_both()
    {
        var (model, group) = FloorsAndWall();
        var ents = model.Entities;
        ents.AddPolyline(new[] { P(10, 0), P(15, 0), P(15, 5), P(10, 5) }, true); // touches the first floor's edge
        var neighbour = Summary(ents.Faces);
        var before = Summary(group.Entities.Faces);
        ents.Explode(group);
        Assert.Equal(before.Concat(neighbour).OrderBy(x => x, StringComparer.Ordinal).ToList(), Summary(ents.Faces));
    }

    [Fact]
    public void Undoing_a_tag_rename_keeps_the_tag_of_faces_inside_groups()
    {
        var model = BoxModel();
        var ents = model.Entities;
        var tag = model.Tags.Add("A", new Rgb(10, 20, 30));
        foreach (var f in ents.Faces) f.Tag = tag;
        ents.MakeGroup(ents.Faces.Cast<Entity>().Concat(ents.Edges).ToList(), "Unit");
        model.ResetHistory();

        model.Run("Rename Tag", () => model.Tags.Rename(model.Tags.Find("A")!, "B"));
        model.Undo();
        var current = model.Tags.Find("A");
        Assert.NotNull(current);
        Assert.All(ents.Groups.Single().Entities.Faces, f => Assert.Same(current, f.Tag));
    }
}
