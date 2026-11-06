using Sketch.IO.Skp;
using Sketch.Operations.Deap;
using Xunit.Abstractions;

namespace Sketch.Model.Tests;

/// <summary>
/// Reads the user's "Kinsale Rd Block 4.skp" (SketchUp 2026) with Sketch's own reader, without SketchUp. The file
/// is looked for next to Sketch.slnx (it is not committed); the counts below were established with the SketchUp
/// extension's own output (62 dwellings, 199 openings). Skipped when the file is absent.
/// </summary>
public class SkpNativeReaderTests(ITestOutputHelper output)
{
    private const string FileName = "Kinsale Rd Block 4.skp";

    private static string? SampleFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sketch.slnx"))) dir = dir.Parent;
        var path = dir is null ? null : Path.Combine(dir.FullName, FileName);
        return path is not null && File.Exists(path) ? path : null;
    }

    [Fact]
    public void Reads_a_2026_file_without_SketchUp()
    {
        var file = SampleFile();
        if (file is null)
        {
            output.WriteLine($"{FileName} not found next to Sketch.slnx: skipped.");
            return;
        }
        Assert.True(SkpNativeReader.CanRead(file));
        Assert.Equal(26, SkpNativeReader.FileVersion(file));

        var model = new SketchModel();
        var result = SkpImporter.Import(file, model);
        output.WriteLine($"faces={result.Faces} edges={result.Edges} groups={result.Groups} images={result.Images} " +
                         $"skipped={result.SkippedFaces} tags={result.Tags.Count} materials={result.Materials.Count}");

        var faces = model.Entities.AllFaces().ToList();
        Assert.Equal(result.Faces, faces.Count);
        Assert.Equal(0, result.SkippedFaces);
        Assert.Equal(3, result.Images);
        Assert.Equal(3, model.Entities.AllImages().Count());
        Assert.All(faces, f => Assert.True(f.Area > 0, $"face {f} is clockwise around its normal"));
        Assert.All(faces, f => Assert.All(f.Vertices, v => Assert.True(Math.Abs(f.Plane.SignedDistance(v.Position)) < 0.01)));

        // Tags come with their visibility; the Glass material is recognised for the 199 windows.
        Assert.Contains("100 External Wall 1", result.Tags);
        Assert.False(model.Tags.Find("998 Unheated space")!.Visible);
        Assert.True(model.Tags.Find("100 External Wall 1")!.Visible);
        Assert.Equal(199, faces.Count(f => f.Material?.Name == "Glass"));

        // 62 dwelling groups, 3 floor groups with the plan images, a north arrow and the 62 QA label groups.
        var groups = model.Entities.Groups;
        Assert.Equal(62, groups.Count(g => g.Name.StartsWith("Unit ", StringComparison.Ordinal)));
        Assert.Contains(groups, g => g.Name == "NorthArrow");

        // DEAP evidence from the imported model: 62 dwellings and 199 opening rows, like the extension's CSV.
        var north = DeapEvidence.NorthVector(model);
        var dwellings = DeapEvidence.FindDwellings(model).Select((g, i) => DeapEvidence.Analyse(g, north, i)).ToList();
        Assert.Equal(62, dwellings.Count);
        var csv = DeapEvidence.ToCsv(dwellings);
        var rows = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(200, rows.Length);
        Assert.All(dwellings, d => Assert.True(d.TotalFloorArea > 10, $"{d.Name}: floor area {d.TotalFloorArea}"));

        // The extension wrote its QA labels ("W01\nOrientation: SW") into the file: our opening ids and compass
        // orientations must agree with them dwelling by dwelling (this also pins the north arrow's placement).
        const string qaPrefix = "DEAP_DESIGN_QA_OPENING_LABELS_";
        var expected = new Dictionary<string, HashSet<string>>();
        foreach (var g in groups.Where(g => g.Name.StartsWith(qaPrefix, StringComparison.Ordinal)))
        {
            var set = expected[g.Name[qaPrefix.Length..]] = new HashSet<string>();
            foreach (var note in g.Entities.AllNotes())
            {
                var lines = note.Text.Split('\n');
                if (lines.Length >= 2 && lines[1].StartsWith("Orientation: ", StringComparison.Ordinal))
                    set.Add(lines[0].Trim() + "|" + lines[1]["Orientation: ".Length..].Trim());
            }
        }
        Assert.Equal(62, expected.Count);
        foreach (var d in dwellings)
        {
            Assert.True(expected.TryGetValue(d.Name, out var labels), $"no QA labels for {d.Name}");
            var ours = d.Openings.Select(o => o.Id + "|" + o.Orientation).ToHashSet();
            Assert.True(labels.SetEquals(ours),
                $"{d.Name}: file says {string.Join(",", labels.Order())}; we say {string.Join(",", ours.Order())}");
        }

        // The floor-plan images (inside the rotated floor groups) must cover the dwellings drawn over them.
        var plan = model.Entities.AllImages().First();
        var unitVertices = groups.Where(g => g.Name.StartsWith("Unit ", StringComparison.Ordinal))
            .SelectMany(g => g.Entities.Vertices).Select(v => v.Position).ToList();
        Assert.NotEmpty(unitVertices);
        Assert.All(unitVertices, p =>
        {
            var r = p - plan.Origin;
            var s = Sketch.Geometry.Vector3d.Dot(r, plan.U) / plan.U.LengthSquared;
            var t = Sketch.Geometry.Vector3d.Dot(r, plan.V) / plan.V.LengthSquared;
            Assert.True(s is >= -0.01 and <= 1.01 && t is >= -0.01 and <= 1.01, $"{p} lies outside the floor plan ({s:F2}, {t:F2})");
        });
    }

    [Fact]
    public void Exploding_any_dwelling_keeps_its_faces()
    {
        var file = SampleFile();
        if (file is null)
        {
            output.WriteLine($"{FileName} not found next to Sketch.slnx: skipped.");
            return;
        }
        var model = new SketchModel();
        SkpImporter.Import(file, model);
        var dwellings = DeapEvidence.FindDwellings(model);
        Assert.Equal(62, dwellings.Count);
        foreach (var d in dwellings)
        {
            var count = d.Entities.AllFaces().Count();
            var area = d.Entities.AllFaces().Sum(f => Math.Abs(f.Area));
            var lastId = model.Entities.LastId;
            model.Entities.Explode(d);
            var result = model.Entities.Faces.Where(f => f.Id > lastId).ToList();
            Assert.True(count == result.Count, $"{d.Name}: {count} faces before, {result.Count} after");
            Assert.Equal(area, result.Sum(f => Math.Abs(f.Area)), 3);
        }
    }
}
