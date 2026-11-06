using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Inference;

/// <summary>
/// Caches that keep inference fast on large models (100 000+ edges): the visible geometry is listed once per model
/// change, and its screen projection is binned into a grid once per camera change, so a mouse move only looks at
/// the edges and points near the cursor. Rays are tested against each top-level group's box before its faces.
/// </summary>
public sealed partial class InferenceEngine
{

    /// <summary>Visible geometry; rebuilt after any model or visibility change.</summary>
    private sealed class ModelCache
    {
        public required List<Edge> Edges { get; init; }
        public required List<Face> Faces { get; init; }
        public required List<Vertex> Vertices { get; init; }
        /// <summary>Visible parts (for looking up a vertex by position and for ray tests by group box).</summary>
        public required List<GroupPart> Parts { get; init; }

        public Vertex? VertexAt((long, long, long) key)
        {
            foreach (var part in Parts)
                if (part.VertexAt.TryGetValue(key, out var v)) return v;
            return null;
        }

        /// <summary>For each edge, the indices of its end points in <see cref="Vertices"/>.</summary>
        public required (int A, int B)[] EdgeEnds { get; init; }

        /// <summary>Axis-aligned box of each face (same order as <see cref="Faces"/>) for quick ray rejection.</summary>
        public required (Vector3d Min, Vector3d Max)[] FaceBoxes { get; init; }

        /// <summary>
        /// The top-level groups' parts, which come first in the lists above (the loose root geometry comes last), and
        /// how many vertices and edges they hold.
        /// </summary>
        public required List<GroupPart> GroupParts { get; init; }
        public required int GroupVertexCount { get; init; }
        public required int GroupEdgeCount { get; init; }
    }

    /// <summary>
    /// Screen positions of the visible vertices for the current camera (NaN when behind the eye), binned into a grid
    /// of <see cref="Cell"/>-pixel cells: vertices by position, edges by the cells their screen box covers and edge
    /// midpoints by position. A query only visits the cells around the cursor.
    /// </summary>
    private sealed class ScreenIndex
    {
        public const float Cell = 32f;
        public required (Vector3d Eye, Vector3d Target, Vector3d Up, double Ortho, bool Persp, double Fov, int W, int H) Key { get; init; }
        public required float[] X { get; init; }
        public required float[] Y { get; init; }
        public required int Cols { get; init; }
        public required int Rows { get; init; }

        /// <summary>Cell tables: one for the groups' geometry (kept across root edits) and one for the loose root geometry.</summary>
        public required Grid[] Grids { get; init; }

        /// <summary>Cell column/row of a screen coordinate (the grid starts NearPx outside the view).</summary>
        public static int Coord(float v) => (int)((v + NearPx) / Cell);

        /// <summary>Cells overlapping the square of radius r around (x, y); empty when it lies outside the grid.</summary>
        public IEnumerable<int> CellsAround(float x, float y, float r)
        {
            int c0 = Math.Max(0, Coord(x - r)), c1 = Math.Min(Cols - 1, Coord(x + r));
            int r0 = Math.Max(0, Coord(y - r)), r1 = Math.Min(Rows - 1, Coord(y + r));
            for (var row = r0; row <= r1; row++)
                for (var col = c0; col <= c1; col++)
                    yield return row * Cols + col;
        }

        /// <summary>Builds a compressed cell → items table from each item's cell range (null = not on the grid); items are offset + index.</summary>
        public static (int[] Start, int[] Items) Table(int cells, (int C0, int C1, int R0, int R1)?[] ranges, int cols, int offset = 0)
        {
            var start = new int[cells + 1];
            foreach (var r in ranges)
            {
                if (r is null) continue;
                for (var row = r.Value.R0; row <= r.Value.R1; row++)
                    for (var col = r.Value.C0; col <= r.Value.C1; col++)
                        start[row * cols + col + 1]++;
            }
            for (var c = 0; c < cells; c++) start[c + 1] += start[c];
            var fill = (int[])start.Clone();
            var items = new int[start[cells]];
            for (var i = 0; i < ranges.Length; i++)
            {
                if (ranges[i] is not { } r) continue;
                for (var row = r.R0; row <= r.R1; row++)
                    for (var col = r.C0; col <= r.C1; col++)
                        items[fill[row * cols + col]++] = offset + i;
            }
            return (start, items);
        }
    }

    /// <summary>Cell → vertex and cell → edge tables (indices into the model lists).</summary>
    private sealed record Grid(int[] VertexStart, int[] VertexItems, int[] EdgeStart, int[] EdgeItems);

    /// <summary>Screen positions and grid of the groups' geometry for one camera, reused while those groups stay the same.</summary>
    private sealed record GroupScreen(object Key, List<GroupPart> Parts, float[] X, float[] Y, Grid Grid);

    private ModelCache? _model;
    private ScreenIndex? _screen;
    private GroupScreen? _groupScreen;

    /// <summary>Visible geometry of one top-level group (or the loose root geometry), reused while unchanged.</summary>
    private sealed class GroupPart
    {
        public required long Version { get; init; }
        public required List<Edge> Edges { get; init; }
        public required List<Face> Faces { get; init; }
        public required List<Vertex> Vertices { get; init; }
        public required (int A, int B)[] Ends { get; init; }
        public required (Vector3d Min, Vector3d Max)[] Boxes { get; init; }
        public required Dictionary<(long, long, long), Vertex> VertexAt { get; init; }
        /// <summary>Box around all the part's faces (a ray that misses it misses every face inside).</summary>
        public required (Vector3d Min, Vector3d Max) FaceBounds { get; init; }

        public static GroupPart Build(long version, List<Edge> edges, List<Face> faces)
        {
            var vertices = new List<Vertex>();
            var index = new Dictionary<Vertex, int>();
            var at = new Dictionary<(long, long, long), Vertex>();
            int Index(Vertex v)
            {
                if (index.TryGetValue(v, out var i)) return i;
                index[v] = i = vertices.Count;
                vertices.Add(v);
                at.TryAdd(PosKey(v.Position), v);
                return i;
            }
            var ends = edges.Select(e => (Index(e.Start), Index(e.End))).ToArray();
            var all = (Min: new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue), Max: new Vector3d(double.MinValue, double.MinValue, double.MinValue));
            var boxes = faces.Select(f =>
            {
                var pts = f.OuterLoop.Vertices;
                var min = pts[0].Position;
                var max = min;
                foreach (var v in pts)
                {
                    var p = v.Position;
                    min = new Vector3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Vector3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
                all.Min = new Vector3d(Math.Min(all.Min.X, min.X), Math.Min(all.Min.Y, min.Y), Math.Min(all.Min.Z, min.Z));
                all.Max = new Vector3d(Math.Max(all.Max.X, max.X), Math.Max(all.Max.Y, max.Y), Math.Max(all.Max.Z, max.Z));
                return (min, max);
            }).ToArray();
            if (faces.Count == 0) all = (Vector3d.Zero, Vector3d.Zero);
            return new GroupPart
            {
                Version = version, Edges = edges, Faces = faces, Vertices = vertices, Ends = ends, Boxes = boxes, VertexAt = at, FaceBounds = all,
            };
        }
    }

    private Dictionary<Group, GroupPart> _parts = new();

    /// <summary>Drops every cache (the host calls this when tag visibility changes without a geometry change).</summary>
    public void Invalidate()
    {
        _parts.Clear();
        OnGeometryChanged();
    }

    /// <summary>After an edit only the combined lists are redone; unchanged groups keep their part.</summary>
    private void OnGeometryChanged()
    {
        _model = null;
        _screen = null;
    }

    private static bool Visible(Entity e) => Entities.IsEffectivelyVisible(e);

    /// <summary>The visible parts: the loose root geometry and each visible top-level group.</summary>
    private List<GroupPart> VisibleParts()
    {
        var parts = new List<GroupPart>
        {
            GroupPart.Build(0, _entities.Edges.Where(Visible).ToList(), _entities.Faces.Where(Visible).ToList()),
        };
        var next = new Dictionary<Group, GroupPart>();
        foreach (var g in _entities.Groups)
        {
            if (!Visible(g)) continue;
            if (!_parts.TryGetValue(g, out var part) || part.Version != g.Entities.Version)
                part = GroupPart.Build(g.Entities.Version, g.Entities.AllEdges().Where(Visible).ToList(), g.Entities.AllFaces().Where(Visible).ToList());
            next[g] = part;
            parts.Add(part);
        }
        _parts = next;
        return parts;
    }

    private static (long, long, long) PosKey(Vector3d p) =>
        ((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000), (long)Math.Round(p.Z * 1000));

    private long _modelVersion = -1;

    private ModelCache Model
    {
        get
        {
            // Property changes (hidden, tag, ...) bump the version without a Changed event: check it too.
            if (_model is not null && _modelVersion == _entities.Version) return _model;
            _modelVersion = _entities.Version;
            var visible = VisibleParts();
            // Groups first, the loose root geometry (most often what a tool preview changes) last.
            var groupParts = visible.Skip(1).ToList();
            var parts = groupParts.Append(visible[0]).ToList();
            var edges = new List<Edge>(parts.Sum(p => p.Edges.Count));
            var faces = new List<Face>(parts.Sum(p => p.Faces.Count));
            var vertices = new List<Vertex>(parts.Sum(p => p.Vertices.Count));
            var ends = new List<(int, int)>(edges.Capacity);
            var boxes = new List<(Vector3d, Vector3d)>(faces.Capacity);
            foreach (var part in parts)
            {
                var offset = vertices.Count;
                edges.AddRange(part.Edges);
                faces.AddRange(part.Faces);
                vertices.AddRange(part.Vertices);
                foreach (var (a, b) in part.Ends) ends.Add((a + offset, b + offset));
                boxes.AddRange(part.Boxes);
            }
            _model = new ModelCache
            {
                Edges = edges, Faces = faces, Vertices = vertices, FaceBoxes = boxes.ToArray(), EdgeEnds = ends.ToArray(),
                Parts = parts, GroupParts = groupParts,
                GroupVertexCount = groupParts.Sum(p => p.Vertices.Count), GroupEdgeCount = groupParts.Sum(p => p.Edges.Count),
            };
            _screen = null;
            return _model;
        }
    }

    /// <summary>
    /// Visible faces whose bounding box the ray passes through (slab test, with a small tolerance). Each top-level
    /// group's box is tested first, so a ray only looks inside the few groups it crosses.
    /// </summary>
    private IEnumerable<Face> FacesAlongRay(Ray3d ray, double maxT = double.MaxValue)
    {
        var model = Model;
        var o = ray.Origin;
        var d = ray.Direction;
        const double eps = Tolerance.Length * 10;
        foreach (var part in model.Parts)
        {
            if (part.Faces.Count == 0) continue;
            if (part.Faces.Count > 1 && !HitsBox(o, d, part.FaceBounds.Min, part.FaceBounds.Max, eps, maxT)) continue;
            for (var i = 0; i < part.Faces.Count; i++)
            {
                var (min, max) = part.Boxes[i];
                if (HitsBox(o, d, min, max, eps, maxT)) yield return part.Faces[i];
            }
        }
    }

    private static bool HitsBox(Vector3d o, Vector3d d, Vector3d min, Vector3d max, double eps, double maxT)
    {
        double t0 = 0, t1 = maxT;
        return Slab(o.X, d.X, min.X - eps, max.X + eps, ref t0, ref t1)
               && Slab(o.Y, d.Y, min.Y - eps, max.Y + eps, ref t0, ref t1)
               && Slab(o.Z, d.Z, min.Z - eps, max.Z + eps, ref t0, ref t1);
    }

    private static bool Slab(double o, double d, double min, double max, ref double t0, ref double t1)
    {
        if (Math.Abs(d) < 1e-15) return o >= min && o <= max;
        var a = (min - o) / d;
        var b = (max - o) / d;
        if (a > b) (a, b) = (b, a);
        t0 = Math.Max(t0, a);
        t1 = Math.Min(t1, b);
        return t0 <= t1;
    }

    private ScreenIndex Screen(InferenceQuery q)
    {
        var cam = q.Camera;
        var key = (cam.Eye, cam.Target, cam.Up, cam.OrthoHeight, cam.IsPerspective, cam.FovYDegrees, q.Width, q.Height);
        var model = Model;
        if (_screen is { } s && s.Key == key) return s;

        // Same mapping as Camera.WorldToScreen, with the camera frame worked out once instead of per point.
        var eye = cam.Eye;
        var dir = cam.Direction;
        var right = cam.Right;
        var up = cam.Up;
        var aspect = q.Width / (double)q.Height;
        var tan = Math.Tan(cam.FovYDegrees * Math.PI / 360);
        var perspective = cam.IsPerspective;
        var orthoX = cam.OrthoHeight * aspect / 2;
        var orthoY = cam.OrthoHeight / 2;
        var vertices = model.Vertices;
        var n = vertices.Count;
        var xs = new float[n];
        var ys = new float[n];
        void Project(int from, int to) => Parallel.For(from, to, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var rel = vertices[i].Position - eye;
            var depth = Vector3d.Dot(rel, dir);
            double nx, ny;
            if (perspective)
            {
                if (depth <= 1e-9)
                {
                    xs[i] = ys[i] = float.NaN;
                    return;
                }
                nx = Vector3d.Dot(rel, right) / (depth * tan * aspect);
                ny = Vector3d.Dot(rel, up) / (depth * tan);
            }
            else
            {
                nx = Vector3d.Dot(rel, right) / orthoX;
                ny = Vector3d.Dot(rel, up) / orthoY;
            }
            xs[i] = (float)((nx + 1) / 2 * q.Width);
            ys[i] = (float)((1 - ny) / 2 * q.Height);
        });

        // Grid over the view plus a NearPx margin: anything further out can never be near the cursor.
        float maxX = q.Width + NearPx, maxY = q.Height + NearPx;
        var cols = ScreenIndex.Coord(maxX) + 1;
        var rows = ScreenIndex.Coord(maxY) + 1;
        var cells = cols * rows;
        (int, int, int, int)? Range(float x0, float x1, float y0, float y1)
        {
            if (float.IsNaN(x0) || float.IsNaN(x1) || float.IsNaN(y0) || float.IsNaN(y1)) return null;
            if (x1 < -NearPx || x0 > maxX || y1 < -NearPx || y0 > maxY) return null;
            return (Math.Max(0, ScreenIndex.Coord(x0)), Math.Min(cols - 1, ScreenIndex.Coord(x1)),
                Math.Max(0, ScreenIndex.Coord(y0)), Math.Min(rows - 1, ScreenIndex.Coord(y1)));
        }
        var ends = model.EdgeEnds;
        Grid Build(int v0, int v1, int e0, int e1)
        {
            var vRanges = new (int, int, int, int)?[v1 - v0];
            var eRanges = new (int, int, int, int)?[e1 - e0];
            Parallel.For(v0, v1, i => vRanges[i - v0] = Range(xs[i], xs[i], ys[i], ys[i]));
            Parallel.For(e0, e1, i =>
            {
                var (a, b) = ends[i];
                eRanges[i - e0] = Range(Math.Min(xs[a], xs[b]), Math.Max(xs[a], xs[b]), Math.Min(ys[a], ys[b]), Math.Max(ys[a], ys[b]));
            });
            var (vStart, vItems) = ScreenIndex.Table(cells, vRanges, cols, v0);
            var (eStart, eItems) = ScreenIndex.Table(cells, eRanges, cols, e0);
            return new Grid(vStart, vItems, eStart, eItems);
        }

        // The groups' part is kept while the camera and the groups stay the same: an edit of the loose root geometry
        // (e.g. every mouse move of a Push/Pull preview) then only redoes the root's few points and edges.
        int gv = model.GroupVertexCount, ge = model.GroupEdgeCount;
        if (_groupScreen is { } g && g.Key.Equals(key) && g.Parts.Count == model.GroupParts.Count
            && g.Parts.Zip(model.GroupParts).All(p => ReferenceEquals(p.First, p.Second)))
        {
            Array.Copy(g.X, xs, gv);
            Array.Copy(g.Y, ys, gv);
        }
        else
        {
            Project(0, gv);
            _groupScreen = g = new GroupScreen(key, model.GroupParts, xs[..gv], ys[..gv], Build(0, gv, 0, ge));
        }
        Project(gv, n);
        _screen = new ScreenIndex
        {
            Key = key, X = xs, Y = ys, Cols = cols, Rows = rows, Grids = new[] { g.Grid, Build(gv, n, ge, ends.Length) },
        };
        return _screen;
    }

    /// <summary>Largest pick radius any query uses (pixels), plus a margin.</summary>
    private const float NearPx = 16;

    private int[] _edgeStamp = Array.Empty<int>();
    private int _stamp;

    /// <summary>Visible edges whose screen projection passes near (x, y), with their screen end points.</summary>
    private IEnumerable<(Edge Edge, Vector2d A, Vector2d B)> EdgesNear(InferenceQuery q, double x, double y)
    {
        var s = Screen(q);
        var model = Model;
        var ends = model.EdgeEnds;
        float fx = (float)x, fy = (float)y;
        if (_edgeStamp.Length < ends.Length) _edgeStamp = new int[ends.Length];
        var stamp = ++_stamp;
        foreach (var grid in s.Grids)
        foreach (var cell in s.CellsAround(fx, fy, NearPx))
        {
            for (var k = grid.EdgeStart[cell]; k < grid.EdgeStart[cell + 1]; k++)
            {
                var i = grid.EdgeItems[k];
                if (_edgeStamp[i] == stamp) continue; // an edge spans several cells
                _edgeStamp[i] = stamp;
                var (ia, ib) = ends[i];
                float ax = s.X[ia], ay = s.Y[ia], bx = s.X[ib], by = s.Y[ib];
                if (!(Math.Min(ax, bx) - NearPx <= fx && fx <= Math.Max(ax, bx) + NearPx
                      && Math.Min(ay, by) - NearPx <= fy && fy <= Math.Max(ay, by) + NearPx)) continue;
                yield return (model.Edges[i], new Vector2d(ax, ay), new Vector2d(bx, by));
            }
        }
    }

    /// <summary>Visible vertices near the cursor with their screen position.</summary>
    private IEnumerable<(Vertex V, float X, float Y)> VerticesNear(InferenceQuery q)
    {
        var s = Screen(q);
        var vertices = Model.Vertices;
        float fx = (float)q.X, fy = (float)q.Y;
        foreach (var grid in s.Grids)
        foreach (var cell in s.CellsAround(fx, fy, NearPx))
        {
            for (var k = grid.VertexStart[cell]; k < grid.VertexStart[cell + 1]; k++)
            {
                var i = grid.VertexItems[k];
                if (Math.Abs(s.X[i] - fx) <= NearPx && Math.Abs(s.Y[i] - fy) <= NearPx) yield return (vertices[i], s.X[i], s.Y[i]);
            }
        }
    }

    /// <summary>
    /// Visible edges whose midpoint is near the cursor, with the midpoint's screen position (parallel projection).
    /// An edge's midpoint lies inside its screen box, so the edge cells around the cursor hold every candidate.
    /// </summary>
    private IEnumerable<(Edge E, float X, float Y)> MidpointEdgesNear(InferenceQuery q)
    {
        var s = Screen(q);
        var model = Model;
        var ends = model.EdgeEnds;
        float fx = (float)q.X, fy = (float)q.Y;
        if (_edgeStamp.Length < ends.Length) _edgeStamp = new int[ends.Length];
        var stamp = ++_stamp;
        foreach (var grid in s.Grids)
        foreach (var cell in s.CellsAround(fx, fy, NearPx))
        {
            for (var k = grid.EdgeStart[cell]; k < grid.EdgeStart[cell + 1]; k++)
            {
                var i = grid.EdgeItems[k];
                if (_edgeStamp[i] == stamp) continue;
                _edgeStamp[i] = stamp;
                var (ia, ib) = ends[i];
                float mx = (s.X[ia] + s.X[ib]) * 0.5f, my = (s.Y[ia] + s.Y[ib]) * 0.5f;
                if (Math.Abs(mx - fx) <= NearPx && Math.Abs(my - fy) <= NearPx) yield return (model.Edges[i], mx, my);
            }
        }
    }

    /// <summary>
    /// The best few of many candidates (lowest score), kept without sorting them all: a max-heap of
    /// MaxOcclusionTests entries. Ordered lists them best first.
    /// </summary>
    private sealed class BestCandidates<T>
    {
        private readonly PriorityQueue<T, double> _heap = new(); // priority = -score: the worst kept one is on top

        public void Offer(double score, T item)
        {
            if (_heap.Count < MaxOcclusionTests) _heap.Enqueue(item, -score);
            else if (_heap.TryPeek(out _, out var worst) && score < -worst) _heap.DequeueEnqueue(item, -score);
        }

        public List<T> Ordered()
        {
            var list = new List<(T Item, double Score)>(_heap.Count);
            while (_heap.TryDequeue(out var item, out var p)) list.Add((item, -p));
            list.Sort((a, b) => a.Score.CompareTo(b.Score));
            return list.Select(x => x.Item).ToList();
        }
    }
}
