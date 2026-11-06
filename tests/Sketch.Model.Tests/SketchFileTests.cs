using Sketch.Geometry;
using Sketch.IO;
using Sketch.Operations;

namespace Sketch.Model.Tests;

public class SketchFileTests
{
    [Fact]
    public void Save_and_load_round_trip()
    {
        var model = new SketchModel();
        var ents = model.Entities;
        ents.AddPolyline(new[] { new Vector3d(0, 0, 0), new Vector3d(40, 0, 0), new Vector3d(40, 20, 0), new Vector3d(0, 20, 0) }, true);
        PushPull.Execute(ents, ents.Faces.Single(), -30);
        ents.Edges[0].Soft = true;
        var camera = new Camera { IsPerspective = false, OrthoHeight = 123 };

        var path = Path.Combine(Path.GetTempPath(), $"sketch-test-{Guid.NewGuid():N}.sketch");
        try
        {
            SketchFile.Save(path, new SketchDocument(ents.CaptureState(), SketchFile.Capture(camera), LengthUnit.Centimeters));
            var doc = SketchFile.Load(path);

            var loaded = new Entities();
            loaded.RestoreState(doc.Entities);
            Assert.Equal(ents.Faces.Count, loaded.Faces.Count);
            Assert.Equal(ents.Edges.Count, loaded.Edges.Count);
            Assert.Equal(ents.Vertices.Count, loaded.Vertices.Count);
            Assert.Contains(loaded.Edges, e => e.Soft);
            foreach (var f in ents.Faces)
            {
                var g = (Face)loaded.FindById(f.Id)!;
                Assert.True(g.Normal.IsEqual(f.Normal, 1e-12));
            }
            Assert.Equal(LengthUnit.Centimeters, doc.Unit);
            var cam2 = new Camera();
            SketchFile.Apply(doc.Camera!, cam2);
            Assert.False(cam2.IsPerspective);
            Assert.Equal(123, cam2.OrthoHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loading_a_non_sketch_file_fails_cleanly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sketch-bad-{Guid.NewGuid():N}.sketch");
        File.WriteAllText(path, "not a model");
        try
        {
            Assert.ThrowsAny<Exception>(() => SketchFile.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("1200", 1200 / 25.4)]
    [InlineData("1.2m", 1200 / 25.4)]
    [InlineData("45cm", 450 / 25.4)]
    [InlineData("12\"", 12)]
    [InlineData("2,5", 2.5 / 25.4)]
    public void Units_parse_lengths(string text, double inches)
    {
        var units = new Units { Unit = LengthUnit.Millimeters };
        Assert.True(units.TryParseLength(text, out var value));
        Assert.Equal(inches, value, 9);
    }
}
