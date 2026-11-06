namespace Sketch.Model;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return new Rgb(Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>A tag (formerly "layer"): controls visibility and, in Color-by-Tag mode, display colour.</summary>
public sealed class Tag
{
    internal Tag(string name, Rgb color, bool visible)
    {
        Name = name;
        Color = color;
        Visible = visible;
    }

    public string Name { get; internal set; }
    public Rgb Color { get; set; }
    public bool Visible { get; set; }

    public override string ToString() => Name;
}

/// <summary>Ordered tag list. "Untagged" is represented by a null tag on entities and is always visible.</summary>
public sealed class TagCollection
{
    public const string UntaggedName = "Untagged";
    public static readonly Rgb UntaggedColor = new(255, 255, 255);

    private readonly List<Tag> _tags = new();

    public IReadOnlyList<Tag> Items => _tags;

    /// <summary>Raised when tags are added, removed, renamed, recoloured or shown/hidden.</summary>
    public event EventHandler? Changed;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public Tag? Find(string? name) =>
        string.IsNullOrEmpty(name) || name == UntaggedName ? null : _tags.FirstOrDefault(t => t.Name == name);

    public Tag Add(string name, Rgb color, bool visible = true)
    {
        if (name == UntaggedName) throw new ArgumentException("'Untagged' is reserved.", nameof(name));
        var existing = Find(name);
        if (existing is not null) return existing;
        var tag = new Tag(name, color, visible);
        _tags.Add(tag);
        RaiseChanged();
        return tag;
    }

    public bool Rename(Tag tag, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName == UntaggedName || _tags.Any(t => t != tag && t.Name == newName)) return false;
        tag.Name = newName;
        RaiseChanged();
        return true;
    }

    /// <summary>Removes a tag; entities that used it become untagged (done by the caller via <see cref="Entities.ReplaceTag"/>).</summary>
    public void Remove(Tag tag)
    {
        if (_tags.Remove(tag)) RaiseChanged();
    }

    public void SetVisible(Tag tag, bool visible)
    {
        if (tag.Visible == visible) return;
        tag.Visible = visible;
        RaiseChanged();
    }

    public void Clear()
    {
        _tags.Clear();
        RaiseChanged();
    }

    /// <summary>Snapshot of the tags (for undo).</summary>
    public IReadOnlyList<(string Name, Rgb Color, bool Visible)> Capture() => _tags.Select(t => (t.Name, t.Color, t.Visible)).ToList();

    /// <summary>
    /// Brings the list back to a snapshot: tags are matched by name and keep their identity; a tag that was removed
    /// comes back with its colour and visibility, an existing one gets its colour back but keeps its current
    /// visibility (showing and hiding tags is not an edit).
    /// </summary>
    public void Restore(IReadOnlyList<(string Name, Rgb Color, bool Visible)> snapshot)
    {
        var next = new List<Tag>(snapshot.Count);
        foreach (var (name, color, visible) in snapshot)
        {
            var tag = Find(name);
            if (tag is null) tag = new Tag(name, color, visible);
            else tag.Color = color;
            next.Add(tag);
        }
        _tags.Clear();
        _tags.AddRange(next);
        RaiseChanged();
    }

    public static bool IsVisible(Tag? tag) => tag is null || tag.Visible;
}

/// <summary>A paint material: a colour with optional transparency.</summary>
public sealed class Material
{
    internal Material(string name, Rgb color, double alpha)
    {
        Name = name;
        Color = color;
        Alpha = alpha;
    }

    public string Name { get; internal set; }
    public Rgb Color { get; set; }

    /// <summary>Opacity from 0 (invisible) to 1 (opaque).</summary>
    public double Alpha { get; set; }

    public override string ToString() => Name;
}

public sealed class MaterialCollection
{
    private readonly List<Material> _materials = new();

    public IReadOnlyList<Material> Items => _materials;

    public event EventHandler? Changed;

    public Material? Find(string? name) => string.IsNullOrEmpty(name) ? null : _materials.FirstOrDefault(m => m.Name == name);

    public Material Add(string name, Rgb color, double alpha = 1)
    {
        var existing = Find(name);
        if (existing is not null) return existing;
        var m = new Material(name, color, Math.Clamp(alpha, 0, 1));
        _materials.Add(m);
        Changed?.Invoke(this, EventArgs.Empty);
        return m;
    }

    public void Remove(Material m)
    {
        if (_materials.Remove(m)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _materials.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Snapshot of the materials (for undo).</summary>
    public IReadOnlyList<(string Name, Rgb Color, double Alpha)> Capture() => _materials.Select(m => (m.Name, m.Color, m.Alpha)).ToList();

    /// <summary>Brings the list back to a snapshot; materials are matched by name and keep their identity.</summary>
    public void Restore(IReadOnlyList<(string Name, Rgb Color, double Alpha)> snapshot)
    {
        var next = new List<Material>(snapshot.Count);
        foreach (var (name, color, alpha) in snapshot)
        {
            var m = Find(name);
            if (m is null) m = new Material(name, color, alpha);
            else
            {
                m.Color = color;
                m.Alpha = alpha;
            }
            next.Add(m);
        }
        _materials.Clear();
        _materials.AddRange(next);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Shared services for all entity containers of one model (ids, tags, materials).</summary>
public sealed class ModelContext
{
    private int _nextId = 1;

    public TagCollection Tags { get; } = new();
    public MaterialCollection Materials { get; } = new();
    public ImageStore Images { get; } = new();

    /// <summary>
    /// The container being edited while the rest of the model is hidden (View &gt; Component Edit &gt; Hide Rest of
    /// Model), else null. Supplied by the model; only that container's content and the groups leading to it show.
    /// </summary>
    public Func<Entities?> Isolation { get; set; } = () => null;

    public int NextId() => _nextId++;
    public int LastId => _nextId - 1;

    internal int PeekNext => _nextId;

    internal void ResetIds(int next) => _nextId = Math.Max(next, 1);

    internal void EnsureAbove(int id)
    {
        if (id >= _nextId) _nextId = id + 1;
    }
}
