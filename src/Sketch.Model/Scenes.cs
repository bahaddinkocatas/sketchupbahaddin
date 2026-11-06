namespace Sketch.Model;

public enum FaceStyle
{
    Wireframe,
    HiddenLine,
    Shaded,
    ShadedWithTextures,
    Monochrome,
}

/// <summary>How the model is drawn (a small subset of SketchUp's Styles).</summary>
public sealed record RenderStyle
{
    public FaceStyle FaceStyle { get; init; } = FaceStyle.ShadedWithTextures;
    public bool XRay { get; init; }
    public bool BackEdges { get; init; }

    /// <summary>Faces take their tag's colour instead of their material (Tags panel "Color by tag").</summary>
    public bool ColorByTag { get; init; }

    /// <summary>Background colour; null = the default (the user's SketchUp template beige).</summary>
    public Rgb? Background { get; init; }

    /// <summary>Edge colour; null = the default orange.</summary>
    public Rgb? EdgeColor { get; init; }

    /// <summary>Section planes are drawn (View &gt; Section Planes).</summary>
    public bool HideSectionPlanes { get; init; }

    /// <summary>The active section plane cuts the model (View &gt; Section Cuts).</summary>
    public bool HideSectionCuts { get; init; }
}

/// <summary>A saved view: camera, tag visibility and style.</summary>
public sealed class Scene
{
    public required string Name { get; set; }
    public Camera? Camera { get; set; }

    /// <summary>Visibility per tag name; tags not listed keep their current visibility.</summary>
    public Dictionary<string, bool> TagVisibility { get; set; } = new();

    public RenderStyle? Style { get; set; }

    /// <summary>Which properties the scene stores (SketchUp's "Properties to save").</summary>
    public bool SavesCamera { get; set; } = true;
    public bool SavesTags { get; set; } = true;
    public bool SavesStyle { get; set; } = true;

    /// <summary>An independent copy (for undo snapshots).</summary>
    public Scene Clone() => new()
    {
        Name = Name, Camera = Camera?.Clone(), TagVisibility = new Dictionary<string, bool>(TagVisibility), Style = Style,
        SavesCamera = SavesCamera, SavesTags = SavesTags, SavesStyle = SavesStyle,
    };
}

public sealed class SceneCollection
{
    private readonly List<Scene> _scenes = new();

    public IReadOnlyList<Scene> Items => _scenes;

    public event EventHandler? Changed;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public Scene Add(Scene scene)
    {
        _scenes.Add(scene);
        RaiseChanged();
        return scene;
    }

    public void Remove(Scene scene)
    {
        if (_scenes.Remove(scene)) RaiseChanged();
    }

    public void Move(Scene scene, int newIndex)
    {
        if (!_scenes.Remove(scene)) return;
        _scenes.Insert(Math.Clamp(newIndex, 0, _scenes.Count), scene);
        RaiseChanged();
    }

    public void Clear()
    {
        _scenes.Clear();
        RaiseChanged();
    }

    /// <summary>Snapshot of the scenes (for undo).</summary>
    public IReadOnlyList<Scene> Capture() => _scenes.Select(s => s.Clone()).ToList();

    /// <summary>Brings the list back to a snapshot (copies of the snapshot's scenes, so the snapshot stays intact).</summary>
    public void Restore(IReadOnlyList<Scene> snapshot)
    {
        _scenes.Clear();
        _scenes.AddRange(snapshot.Select(s => s.Clone()));
        RaiseChanged();
    }

    /// <summary>A name like "Sahne 3" that is not taken yet.</summary>
    public string NextName()
    {
        for (var i = _scenes.Count + 1; ; i++)
        {
            var name = $"Scene {i}";
            if (_scenes.All(s => s.Name != name)) return name;
        }
    }
}
