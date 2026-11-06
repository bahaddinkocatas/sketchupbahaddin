using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Operations;

/// <summary>
/// 3D Text: turns flattened glyph outlines (closed loops) into faces, optionally extruded, inside a new group.
/// </summary>
public static class Text3D
{
    /// <summary>
    /// Creates the text in <paramref name="entities"/>. Loops are in world space on one plane with
    /// <paramref name="normal"/>; holes (letter counters) are found by nesting. Returns the new group.
    /// </summary>
    public static Group Create(Entities entities, IReadOnlyList<IReadOnlyList<Vector3d>> loops, Vector3d normal,
        bool filled, double extrude, string name)
    {
        // Built inside a new (empty) group so the letters never stick to existing geometry.
        var group = entities.MakeGroup(Array.Empty<Entity>(), name);
        var content = group.Entities;
        foreach (var loop in loops.Where(l => l.Count >= 3))
            content.AddPolyline(loop, closed: true);

        if (!filled)
        {
            foreach (var f in content.Faces.ToList()) content.Erase(new Entity[] { f });
            return group;
        }

        // Face up toward the reader; counters (holes) must stay open, so faces inside another face's hole are removed.
        foreach (var f in content.Faces.ToList())
        {
            if (Vector3d.Dot(f.Normal, normal) < 0) content.Reverse(f);
        }
        foreach (var f in content.Faces.Where(f => IsCounter(f, content)).ToList()) content.Erase(new Entity[] { f });

        if (extrude > Tolerance.Length)
        {
            foreach (var f in content.Faces.ToList())
                if (!f.IsDeleted) PushPull.Execute(content, f, extrude);
        }
        return group;
    }

    /// <summary>A face lying in another face's hole is a letter counter (e.g. the inside of "o").</summary>
    private static bool IsCounter(Face f, Entities content) =>
        content.Faces.Any(other => !ReferenceEquals(other, f) && other.InnerLoops.Any(l =>
            l.Vertices.Count == f.OuterLoop.Vertices.Count && l.Vertices.All(v => f.OuterLoop.Vertices.Contains(v))));
}
