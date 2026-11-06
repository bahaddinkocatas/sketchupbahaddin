using Sketch.Geometry;

namespace Sketch.Model;

/// <summary>Immutable snapshot of an <see cref="Entities"/> collection (used for undo/redo, copying and saving).</summary>
public sealed record EntitiesState(
    IReadOnlyList<EntitiesState.VertexData> Vertices,
    IReadOnlyList<EntitiesState.EdgeData> Edges,
    IReadOnlyList<EntitiesState.FaceData> Faces,
    int NextId,
    IReadOnlyList<EntitiesState.GroupData>? Groups = null,
    IReadOnlyList<EntitiesState.GuideData>? Guides = null,
    IReadOnlyList<EntitiesState.DimensionData>? Dimensions = null,
    IReadOnlyList<EntitiesState.NoteData>? Notes = null,
    IReadOnlyList<EntitiesState.ImageData>? Images = null,
    IReadOnlyList<EntitiesState.SectionData>? Sections = null)
{
    public sealed record SectionData(int Id, Vector3d Point, Vector3d Normal, double Size, string Name, bool Active, string? Tag, bool Hidden);

    public sealed record ImageData(int Id, string Key, Vector3d Origin, Vector3d U, Vector3d V, string Name, string? Tag, bool Hidden);

    public sealed record DimensionData(int Id, Vector3d Start, Vector3d End, Vector3d Offset, string? Tag, bool Hidden);

    public sealed record NoteData(int Id, Vector3d Anchor, Vector3d LabelOffset, string Text, bool HasLeader, string? Tag, bool Hidden);

    public sealed record GuideData(int Id, Vector3d Point, Vector3d Direction, string? Tag, bool Hidden);

    public sealed record VertexData(int Id, Vector3d Position);

    public sealed record EdgeData(int Id, int Start, int End, bool Soft, bool Smooth, bool Hidden, string? Tag = null);

    public sealed record FaceData(int Id, IReadOnlyList<int> Outer, IReadOnlyList<IReadOnlyList<int>> Inner, Vector3d Normal,
        string? Tag = null, string? Material = null, bool Hidden = false);

    public sealed record GroupData(int Id, string Name, string? Tag, string? Material, bool Hidden, EntitiesState Content)
    {
        /// <summary>The group this was captured from and its content version then (not saved to files).</summary>
        internal WeakReference<Group>? Source { get; init; }

        internal long SourceVersion { get; init; }
    }

    public static readonly EntitiesState Empty = new(
        Array.Empty<VertexData>(), Array.Empty<EdgeData>(), Array.Empty<FaceData>(), 1, Array.Empty<GroupData>());
}

public sealed partial class Entities
{
    public EntitiesState CaptureState() => new(
        _vertices.Select(v => new EntitiesState.VertexData(v.Id, v.Position)).ToList(),
        _edges.Select(e => new EntitiesState.EdgeData(e.Id, e.Start.Id, e.End.Id, e.Soft, e.Smooth, e.Hidden, e.Tag?.Name)).ToList(),
        _faces.Select(f => new EntitiesState.FaceData(
            f.Id,
            f.OuterLoop.List.Select(v => v.Id).ToList(),
            f.InnerList.Select(l => (IReadOnlyList<int>)l.List.Select(v => v.Id).ToList()).ToList(),
            f.Normal, f.Tag?.Name, f.Material?.Name, f.Hidden)).ToList(),
        Context.PeekNext,
        _groups.Select(CaptureGroup).ToList(),
        _guides.Select(g => new EntitiesState.GuideData(g.Id, g.Point, g.Direction, g.Tag?.Name, g.Hidden)).ToList(),
        CaptureDimensions().ToList(),
        CaptureNotes().ToList(),
        CaptureImages().ToList(),
        CaptureSections().ToList());

    public static EntitiesState.GroupData CaptureGroup(Group g) =>
        new(g.Id, g.Name, g.Tag?.Name, g.Material?.Name, g.Hidden, g.Entities.CaptureState())
        {
            Source = new WeakReference<Group>(g), SourceVersion = g.Entities.Version,
        };

    /// <summary>Replaces the whole content with a snapshot (ids are kept, so tools can find entities again).</summary>
    /// <param name="keepUnchangedGroups">
    /// For tool previews, which restore the container they edit on every mouse move: groups whose content has not
    /// changed since the snapshot are kept as they are instead of being rebuilt (rebuilding 100+ untouched groups,
    /// and everything cached per group, made dragging in large models stutter). Not for undo/redo: some edits (tag
    /// renames, names of nested items, section activation) do not mark the groups they affect as changed.
    /// </param>
    public void RestoreState(EntitiesState state, bool keepUnchangedGroups = false)
    {
        var unchanged = new HashSet<Group>();
        if (keepUnchangedGroups)
            foreach (var gd in state.Groups ?? Array.Empty<EntitiesState.GroupData>())
                if (Unchanged(gd) is { } g) unchanged.Add(g);
        ClearSilently(unchanged);
        Load(state, keepIds: true, offset: Vector3d.Zero, unchanged);
        if (Owner is null) Context.ResetIds(Math.Max(state.NextId, Context.PeekNext));
        RaiseChanged();
    }

    /// <summary>The group a snapshot entry came from, when it is still here and its content is exactly as captured.</summary>
    private Group? Unchanged(EntitiesState.GroupData gd) =>
        gd.Source is { } source && source.TryGetTarget(out var g) && !g.IsDeleted && ReferenceEquals(g.Parent, this)
        && g.Id == gd.Id && g.Entities.Version == gd.SourceVersion
            ? g
            : null;

    private void ClearSilently(IReadOnlySet<Group>? keep = null)
    {
        foreach (var v in _vertices) v.IsDeleted = true;
        foreach (var e in _edges) e.IsDeleted = true;
        foreach (var f in _faces) f.IsDeleted = true;
        foreach (var g in _groups)
            if (keep is null || !keep.Contains(g)) MarkDeleted(g);
        foreach (var g in _guides) g.IsDeleted = true;
        _guides.Clear();
        ClearAnnotations();
        ClearImages();
        _vertices.Clear();
        _edges.Clear();
        _faces.Clear();
        _groups.Clear();
        _edgeMap.Clear();
    }

    private static void MarkDeleted(Group g)
    {
        g.IsDeleted = true;
        g.Entities.ClearSilently();
    }

    /// <summary>
    /// Adds the content of a snapshot to this container, optionally translated. With <paramref name="keepIds"/>
    /// false every entity gets a fresh id (used for copies). Returns the top-level entities that were added.
    /// </summary>
    /// <param name="reuse">Groups (from <see cref="RestoreState"/>) to put back as they are instead of rebuilding them.</param>
    internal List<Entity> Load(EntitiesState state, bool keepIds, Vector3d offset, IReadOnlySet<Group>? reuse = null)
    {
        var added = new List<Entity>();
        int Id(int stored)
        {
            if (!keepIds) return Context.NextId();
            Context.EnsureAbove(stored);
            return stored;
        }

        var vertices = new Dictionary<int, Vertex>();
        foreach (var vd in state.Vertices)
        {
            var v = new Vertex { Id = Id(vd.Id), Position = vd.Position + offset, Parent = this };
            vertices[vd.Id] = v;
            _vertices.Add(v);
        }
        foreach (var ed in state.Edges)
        {
            var a = vertices[ed.Start];
            var b = vertices[ed.End];
            var e = new Edge
            {
                Id = Id(ed.Id), Start = a, End = b, Soft = ed.Soft, Smooth = ed.Smooth, Hidden = ed.Hidden,
                Tag = Context.Tags.Find(ed.Tag), Parent = this,
            };
            _edges.Add(e);
            _edgeMap[Key(a, b)] = e;
            a.EdgeList.Add(e);
            b.EdgeList.Add(e);
            added.Add(e);
        }
        foreach (var fd in state.Faces)
        {
            var face = CreateFace(
                fd.Outer.Select(id => vertices[id]).ToList(),
                fd.Inner.Select(l => (IReadOnlyList<Vertex>)l.Select(id => vertices[id]).ToList()),
                fd.Normal);
            face.Id = keepIds ? fd.Id : face.Id;
            if (keepIds) Context.EnsureAbove(fd.Id);
            face.Tag = Context.Tags.Find(fd.Tag);
            face.Material = Context.Materials.Find(fd.Material);
            face.Hidden = fd.Hidden;
            added.Add(face);
        }
        foreach (var gd in state.Groups ?? Array.Empty<EntitiesState.GroupData>())
        {
            if (reuse is not null && gd.Source is { } source && source.TryGetTarget(out var kept) && reuse.Contains(kept))
            {
                _groups.Add(kept);
                Context.EnsureAbove(gd.Id);
                kept.Name = gd.Name;
                kept.Tag = Context.Tags.Find(gd.Tag);
                kept.Material = Context.Materials.Find(gd.Material);
                kept.Hidden = gd.Hidden;
                added.Add(kept);
                continue;
            }
            added.Add(LoadGroup(gd, keepIds, offset));
        }
        foreach (var gd in state.Guides ?? Array.Empty<EntitiesState.GuideData>())
        {
            var guide = new Guide
            {
                Id = Id(gd.Id), Point = gd.Point + offset, Direction = gd.Direction, Parent = this,
                Tag = Context.Tags.Find(gd.Tag), Hidden = gd.Hidden,
            };
            _guides.Add(guide);
            added.Add(guide);
        }
        LoadAnnotations(state, Id, offset, added);
        LoadImages(state, Id, offset, added);
        LoadSections(state, Id, offset, added);
        return added;
    }

    internal Group LoadGroup(EntitiesState.GroupData gd, bool keepIds, Vector3d offset)
    {
        var group = NewGroupObject();
        if (keepIds)
        {
            group.Id = gd.Id;
            Context.EnsureAbove(gd.Id);
        }
        group.Name = gd.Name;
        group.Tag = Context.Tags.Find(gd.Tag);
        group.Material = Context.Materials.Find(gd.Material);
        group.Hidden = gd.Hidden;
        group.Entities.Load(gd.Content, keepIds, offset);
        return group;
    }

    /// <summary>Adds a snapshot's content as new entities (fresh ids), without sticky merging (used by importers).</summary>
    public List<Entity> Import(EntitiesState state)
    {
        var added = Load(state, keepIds: false, offset: Vector3d.Zero);
        RaiseChanged();
        return added;
    }

    public void Clear() => RestoreState(EntitiesState.Empty);
}
