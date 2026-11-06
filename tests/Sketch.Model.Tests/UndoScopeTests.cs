using Sketch.Geometry;

namespace Sketch.Model.Tests;

/// <summary>Undo covers tags, materials, scenes and the drawing axes, not only geometry.</summary>
public class UndoScopeTests
{
    [Fact]
    public void Deleting_a_tag_can_be_undone()
    {
        var model = new SketchModel();
        var tag = model.Tags.Add("100 External Wall 1", new Rgb(1, 2, 3));
        model.Run("Delete Tag", () =>
        {
            model.Entities.ReplaceTag(tag, null);
            model.Tags.Remove(tag);
        });
        Assert.Null(model.Tags.Find("100 External Wall 1"));

        Assert.True(model.Undo());
        var back = model.Tags.Find("100 External Wall 1");
        Assert.NotNull(back);
        Assert.Equal(new Rgb(1, 2, 3), back!.Color);

        Assert.True(model.Redo());
        Assert.Null(model.Tags.Find("100 External Wall 1"));
    }

    [Fact]
    public void Tag_identity_survives_undo_and_visibility_is_not_an_edit()
    {
        var model = new SketchModel();
        var tag = model.Tags.Add("Walls", new Rgb(9, 9, 9));
        model.Run("Tag Color", () => tag.Color = new Rgb(200, 0, 0));
        model.Tags.SetVisible(tag, false); // shown/hidden after the edit: must stay hidden after undo
        Assert.True(model.Undo());
        Assert.Same(tag, model.Tags.Find("Walls"));
        Assert.Equal(new Rgb(9, 9, 9), tag.Color);
        Assert.False(tag.Visible);
    }

    [Fact]
    public void Material_scene_and_axes_edits_can_be_undone()
    {
        var model = new SketchModel();
        var glass = model.Materials.Add("Glass", new Rgb(100, 150, 200), 0.5);
        model.Run("Material Opacity", () => glass.Alpha = 1);
        Assert.True(model.Undo());
        Assert.Equal(0.5, model.Materials.Find("Glass")!.Alpha);

        model.Run("Add Scene", () => model.Scenes.Add(new Scene { Name = "Roof TE" }));
        Assert.Contains(model.Scenes.Items, s => s.Name == "Roof TE");
        Assert.True(model.Undo());
        Assert.DoesNotContain(model.Scenes.Items, s => s.Name == "Roof TE");
        Assert.True(model.Redo());
        Assert.Contains(model.Scenes.Items, s => s.Name == "Roof TE");

        var moved = ModelAxes.FromPoints(new Vector3d(10, 0, 0), new Vector3d(10, 5, 0), new Vector3d(0, 0, 0))!;
        model.Run("Axes", () => model.Axes = moved);
        Assert.False(model.Axes.IsWorld);
        Assert.True(model.Undo());
        Assert.True(model.Axes.IsWorld);
    }
}
