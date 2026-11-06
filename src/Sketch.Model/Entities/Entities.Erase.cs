using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    /// <summary>
    /// Erases edges and faces. Erasing an edge removes the faces it bounds, except that an edge between two
    /// coplanar faces heals them into one face. Erasing only a face keeps its edges.
    /// </summary>
    public void Erase(IEnumerable<Entity> items)
    {
        var list = items.ToList();
        var groups = list.OfType<Group>().Where(g => !g.IsDeleted && ReferenceEquals(g.Parent, this)).ToList();
        var edges = list.OfType<Edge>().Where(e => !e.IsDeleted && ReferenceEquals(e.Parent, this)).Distinct().ToList();
        var faces = list.OfType<Face>().Where(f => !f.IsDeleted && ReferenceEquals(f.Parent, this)).Distinct().ToHashSet();
        foreach (var g in groups) RemoveGroup(g);
        var guidesRemoved = list.OfType<Guide>().Count(RemoveGuide) + list.Count(RemoveAnnotation) + list.Count(RemoveImage);
        if (edges.Count == 0 && faces.Count == 0)
        {
            if (groups.Count > 0 || guidesRemoved > 0) RaiseChanged();
            return;
        }

        var healPlanes = new List<(PlaneBasis Plane, List<FillReference> Refs)>();
        var touched = new HashSet<Vertex>();

        foreach (var e in edges)
        {
            var fs = e.FaceList.ToList();
            if (fs.Count == 2 && !faces.Contains(fs[0]) && !faces.Contains(fs[1]) && !fs[0].IsDeleted && !fs[1].IsDeleted
                && Vector3d.Cross(fs[0].Normal, fs[1].Normal).Length < 1e-6
                && Math.Abs(fs[0].Plane.SignedDistance(fs[1].OuterLoop.List[0].Position)) <= Tolerance.Length)
            {
                var plane = new PlaneBasis(fs[0].OuterLoop.List[0].Position, fs[0].Normal);
                var refs = fs.Select(FillReference.Of).ToList();
                var existingPlane = healPlanes.FindIndex(h =>
                    Vector3d.Cross(h.Plane.Normal, plane.Normal).Length < 1e-6 && OnPlane(plane.Origin, h.Plane));
                if (existingPlane >= 0) healPlanes[existingPlane].Refs.AddRange(refs);
                else healPlanes.Add((plane, refs));
            }
            foreach (var f in fs) RemoveFace(f);
        }

        foreach (var f in faces) RemoveFace(f);

        foreach (var e in edges)
        {
            touched.Add(e.Start);
            touched.Add(e.End);
            RemoveEdgeOnly(e);
        }

        foreach (var (plane, refs) in healPlanes)
        {
            var all = refs.Concat(_faces.Where(f => FaceInPlane(f, plane)).Select(FillReference.Of)).ToList();
            RebuildPlane(plane, new HashSet<Edge>(), all);
        }

        foreach (var v in touched)
        {
            RemoveVertexIfIsolated(v);
            if (!v.IsDeleted) HealCollinear(v);
        }

        RaiseChanged();
    }

    /// <summary>Merges the two edges at a vertex when they are collinear and bound the same faces.</summary>
    private void HealCollinear(Vertex v)
    {
        if (v.EdgeList.Count != 2) return;
        var e1 = v.EdgeList[0];
        var e2 = v.EdgeList[1];
        var a = e1.Other(v);
        var b = e2.Other(v);
        if (ReferenceEquals(a, b)) return;
        if (!Segment3d.AreParallel(v.Position - a.Position, b.Position - v.Position)) return;
        if (Vector3d.Dot(v.Position - a.Position, b.Position - v.Position) <= 0) return;
        if (!e1.FaceList.ToHashSet().SetEquals(e2.FaceList)) return;
        if (FindEdge(a, b) is not null) return;

        var faces = e1.FaceList.ToList();
        var (soft, smooth, hidden, tag) = (e1.Soft, e1.Smooth, e1.Hidden, e1.Tag);
        RemoveEdgeOnly(e1);
        RemoveEdgeOnly(e2);
        var merged = Connect(a, b, out _);
        merged.Soft = soft;
        merged.Smooth = smooth;
        merged.Hidden = hidden;
        merged.Tag = tag;
        foreach (var f in faces)
        {
            foreach (var loop in f.Loops) loop.List.Remove(v);
            f.EdgeList.Remove(e1);
            f.EdgeList.Remove(e2);
            f.EdgeList.Add(merged);
            merged.FaceList.Add(f);
        }
        _vertices.Remove(v);
        v.IsDeleted = true;
    }
}
