using Sketch.IO.Skp;
using Xunit.Abstractions;

namespace Sketch.Model.Tests;

/// <summary>Reads SketchUp's own template files through the installed SketchUp C API (skipped without SketchUp).</summary>
public class SkpImportTests(ITestOutputHelper output)
{
    private static string? TemplatesDir()
    {
        var dll = SkpImporter.FindApiLibrary();
        if (dll is null) return null;
        var dir = Path.Combine(Path.GetDirectoryName(dll)!, "Resources", "en-US", "Templates");
        return Directory.Exists(dir) ? dir : null;
    }

    [Fact]
    public void Imports_every_installed_template()
    {
        var dir = TemplatesDir();
        if (dir is null)
        {
            output.WriteLine("SketchUp not installed: skipped.");
            return;
        }
        foreach (var file in Directory.GetFiles(dir, "*.skp"))
        {
            var model = new SketchModel();
            var result = SkpImporter.Import(file, model);
            var faces = model.Entities.AllFaces().ToList();
            output.WriteLine($"{Path.GetFileName(file)}: faces={result.Faces} edges={result.Edges} groups={result.Groups} tags={result.Tags.Count} materials={result.Materials.Count}");
            Assert.Equal(result.Faces, faces.Count);
            Assert.Equal(0, result.SkippedFaces);
            Assert.All(faces, f => Assert.True(f.Area > 0, $"{file}: face {f} is clockwise around its normal"));
            Assert.All(faces, f => Assert.All(f.Vertices, v => Assert.True(Math.Abs(f.Plane.SignedDistance(v.Position)) < 0.01)));
        }
    }
}
