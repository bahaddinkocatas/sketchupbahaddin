using Sketch.Geometry;

namespace Sketch.Model.Templates;

/// <summary>
/// Default template for thermal-element modelling: the user's tag list, the Glass (windows) and Door materials and
/// the scene tabs. Tag colours are grouped by element type (floors, walls, roofs/ceilings, spaces, imports).
/// </summary>
public static class ThermalTemplate
{
    public static readonly string[] TagNames =
    {
        "000 Dimension Check", "000 Import Plan", "000 Import Section",
        "001 Exposed Floor 1", "002 Exposed Floor 2", "003 Exposed Floor 3", "004 Exposed Floor 4", "005 Exposed Floor Living Area",
        "010 Semi-Exposed Floor 1", "011 Semi-Exposed Floor 2", "012 Semi-Exposed Floor 3", "013 Semi-Exposed Floor 4",
        "014 Semi-Exposed Floor Living Area",
        "020 Non-heat Loss Floor 1", "021 Non-heat Loss Floor 2", "022 Non-heat-loss Floor 3", "023 Non-heat Loss Floor 4",
        "024 Non-heat Loss Floor Living Area",
        "100 External Wall 1", "101 External Wall 2", "102 External Wall 3", "103 External Wall 4",
        "110 Semi-Exposed Wall 1", "111 Semi-Exposed Wall 2", "112 SEmi-Exposed Wall 3", "113 Semi-Exposed Wall 4",
        "120 Internal Wall 1", "121 Internal Wall 2", "122 Internal Wall 3", "123 Internal Wall 4",
        "200 Pitched Roof - flat ceiling 1", "201 Pitched Roof - flat ceiling 2", "202 Pitched Roof - flat ceiling 3",
        "203 Pitched Roof - flat ceiling 4",
        "210 Pitched Roof - sloped ceiling 1", "211 Pitched Roof - sloped ceiling 2", "212 Pitched Roof - sloped ceiling 3",
        "213 Pitched Roof - sloped ceiling 4",
        "220 Flat Roof Type 1", "221 Flat Roof Type 2", "222 Flat Roof Type 3", "223 Flat Roof Type 4",
        "230 Semi-Exposed Roof Type 1", "231 Semi-Exposed Roof Type 2", "232 Semi-Exposed Roof Type 3", "233 Semi-Exposed Roof Type 4",
        "240 Non-heat Loss Ceiling 1", "241 Non-heat Loss Ceiling 2", "242 Non-heat Loss Ceiling 3", "243 Non-heat Loss Ceiling 4",
        "998 Heated space", "998 Unheated space", "999 Air tightness envelope", "999 Null",
    };

    public const string GlassName = "Glass";
    public const string DoorName = "Door";

    /// <summary>
    /// The opening material a material name stands for: "Door" for doors (any name with "door" or "wood", as the
    /// DEAP extension's wood doors), "Glass" for windows (any name with "glass"), else null. Used to merge the many
    /// glass/wood materials of imported files into the two template ones, and by the DEAP evidence.
    /// </summary>
    public static string? OpeningMaterial(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("door") || n.Contains("wood")) return DoorName;
        return n.Contains("glass") ? GlassName : null;
    }

    /// <summary>The template's Glass or Door material, added with its template colour when missing.</summary>
    public static Material OpeningMaterial(SketchModel model, string name) =>
        model.Materials.Find(name) ?? (name == GlassName
            ? model.Materials.Add(GlassName, Rgb.FromHex("#8FB8DE"), 0.45)
            : model.Materials.Add(DoorName, Rgb.FromHex("#A0582C")));

    public enum Category
    {
        Import,
        Floor,
        Wall,
        Roof,
        Space,
    }

    /// <summary>Element category from the tag's name (number prefix, with wall/roof words for the 2xx overlaps).</summary>
    public static Category CategoryOf(string name)
    {
        if (name.StartsWith("000")) return Category.Import;
        if (name.StartsWith("998") || name.StartsWith("999")) return Category.Space;
        if (name.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return Category.Wall;
        if (name.Contains("Floor", StringComparison.OrdinalIgnoreCase)) return Category.Floor;
        return Category.Roof;
    }

    private static readonly Dictionary<Category, Rgb[]> Palettes = new()
    {
        [Category.Import] = new[] { Rgb.FromHex("#B0B0B0"), Rgb.FromHex("#9A9A9A"), Rgb.FromHex("#C4C4C4") },
        [Category.Floor] = new[]
        {
            Rgb.FromHex("#0F7F7A"), Rgb.FromHex("#14A39B"), Rgb.FromHex("#3CC2B5"), Rgb.FromHex("#7FD6CC"), Rgb.FromHex("#0B5E5A"),
            Rgb.FromHex("#2E8B57"), Rgb.FromHex("#3CB371"), Rgb.FromHex("#66CDAA"), Rgb.FromHex("#8FBC8F"),
        },
        [Category.Wall] = new[]
        {
            Rgb.FromHex("#EE7600"), Rgb.FromHex("#FF9933"), Rgb.FromHex("#FFB266"), Rgb.FromHex("#CC5500"),
            Rgb.FromHex("#E8505B"), Rgb.FromHex("#F27983"), Rgb.FromHex("#C9303C"), Rgb.FromHex("#F5A3AA"),
            Rgb.FromHex("#D4A017"), Rgb.FromHex("#E6C200"), Rgb.FromHex("#B8860B"), Rgb.FromHex("#F0D264"),
        },
        [Category.Roof] = new[]
        {
            Rgb.FromHex("#4F5A75"), Rgb.FromHex("#66708F"), Rgb.FromHex("#7D87A6"), Rgb.FromHex("#3A4257"),
            Rgb.FromHex("#6A4C93"), Rgb.FromHex("#8466AD"), Rgb.FromHex("#9E82C6"), Rgb.FromHex("#523A73"),
        },
        [Category.Space] = new[] { Rgb.FromHex("#F4E04D"), Rgb.FromHex("#BDB76B"), Rgb.FromHex("#FF69B4"), Rgb.FromHex("#808080") },
    };

    public static Rgb DefaultColor(string name, int indexInCategory)
    {
        var palette = Palettes[CategoryOf(name)];
        return palette[indexInCategory % palette.Length];
    }

    /// <summary>Fills an empty model with the template's tags, materials and scenes.</summary>
    public static void Apply(SketchModel model)
    {
        var counters = new Dictionary<Category, int>();
        foreach (var name in TagNames)
        {
            var cat = CategoryOf(name);
            counters.TryGetValue(cat, out var i);
            counters[cat] = i + 1;
            model.Tags.Add(name, DefaultColor(name, i));
        }

        OpeningMaterial(model, GlassName);
        OpeningMaterial(model, DoorName);

        bool IsThermal(string n) => CategoryOf(n) is Category.Floor or Category.Wall or Category.Roof;
        Scene Make(string name, Func<string, bool> visible, bool colorByTag) => new()
        {
            Name = name,
            SavesCamera = false,
            TagVisibility = TagNames.ToDictionary(n => n, visible),
            Style = new RenderStyle { ColorByTag = colorByTag },
        };

        model.Scenes.Add(Make("(Default)", _ => true, false));
        model.Scenes.Add(Make("(All On)", _ => true, false));
        model.Scenes.Add(Make("(All Off)", _ => false, false));
        model.Scenes.Add(Make("(Thermal Elements)", IsThermal, true));
        model.Scenes.Add(Make("(Floor TE)", n => CategoryOf(n) == Category.Floor, true));
        model.Scenes.Add(Make("(Wall TE)", n => CategoryOf(n) == Category.Wall, true));
        model.Scenes.Add(Make("(Roof TE)", n => CategoryOf(n) == Category.Roof, true));

        AddNorthArrow(model.Entities);
    }

    public const string NorthArrowName = "NorthArrow";

    /// <summary>Angle of the default north arrow from the -red axis towards +green.</summary>
    public const double NorthArrowDegrees = 15;

    /// <summary>
    /// The default north arrow: a group named "NorthArrow" starting at the origin and pointing between the -red and
    /// +green axes, close to -red (<see cref="NorthArrowDegrees"/>).
    /// Its shaft is the longest edge and runs from the tail to the tip, which is how the DEAP evidence reads north;
    /// rotating the group therefore turns north with it.
    /// </summary>
    public static Group AddNorthArrow(Entities entities)
    {
        const double metre = 1 / 0.0254; // model units are inches
        var angle = NorthArrowDegrees * Math.PI / 180;
        var dir = new Vector3d(-Math.Cos(angle), Math.Sin(angle), 0);
        var side = new Vector3d(dir.Y, -dir.X, 0);
        var tail = Vector3d.Zero;
        var tip = tail + dir * (2 * metre);
        var barb = 0.35 * metre;
        var before = entities.LastId;
        entities.AddEdge(tail, tip);
        entities.AddEdge(tip, tip - dir * barb + side * barb * 0.5);
        entities.AddEdge(tip, tip - dir * barb - side * barb * 0.5);
        var items = entities.Edges.Where(e => e.Id > before).Cast<Entity>().ToList();
        var group = entities.MakeGroup(items, NorthArrowName);
        group.Tag = null;
        var label = group.Entities.AddNote(tip + dir * (0.3 * metre), Vector3d.Zero, "N", leader: false);
        label.Tag = null;
        return group;
    }
}
