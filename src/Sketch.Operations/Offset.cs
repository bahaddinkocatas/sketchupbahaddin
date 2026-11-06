using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Operations;

/// <summary>Offset (F): a copy of a face's outer loop moved inward (positive) or outward (negative).</summary>
public static class Offset
{
    /// <summary>The offset loop of a polygon (counter-clockwise around <paramref name="normal"/>), or null if it collapses.</summary>
    public static List<Vector3d>? OffsetLoop(IReadOnlyList<Vector3d> loop, Vector3d normal, double distance)
    {
        var n = loop.Count;
        if (n < 3) return null;
        normal = normal.Normalized();
        var basis = new PlaneBasis(loop[0], normal);
        var pts = loop.Select(basis.ToLocal).ToList();
        var ccw = Polygon2d.SignedArea(pts) > 0;

        // Offset every edge line along its inward normal, then intersect neighbouring lines (mitred corners).
        var lines = new List<(Vector2d P, Vector2d D)>();
        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var d = b - a;
            var len = d.Length;
            if (len < Tolerance.Length) continue;
            d = d * (1 / len);
            var inward = ccw ? new Vector2d(-d.Y, d.X) : new Vector2d(d.Y, -d.X);
            lines.Add((a + inward * distance, d));
        }
        if (lines.Count < 3) return null;

        var result = new List<Vector2d>();
        for (var i = 0; i < lines.Count; i++)
        {
            var (p1, d1) = lines[(i - 1 + lines.Count) % lines.Count];
            var (p2, d2) = lines[i];
            var cross = Vector2d.Cross(d1, d2);
            if (Math.Abs(cross) < 1e-12)
            {
                result.Add(p2); // collinear neighbours: the start of this offset edge
                continue;
            }
            var t = Vector2d.Cross(p2 - p1, d2) / cross;
            result.Add(p1 + d1 * t);
        }

        // A loop that flips orientation, or whose edges reverse direction, has collapsed (offset too large).
        var area = Polygon2d.SignedArea(result);
        if (Math.Abs(area) < Tolerance.Length * Tolerance.Length || (area > 0) != ccw) return null;
        for (var i = 0; i < lines.Count; i++)
        {
            var edge = result[(i + 1) % result.Count] - result[i];
            if (Vector2d.Dot(edge, lines[i].D) <= 0) return null;
        }
        return result.Select(basis.ToWorld).ToList();
    }

    /// <summary>Offsets a face's outer loop and adds it to the container (splitting the face). Returns the new edges.</summary>
    public static IReadOnlyList<Edge> Execute(Entities entities, Face face, double distance)
    {
        var loop = face.OuterLoop.Vertices.Select(v => v.Position).ToList();
        var offset = OffsetLoop(loop, face.Normal, distance);
        return offset is null ? Array.Empty<Edge>() : entities.AddPolyline(offset, closed: true);
    }

    /// <summary>Signed offset distance for a cursor point: positive inside the face, negative outside.</summary>
    public static double DistanceFromBoundary(Face face, Vector3d point)
    {
        var basis = new PlaneBasis(face.OuterLoop.Vertices[0].Position, face.Normal);
        var poly = face.OuterLoop.Vertices.Select(v => basis.ToLocal(v.Position)).ToList();
        var q = basis.ToLocal(point);
        var d = double.MaxValue;
        for (var i = 0; i < poly.Count; i++)
            d = Math.Min(d, Polygon2d.DistanceToSegment(q, poly[i], poly[(i + 1) % poly.Count]));
        return Polygon2d.Contains(poly, q) ? d : -d;
    }
}
