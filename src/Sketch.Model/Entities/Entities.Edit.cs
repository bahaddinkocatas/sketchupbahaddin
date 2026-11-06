using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    /// <summary>Highest id handed out so far; entities created later have larger ids.</summary>
    public int LastId => Context.LastId;

    /// <summary>Finds a live entity by id (ids survive undo/redo and state restores).</summary>
    public Entity? FindById(int id) =>
        (Entity?)_vertices.FirstOrDefault(v => v.Id == id)
        ?? (Entity?)_edges.FirstOrDefault(e => e.Id == id)
        ?? (Entity?)_faces.FirstOrDefault(f => f.Id == id)
        ?? (Entity?)_groups.FirstOrDefault(g => g.Id == id)
        ?? (Entity?)_guides.FirstOrDefault(g => g.Id == id)
        ?? (Entity?)_dimensions.FirstOrDefault(d => d.Id == id)
        ?? (Entity?)_notes.FirstOrDefault(n => n.Id == id)
        ?? _images.FirstOrDefault(i => i.Id == id);

    public IEnumerable<Entity> FindByIds(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        foreach (var v in _vertices) if (set.Contains(v.Id)) yield return v;
        foreach (var e in _edges) if (set.Contains(e.Id)) yield return e;
        foreach (var f in _faces) if (set.Contains(f.Id)) yield return f;
        foreach (var g in _groups) if (set.Contains(g.Id)) yield return g;
        foreach (var g in _guides) if (set.Contains(g.Id)) yield return g;
        foreach (var d in _dimensions) if (set.Contains(d.Id)) yield return d;
        foreach (var n in _notes) if (set.Contains(n.Id)) yield return n;
        foreach (var i in _images) if (set.Contains(i.Id)) yield return i;
    }

    /// <summary>
    /// Moves vertices. Faces that stay planar get their normal refreshed; faces that become non-planar are
    /// folded into triangles (SketchUp's "auto-fold").
    /// </summary>
    public void MoveVertices(IReadOnlyCollection<Vertex> vertices, Vector3d delta)
    {
        if (vertices.Count == 0 || delta.IsZero(1e-12)) return;
        var faces = vertices.SelectMany(v => v.EdgeList).SelectMany(e => e.FaceList).Distinct().ToList();
        foreach (var v in vertices) v.Position += delta;

        foreach (var f in faces)
        {
            if (f.IsDeleted) continue;
            var newell = NewellNormal(f.OuterLoop.List);
            if (newell.IsZero(1e-12))
            {
                RemoveFace(f);
                continue;
            }
            f.Normal = newell.Normalized();
            if (!IsPlanar(f)) FoldIntoTriangles(f);
        }
        RaiseChanged();
    }

    private static Vector3d NewellNormal(IReadOnlyList<Vertex> loop)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i].Position;
            var b = loop[(i + 1) % loop.Count].Position;
            x += (a.Y - b.Y) * (a.Z + b.Z);
            y += (a.Z - b.Z) * (a.X + b.X);
            z += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vector3d(x, y, z);
    }

    private static bool IsPlanar(Face f)
    {
        var plane = f.Plane;
        return f.Vertices.All(v => Math.Abs(plane.SignedDistance(v.Position)) <= Tolerance.Length);
    }

    /// <summary>Replaces a non-planar face (outer loop only) by a triangle fan, adding the fold edges.</summary>
    private void FoldIntoTriangles(Face f)
    {
        var loop = f.OuterLoop.List.ToList();
        var reference = f.Normal;
        var (material, tag) = (f.Material, f.Tag);
        RemoveFace(f);
        for (var i = 1; i + 1 < loop.Count; i++)
        {
            var tri = new List<Vertex> { loop[0], loop[i], loop[i + 1] };
            var n = NewellNormal(tri);
            if (n.IsZero(1e-12)) continue;
            Connect(loop[0], loop[i], out _);
            Connect(loop[i], loop[i + 1], out _);
            Connect(loop[i + 1], loop[0], out _);
            if (Vector3d.Dot(n, reference) < 0) tri.Reverse();
            var part = CreateFace(tri, Array.Empty<IReadOnlyList<Vertex>>(), Vector3d.Dot(n, reference) < 0 ? -n : n);
            part.Material = material;
            part.Tag = tag;
        }
    }

    /// <summary>
    /// Flips faces in <paramref name="mutable"/> so that every edge shared by exactly two faces is walked in
    /// opposite directions by them (consistent orientation). Faces not in the set are fixed references.
    /// </summary>
    public void OrientConsistently(IReadOnlySet<Face> mutable)
    {
        var settled = new HashSet<Face>(_faces.Where(f => !mutable.Contains(f)));
        var queue = new Queue<Face>(settled.Where(f => f.EdgeList.Any(e => e.FaceList.Any(mutable.Contains))));
        var remaining = new HashSet<Face>(mutable.Where(f => !f.IsDeleted));

        while (remaining.Count > 0)
        {
            if (queue.Count == 0)
            {
                // A component with no fixed neighbour: keep the first face as it is and propagate from it.
                var seed = remaining.First();
                remaining.Remove(seed);
                settled.Add(seed);
                queue.Enqueue(seed);
            }
            var face = queue.Dequeue();
            foreach (var e in face.EdgeList)
            {
                if (e.FaceList.Count != 2) continue;
                var other = ReferenceEquals(e.FaceList[0], face) ? e.FaceList[1] : e.FaceList[0];
                if (!remaining.Contains(other)) continue;
                var faceWalks = face.Loops.Any(l => l.Traverses(e.Start, e.End));
                var otherWalks = other.Loops.Any(l => l.Traverses(e.Start, e.End));
                if (faceWalks == otherWalks) FlipSilently(other);
                remaining.Remove(other);
                settled.Add(other);
                queue.Enqueue(other);
            }
        }
        RaiseChanged();
    }

    /// <summary>
    /// For a consistently oriented closed set of faces: flips them all when they point inward (negative volume).
    /// </summary>
    public void EnsureOutward(IReadOnlySet<Face> faces)
    {
        double volume = 0;
        foreach (var f in faces.Where(f => !f.IsDeleted))
            volume += Vector3d.Dot(f.OuterLoop.List[0].Position, f.Normal) * f.Area / 3;
        if (volume >= 0) return;
        foreach (var f in faces.Where(f => !f.IsDeleted)) FlipSilently(f);
        RaiseChanged();
    }

    private static void FlipSilently(Face f)
    {
        f.Normal = -f.Normal;
        f.OuterLoop.List.Reverse();
        foreach (var l in f.InnerList) l.List.Reverse();
    }

    /// <summary>All vertices used by the given edges and faces.</summary>
    public static HashSet<Vertex> VerticesOf(IEnumerable<Entity> items)
    {
        var set = new HashSet<Vertex>();
        foreach (var item in items)
        {
            switch (item)
            {
                case Edge e:
                    set.Add(e.Start);
                    set.Add(e.End);
                    break;
                case Face f:
                    foreach (var v in f.Vertices) set.Add(v);
                    break;
                case Vertex v:
                    set.Add(v);
                    break;
                case Group g:
                    foreach (var v in g.Entities.AllVertices()) set.Add(v);
                    break;
            }
        }
        return set;
    }

    /// <summary>
    /// Every point that gives the items their extent: geometry vertices plus images, texts, dimensions, guide
    /// points and section planes (also inside groups). Used for selection boxes (Scale grips, Flip centre), so that
    /// e.g. a group holding only a floor plan picture can be scaled too.
    /// </summary>
    public static List<Vector3d> PointsOf(IEnumerable<Entity> items)
    {
        var list = VerticesOf(items).Select(v => v.Position).ToList();
        void AddContent(Entities c)
        {
            list.AddRange(c.AllImages().SelectMany(i => i.Corners));
            list.AddRange(c.AllNotes().Select(n => n.Anchor));
            list.AddRange(c.AllDimensions().SelectMany(d => new[] { d.Start, d.End }));
            list.AddRange(c.AllGuides().Where(g => g.IsPoint).Select(g => g.Point));
            list.AddRange(c.AllSections().SelectMany(s => s.Corners));
        }
        foreach (var item in items)
        {
            switch (item)
            {
                case ImageEntity i:
                    list.AddRange(i.Corners);
                    break;
                case Note n:
                    list.Add(n.Anchor);
                    break;
                case Dimension d:
                    list.Add(d.Start);
                    list.Add(d.End);
                    break;
                case Guide { IsPoint: true } g:
                    list.Add(g.Point);
                    break;
                case SectionPlane s:
                    list.AddRange(s.Corners);
                    break;
                case Group g:
                    AddContent(g.Entities);
                    break;
            }
        }
        return list;
    }

    /// <summary>Faces connected to the given face through shared edges (the whole connected surface).</summary>
    public static HashSet<Entity> ConnectedGeometry(Entity start)
    {
        var result = new HashSet<Entity>();
        var queue = new Queue<Entity>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            if (!result.Add(item)) continue;
            switch (item)
            {
                case Face f:
                    foreach (var e in f.EdgeList) queue.Enqueue(e);
                    break;
                case Edge e:
                    foreach (var f in e.FaceList) queue.Enqueue(f);
                    foreach (var adj in e.Start.EdgeList.Concat(e.End.EdgeList)) queue.Enqueue(adj);
                    break;
            }
        }
        return result;
    }
}
