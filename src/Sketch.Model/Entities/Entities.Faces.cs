using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    /// <summary>A face region that should stay filled after a plane is rebuilt (an existing or just-removed face).</summary>
    private sealed class FillReference
    {
        public required List<Vector3d> Outer { get; init; }
        public required List<List<Vector3d>> Holes { get; init; }
        public required Vector3d Normal { get; init; }
        public Face? Face { get; init; }
        public Material? Material { get; init; }
        public Tag? Tag { get; init; }
        public bool Hidden { get; init; }

        public static FillReference Of(Face f) => new()
        {
            Outer = f.OuterLoop.List.Select(v => v.Position).ToList(),
            Holes = f.InnerList.Select(l => l.List.Select(v => v.Position).ToList()).ToList(),
            Normal = f.Normal,
            Face = f,
            Material = f.Material,
            Tag = f.Tag,
            Hidden = f.Hidden,
        };

        public bool Contains(PlaneBasis basis, Vector2d q)
        {
            var outer = Outer.Select(basis.ToLocal).ToList();
            if (!Polygon2d.Contains(outer, q)) return false;
            return !Holes.Any(h => Polygon2d.Contains(h.Select(basis.ToLocal).ToList(), q));
        }
    }

    private sealed class Region
    {
        public required List<Vertex> Outer { get; init; }
        public required double Area { get; init; }
        public List<List<Vertex>> Holes { get; } = new();
    }

    private static bool OnPlane(Vector3d p, PlaneBasis basis) =>
        Math.Abs(Vector3d.Dot(p - basis.Origin, basis.Normal)) <= Tolerance.Length;

    private static bool FaceInPlane(Face f, PlaneBasis basis) =>
        Vector3d.Cross(f.Normal, basis.Normal).Length < 1e-6 && f.Vertices.All(v => OnPlane(v.Position, basis));

    /// <summary>Planes in which the new edges may have closed or split faces.</summary>
    /// <param name="keepFilled">
    /// Regions that must end up filled, besides the faces already here (e.g. the faces of a group being exploded).
    /// </param>
    private void UpdateFacesForNewEdges(IReadOnlyList<Edge> newEdges, IReadOnlyList<FillReference>? keepFilled = null)
    {
        var planes = new List<PlaneBasis>();

        void Consider(Vector3d origin, Vector3d normal)
        {
            normal = normal.Normalized();
            if (normal.IsZero(1e-12)) return;
            foreach (var p in planes)
                if (Vector3d.Cross(p.Normal, normal).Length < 1e-6 && OnPlane(origin, p))
                    return;
            planes.Add(new PlaneBasis(origin, normal));
        }

        foreach (var e in newEdges)
        {
            if (e.IsDeleted) continue;
            var a = e.Start.Position;
            var b = e.End.Position;
            var d = b - a;

            foreach (var f in _faces)
                if (Math.Abs(f.Plane.SignedDistance(a)) <= Tolerance.Length && Math.Abs(f.Plane.SignedDistance(b)) <= Tolerance.Length)
                    Consider(f.OuterLoop.List[0].Position, f.Normal);

            foreach (var v in new[] { e.Start, e.End })
            foreach (var adj in v.EdgeList)
            {
                if (ReferenceEquals(adj, e)) continue;
                var w = adj.Other(v).Position;
                var n = Vector3d.Cross(d, w - a);
                if (n.Length > Tolerance.Length * d.Length) Consider(a, n);
            }
        }

        foreach (var r in keepFilled ?? Array.Empty<FillReference>())
            if (r.Outer.Count >= 3) Consider(r.Outer[0], r.Normal);

        var newSet = newEdges.Where(e => !e.IsDeleted).ToHashSet();
        foreach (var plane in planes)
        {
            var refs = _faces.Where(f => FaceInPlane(f, plane)).Select(FillReference.Of).ToList();
            if (keepFilled is not null)
                refs.AddRange(keepFilled.Where(r => Vector3d.Cross(r.Normal.Normalized(), plane.Normal).Length < 1e-6
                                                    && r.Outer.All(p => OnPlane(p, plane))));
            RebuildPlane(plane, newSet, refs);
        }
    }

    /// <summary>
    /// Recomputes the faces of one plane from its edges. Each closed region becomes a face when it lies inside a
    /// previous face (faces are split or healed) or when its boundary uses one of <paramref name="newEdges"/>
    /// (a loop was just closed). Faces whose loops did not change are kept as they are.
    /// </summary>
    private void RebuildPlane(PlaneBasis basis, IReadOnlySet<Edge> newEdges, IReadOnlyList<FillReference> references)
    {
        var planeEdges = _edges.Where(e => OnPlane(e.Start.Position, basis) && OnPlane(e.End.Position, basis)).ToList();
        var regions = FindRegions(basis, planeEdges);

        var existing = _faces.Where(f => FaceInPlane(f, basis)).ToList();
        var keep = new HashSet<Face>();
        var toCreate = new List<(Region Region, Vector3d Normal, FillReference? Reference)>();

        foreach (var region in regions)
        {
            var match = existing.FirstOrDefault(f => !keep.Contains(f) && SameLoops(f, region));
            if (match is not null)
            {
                keep.Add(match);
                continue;
            }

            var q = InteriorPoint(basis, region);
            if (q is null) continue;
            var reference = references.FirstOrDefault(r => r.Contains(basis, q.Value));
            var closesNewLoop = LoopUsesAny(region.Outer, newEdges);
            if (reference is null && !closesNewLoop) continue;

            var normal = reference is not null
                ? (Vector3d.Dot(reference.Normal, basis.Normal) >= 0 ? basis.Normal : -basis.Normal)
                : ChooseNewFaceNormal(basis, region);
            toCreate.Add((region, normal, reference));
        }

        foreach (var f in existing)
            if (!keep.Contains(f))
                RemoveFace(f);

        foreach (var (region, normal, reference) in toCreate)
        {
            // Regions are counter-clockwise around basis.Normal; flip all loops for a back-facing normal.
            var sameSide = Vector3d.Dot(normal, basis.Normal) > 0;
            var outer = sameSide ? region.Outer : Reversed(region.Outer);
            var holes = region.Holes.Select(h => (IReadOnlyList<Vertex>)(sameSide ? h : Reversed(h)));
            var face = CreateFace(outer, holes, normal);
            if (reference is not null)
            {
                // Split or healed faces keep the material and tag of the face they came from.
                face.Material = reference.Material;
                face.Tag = reference.Tag;
                face.Hidden = reference.Hidden;
            }
        }
    }

    private static List<Vertex> Reversed(List<Vertex> loop)
    {
        var r = new List<Vertex>(loop);
        r.Reverse();
        return r;
    }

    private bool LoopUsesAny(List<Vertex> loop, IReadOnlySet<Edge> edges)
    {
        if (edges.Count == 0) return false;
        for (var i = 0; i < loop.Count; i++)
        {
            var e = FindEdge(loop[i], loop[(i + 1) % loop.Count]);
            if (e is not null && edges.Contains(e)) return true;
        }
        return false;
    }

    private static bool SameLoops(Face f, Region r)
    {
        if (!SameCycle(f.OuterLoop.List, r.Outer)) return false;
        if (f.InnerList.Count != r.Holes.Count) return false;
        return f.InnerList.All(l => r.Holes.Any(h => SameCycle(l.List, h)));
    }

    private static bool SameCycle(IReadOnlyList<Vertex> a, IReadOnlyList<Vertex> b) =>
        a.Count == b.Count && a.ToHashSet().SetEquals(b);

    /// <summary>
    /// New face orientation: consistent with an adjacent non-coplanar face if there is one (shared edges are
    /// walked in opposite directions); otherwise faces on the ground face down (back side up, as in SketchUp)
    /// and other horizontal faces face up.
    /// </summary>
    private Vector3d ChooseNewFaceNormal(PlaneBasis basis, Region region)
    {
        var loop = region.Outer;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            var e = FindEdge(a, b);
            if (e is null) continue;
            foreach (var f in e.FaceList)
            {
                if (Vector3d.Cross(f.Normal, basis.Normal).Length < 1e-6) continue; // coplanar
                var fWalksAB = f.Loops.Any(l => l.Traverses(a, b));
                // Our loop walks a->b around basis.Normal; the neighbour must walk the shared edge the other way.
                return fWalksAB ? -basis.Normal : basis.Normal;
            }
        }

        var n = basis.Normal;
        if (Math.Abs(n.Z) > 1 - 1e-9)
        {
            var onGround = Math.Abs(basis.Origin.Z) <= Tolerance.Length;
            var wantUp = !onGround;
            return (n.Z > 0) == wantUp ? n : -n;
        }
        // Vertical / sloped faces with no neighbours: face toward -Y (the default front view), else +X.
        if (Math.Abs(n.Y) > 1e-9) return n.Y < 0 ? n : -n;
        return n.X > 0 ? n : -n;
    }

    /// <summary>A point strictly inside the region (inside the outer loop, outside the holes), in plane coordinates.</summary>
    private static Vector2d? InteriorPoint(PlaneBasis basis, Region region)
    {
        var outer = region.Outer.Select(v => basis.ToLocal(v.Position)).ToList();
        var holes = region.Holes.Select(h => h.Select(v => basis.ToLocal(v.Position)).ToList()).ToList();
        for (var i = 0; i < outer.Count; i++)
        {
            var a = outer[i];
            var b = outer[(i + 1) % outer.Count];
            var edge = b - a;
            var len = edge.Length;
            if (len < Tolerance.Length) continue;
            var inward = new Vector2d(-edge.Y / len, edge.X / len); // left side of a CCW loop
            foreach (var offset in new[] { 0.01, 0.001, 0.0002 })
            {
                var q = a + edge * 0.5 + inward * Math.Min(offset, len * 0.25);
                if (Polygon2d.Contains(outer, q) && !holes.Any(h => Polygon2d.Contains(h, q)))
                    return q;
            }
        }
        return null;
    }

    /// <summary>
    /// Traces all bounded regions of the planar graph formed by the edges. Bounded faces come out
    /// counter-clockwise; the outer boundary of each connected component comes out clockwise and is attached
    /// as a hole to the smallest region that contains it.
    /// </summary>
    private static List<Region> FindRegions(PlaneBasis basis, List<Edge> planeEdges)
    {
        var local = new Dictionary<Vertex, Vector2d>();
        var neighbours = new Dictionary<Vertex, List<Vertex>>();
        foreach (var e in planeEdges)
        {
            foreach (var v in new[] { e.Start, e.End })
            {
                if (!local.ContainsKey(v)) local[v] = basis.ToLocal(v.Position);
                if (!neighbours.ContainsKey(v)) neighbours[v] = new List<Vertex>();
            }
            neighbours[e.Start].Add(e.End);
            neighbours[e.End].Add(e.Start);
        }
        foreach (var (v, list) in neighbours)
        {
            var o = local[v];
            list.Sort((x, y) =>
            {
                var dx = local[x] - o;
                var dy = local[y] - o;
                return Math.Atan2(dx.Y, dx.X).CompareTo(Math.Atan2(dy.Y, dy.X));
            });
        }

        var visited = new HashSet<(Vertex, Vertex)>();
        var positive = new List<Region>();
        var negative = new List<(List<Vertex> Loop, double Area)>();
        var maxSteps = planeEdges.Count * 2 + 4;

        foreach (var e in planeEdges)
        {
            foreach (var (start, next) in new[] { (e.Start, e.End), (e.End, e.Start) })
            {
                if (visited.Contains((start, next))) continue;
                var cycle = new List<Vertex>();
                var a = start;
                var b = next;
                var steps = 0;
                while (steps++ < maxSteps)
                {
                    visited.Add((a, b));
                    cycle.Add(a);
                    // Next half-edge: at b, the neighbour just clockwise of the way back to a (keeps the region on the left).
                    var around = neighbours[b];
                    var idx = around.IndexOf(a);
                    var c = around[(idx - 1 + around.Count) % around.Count];
                    a = b;
                    b = c;
                    if (ReferenceEquals(a, start) && ReferenceEquals(b, next)) break;
                }

                var loop = StripSpikes(cycle);
                if (loop.Count < 3) continue;
                var area = Polygon2d.SignedArea(loop.Select(v => local[v]).ToList());
                var minArea = Tolerance.Length * Tolerance.Length;
                if (area > minArea) positive.Add(new Region { Outer = loop, Area = area });
                else if (area < -minArea) negative.Add((loop, area));
            }
        }

        foreach (var (hole, area) in negative)
        {
            Region? best = null;
            foreach (var region in positive)
            {
                if (region.Area <= -area + 1e-12) continue;
                if (hole.Any(v => region.Outer.Contains(v))) continue; // same component: it's that region's own boundary
                var poly = region.Outer.Select(v => local[v]).ToList();
                if (!Polygon2d.ContainsStrict(poly, local[hole[0]], Tolerance.Length)) continue;
                if (best is null || region.Area < best.Area) best = region;
            }
            best?.Holes.Add(hole);
        }
        return positive;
    }

    /// <summary>Removes back-and-forth excursions (a, b, a) caused by dangling edges attached to a loop.</summary>
    private static List<Vertex> StripSpikes(List<Vertex> cycle)
    {
        var list = new List<Vertex>(cycle);
        var changed = true;
        while (changed && list.Count >= 3)
        {
            changed = false;
            for (var i = 0; i < list.Count; i++)
            {
                var prev = list[(i - 1 + list.Count) % list.Count];
                var next = list[(i + 1) % list.Count];
                if (ReferenceEquals(prev, next))
                {
                    // Remove the spike tip and one copy of the base.
                    var tip = i;
                    var baseIdx = (i + 1) % list.Count;
                    if (baseIdx > tip)
                    {
                        list.RemoveAt(baseIdx);
                        list.RemoveAt(tip);
                    }
                    else
                    {
                        list.RemoveAt(tip);
                        list.RemoveAt(baseIdx);
                    }
                    changed = true;
                    break;
                }
            }
        }
        // Any vertex still repeated means the loop touches itself; keep it only if it is a simple cycle.
        return list.Distinct().Count() == list.Count ? list : SplitAtFirstRepeat(list);
    }

    /// <summary>For loops that pinch at a vertex, keep the largest simple sub-cycle.</summary>
    private static List<Vertex> SplitAtFirstRepeat(List<Vertex> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var j = list.IndexOf(list[i], i + 1);
            if (j < 0) continue;
            var inner = list.GetRange(i, j - i);
            var outer = list.Take(i).Concat(list.Skip(j)).ToList();
            var pick = inner.Count >= outer.Count ? inner : outer;
            return pick.Distinct().Count() == pick.Count ? pick : SplitAtFirstRepeat(pick);
        }
        return list;
    }
}
