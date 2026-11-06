using Sketch.Geometry;
using Sketch.IO.Skp;
using Xunit.Abstractions;

namespace Sketch.Model.Tests;

/// <summary>
/// Reads the user's small "ornek.skp" (SketchUp 2026) that holds what Block 4 lacks: a guide line, a section
/// plane, a dimension, a component placed twice and two scenes. Looked for next to Sketch.slnx; skipped when absent.
/// </summary>
public class SkpSampleFileTests(ITestOutputHelper output)
{
    private static string? SampleFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sketch.slnx"))) dir = dir.Parent;
        var path = dir is null ? null : Path.Combine(dir.FullName, "ornek.skp");
        return path is not null && File.Exists(path) ? path : null;
    }

    [Fact]
    public void Reads_guides_sections_dimensions_components_and_scenes()
    {
        var file = SampleFile();
        if (file is null)
        {
            output.WriteLine("ornek.skp not found next to Sketch.slnx: skipped.");
            return;
        }
        var model = new SketchModel();
        var result = SkpImporter.Import(file, model);
        var ents = model.Entities;
        output.WriteLine($"faces={result.Faces} edges={result.Edges} groups={result.Groups} guides={ents.Guides.Count} " +
                         $"dimensions={ents.Dimensions.Count} sections={ents.Sections.Count} scenes={model.Scenes.Items.Count}");

        // A guide line through two points of the tape measure.
        var guide = Assert.Single(ents.Guides);
        Assert.False(guide.Direction.IsZero(), "the guide should be a line, not a point");
        Assert.True(Math.Abs(guide.Direction.Length - 1) < 1e-9);

        // One section plane, active, horizontal, named by SketchUp.
        var section = Assert.Single(ents.Sections);
        Assert.Equal("Section 1", section.Name);
        Assert.True(section.IsActive);
        Assert.True(Math.Abs(Math.Abs(section.Normal.Z) - 1) < 1e-9, $"normal {section.Normal}");
        Assert.True(section.Size > 10);

        // One dimension attached to two vertices of the box, offset away from the measured edge.
        var dimension = Assert.Single(ents.Dimensions);
        Assert.True(dimension.Length > 1, $"length {dimension.Length}");
        Assert.False(dimension.Offset.IsZero());
        output.WriteLine($"dimension {dimension.Start} -> {dimension.End}, offset {dimension.Offset}");
        Assert.Contains(ents.Vertices, v => v.Position.IsEqual(dimension.Start, 0.01));
        Assert.Contains(ents.Vertices, v => v.Position.IsEqual(dimension.End, 0.01));

        // The component placed twice becomes two groups with the same content; SketchUp's figure is the third.
        var copies = ents.Groups.Where(g => g.Name == "Component#1").ToList();
        Assert.Equal(2, copies.Count);
        Assert.Equal(copies[0].Entities.Faces.Count, copies[1].Entities.Faces.Count);
        Assert.True(copies[0].Entities.Faces.Count > 0);
        // SketchUp's figure ("Sree") stays in the file as an unused definition only: no group for it.

        // The file's scenes replace the template's, each with its own camera.
        Assert.Equal(new[] { "Scene 1", "Scene 2" }, model.Scenes.Items.Select(s => s.Name).ToArray());
        Assert.All(model.Scenes.Items, s => Assert.True(s.SavesCamera && s.Camera is not null));
        Assert.All(model.Scenes.Items, s => Assert.True(s.Camera!.Distance > 1));
    }
}
