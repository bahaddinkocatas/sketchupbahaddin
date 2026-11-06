using Sketch.Geometry;

namespace Sketch.Model;

/// <summary>
/// A collection of connected ("sticky") geometry: vertices, edges and faces. New edges merge with existing
/// vertices, split the edges they touch or cross, and closed coplanar loops become faces automatically.
/// </summary>
public sealed partial class Entities
{
    private readonly List<Vertex> _vertices = new();
    private readonly List<Edge> _edges = new();
    private readonly List<Face> _faces = new();
    private readonly Dictionary<(int, int), Edge> _edgeMap = new();
    private readonly List<Group> _groups = new();

    public Entities() : this(new ModelContext(), null)
    {
    }

    public Entities(ModelContext context, Group? owner)
    {
        Context = context;
        _owner = owner;
    }

    private Group? _owner;

    /// <summary>Ids, tags and materials shared by every container of the model.</summary>
    public ModelContext Context { get; }

    /// <summary>The group whose content this is, or null for the model root.</summary>
    public Group? Owner => _owner;

    public IReadOnlyList<Vertex> Vertices => _vertices;
    public IReadOnlyList<Edge> Edges => _edges;
    public IReadOnlyList<Face> Faces => _faces;
    public IReadOnlyList<Group> Groups => _groups;

    /// <summary>Tag new edges, faces and groups receive (the "active tag").</summary>
    public Func<Tag?>? DefaultTag { get; set; }

    /// <summary>Raised after any change to the geometry (including changes inside nested groups).</summary>
    public event EventHandler? Changed;

    internal void RaiseChanged()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
        Owner?.Parent?.RaiseChanged();
    }

    /// <summary>
    /// Increases whenever anything in this container or in a group nested in it changes (geometry or an entity's
    /// tag, material, hidden or soft flag). Views use it to rebuild only what changed.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>Marks a property change without raising <see cref="Changed"/> (used by entity setters).</summary>
    internal void BumpVersion()
    {
        Version++;
        Owner?.Parent?.BumpVersion();
    }

    private Tag? NewEntityTag() => (DefaultTag ?? Owner?.Parent?.DefaultTag)?.Invoke();

    /// <summary>Signals that entity properties (e.g. edge flags) changed so views redraw.</summary>
    public void Touch() => RaiseChanged();

    private static (int, int) Key(Vertex a, Vertex b) => a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);

    public Edge? FindEdge(Vertex a, Vertex b) => _edgeMap.GetValueOrDefault(Key(a, b));

    public Vertex? FindVertex(Vector3d p, double tolerance = Tolerance.Length)
    {
        Vertex? best = null;
        var bestD = tolerance;
        foreach (var v in _vertices)
        {
            var d = Vector3d.Distance(v.Position, p);
            if (d <= bestD)
            {
                best = v;
                bestD = d;
            }
        }
        return best;
    }

    /// <summary>Bounding sphere of all vertices, or null when empty.</summary>
    public (Vector3d Center, double Radius)? BoundingSphere()
    {
        // Only what is visible counts (hidden groups and hidden tags are ignored), plus imported images.
        var points = AllEdges().Where(IsEffectivelyVisible).SelectMany(e => new[] { e.Start.Position, e.End.Position })
            .Concat(AllImages().Where(IsEffectivelyVisible).SelectMany(i => i.Corners))
            .ToList();
        if (points.Count == 0) points = AllVertices().Select(v => v.Position).ToList();
        if (points.Count == 0) return null;
        var min = points[0];
        var max = min;
        foreach (var p in points)
        {
            min = new Vector3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vector3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        var center = (min + max) / 2;
        return (center, Math.Max((max - min).Length / 2, Tolerance.Length));
    }

    // ------------------------------------------------------------------ public editing API

    /// <summary>Adds a straight edge between two points (sticky: merges, splits and creates faces).</summary>
    public IReadOnlyList<Edge> AddEdge(Vector3d a, Vector3d b) => AddPolyline(new[] { a, b }, closed: false);

    /// <summary>
    /// Adds connected edges through the points. Faces are resolved once after all segments are inserted,
    /// so a closed rectangle produces exactly one face.
    /// </summary>
    public IReadOnlyList<Edge> AddPolyline(IReadOnlyList<Vector3d> points, bool closed)
    {
        var created = new List<Edge>();
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
            InsertSegment(points[i], points[(i + 1) % points.Count], created);
        if (created.Count > 0)
        {
            UpdateFacesForNewEdges(created);
            RaiseChanged();
        }
        return created;
    }

    // ------------------------------------------------------------------ low-level topology

    private Vertex NewVertex(Vector3d p)
    {
        var v = new Vertex { Id = Context.NextId(), Position = p, Parent = this };
        _vertices.Add(v);
        return v;
    }

    /// <summary>Returns the edge between a and b, creating it (without any splitting) if missing.</summary>
    private Edge Connect(Vertex a, Vertex b, out bool isNew)
    {
        if (_edgeMap.TryGetValue(Key(a, b), out var existing))
        {
            isNew = false;
            return existing;
        }
        var e = new Edge { Id = Context.NextId(), Start = a, End = b, Parent = this, Tag = NewEntityTag() };
        _edges.Add(e);
        _edgeMap[Key(a, b)] = e;
        a.EdgeList.Add(e);
        b.EdgeList.Add(e);
        isNew = true;
        return e;
    }

    private void RemoveEdgeOnly(Edge e)
    {
        _edges.Remove(e);
        _edgeMap.Remove(Key(e.Start, e.End));
        e.Start.EdgeList.Remove(e);
        e.End.EdgeList.Remove(e);
        e.IsDeleted = true;
    }

    private void RemoveVertexIfIsolated(Vertex v)
    {
        if (v.EdgeList.Count > 0 || v.IsDeleted) return;
        _vertices.Remove(v);
        v.IsDeleted = true;
    }

    /// <summary>Splits an edge at a point on it. Faces using the edge get the new vertex inserted in their loops.</summary>
    private Vertex SplitEdge(Edge e, Vector3d p)
    {
        var (t, _) = Segment3d.ProjectPoint(p, e.Start.Position, e.End.Position);
        var x = NewVertex(Vector3d.Lerp(e.Start.Position, e.End.Position, Math.Clamp(t, 0, 1)));
        var s = e.Start;
        var end = e.End;
        var faces = e.FaceList.ToList();
        var (soft, smooth, hidden, tag) = (e.Soft, e.Smooth, e.Hidden, e.Tag);

        RemoveEdgeOnly(e);
        var e1 = Connect(s, x, out _);
        var e2 = Connect(x, end, out _);
        foreach (var edge in new[] { e1, e2 })
        {
            edge.Soft = soft;
            edge.Smooth = smooth;
            edge.Hidden = hidden;
            edge.Tag = tag;
        }

        foreach (var f in faces)
        {
            foreach (var loop in f.Loops)
            {
                var list = loop.List;
                for (var i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    var b = list[(i + 1) % list.Count];
                    if ((ReferenceEquals(a, s) && ReferenceEquals(b, end)) || (ReferenceEquals(a, end) && ReferenceEquals(b, s)))
                    {
                        list.Insert(i + 1, x);
                        break;
                    }
                }
            }
            f.EdgeList.Remove(e);
            f.EdgeList.Add(e1);
            f.EdgeList.Add(e2);
            e1.FaceList.Add(f);
            e2.FaceList.Add(f);
        }
        return x;
    }

    /// <summary>Finds or creates the vertex at p: reuses a vertex, splits an edge passing through p, or adds a new one.</summary>
    private Vertex VertexAt(Vector3d p)
    {
        var existing = FindVertex(p);
        if (existing is not null) return existing;
        foreach (var e in _edges)
        {
            var (t, d) = Segment3d.ProjectPoint(p, e.Start.Position, e.End.Position);
            if (d <= Tolerance.Length && t > 0 && t < 1)
                return SplitEdge(e, p);
        }
        return NewVertex(p);
    }

    /// <summary>Inserts segment a-b, splitting at existing vertices and edge crossings along it.</summary>
    private void InsertSegment(Vector3d a, Vector3d b, List<Edge> created)
    {
        if (Vector3d.Distance(a, b) <= Tolerance.Length) return;
        var va = VertexAt(a);
        var vb = VertexAt(b);
        if (ReferenceEquals(va, vb)) return;

        var pa = va.Position;
        var pb = vb.Position;
        var dir = pb - pa;
        var stops = new List<(double T, Vertex V)> { (0, va), (1, vb) };

        // Crossings with existing edges (non-parallel, within tolerance) split both edges.
        foreach (var e in _edges.ToList())
        {
            if (e.Uses(va) || e.Uses(vb)) continue;
            if (Segment3d.AreParallel(dir, e.End.Position - e.Start.Position)) continue;
            var (s, t, d) = Segment3d.ClosestParameters(pa, pb, e.Start.Position, e.End.Position);
            if (d > Tolerance.Length) continue;
            var len = dir.Length;
            var elen = e.Length;
            var interiorNew = s * len > Tolerance.Length && (1 - s) * len > Tolerance.Length;
            var interiorOld = t * elen > Tolerance.Length && (1 - t) * elen > Tolerance.Length;
            if (interiorNew && interiorOld)
                SplitEdge(e, Vector3d.Lerp(pa, pb, s));
        }

        // Existing vertices lying on the segment interior (including those just created by splits).
        foreach (var v in _vertices)
        {
            if (ReferenceEquals(v, va) || ReferenceEquals(v, vb)) continue;
            var (t, d) = Segment3d.ProjectPoint(v.Position, pa, pb);
            if (d <= Tolerance.Length && t > 0 && t < 1) stops.Add((t, v));
        }

        stops.Sort((x, y) => x.T.CompareTo(y.T));
        for (var i = 0; i + 1 < stops.Count; i++)
        {
            var from = stops[i].V;
            var to = stops[i + 1].V;
            if (ReferenceEquals(from, to)) continue;
            var edge = Connect(from, to, out var isNew);
            if (isNew) created.Add(edge);
            else if (!created.Contains(edge)) created.Add(edge); // re-drawn edges can still close new faces
        }
    }

    // ------------------------------------------------------------------ faces

    /// <summary>Creates a face from vertex loops. Loops must already be connected by edges.</summary>
    private Face CreateFace(IReadOnlyList<Vertex> outer, IEnumerable<IReadOnlyList<Vertex>> holes, Vector3d normal)
    {
        var face = new Face { Id = Context.NextId(), Normal = normal.Normalized(), OuterLoop = new Loop(outer), Parent = this, Tag = NewEntityTag() };
        foreach (var h in holes) face.InnerList.Add(new Loop(h));
        foreach (var loop in face.Loops)
        {
            foreach (var (a, b) in loop.Segments())
            {
                var e = FindEdge(a, b) ?? Connect(a, b, out _);
                if (!face.EdgeList.Contains(e)) face.EdgeList.Add(e);
                if (!e.FaceList.Contains(face)) e.FaceList.Add(face);
            }
        }
        _faces.Add(face);
        return face;
    }

    private void RemoveFace(Face f)
    {
        if (f.IsDeleted) return;
        foreach (var e in f.EdgeList) e.FaceList.Remove(f);
        _faces.Remove(f);
        f.IsDeleted = true;
    }

    /// <summary>
    /// Adds a planar face through the given points (connecting them with edges). Used by tools that build
    /// geometry directly, e.g. Push/Pull. Returns null for degenerate input.
    /// </summary>
    public Face? AddFace(IReadOnlyList<Vector3d> points)
    {
        if (points.Count < 3) return null;
        var created = new List<Edge>();
        for (var i = 0; i < points.Count; i++)
            InsertSegment(points[i], points[(i + 1) % points.Count], created);
        UpdateFacesForNewEdges(created);
        RaiseChanged();
        var probe = points.Aggregate(Vector3d.Zero, (s, p) => s + p) / points.Count;
        return _faces.FirstOrDefault(f => f.ContainsPoint(probe));
    }

    /// <summary>Flips the front and back of a face.</summary>
    public void Reverse(Face f)
    {
        f.Normal = -f.Normal;
        f.OuterLoop.List.Reverse();
        foreach (var l in f.InnerList) l.List.Reverse();
        RaiseChanged();
    }
}
