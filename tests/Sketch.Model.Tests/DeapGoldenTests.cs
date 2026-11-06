using System.Globalization;
using Sketch.IO.Skp;
using Sketch.Operations.Deap;
using Xunit.Abstractions;

namespace Sketch.Model.Tests;

/// <summary>
/// Compares Sketch's DEAP evidence with the CSV the original SketchUp extension produced for the same .skp file.
/// The pairs are listed in tests/Fixtures/deap-golden.txt as "path\to\model.skp | path\to\DEAP_Evidence_Output.csv"
/// (one per line, '#' starts a comment). Skipped when the list or SketchUp is missing.
/// Numbers are compared as numbers (a CSV saved again by Excel writes 51.4 for 51.40) and dwellings are matched by
/// their values, not their names (names may have been edited in the CSV).
/// </summary>
public class DeapGoldenTests(ITestOutputHelper output)
{
    private const int OpeningColumns = 4;

    private static string? SolutionDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sketch.slnx"))) dir = dir.Parent;
        return dir?.FullName;
    }

    private static string? ListFile()
    {
        var path = SolutionDir() is { } dir ? Path.Combine(dir, "tests", "Fixtures", "deap-golden.txt") : null;
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <summary>Paths in the list may be relative to the solution folder (where the user drops the files).</summary>
    private static string Resolve(string path) =>
        Path.IsPathRooted(path) || SolutionDir() is not { } dir ? path : Path.Combine(dir, path);

    /// <summary>2021+ files need no SketchUp; older ones need its C API library.</summary>
    private static bool CanRead(string skp) => SkpNativeReader.CanRead(skp) || SkpImporter.FindApiLibrary() is not null;

    private static List<string[]> ReadCsv(string path) =>
        File.ReadAllLines(path).Where(l => l.Length > 0).Select(l => l.Split(',')).ToList();

    private static string Num(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v.ToString("F2", CultureInfo.InvariantCulture) : s;

    /// <summary>A dwelling's values (without its name) plus its openings as a sorted set of type|orientation|area.</summary>
    private static string Signature(IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        var values = string.Join(",", list[0].Skip(1).Take(list[0].Length - 1 - OpeningColumns).Select(Num));
        var openings = list.Select(r => $"{r[^3]}|{r[^2]}|{Num(r[^1])}").OrderBy(x => x, StringComparer.Ordinal);
        return values + " :: " + string.Join("; ", openings);
    }

    [Fact]
    public void Matches_the_SketchUp_extension_output()
    {
        var list = ListFile();
        var pairs = list is null ? new List<string>() : File.ReadAllLines(list).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        if (pairs.Count == 0)
        {
            output.WriteLine("No golden files listed (tests/Fixtures/deap-golden.txt): skipped.");
            return;
        }
        var failures = new List<string>();
        foreach (var line in pairs)
        {
            var parts = line.Split('|').Select(p => Resolve(p.Trim().Trim('"'))).ToArray();
            var (skp, csv) = (parts[0], parts[1]);
            if (!File.Exists(skp) || !File.Exists(csv))
            {
                output.WriteLine($"Missing file, skipped: {line}");
                continue;
            }
            if (!CanRead(skp))
            {
                output.WriteLine($"Needs SketchUp (pre-2021 file), skipped: {line}");
                continue;
            }
            var model = new SketchModel();
            SkpImporter.Import(skp, model);
            var north = DeapEvidence.NorthVector(model);
            var ours = DeapEvidence.FindDwellings(model).Select((d, i) => DeapEvidence.Analyse(d, north, i + 1)).ToList();
            var expected = ReadCsv(csv);
            Assert.Equal(DeapEvidence.CsvHeaders, expected[0]);

            var theirs = expected.Skip(1).GroupBy(r => r[0]).Select(g => (Name: g.Key, Sig: Signature(g))).ToList();
            var mine = ours.Select(d => (Name: d.Name, Sig: Signature(DeapEvidence.CsvRows(d).Select(r => r.ToArray())))).ToList();
            output.WriteLine($"{Path.GetFileName(skp)}: dwellings Sketch {mine.Count} / SketchUp {theirs.Count}");

            // Match equal signatures one to one; whatever is left over on either side is a difference.
            var unmatched = mine.ToList();
            var matched = 0;
            foreach (var t in theirs)
            {
                var i = unmatched.FindIndex(m => m.Sig == t.Sig);
                if (i >= 0)
                {
                    output.WriteLine($"  OK  {t.Name} = {unmatched[i].Name}");
                    unmatched.RemoveAt(i);
                    matched++;
                }
                else failures.Add($"{Path.GetFileName(skp)}: SketchUp {t.Name} has no equal dwelling in Sketch\n    SketchUp: {t.Sig}");
            }
            foreach (var m in unmatched) failures.Add($"{Path.GetFileName(skp)}: Sketch {m.Name} not in the SketchUp CSV\n    Sketch:   {m.Sig}");
            output.WriteLine($"  {matched} of {theirs.Count} dwellings identical");
        }
        foreach (var f in failures) output.WriteLine(f);
        Assert.Empty(failures);
    }
}
