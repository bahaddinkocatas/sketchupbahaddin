using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    private readonly List<Guide> _guides = new();

    public IReadOnlyList<Guide> Guides => _guides;

    public IEnumerable<Guide> AllGuides() => _guides.Concat(_groups.SelectMany(g => g.Entities.AllGuides()));

    public Guide AddGuideLine(Vector3d point, Vector3d direction)
    {
        var g = new Guide { Id = Context.NextId(), Point = point, Direction = direction.Normalized(), Parent = this, Tag = NewEntityTag() };
        _guides.Add(g);
        RaiseChanged();
        return g;
    }

    public Guide AddGuidePoint(Vector3d point)
    {
        var g = new Guide { Id = Context.NextId(), Point = point, Direction = Vector3d.Zero, Parent = this, Tag = NewEntityTag() };
        _guides.Add(g);
        RaiseChanged();
        return g;
    }

    /// <summary>Removes every guide in this container and nested groups (Edit &gt; Delete Guides).</summary>
    public void DeleteAllGuides()
    {
        foreach (var g in _guides) g.IsDeleted = true;
        _guides.Clear();
        foreach (var grp in _groups) grp.Entities.DeleteAllGuides();
        RaiseChanged();
    }

    private bool RemoveGuide(Guide g)
    {
        if (!_guides.Remove(g)) return false;
        g.IsDeleted = true;
        return true;
    }

    /// <summary>
    /// Applies a transformation to vertices (rotate, scale, mirror). Faces whose vertices all moved keep their
    /// front side outward (a mirroring transform reverses their loops); faces that become non-planar are folded.
    /// Guides in the transformed set (by id) are transformed too.
    /// </summary>
    public void TransformVertices(IReadOnlyCollection<Vertex> vertices, Matrix4d transform, IEnumerable<Guide>? guides = null)
    {
        if (vertices.Count == 0 && guides is null) return;
        var set = vertices.ToHashSet();
        var faces = vertices.SelectMany(v => v.EdgeList).SelectMany(e => e.FaceList).Distinct().ToList();
        var mirror = Determinant3(transform) < 0;

        foreach (var v in vertices) v.Position = transform.TransformPoint(v.Position);
        foreach (var g in guides ?? Array.Empty<Guide>())
        {
            g.Point = transform.TransformPoint(g.Point);
            if (!g.IsPoint) g.Direction = transform.TransformVector(g.Direction).Normalized();
        }

        foreach (var f in faces)
        {
            if (f.IsDeleted) continue;
            var whole = f.Vertices.All(set.Contains);
            if (whole && mirror)
            {
                // A mirrored loop runs the other way around; reverse it so the face still faces outward.
                f.OuterLoop.List.Reverse();
                foreach (var l in f.InnerList) l.List.Reverse();
            }
            var newell = NewellNormal(f.OuterLoop.List);
            if (newell.IsZero(1e-12))
            {
                RemoveFace(f);
                continue;
            }
            f.Normal = newell.Normalized();
            if (!IsPlanar(f)) FoldIntoTriangles(f);
        }
        ApplyGroundRule(faces);
        RaiseChanged();
    }

    /// <summary>
    /// SketchUp's convention: a loose face lying on the ground (not part of a solid) shows its back side up.
    /// Keeps faces that were mirrored or rotated onto the ground from turning white.
    /// </summary>
    public static void ApplyGroundRule(IEnumerable<Face> faces)
    {
        foreach (var f in faces)
        {
            if (f.IsDeleted || f.Normal.Z < 1 - 1e-9) continue;
            if (f.Vertices.Any(v => Math.Abs(v.Position.Z) > Tolerance.Length)) continue;
            var loose = f.Edges.All(e => e.Faces.All(g => Math.Abs(Math.Abs(g.Normal.Z) - 1) < 1e-9));
            if (loose) f.Parent!.Reverse(f);
        }
    }

    private static double Determinant3(Matrix4d m) =>
        m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
        - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
        + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
}
