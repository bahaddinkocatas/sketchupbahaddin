using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.IO;

/// <summary>Camera settings stored with a document.</summary>
public sealed record CameraData(Vector3d Eye, Vector3d Target, Vector3d Up, bool Perspective, double FovY, double OrthoHeight);

public sealed record TagData(string Name, string Color, bool Visible);

public sealed record MaterialData(string Name, string Color, double Alpha);

public sealed record SceneData(string Name, CameraData? Camera, Dictionary<string, bool> TagVisibility, RenderStyle? Style,
    bool SavesCamera = true, bool SavesTags = true, bool SavesStyle = true);

/// <summary>Everything stored in a .sketch file. Fields after Unit were added in format version 2.</summary>
public sealed record SketchDocument(EntitiesState Entities, CameraData? Camera, LengthUnit Unit,
    IReadOnlyList<TagData>? Tags = null, IReadOnlyList<MaterialData>? Materials = null, IReadOnlyList<SceneData>? Scenes = null,
    RenderStyle? Style = null, string? ActiveTag = null, Dictionary<string, string>? ImageData = null, ModelAxes? Axes = null)
{
    /// <summary>Captures a whole model (geometry, tags, materials, scenes, style) plus the view camera.</summary>
    public static SketchDocument From(SketchModel model, Camera camera) => new(
        model.Entities.CaptureState(),
        SketchFile.Capture(camera),
        Units.Default.Unit,
        model.Tags.Items.Select(t => new TagData(t.Name, t.Color.ToHex(), t.Visible)).ToList(),
        model.Materials.Items.Select(m => new MaterialData(m.Name, m.Color.ToHex(), m.Alpha)).ToList(),
        model.Scenes.Items.Select(s => new SceneData(s.Name, s.Camera is null ? null : SketchFile.Capture(s.Camera),
            s.TagVisibility, s.Style, s.SavesCamera, s.SavesTags, s.SavesStyle)).ToList(),
        model.Style,
        model.ActiveTag?.Name,
        // Only images that are still used are saved (base64).
        model.Entities.AllImages().Select(i => i.ImageKey).Distinct()
            .Where(k => model.Context.Images.Get(k) is not null)
            .ToDictionary(k => k, k => Convert.ToBase64String(model.Context.Images.Get(k)!)),
        model.Axes.IsWorld ? null : model.Axes);

    /// <summary>Loads this document into a model (replacing its content). Tags/materials must exist before geometry.</summary>
    public void ApplyTo(SketchModel model, Camera camera)
    {
        model.Tags.Clear();
        foreach (var t in Tags ?? Array.Empty<TagData>()) model.Tags.Add(t.Name, Rgb.FromHex(t.Color), t.Visible);
        model.Materials.Clear();
        foreach (var m in Materials ?? Array.Empty<MaterialData>()) model.Materials.Add(m.Name, Rgb.FromHex(m.Color), m.Alpha);
        model.Scenes.Clear();
        foreach (var s in Scenes ?? Array.Empty<SceneData>())
        {
            Camera? cam = null;
            if (s.Camera is { } cd)
            {
                cam = new Camera();
                SketchFile.Apply(cd, cam);
            }
            model.Scenes.Add(new Scene
            {
                Name = s.Name, Camera = cam, TagVisibility = s.TagVisibility ?? new(), Style = s.Style,
                SavesCamera = s.SavesCamera, SavesTags = s.SavesTags, SavesStyle = s.SavesStyle,
            });
        }
        model.Style = Style ?? new RenderStyle();
        model.Context.Images.Clear();
        foreach (var (key, b64) in ImageData ?? new Dictionary<string, string>()) model.Context.Images.Set(key, Convert.FromBase64String(b64));
        model.ActiveTag = model.Tags.Find(ActiveTag);
        model.Axes = Axes ?? ModelAxes.World;
        model.Entities.RestoreState(Entities);
        Units.Default.Unit = Unit;
        if (Camera is { } c) SketchFile.Apply(c, camera);
    }
}

/// <summary>
/// Reads and writes the .sketch format: UTF-8 JSON (gzip-compressed), with a format name and version so later
/// versions can migrate old files.
/// </summary>
public static class SketchFile
{
    public const string Extension = ".sketch";
    public const string FormatName = "sketch-model";
    public const int CurrentVersion = 2;

    private sealed record Envelope(string Format, int Version, string App, DateTime SavedUtc, SketchDocument Document);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new Vector3dConverter(), new JsonStringEnumConverter() },
    };

    public static void Save(string path, SketchDocument document)
    {
        var envelope = new Envelope(FormatName, CurrentVersion, "Sketch", DateTime.UtcNow, document);
        var temp = path + ".tmp";
        using (var file = File.Create(temp))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        {
            JsonSerializer.Serialize(gzip, envelope, Options);
        }
        // Replace atomically so a crash while saving never destroys the previous file.
        File.Move(temp, path, overwrite: true);
    }

    public static SketchDocument Load(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        var envelope = JsonSerializer.Deserialize<Envelope>(gzip, Options)
                       ?? throw new InvalidDataException("The file is empty or cannot be read.");
        if (envelope.Format != FormatName) throw new InvalidDataException("This is not a Sketch model file.");
        if (envelope.Version > CurrentVersion)
            throw new InvalidDataException($"The file was saved with a newer version of Sketch (version {envelope.Version}).");
        return envelope.Document;
    }

    public static CameraData Capture(Camera c) => new(c.Eye, c.Target, c.Up, c.IsPerspective, c.FovYDegrees, c.OrthoHeight);

    public static void Apply(CameraData data, Camera c)
    {
        c.Set(data.Eye, data.Target, data.Up);
        c.IsPerspective = data.Perspective;
        c.FovYDegrees = data.FovY;
        c.OrthoHeight = data.OrthoHeight;
    }

    /// <summary>Writes a vector as a compact [x, y, z] array.</summary>
    private sealed class Vector3dConverter : JsonConverter<Vector3d>
    {
        public override Vector3d Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Vector expected.");
            var v = new double[3];
            for (var i = 0; i < 3; i++)
            {
                reader.Read();
                v[i] = reader.GetDouble();
            }
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Vector must have 3 values.");
            return new Vector3d(v[0], v[1], v[2]);
        }

        public override void Write(Utf8JsonWriter writer, Vector3d value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteNumberValue(value.Z);
            writer.WriteEndArray();
        }
    }
}
