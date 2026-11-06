using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Operations;

/// <summary>Follow Me: sweeps a face (profile) along a path of connected edges.</summary>
public static class FollowMe
{
    /// <summary>
    /// Orders connected edges into a path of points. Returns null when the edges branch or are disconnected.
    /// <paramref name="closed"/> reports a loop.
    /// </summary>
    public static List<Vector3d>? OrderPath(IReadOnlyCollection<Edge> edges, out bool closed)
    {
        closed = false;
        if (edges.Count == 0) return null;
        var adjacency = new Dictionary<Vertex, List<Edge>>();
        foreach (var e in edges)
        {
            foreach (var v in new[] { e.Start, e.End })
            {
                if (!adjacency.TryGetValue(v, out var list)) adjacency[v] = list = new List<Edge>();
                list.Add(e);
            }
        }
        if (adjacency.Values.Any(l => l.Count > 2)) return null;
        var ends = adjacency.Where(kv => kv.Value.Count == 1).Select(kv => kv.Key).ToList();
        if (ends.Count != 0 && ends.Count != 2) return null;
        closed = ends.Count == 0;

        var start = closed ? edges.First().Start : ends[0];
        var path = new List<Vector3d> { start.Position };
        var used = new HashSet<Edge>();
        var current = start;
        while (true)
        {
            var next = adjacency[current].FirstOrDefault(e => !used.Contains(e));
            if (next is null) break;
            used.Add(next);
            current = next.Other(current);
            if (closed && ReferenceEquals(current, start)) break;
            path.Add(current.Position);
        }
        return used.Count == edges.Count ? path : null;
    }

    /// <summary>
    /// Sweeps the profile face along the path. The path should start at (or near) the profile; if its far end is
    /// closer, the path is reversed. The profile face stays as the start cap (unless the path is closed).
    /// </summary>
    public static bool Execute(Entities entities, Face profile, IReadOnlyList<Vector3d> path, bool closed)
    {
        if (path.Count < 2) return false;
        var loop = profile.OuterLoop.Vertices.Select(v => v.Position).ToList();
        var centroid = loop.Aggregate(Vector3d.Zero, (s, p) => s + p) / loop.Count;
        var pts = path.ToList();
        if (!closed && Vector3d.Distance(pts[^1], centroid) < Vector3d.Distance(pts[0], centroid)) pts.Reverse();

        // Rings of the profile at every path vertex: carry each profile point along the segment direction and
        // stop on the mitre plane (bisecting the turn) at the next vertex.
        var rings = new List<List<Vector3d>> { loop };
        var count = closed ? pts.Count : pts.Count - 1;
        for (var i = 1; i <= count; i++)
        {
            var prev = pts[i - 1];
            var cur = pts[i % pts.Count];
            var dirIn = (cur - prev).Normalized();
            Vector3d mitreNormal;
            var hasNext = closed || i < pts.Count - 1;
            if (hasNext)
            {
                var dirOut = (pts[(i + 1) % pts.Count] - cur).Normalized();
                mitreNormal = (dirIn + dirOut).Normalized();
                if (mitreNormal.IsZero(1e-9)) mitreNormal = dirIn;
            }
            else
            {
                mitreNormal = dirIn;
            }
            var plane = new Plane3d(cur, mitreNormal);
            var ring = new List<Vector3d>();
            foreach (var p in rings[^1])
            {
                var t = plane.Intersect(new Ray3d(p, dirIn));
                ring.Add(t is null ? p + (cur - prev) : p + dirIn * t.Value);
            }
            rings.Add(ring);
        }

        // The profile stays as the start cap of an open sweep and must face away from the sweep.
        if (!closed && Vector3d.Dot(profile.Normal, pts[1] - pts[0]) > 0) entities.Reverse(profile);

        var lastIdBefore = entities.LastId;
        var m = loop.Count;
        for (var r = 0; r + 1 < rings.Count; r++)
        {
            var a = rings[r];
            var b = rings[r + 1];
            for (var k = 0; k < m; k++)
            {
                var quad = new[] { a[k], a[(k + 1) % m], b[(k + 1) % m], b[k] };
                entities.AddPolyline(quad, closed: true);
            }
        }
        if (!closed) entities.AddPolyline(rings[^1], closed: true);

        // Closing the side quads also closes every intermediate profile ring, which the sticky geometry turns
        // into a face across the inside of the solid. Remove those cross-sections.
        var lastInner = closed ? rings.Count - 1 : rings.Count - 2;
        for (var r = 1; r <= lastInner; r++)
        {
            var ring = rings[r];
            var section = entities.Faces.FirstOrDefault(f => f.Id > lastIdBefore && f.InnerLoops.Count == 0
                && f.OuterLoop.Vertices.Count == ring.Count
                && f.OuterLoop.Vertices.All(v => ring.Any(p => p.IsEqual(v.Position))));
            if (section is not null) entities.Erase(new Entity[] { section });
        }

        var created = entities.Faces.Where(f => f.Id > lastIdBefore).ToHashSet();
        if (closed && !profile.IsDeleted)
        {
            // A closed sweep ends where it started: the profile is now inside the solid.
            entities.Erase(new Entity[] { profile });
        }
        entities.OrientConsistently(created);
        if (closed) entities.EnsureOutward(created);
        return true;
    }
}
