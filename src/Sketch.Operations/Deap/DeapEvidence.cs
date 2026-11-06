using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Model.Templates;
using Group = Sketch.Model.Group;

namespace Sketch.Operations.Deap;

/// <summary>How an element's value is worked out from its tagged faces.</summary>
public enum DeapCalculation
{
    SumArea,

    /// <summary>External walls: the tagged wall faces' own area (window/door holes are already excluded).</summary>
    NetExternalWallArea,
}

/// <summary>One DEAP element (a CSV column) and the tag numbers whose faces make it up.</summary>
public sealed record DeapElement(string Key, string Name, IReadOnlyList<int> Tags, string CsvHeading, int CsvOrder,
    bool IncludeInTotalFloorArea, DeapCalculation Calculation = DeapCalculation.SumArea);

/// <summary>A window, rooflight or door found in a dwelling.</summary>
public sealed record DeapOpening(string Id, string Type, string Orientation, double AreaSqm, Face Face);

/// <summary>Everything worked out for one dwelling.</summary>
public sealed record DeapDwelling(Group Group, string Name, IReadOnlyDictionary<string, double> Elements, double TotalFloorArea,
    double HeightM, IReadOnlyList<DeapOpening> Openings);

/// <summary>
/// DEAP evidence for the design stage, following EDC's "DEAP Evidence Tool - Design Stage" v2.0 SketchUp
/// extension: per dwelling, the areas of the faces grouped by the number at the start of their (effective) tag,
/// the dwelling height, and the openings (Glass = window/rooflight, Door = door) with their
/// compass orientation. Writes the same CSV. Lengths in the model are inches, as in SketchUp.
/// </summary>
public static class DeapEvidence
{
    public const double InchToM = 0.0254;
    public const double SqInToSqM = InchToM * InchToM;
    public const string CsvFileName = "DEAP_Evidence_Output.csv";
    public const string QaGroupPrefix = "DEAP_DESIGN_QA_OPENING_LABELS";
    public const string AnnotationMaterialName = "DEAP_Annotation_Red";

    private static int[] Range(int from, int to) => Enumerable.Range(from, to - from + 1).ToArray();

    public static readonly IReadOnlyList<DeapElement> Registry = new[]
    {
        new DeapElement("exposed_floor_area", "Exposed Floor Area", Range(1, 5), "Exposed Floor Area", 10, true),
        new DeapElement("semi_exposed_floor_area", "Semi-Exposed Floor Area", Range(10, 14), "Semi-Exposed Floor Area", 20, true),
        new DeapElement("non_heat_loss_floor_area", "Non-Heat Loss Floor Area", Range(20, 24), "Non-Heat Loss Floor Area", 30, true),
        new DeapElement("living_area", "Living Area", new[] { 5, 14, 24 }, "Living Area", 40, false),
        new DeapElement("external_wall_area", "External Wall Area", Range(100, 103), "External Wall Area", 50, false, DeapCalculation.NetExternalWallArea),
        new DeapElement("semi_exposed_wall_area", "Semi-Exposed Wall Area", Range(110, 113), "Semi-Exposed Wall Area", 60, false),
        new DeapElement("internal_wall_area", "Internal Wall Area", Range(120, 123), "Internal Wall Area", 70, false),
        new DeapElement("ceiling_area", "Ceiling Area", Range(200, 203).Concat(Range(210, 213)).Concat(Range(240, 243)).ToArray(), "Ceiling Area", 80, false),
        new DeapElement("roof_area", "Roof Area", Range(220, 223).Concat(Range(230, 233)).ToArray(), "Roof Area", 90, false),
    };

    private const int CsvHeightBreakOrder = 50;

    private static readonly HashSet<int> FloorTags = Registry.Where(e => e.IncludeInTotalFloorArea).SelectMany(e => e.Tags).ToHashSet();
    private static readonly HashSet<int> HeightTopTags = Registry.Where(e => e.Key is "ceiling_area" or "roof_area").SelectMany(e => e.Tags).ToHashSet();
    private static readonly HashSet<int> ExternalWallTags = Registry.Single(e => e.Key == "external_wall_area").Tags.ToHashSet();

    private static IEnumerable<DeapElement> CsvElements => Registry.OrderBy(e => e.CsvOrder);

    public static IReadOnlyList<string> CsvHeaders =>
        new[] { "Dwelling Name", "Total Floor Area" }
            .Concat(CsvElements.Where(e => e.CsvOrder < CsvHeightBreakOrder).Select(e => e.CsvHeading))
            .Append("Dwelling Height")
            .Concat(CsvElements.Where(e => e.CsvOrder >= CsvHeightBreakOrder).Select(e => e.CsvHeading))
            .Concat(new[] { "Opening ID", "Opening Type", "Opening Orientation", "Opening Area" })
            .ToList();

    private static readonly Regex CodePattern = new(@"^\s*0*(\d+)", RegexOptions.Compiled);
    private static readonly Regex NorthArrowName = new(@"north\s*arrow|northarrow", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The number at the start of a tag's name ("100 External Wall 1" → 100), or null.</summary>
    public static int? TagCode(Tag? tag)
    {
        if (tag is null) return null;
        var m = CodePattern.Match(tag.Name);
        return m.Success && int.TryParse(m.Groups[1].Value, out var code) ? code : null;
    }

    /// <summary>
    /// The face's own tag code, else the nearest enclosing group's (construction tags are often put on the
    /// containing group rather than on every face; a tag without a number does not count).
    /// </summary>
    public static int? EffectiveCode(Entity e)
    {
        if (TagCode(e.Tag) is { } own) return own;
        for (var g = e.Parent?.Owner; g is not null; g = g.Parent?.Owner)
            if (TagCode(g.Tag) is { } code) return code;
        return null;
    }

    private static bool IsNorthArrow(Group g) => NorthArrowName.IsMatch(g.Name);

    /// <summary>A top-level group that contains at least one face with a floor tag (the north arrow never is).</summary>
    public static bool IsDwelling(Group g) =>
        !IsNorthArrow(g) && g.Entities.AllFaces().Any(f => EffectiveCode(f) is { } c && FloorTags.Contains(c));

    /// <summary>The selected dwellings if any are selected, otherwise every dwelling at the top level of the model.</summary>
    public static List<Group> FindDwellings(SketchModel model, IEnumerable<Entity>? selection = null)
    {
        var selected = (selection ?? Array.Empty<Entity>()).OfType<Group>().Where(g => !g.IsDeleted && IsDwelling(g)).ToList();
        return selected.Count > 0 ? selected : model.Entities.Groups.Where(IsDwelling).ToList();
    }

    /// <summary>North: the longest edge of the top-level "North Arrow" group (start to end), else the green axis.</summary>
    public static Vector3d NorthVector(SketchModel model)
    {
        foreach (var g in model.Entities.Groups.Where(IsNorthArrow))
        {
            Edge? longest = null;
            foreach (var e in g.Entities.Edges)
                if (longest is null || e.Length > longest.Length) longest = e;
            if (longest is not null)
            {
                var v = longest.End.Position - longest.Start.Position;
                if (v.Length > 0.001) return v;
            }
        }
        return Vector3d.UnitY;
    }

    /// <summary>N, NE, E … NW for a face normal, relative to north (empty for horizontal faces).</summary>
    public static string CardinalOrientation(Vector3d normal, Vector3d north)
    {
        var horizontal = new Vector3d(normal.X, normal.Y, 0);
        if (horizontal.Length < 0.001) return string.Empty;
        horizontal = horizontal.Normalized();
        var n = new Vector3d(north.X, north.Y, 0);
        if (n.Length < 0.001) n = Vector3d.UnitY;
        n = n.Normalized();
        var east = new Vector3d(n.Y, -n.X, 0);
        var angle = Math.Atan2(Vector3d.Dot(horizontal, east), Vector3d.Dot(horizontal, n)) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;
        string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return dirs[(int)Math.Floor((angle + 22.5) / 45.0) % 8];
    }

    /// <summary>
    /// Glass or Door for an opening face (its own material only): names with "door" or "wood" are doors (the
    /// extension's wood doors, which Sketch calls Door), names with "glass" are windows.
    /// </summary>
    private static string? OpeningMaterial(Face f) => f.Material is { } m ? ThermalTemplate.OpeningMaterial(m.Name) : null;

    public static double AreaSqm(Face f) => Math.Abs(f.Area) * SqInToSqM;

    public static DeapDwelling Analyse(Group dwelling, Vector3d north, int index)
    {
        var faces = dwelling.Entities.AllFaces().Select(f => (Face: f, Code: EffectiveCode(f), Area: AreaSqm(f))).ToList();

        var openings = new List<DeapOpening>();
        var count = 0;
        foreach (var (face, _, area) in faces)
        {
            if (OpeningMaterial(face) is not { } material) continue;
            var door = material == ThermalTemplate.DoorName;
            count++;
            var type = door ? "Door" : Math.Abs(face.Normal.Z) > 0.45 ? "Rooflight" : "Window";
            var orientation = type == "Door" ? string.Empty : CardinalOrientation(face.Normal, north);
            var prefix = type switch { "Door" => "D", "Rooflight" => "R", _ => "W" };
            openings.Add(new DeapOpening($"{prefix}{count:00}", type, orientation, area, face));
        }

        double Sum(IEnumerable<int> tags)
        {
            var set = tags.ToHashSet();
            return faces.Where(f => f.Code is { } c && set.Contains(c)).Sum(f => f.Area);
        }

        var elements = new Dictionary<string, double>();
        foreach (var e in Registry)
            elements[e.Key] = e.Calculation == DeapCalculation.NetExternalWallArea ? Sum(ExternalWallTags) : Sum(e.Tags);
        var total = Registry.Where(e => e.IncludeInTotalFloorArea).Sum(e => elements[e.Key]);

        List<double> Zs(HashSet<int> tags) => faces.Where(f => f.Code is { } c && tags.Contains(c))
            .SelectMany(f => f.Face.Vertices.Select(v => v.Position.Z)).ToList();
        var floorZs = Zs(FloorTags);
        var topZs = Zs(HeightTopTags);
        var height = floorZs.Count > 0 && topZs.Count > 0 ? Math.Abs((topZs.Max() - floorZs.Min()) * InchToM) : 0.0;

        var name = SafeName(string.IsNullOrEmpty(dwelling.Name) ? $"Dwelling_{index}" : dwelling.Name);
        return new DeapDwelling(dwelling, name, elements, total, height, openings);
    }

    /// <summary>Letters, digits, '_' and '-' only (other runs become one '_'), trimmed of '_' at both ends.</summary>
    public static string SafeName(string name)
    {
        var s = Regex.Replace(name.Trim(), @"[^0-9A-Za-z_\-]+", "_");
        s = Regex.Replace(s, "_+", "_");
        if (s.StartsWith('_')) s = s[1..];
        if (s.EndsWith('_')) s = s[..^1];
        return s;
    }

    /// <summary>Two decimals with a point, whatever the Windows language.</summary>
    public static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>One CSV row per opening (dwelling values repeated), or one row with empty opening fields.</summary>
    public static List<List<string>> CsvRows(DeapDwelling d)
    {
        var rows = new List<List<string>>();
        var openings = d.Openings.Count > 0
            ? d.Openings.Select(o => (o.Id, o.Type, o.Orientation, o.AreaSqm)).ToList()
            : new List<(string, string, string, double)> { (string.Empty, string.Empty, string.Empty, 0.0) };
        foreach (var (id, type, orientation, area) in openings)
        {
            var row = new List<string> { d.Name, Fmt(d.TotalFloorArea) };
            row.AddRange(CsvElements.Where(e => e.CsvOrder < CsvHeightBreakOrder).Select(e => Fmt(d.Elements[e.Key])));
            row.Add(Fmt(d.HeightM));
            row.AddRange(CsvElements.Where(e => e.CsvOrder >= CsvHeightBreakOrder).Select(e => Fmt(d.Elements[e.Key])));
            row.AddRange(new[] { id, type, orientation, Fmt(area) });
            rows.Add(row);
        }
        return rows;
    }

    private static string CsvField(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public static string ToCsv(IEnumerable<DeapDwelling> dwellings)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", CsvHeaders.Select(CsvField))).Append("\r\n");
        foreach (var d in dwellings)
            foreach (var row in CsvRows(d))
                sb.Append(string.Join(",", row.Select(CsvField))).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// Replaces the QA label groups: one group per dwelling with a red "W01 / Orientation: S" note above each
    /// opening (call inside an undoable operation).
    /// </summary>
    public static void AddQaOpeningLabels(SketchModel model, IEnumerable<DeapDwelling> dwellings)
    {
        var old = model.Entities.Groups.Where(g => g.Name.StartsWith(QaGroupPrefix, StringComparison.Ordinal)).Cast<Entity>().ToList();
        if (old.Count > 0) model.Entities.Erase(old);
        var red = model.Materials.Find(AnnotationMaterialName) ?? model.Materials.Add(AnnotationMaterialName, new Rgb(255, 0, 0), 1);
        foreach (var d in dwellings.Where(d => d.Openings.Count > 0))
        {
            var group = model.Entities.MakeGroup(Array.Empty<Entity>(), $"{QaGroupPrefix}_{d.Name}");
            group.Material = red;
            group.Tag = null;
            foreach (var o in d.Openings)
            {
                var pts = o.Face.Vertices.Select(v => v.Position).ToList();
                var min = pts.Aggregate((a, b) => new Vector3d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
                var max = pts.Aggregate((a, b) => new Vector3d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
                var anchor = new Vector3d((min.X + max.X) / 2, (min.Y + max.Y) / 2, max.Z + 0.20 / InchToM);
                var orientation = string.IsNullOrWhiteSpace(o.Orientation) ? "N/A" : o.Orientation.Trim();
                var note = group.Entities.AddNote(anchor, Vector3d.Zero, $"{o.Id}\nOrientation: {orientation}", leader: false);
                note.Tag = null;
            }
        }
    }
}
