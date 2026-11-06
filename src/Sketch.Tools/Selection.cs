using Sketch.Model;

namespace Sketch.Tools;

/// <summary>The current selection set.</summary>
public sealed class Selection
{
    private readonly HashSet<Entity> _items = new();

    public IReadOnlySet<Entity> Items => _items;
    public int Count => _items.Count;
    public bool IsEmpty => _items.Count == 0;

    public event EventHandler? Changed;

    public bool Contains(Entity e) => _items.Contains(e);

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Set(IEnumerable<Entity> items)
    {
        _items.Clear();
        foreach (var i in items) _items.Add(i);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Add(IEnumerable<Entity> items)
    {
        var changed = false;
        foreach (var i in items) changed |= _items.Add(i);
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(IEnumerable<Entity> items)
    {
        var changed = false;
        foreach (var i in items) changed |= _items.Remove(i);
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(IEnumerable<Entity> items)
    {
        foreach (var i in items)
            if (!_items.Remove(i)) _items.Add(i);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops entities that no longer exist (after undo, erase, etc.).</summary>
    public void Prune()
    {
        if (_items.RemoveWhere(e => e.IsDeleted) > 0) Changed?.Invoke(this, EventArgs.Empty);
    }
}
