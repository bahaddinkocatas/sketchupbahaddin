using Sketch.Geometry;
using Sketch.Model;
using Sketch.Model.Templates;
using Sketch.Operations.Deap;

namespace Sketch.Operations.Tests;

/// <summary>DEAP design-stage evidence on a small dwelling whose every value can be worked out by hand.</summary>
public class DeapEvidenceTests
{
    private const double M = 1 / DeapEvidence.InchToM; // one metre in model units (inches)

    private static Vector3d P(double x, double y, double z) => new(x * M, y * M, z * M);

    private static Face AddRect(Entities ents, Vector3d a, Vector3d b, Vector3d c, Vector3d d)
    {
        var before = ents.LastId;
        ents.AddPolyline(new[] { a, b, c, d }, true);
        var center = (a + b + c + d) / 4;
        return ents.Faces.Where(f => f.Id > before || f.ContainsPoint(center)).Single(f => f.ContainsPoint(center));
    }

    /// <summary>
    /// "Unit 1": 5 x 4 m exposed floor at z = 0, a 2 x 3 m living-area floor next to it, 5 x 2.5 m external wall
    /// on y = 0 with a 1 x 1 m window, a 1 x 2 m Door on x = 5, a ceiling at z = 2.5 and a 1 x 1 m
    /// rooflight; an internal wall in a sub-group tagged "120 Internal Wall 1" (its face untagged).
    /// </summary>
    private static (SketchModel Model, Group Dwelling) Build(Vector3d? north = null)
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        var ents = model.Entities;
        // Start without the template's north arrow (north = green); tests that need one add it.
        ents.Erase(ents.Groups.Where(g => g.Name == ThermalTemplate.NorthArrowName).Cast<Entity>().ToList());
        Tag T(string name) => model.Tags.Find(name)!;

        var before = ents.LastId;
        AddRect(ents, P(0, 0, 0), P(5, 0, 0), P(5, 4, 0), P(0, 4, 0)).Tag = T("001 Exposed Floor 1");
        AddRect(ents, P(6, 0, 0), P(8, 0, 0), P(8, 3, 0), P(6, 3, 0)).Tag = T("005 Exposed Floor Living Area");
        var wall = AddRect(ents, P(0, 0, 0), P(5, 0, 0), P(5, 0, 2.5), P(0, 0, 2.5));
        wall.Tag = T("100 External Wall 1");
        var window = AddRect(ents, P(1, 0, 1), P(2, 0, 1), P(2, 0, 2), P(1, 0, 2));
        window.Material = model.Materials.Find("Glass");
        window.Tag = null;
        AddRect(ents, P(5, 1, 0), P(5, 2, 0), P(5, 2, 2), P(5, 1, 2)).Material = model.Materials.Find("Door");
        var ceiling = AddRect(ents, P(0, 0, 2.5), P(5, 0, 2.5), P(5, 4, 2.5), P(0, 4, 2.5));
        ceiling.Tag = T("240 Non-heat Loss Ceiling 1");
        AddRect(ents, P(10, 0, 2.5), P(11, 0, 2.5), P(11, 1, 2.5), P(10, 1, 2.5)).Material = model.Materials.Find("Glass");

        var internalItems = new List<Entity>();
        var b2 = ents.LastId;
        AddRect(ents, P(20, 0, 0), P(22, 0, 0), P(22, 0, 2.5), P(20, 0, 2.5));
        internalItems.AddRange(ents.Faces.Where(f => f.Id > b2));
        internalItems.AddRange(ents.Edges.Where(e => e.Id > b2));
        var sub = ents.MakeGroup(internalItems, "internal");
        sub.Tag = T("120 Internal Wall 1");

        var items = ents.Faces.Cast<Entity>().Concat(ents.Edges).Concat(ents.Groups).ToList();
        var dwelling = ents.MakeGroup(items, "Unit 1");

        if (north is { } n)
        {
            var b3 = ents.LastId;
            ents.AddEdge(P(30, 0, 0), P(30, 0, 0) + n);
            var arrow = ents.Edges.Where(e => e.Id > b3).Cast<Entity>().ToList();
            ents.MakeGroup(arrow, "NorthArrow");
        }
        return (model, dwelling);
    }

    [Fact]
    public void Tag_codes_come_from_the_leading_number()
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        Assert.Equal(100, DeapEvidence.TagCode(model.Tags.Find("100 External Wall 1")));
        Assert.Equal(5, DeapEvidence.TagCode(model.Tags.Find("005 Exposed Floor Living Area")));
        Assert.Null(DeapEvidence.TagCode(model.Tags.Add("Walls", new Rgb(1, 2, 3))));
        Assert.Null(DeapEvidence.TagCode(null));
    }

    [Fact]
    public void Areas_height_and_openings_match_hand_calculation()
    {
        var (model, dwelling) = Build();
        var found = DeapEvidence.FindDwellings(model);
        Assert.Same(dwelling, Assert.Single(found));

        var d = DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1);
        Assert.Equal("Unit_1", d.Name);
        Assert.Equal(26.0, d.Elements["exposed_floor_area"], 6);            // 20 + 6 (living floor counts too)
        Assert.Equal(6.0, d.Elements["living_area"], 6);
        Assert.Equal(26.0, d.TotalFloorArea, 6);
        Assert.Equal(12.5 - 1.0, d.Elements["external_wall_area"], 6);     // net of the window hole
        Assert.Equal(5.0, d.Elements["internal_wall_area"], 6);            // tag inherited from its group
        Assert.Equal(20.0, d.Elements["ceiling_area"], 6);
        Assert.Equal(0.0, d.Elements["roof_area"], 6);
        Assert.Equal(2.5, d.HeightM, 6);

        Assert.Equal(3, d.Openings.Count);
        var w = d.Openings.Single(o => o.Type == "Window");
        Assert.Equal(1.0, w.AreaSqm, 6);
        Assert.Equal("S", w.Orientation);                                   // faces -Y, north = +Y by default
        var door = d.Openings.Single(o => o.Type == "Door");
        Assert.Equal(2.0, door.AreaSqm, 6);
        Assert.Equal(string.Empty, door.Orientation);
        Assert.Equal(1.0, d.Openings.Single(o => o.Type == "Rooflight").AreaSqm, 6);
        Assert.Equal(new[] { 1, 2, 3 }, d.Openings.Select(o => int.Parse(o.Id[1..])).OrderBy(i => i));
    }

    [Fact]
    public void Orientation_follows_the_north_arrow()
    {
        var (model, dwelling) = Build(north: P(1, 0, 0)); // north points along red
        var d = DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1);
        Assert.Single(DeapEvidence.FindDwellings(model)); // the arrow is not a dwelling
        Assert.Equal("E", d.Openings.Single(o => o.Type == "Window").Orientation);
    }

    [Theory]
    [InlineData(0, 1, "N")]
    [InlineData(1, 1, "NE")]
    [InlineData(1, 0, "E")]
    [InlineData(1, -1, "SE")]
    [InlineData(0, -1, "S")]
    [InlineData(-1, -1, "SW")]
    [InlineData(-1, 0, "W")]
    [InlineData(-1, 1, "NW")]
    public void Eight_compass_directions(double x, double y, string expected) =>
        Assert.Equal(expected, DeapEvidence.CardinalOrientation(new Vector3d(x, y, 0.3), Vector3d.UnitY));

    [Fact]
    public void Csv_has_the_extension_s_columns_and_point_decimals()
    {
        var (model, dwelling) = Build();
        var d = DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1);
        var prev = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var lines = DeapEvidence.ToCsv(new[] { d }).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Dwelling Name,Total Floor Area,Exposed Floor Area,Semi-Exposed Floor Area,Non-Heat Loss Floor Area,Living Area,"
                         + "Dwelling Height,External Wall Area,Semi-Exposed Wall Area,Internal Wall Area,Ceiling Area,Roof Area,"
                         + "Opening ID,Opening Type,Opening Orientation,Opening Area", lines[0]);
            Assert.Equal(4, lines.Length); // header + one row per opening
            var window = lines.Single(l => l.Contains(",Window,"));
            Assert.StartsWith("Unit_1,26.00,26.00,0.00,0.00,6.00,2.50,11.50,0.00,5.00,20.00,0.00,W", window);
            Assert.EndsWith(",Window,S,1.00", window);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Qa_labels_are_replaced_not_duplicated()
    {
        var (model, dwelling) = Build();
        var results = new[] { DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1) };
        DeapEvidence.AddQaOpeningLabels(model, results);
        DeapEvidence.AddQaOpeningLabels(model, results);
        var qa = Assert.Single(model.Entities.Groups, g => g.Name.StartsWith(DeapEvidence.QaGroupPrefix));
        Assert.Equal(3, qa.Entities.Notes.Count);
        Assert.Contains(qa.Entities.Notes, n => n.Text.StartsWith("W") && n.Text.EndsWith("Orientation: S"));
        Assert.Equal(new Rgb(255, 0, 0), qa.Material!.Color);
        Assert.Single(DeapEvidence.FindDwellings(model)); // the label group is not a dwelling
    }

    [Fact]
    public void Door_and_wood_materials_are_doors_and_glass_ones_windows()
    {
        var (model, dwelling) = Build();
        var wood = model.Materials.Add("Wood_Floor_Oak", new Rgb(150, 100, 50));
        var window = dwelling.Entities.AllFaces().Single(f => f.Material?.Name == "Glass" && Math.Abs(f.Normal.Z) < 0.1);
        window.Material = wood;
        var d = DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1);
        Assert.Equal(2, d.Openings.Count(o => o.Type == "Door"));          // the wood face and the Door
        Assert.All(d.Openings.Where(o => o.Type == "Door"), o => Assert.Equal(string.Empty, o.Orientation));

        window.Material = model.Materials.Add("Glass_Basic_01", new Rgb(150, 180, 220), 0.5);
        Assert.Single(DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1).Openings, o => o.Type == "Door");
    }

    [Theory]
    [InlineData("Glass", "Glass")]
    [InlineData("[Translucent Glass Blue]", "Glass")]
    [InlineData("Glass_Basic_01", "Glass")]
    [InlineData("[Wood Cherry Original]", "Door")]
    [InlineData("NonSlidingDoor", "Door")]
    [InlineData("Door", "Door")]
    [InlineData("96,96,96", null)]
    public void Opening_materials_are_recognised_by_name(string name, string? expected) =>
        Assert.Equal(expected, ThermalTemplate.OpeningMaterial(name));

    [Fact]
    public void New_models_have_a_north_arrow_from_the_origin_close_to_minus_red()
    {
        var model = new SketchModel();
        ThermalTemplate.Apply(model);
        var arrow = Assert.Single(model.Entities.Groups, g => g.Name == ThermalTemplate.NorthArrowName);
        var north = DeapEvidence.NorthVector(model).Normalized();
        Assert.Equal(-Math.Cos(Math.PI / 12), north.X, 9); // 15° from -red towards +green
        Assert.Equal(Math.Sin(Math.PI / 12), north.Y, 9);
        Assert.Contains(arrow.Entities.Edges, e => e.Start.Position.IsEqual(Vector3d.Zero)); // the tail is at the origin
        Assert.False(DeapEvidence.IsDwelling(arrow));

        // A window facing -Y (towards the viewer in the default view) is then west.
        Assert.Equal("W", DeapEvidence.CardinalOrientation(new Vector3d(0, -1, 0), north));
    }

    [Fact]
    public void Rotating_the_north_arrow_turns_the_orientations()
    {
        var (model, dwelling) = Build();
        var arrow = ThermalTemplate.AddNorthArrow(model.Entities);
        Assert.Single(model.Entities.Groups, g => g.Name == ThermalTemplate.NorthArrowName);
        string WindowOrientation() => DeapEvidence.Analyse(dwelling, DeapEvidence.NorthVector(model), 1).Openings.Single(o => o.Type == "Window").Orientation;

        Assert.Equal("W", WindowOrientation());
        // Rotate the arrow group 90° anticlockwise (seen from above) about its tail: north now points (nearly) at -green.
        MoveCopy.Transform(model.Entities, new Entity[] { arrow }, Matrix4d.Rotation(Vector3d.UnitZ, Math.PI / 2));
        var north = DeapEvidence.NorthVector(model).Normalized();
        Assert.Equal(-Math.Sin(Math.PI / 12), north.X, 9);
        Assert.Equal(-Math.Cos(Math.PI / 12), north.Y, 9);
        Assert.Equal("N", WindowOrientation());
        // Another 90°: north points (nearly) at +red.
        MoveCopy.Transform(model.Entities, new Entity[] { arrow }, Matrix4d.Rotation(Vector3d.UnitZ, Math.PI / 2));
        Assert.Equal("E", WindowOrientation());
    }
}
