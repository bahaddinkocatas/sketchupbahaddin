using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    private Group NewGroupObject()
    {
        var content = new Entities(Context, null);
        var group = new Group(content) { Id = Context.NextId(), Parent = this, Tag = NewEntityTag() };
        content._owner = group;
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Turns the selected edges and faces (and groups) of this container into a new group. Edges still used by
    /// faces that stay outside are copied into the group and also kept outside.
    /// </summary>
    public Group MakeGroup(IEnumerable<Entity> items, string name = "")
    {
        var list = items.Where(i => !i.IsDeleted && ReferenceEquals(i.Parent, this)).ToList();
        var faces = list.OfType<Face>().ToHashSet();
        var edges = list.OfType<Edge>().Concat(faces.SelectMany(f => f.Edges)).ToHashSet();
        var groups = list.OfType<Group>().ToList();

        var group = NewGroupObject();
        group.Name = name;
        // The group itself starts untagged; its content keeps its own tags.
        group.Tag = null;

        var content = group.Entities;
        var map = new Dictionary<Vertex, Vertex>();
        Vertex Copy(Vertex v)
        {
            if (!map.TryGetValue(v, out var c))
            {
                c = content.NewVertex(v.Position);
                map[v] = c;
            }
            return c;
        }

        foreach (var e in edges)
        {
            var ce = content.Connect(Copy(e.Start), Copy(e.End), out _);
            ce.Soft = e.Soft;
            ce.Smooth = e.Smooth;
            ce.Hidden = e.Hidden;
            ce.Tag = e.Tag;
        }
        foreach (var f in faces)
        {
            var cf = content.CreateFace(f.OuterLoop.List.Select(Copy).ToList(),
                f.InnerList.Select(l => (IReadOnlyList<Vertex>)l.List.Select(Copy).ToList()), f.Normal);
            cf.Material = f.Material;
            cf.Tag = f.Tag;
            cf.Hidden = f.Hidden;
        }
        foreach (var g in groups)
        {
            _groups.Remove(g);
            content._groups.Add(g);
            g.Parent = content;
        }
        // Images, texts, dimensions, guides and section planes move into the group as they are.
        foreach (var item in list)
        {
            var moved = item switch
            {
                ImageEntity i when _images.Remove(i) => Add(content._images, i),
                Note n when _notes.Remove(n) => Add(content._notes, n),
                Dimension d when _dimensions.Remove(d) => Add(content._dimensions, d),
                Guide g when _guides.Remove(g) => Add(content._guides, g),
                SectionPlane s when _sections.Remove(s) => Add(content._sections, s),
                _ => false,
            };
            if (moved) item.Parent = content;
        }

        // Remove the originals: the faces, and the edges no remaining face needs.
        foreach (var f in faces) RemoveFace(f);
        foreach (var e in edges.Where(e => e.FaceList.Count == 0))
        {
            var (a, b) = (e.Start, e.End);
            RemoveEdgeOnly(e);
            RemoveVertexIfIsolated(a);
            RemoveVertexIfIsolated(b);
        }
        RaiseChanged();
        return group;
    }

    private static bool Add<T>(List<T> list, T item)
    {
        list.Add(item);
        return true;
    }

    /// <summary>Moves the content of a group into this container (merging with the geometry here) and removes the group.</summary>
    public List<Entity> Explode(Group group)
    {
        if (!ReferenceEquals(group.Parent, this)) throw new ArgumentException("Group is not in this container.");
        var state = group.Entities.CaptureState();
        var groupTag = group.Tag;
        var groupMaterial = group.Material;
        var at = state.Vertices.ToDictionary(v => v.Id, v => v.Position);
        // Every face of the group must come out as a face again, with its holes, side, material and tag (untagged
        // ones take the group's). Rebuilding faces only from the edges lost some: e.g. floors next to other floors.
        var faces = state.Faces.Select(fd => new FillReference
        {
            Outer = fd.Outer.Select(id => at[id]).ToList(),
            Holes = fd.Inner.Select(l => l.Select(id => at[id]).ToList()).ToList(),
            Normal = fd.Normal,
            Material = Context.Materials.Find(fd.Material) ?? groupMaterial,
            Tag = Context.Tags.Find(fd.Tag) ?? groupTag,
            Hidden = fd.Hidden,
        }).ToList();
        RemoveGroup(group);

        // All edges first (merging with the geometry here), then the faces of every touched plane at once.
        var lastId = LastId;
        var created = new List<Edge>();
        foreach (var e in state.Edges) InsertSegment(at[e.Start], at[e.End], created);
        UpdateFacesForNewEdges(created, faces);

        // Edges keep their tag and soft/smooth/hidden flags; untagged ones take the group's tag (as faces do).
        var newEdges = _edges.Where(e => e.Id > lastId).ToList();
        foreach (var ed in state.Edges)
        {
            var a = at[ed.Start];
            var b = at[ed.End];
            foreach (var e in newEdges.Where(e => OnSegment(e.Start.Position, a, b) && OnSegment(e.End.Position, a, b)))
            {
                e.Tag = Context.Tags.Find(ed.Tag) ?? groupTag;
                e.Soft = ed.Soft;
                e.Smooth = ed.Smooth;
                e.Hidden = ed.Hidden;
            }
        }
        var result = new List<Entity>();
        foreach (var gd in state.Groups ?? Array.Empty<EntitiesState.GroupData>())
            result.Add(LoadGroup(gd, keepIds: true, Vector3d.Zero));
        // Images, texts, dimensions, guides and section planes come out unchanged.
        var others = state with
        {
            Vertices = Array.Empty<EntitiesState.VertexData>(), Edges = Array.Empty<EntitiesState.EdgeData>(),
            Faces = Array.Empty<EntitiesState.FaceData>(), Groups = null,
        };
        result.AddRange(Load(others, keepIds: true, Vector3d.Zero));
        result.AddRange(_edges.Where(e => e.Id > lastId));
        result.AddRange(_faces.Where(f => f.Id > lastId));
        RaiseChanged();
        return result;
    }

    private static bool OnSegment(Vector3d p, Vector3d a, Vector3d b) =>
        Math.Abs(Vector3d.Distance(a, p) + Vector3d.Distance(p, b) - Vector3d.Distance(a, b)) < Tolerance.Length;

    private static Vector3d? InteriorProbe(IReadOnlyList<Vector3d> outer, Vector3d normal)
    {
        var basis = new PlaneBasis(outer[0], normal);
        var poly = outer.Select(basis.ToLocal).ToList();
        if (Polygon2d.SignedArea(poly) < 0) poly.Reverse();
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var edge = b - a;
            var len = edge.Length;
            if (len < Tolerance.Length) continue;
            var q = a + edge * 0.5 + new Vector2d(-edge.Y / len, edge.X / len) * Math.Min(0.01, len * 0.25);
            if (Polygon2d.Contains(poly, q)) return basis.ToWorld(q);
        }
        return null;
    }

    /// <summary>Copies a group (with all its content) by an offset.</summary>
    public Group DuplicateGroup(Group group, Vector3d offset)
    {
        var copy = LoadGroup(CaptureGroup(group), keepIds: false, offset);
        RaiseChanged();
        return copy;
    }

    private void RemoveGroup(Group g)
    {
        if (_groups.Remove(g)) MarkDeleted(g);
    }

    /// <summary>Every edge in this container and all nested groups.</summary>
    public IEnumerable<Edge> AllEdges() => _edges.Concat(_groups.SelectMany(g => g.Entities.AllEdges()));

    public IEnumerable<Face> AllFaces() => _faces.Concat(_groups.SelectMany(g => g.Entities.AllFaces()));

    public IEnumerable<Vertex> AllVertices() => _vertices.Concat(_groups.SelectMany(g => g.Entities.AllVertices()));

    public IEnumerable<Group> AllGroups() => _groups.Concat(_groups.SelectMany(g => g.Entities.AllGroups()));

    /// <summary>The groups enclosing this container, innermost first.</summary>
    public IEnumerable<Group> Ancestors()
    {
        for (var g = Owner; g is not null; g = g.Parent?.Owner) yield return g;
    }

    /// <summary>
    /// The entity itself if it lives directly in <paramref name="container"/>, otherwise the enclosing group that
    /// does; null when the entity is not inside the container at all.
    /// </summary>
    public static Entity? AncestorIn(Entity e, Entities container)
    {
        Entity? current = e;
        while (current is not null)
        {
            if (ReferenceEquals(current.Parent, container)) return current;
            current = current.Parent?.Owner;
        }
        return null;
    }

    /// <summary>True when <paramref name="inner"/> is <paramref name="outer"/> or nested somewhere inside it.</summary>
    public static bool IsWithin(Entities? inner, Entities outer)
    {
        for (var c = inner; c is not null; c = c.Owner?.Parent)
            if (ReferenceEquals(c, outer)) return true;
        return false;
    }

    /// <summary>
    /// Axis-aligned bounds of everything in a group (geometry, pictures, notes, dimensions), or null when empty,
    /// so a group holding only a floor plan still has a box to highlight.
    /// </summary>
    public static (Vector3d Min, Vector3d Max)? Bounds(Group g)
    {
        var points = g.Entities.AllVertices().Select(v => v.Position)
            .Concat(g.Entities.AllImages().SelectMany(i => i.Corners))
            .Concat(g.Entities.AllNotes().Select(n => n.Anchor))
            .Concat(g.Entities.AllDimensions().SelectMany(d => new[] { d.Start, d.End }));
        Vector3d? min = null, max = null;
        foreach (var p in points)
        {
            min = min is { } a ? new Vector3d(Math.Min(a.X, p.X), Math.Min(a.Y, p.Y), Math.Min(a.Z, p.Z)) : p;
            max = max is { } b ? new Vector3d(Math.Max(b.X, p.X), Math.Max(b.Y, p.Y), Math.Max(b.Z, p.Z)) : p;
        }
        return min is { } lo && max is { } hi ? (lo, hi) : null;
    }

    /// <summary>
    /// True when the entity and every enclosing group are visible (not hidden, tag visible) and, while a group is
    /// edited with the rest of the model hidden, the entity is inside that group or is one of the groups around it.
    /// </summary>
    public static bool IsEffectivelyVisible(Entity e)
    {
        if (e.Hidden || !TagCollection.IsVisible(e.Tag)) return false;
        for (var g = e.Parent?.Owner; g is not null; g = g.Parent?.Owner)
            if (g.Hidden || !TagCollection.IsVisible(g.Tag)) return false;
        if (e.Parent?.Context.Isolation() is not { } isolated) return true;
        return IsWithin(e.Parent, isolated) || e is Group group && IsWithin(isolated, group.Entities);
    }

    /// <summary>Tag used for colouring: the entity's own tag, else the nearest tagged enclosing group's.</summary>
    public static Tag? EffectiveTag(Entity e)
    {
        if (e.Tag is not null) return e.Tag;
        for (var g = e.Parent?.Owner; g is not null; g = g.Parent?.Owner)
            if (g.Tag is not null) return g.Tag;
        return null;
    }

    /// <summary>Material used for display: the face's own, else the nearest enclosing group's.</summary>
    public static Material? EffectiveMaterial(Face f)
    {
        if (f.Material is not null) return f.Material;
        for (var g = f.Parent?.Owner; g is not null; g = g.Parent?.Owner)
            if (g.Material is not null) return g.Material;
        return null;
    }

    /// <summary>Replaces a tag on every entity (used when a tag is deleted: pass null to untag).</summary>
    /// <summary>Moves every entity (geometry, groups, guides, annotations, pictures, sections) off a tag, e.g. when it is deleted.</summary>
    public void ReplaceTag(Tag tag, Tag? replacement)
    {
        foreach (var e in Everything().Where(e => e.Tag == tag)) e.Tag = replacement;
        foreach (var g in _groups) g.Entities.ReplaceTag(tag, replacement);
        RaiseChanged();
    }

    public void ReplaceMaterial(Material material, Material? replacement)
    {
        foreach (var f in _faces.Where(f => f.Material == material)) f.Material = replacement;
        foreach (var g in _groups)
        {
            if (g.Material == material) g.Material = replacement;
            g.Entities.ReplaceMaterial(material, replacement);
        }
        RaiseChanged();
    }

    /// <summary>Every entity directly in this container (not inside its groups).</summary>
    private IEnumerable<Entity> Everything() =>
        _edges.Cast<Entity>().Concat(_faces).Concat(_groups).Concat(_guides).Concat(_dimensions).Concat(_notes).Concat(_images).Concat(_sections);
}
