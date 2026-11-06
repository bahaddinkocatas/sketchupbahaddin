using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Operations;

/// <summary>
/// Push/Pull: extrudes a face along its normal.
/// <list type="bullet">
/// <item>When every boundary edge is shared with a face that runs along the push direction (e.g. the top of a
/// box), the face is simply moved and its neighbours stretch.</item>
/// <item>A free-standing face becomes a closed solid; the original face stays as the cap on the far side.</item>
/// <item>A face cut out of a larger coplanar face (e.g. a rectangle drawn on a box top) is replaced by the
/// extrusion and leaves an opening in the larger face (pull = bump, push = pocket).</item>
/// </list>
/// </summary>
public static class PushPull
{
    /// <summary>Extrudes <paramref name="face"/> by <paramref name="distance"/> along its normal. Returns the moved/new face.</summary>
    public static Face? Execute(Entities entities, Face face, double distance)
    {
        if (face.IsDeleted || Math.Abs(distance) <= Tolerance.Length) return face;
        var n = face.Normal;
        var offset = n * distance;
        var edges = face.Edges.ToList();

        if (ShouldStretch(face))
        {
            entities.MoveVertices(face.Vertices.Distinct().ToList(), offset);
            return face.IsDeleted ? null : face;
        }

        var isFree = edges.All(e => e.Faces.Count == 1);
        var hasCoplanarNeighbour = edges.Any(e => e.Faces.Any(g => !ReferenceEquals(g, face) && IsCoplanar(g, face)));
        var inSolid = !isFree && IsPartOfClosedShell(face);

        var loops = face.Loops.Select(l => l.Points.ToList()).ToList();
        var lastIdBefore = entities.LastId;

        // Edges whose neighbour face contains the push direction: that neighbour stretches (or shrinks) instead
        // of getting a separate side face, as in SketchUp.
        var stretchEdges = new List<(Vector3d A, Vector3d B, Vector3d Normal, bool Into)>();
        foreach (var e in edges)
        {
            var g = e.Faces.FirstOrDefault(x => !ReferenceEquals(x, face) && !IsCoplanar(x, face)
                                                && Math.Abs(Vector3d.Dot(x.Normal, n)) < 1e-6);
            if (g is null) continue;
            var a = e.Start.Position;
            var b = e.End.Position;
            var quadCenter = (a + b) / 2 + offset / 2;
            // "Into" only makes sense for solids: an open neighbour surface has no material to cut away.
            stretchEdges.Add((a, b, g.Normal, inSolid && g.ContainsPoint(quadCenter)));
        }

        if (hasCoplanarNeighbour || inSolid)
        {
            // The face's area ends up inside the solid (or becomes the opening of a pocket): remove it.
            entities.Erase(new Entity[] { face });
        }
        else if (distance > 0)
        {
            // The original face stays as the cap opposite the extrusion and must face away from it.
            entities.Reverse(face);
        }

        // Side edges first, then the offset loops: closing each loop forms the side and top faces.
        foreach (var loop in loops)
            foreach (var p in loop)
                entities.AddEdge(p, p + offset);
        foreach (var loop in loops)
            entities.AddPolyline(loop.Select(p => p + offset).ToList(), closed: true);

        foreach (var (a, b, normal, into) in stretchEdges)
        {
            var va = entities.FindVertex(a);
            var vb = entities.FindVertex(b);
            var oldEdge = va is null || vb is null ? null : entities.FindEdge(va, vb);
            if (oldEdge is null) continue;
            if (into)
            {
                // Pushed into the neighbour: the swept strip is cut away from it.
                var center = (a + b) / 2 + offset / 2;
                var strip = entities.Faces.FirstOrDefault(f => Vector3d.Cross(f.Normal, normal).Length < 1e-6 && f.ContainsPoint(center));
                entities.Erase(strip is null ? new Entity[] { oldEdge } : new Entity[] { strip, oldEdge });
            }
            else if (oldEdge.Faces.Count == 2 && IsCoplanar(oldEdge.Faces[0], oldEdge.Faces[1]))
            {
                // Pulled out: the strip is coplanar with the neighbour; erasing the old edge merges them.
                entities.Erase(new Entity[] { oldEdge });
            }
        }

        // Holes stay open: closing an offset inner loop must not put a lid over the hole.
        foreach (var hole in loops.Skip(1).Select(l => l.Select(p => p + offset).ToList()))
        {
            var lid = entities.Faces.FirstOrDefault(f => f.Id > lastIdBefore && f.InnerLoops.Count == 0
                && f.OuterLoop.Vertices.Count == hole.Count
                && f.OuterLoop.Vertices.All(v => hole.Any(h => h.IsEqual(v.Position))));
            if (lid is not null) entities.Erase(new Entity[] { lid });
        }

        var created = entities.Faces.Where(f => f.Id > lastIdBefore).ToHashSet();
        entities.OrientConsistently(created);

        var probe = loops[0].Aggregate(Vector3d.Zero, (s, p) => s + p) / loops[0].Count + offset;
        // Orientation comes from consistency with the neighbouring faces; no face is flipped on its own here.
        return entities.Faces.FirstOrDefault(f => created.Contains(f) && f.ContainsPoint(probe));
    }

    /// <summary>True when the face belongs to a connected set of faces in which every edge has exactly two faces.</summary>
    public static bool IsPartOfClosedShell(Face face)
    {
        var seen = new HashSet<Face> { face };
        var queue = new Queue<Face>();
        queue.Enqueue(face);
        while (queue.Count > 0)
        {
            foreach (var e in queue.Dequeue().Edges)
            {
                if (e.Faces.Count != 2) return false;
                foreach (var g in e.Faces)
                    if (seen.Add(g)) queue.Enqueue(g);
            }
        }
        return true;
    }

    /// <summary>True when every boundary edge has another face whose plane contains the push direction.</summary>
    public static bool ShouldStretch(Face face)
    {
        var n = face.Normal;
        foreach (var e in face.Edges)
        {
            var others = e.Faces.Where(g => !ReferenceEquals(g, face)).ToList();
            if (others.Count == 0) return false;
            if (!others.Any(g => Math.Abs(Vector3d.Dot(g.Normal, n)) < 1e-6)) return false;
        }
        return true;
    }

    private static bool IsCoplanar(Face a, Face b) =>
        Vector3d.Cross(a.Normal, b.Normal).Length < 1e-6
        && Math.Abs(a.Plane.SignedDistance(b.OuterLoop.Vertices[0].Position)) <= Tolerance.Length;
}
