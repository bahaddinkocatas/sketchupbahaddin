using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Operations;

public static class MoveCopy
{
    /// <summary>Moves the selected geometry; connected geometry stretches (auto-fold keeps faces planar).</summary>
    public static void Move(Entities entities, IEnumerable<Entity> selection, Vector3d delta) =>
        Transform(entities, selection, Matrix4d.Translation(delta));

    /// <summary>Applies a transformation (move, rotate, scale) to the selected geometry, groups and guides.</summary>
    public static void Transform(Entities entities, IEnumerable<Entity> selection, Matrix4d transform)
    {
        var items = selection.ToList();
        var vertices = Entities.VerticesOf(items);
        var guides = items.OfType<Guide>().Concat(items.OfType<Group>().SelectMany(g => g.Entities.AllGuides())).ToList();
        entities.TransformVertices(vertices, transform, guides);
        entities.TransformAnnotations(items.Where(i => i is Dimension or Note or ImageEntity or Group), transform);
    }

    /// <summary>
    /// Copies the selected geometry by <paramref name="delta"/>, <paramref name="count"/> times (array copies).
    /// Copies merge with existing geometry like any new edges. Returns the copied entities of the last copy.
    /// </summary>
    public static List<Entity> Copy(Entities entities, IEnumerable<Entity> selection, Vector3d delta, int count = 1) =>
        CopyTransformed(entities, selection, Matrix4d.Translation(delta), count);

    /// <summary>
    /// Copies the selection with a transformation applied: copy i gets <paramref name="step"/> applied i times
    /// (so a rotation gives a polar array). Returns the entities of the last copy.
    /// </summary>
    public static List<Entity> CopyTransformed(Entities entities, IEnumerable<Entity> selection, Matrix4d step, int count = 1)
    {
        var items = selection.ToList();
        var edges = items.OfType<Edge>()
            .Concat(items.OfType<Face>().SelectMany(f => f.Edges))
            .Distinct()
            .Select(e => (A: e.Start.Position, B: e.End.Position))
            .ToList();
        var faces = items.OfType<Face>()
            .Select(f => (Probe: InteriorProbe(f), f.Normal, f.Material, f.Tag))
            .ToList();
        var groups = items.OfType<Group>().ToList();
        var guides = items.OfType<Guide>().ToList();

        var result = new List<Entity>();
        var t = Matrix4d.Identity;
        for (var i = 1; i <= count; i++)
        {
            t = step * t;
            var lastIdBefore = entities.LastId;
            foreach (var (a, b) in edges) entities.AddEdge(t.TransformPoint(a), t.TransformPoint(b));

            // Copied faces keep the orientation, material and tag of their originals.
            foreach (var (probe, normal, material, tag) in faces)
            {
                if (probe is null) continue;
                var p = t.TransformPoint(probe.Value);
                var copy = entities.Faces.FirstOrDefault(f => f.Id > lastIdBefore && f.ContainsPoint(p));
                if (copy is null) continue;
                if (Vector3d.Dot(copy.Normal, t.TransformVector(normal)) < 0) entities.Reverse(copy);
                copy.Material = material;
                copy.Tag = tag;
            }

            var groupCopies = new List<Group>();
            foreach (var g in groups)
            {
                var copy = entities.DuplicateGroup(g, Vector3d.Zero);
                copy.Entities.TransformVertices(copy.Entities.AllVertices().ToList(), t, copy.Entities.AllGuides().ToList());
                groupCopies.Add(copy);
            }

            var imageCopies = items.OfType<ImageEntity>().Select(img => entities.AddImage(img.ImageKey,
                t.TransformPoint(img.Origin), t.TransformVector(img.U), t.TransformVector(img.V), img.Name)).ToList();

            var guideCopies = guides.Select(g => g.IsPoint
                ? entities.AddGuidePoint(t.TransformPoint(g.Point))
                : entities.AddGuideLine(t.TransformPoint(g.Point), t.TransformVector(g.Direction))).ToList();

            Entities.ApplyGroundRule(entities.Faces.Where(f => f.Id > lastIdBefore).ToList());

            if (i == count)
            {
                result.AddRange(entities.Edges.Where(e => e.Id > lastIdBefore));
                result.AddRange(entities.Faces.Where(f => f.Id > lastIdBefore));
                result.AddRange(groupCopies);
                result.AddRange(guideCopies);
                result.AddRange(imageCopies);
            }
        }
        return result;
    }

    /// <summary>A point strictly inside a face (used to find its copy).</summary>
    public static Vector3d? InteriorProbe(Face f)
    {
        var loop = f.OuterLoop.Vertices;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i].Position;
            var b = loop[(i + 1) % loop.Count].Position;
            var edge = b - a;
            var inward = Vector3d.Cross(f.Normal, edge).Normalized(); // left of a CCW edge
            var step = Math.Min(edge.Length * 0.25, 0.01);
            var q = (a + b) / 2 + inward * step;
            if (f.ContainsPoint(q)) return q;
        }
        return null;
    }
}
