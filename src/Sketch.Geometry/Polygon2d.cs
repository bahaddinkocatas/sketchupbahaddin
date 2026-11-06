namespace Sketch.Geometry;

public readonly record struct Vector2d(double X, double Y)
{
    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator *(Vector2d a, double s) => new(a.X * s, a.Y * s);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static double Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;
    public static double Dot(Vector2d a, Vector2d b) => a.X * b.X + a.Y * b.Y;
}

public static class Polygon2d
{
    /// <summary>Signed area; positive for counter-clockwise polygons.</summary>
    public static double SignedArea(IReadOnlyList<Vector2d> poly)
    {
        double a = 0;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % n];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>Distance from a point to a segment.</summary>
    public static double DistanceToSegment(Vector2d p, Vector2d a, Vector2d b)
    {
        var ab = b - a;
        var len2 = Vector2d.Dot(ab, ab);
        var t = len2 < 1e-24 ? 0 : Math.Clamp(Vector2d.Dot(p - a, ab) / len2, 0, 1);
        return (p - (a + ab * t)).Length;
    }

    public static bool IsOnBoundary(Vector2d p, IReadOnlyList<Vector2d> poly, double tolerance)
    {
        for (int i = 0, n = poly.Count; i < n; i++)
            if (DistanceToSegment(p, poly[i], poly[(i + 1) % n]) <= tolerance)
                return true;
        return false;
    }

    /// <summary>Even-odd point-in-polygon test (boundary points give an arbitrary answer).</summary>
    public static bool Contains(IReadOnlyList<Vector2d> poly, Vector2d p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Strictly inside: inside and farther than <paramref name="tolerance"/> from the boundary.</summary>
    public static bool ContainsStrict(IReadOnlyList<Vector2d> poly, Vector2d p, double tolerance) =>
        !IsOnBoundary(p, poly, tolerance) && Contains(poly, p);
}

/// <summary>Orthonormal 2D coordinate system on a plane.</summary>
public readonly struct PlaneBasis
{
    public Vector3d Origin { get; }
    public Vector3d Normal { get; }
    public Vector3d U { get; }
    public Vector3d V { get; }

    public PlaneBasis(Vector3d origin, Vector3d normal)
    {
        Origin = origin;
        Normal = normal.Normalized();
        U = Normal.AnyPerpendicular();
        V = Vector3d.Cross(Normal, U);
    }

    public Vector2d ToLocal(Vector3d p)
    {
        var d = p - Origin;
        return new Vector2d(Vector3d.Dot(d, U), Vector3d.Dot(d, V));
    }

    public Vector3d ToWorld(Vector2d p) => Origin + U * p.X + V * p.Y;
}

public static class Segment3d
{
    /// <summary>Closest points between segments p0-p1 and q0-q1 as parameters s, t in [0, 1].</summary>
    public static (double S, double T, double Distance) ClosestParameters(Vector3d p0, Vector3d p1, Vector3d q0, Vector3d q1)
    {
        var d1 = p1 - p0;
        var d2 = q1 - q0;
        var r = p0 - q0;
        var a = Vector3d.Dot(d1, d1);
        var e = Vector3d.Dot(d2, d2);
        var f = Vector3d.Dot(d2, r);
        double s, t;
        if (a <= 1e-24 && e <= 1e-24) return (0, 0, r.Length);
        if (a <= 1e-24)
        {
            s = 0;
            t = Math.Clamp(f / e, 0, 1);
        }
        else
        {
            var c = Vector3d.Dot(d1, r);
            if (e <= 1e-24)
            {
                t = 0;
                s = Math.Clamp(-c / a, 0, 1);
            }
            else
            {
                var b = Vector3d.Dot(d1, d2);
                var denom = a * e - b * b;
                s = denom > 1e-18 ? Math.Clamp((b * f - c * e) / denom, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0)
                {
                    t = 0;
                    s = Math.Clamp(-c / a, 0, 1);
                }
                else if (t > 1)
                {
                    t = 1;
                    s = Math.Clamp((b - c) / a, 0, 1);
                }
            }
        }
        var cp = p0 + d1 * s;
        var cq = q0 + d2 * t;
        return (s, t, (cp - cq).Length);
    }

    /// <summary>Parameter of the projection of p onto the line a-b, and the distance from p to the segment.</summary>
    public static (double T, double Distance) ProjectPoint(Vector3d p, Vector3d a, Vector3d b)
    {
        var ab = b - a;
        var len2 = ab.LengthSquared;
        if (len2 < 1e-24) return (0, (p - a).Length);
        var t = Vector3d.Dot(p - a, ab) / len2;
        var closest = a + ab * Math.Clamp(t, 0, 1);
        return (t, (p - closest).Length);
    }

    public static bool AreParallel(Vector3d d1, Vector3d d2) =>
        Vector3d.Cross(d1.Normalized(), d2.Normalized()).Length < 1e-9;
}
