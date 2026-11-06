using Sketch.Geometry;

namespace Sketch.Operations;

/// <summary>Point generators for curve tools.</summary>
public static class Shapes
{
    /// <summary>
    /// Points of a circular arc through <paramref name="start"/> and <paramref name="end"/> whose midpoint is
    /// <paramref name="bulge"/> away from the chord (signed, along <paramref name="bulgeDir"/>).
    /// Returns segments+1 points from start to end; a near-zero bulge gives a straight segment.
    /// </summary>
    public static List<Vector3d> ArcByBulge(Vector3d start, Vector3d end, Vector3d bulgeDir, double bulge, int segments)
    {
        var chord = end - start;
        var h = chord.Length / 2;
        if (h < Tolerance.Length || Math.Abs(bulge) < Tolerance.Length) return new List<Vector3d> { start, end };

        // 2D frame: x along the chord, y toward the bulge (perpendicular to the chord); origin at the midpoint.
        var x = chord / (2 * h);
        var y = bulgeDir - x * Vector3d.Dot(bulgeDir, x);
        if (y.IsZero(1e-12)) return new List<Vector3d> { start, end };
        y = y.Normalized();
        var mid = (start + end) / 2;
        Vector3d World(double px, double py) => mid + x * px + y * py;

        // Circle through (-h,0), (h,0) and (0,s): centre (0,c).
        var s = bulge;
        var c = (s * s - h * h) / (2 * s);
        var r = Math.Abs(s - c);
        var a0 = Math.Atan2(-c, -h);
        var a1 = Math.Atan2(-c, h);
        // Sweep from a0 to a1 on the side containing the bulge point (angle +90° or -90°).
        var sweep = a1 - a0;
        var bulgeAngle = Math.Atan2(s - c, 0);
        static bool Contains(double from, double delta, double angle)
        {
            // The angle may be off by whole turns; try them.
            for (var k = -2; k <= 2; k++)
            {
                var t = (angle + 2 * Math.PI * k - from) / delta;
                if (t is >= 0 and <= 1) return true;
            }
            return false;
        }
        if (!Contains(a0, sweep, bulgeAngle)) sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;

        segments = Math.Max(segments, 1);
        var pts = new List<Vector3d>(segments + 1);
        for (var i = 0; i <= segments; i++)
        {
            var a = a0 + sweep * i / segments;
            pts.Add(World(r * Math.Cos(a), c + r * Math.Sin(a)));
        }
        pts[0] = start;
        pts[^1] = end;
        return pts;
    }

    /// <summary>
    /// Points of an arc around <paramref name="center"/> starting at <paramref name="start"/>, sweeping
    /// <paramref name="sweep"/> radians counter-clockwise about <paramref name="normal"/> (negative = clockwise).
    /// </summary>
    public static List<Vector3d> ArcAround(Vector3d center, Vector3d normal, Vector3d start, double sweep, int segments)
    {
        normal = normal.Normalized();
        var r = start - center;
        var u = r;
        var v = Vector3d.Cross(normal, u);
        segments = Math.Max(segments, 1);
        var pts = new List<Vector3d>(segments + 1);
        for (var i = 0; i <= segments; i++)
        {
            var a = sweep * i / segments;
            pts.Add(center + u * Math.Cos(a) + v * Math.Sin(a));
        }
        pts[0] = start;
        return pts;
    }

    /// <summary>Signed angle from <paramref name="from"/> to <paramref name="to"/> about <paramref name="normal"/>, in (-π, π].</summary>
    public static double SignedAngle(Vector3d from, Vector3d to, Vector3d normal) =>
        Math.Atan2(Vector3d.Dot(Vector3d.Cross(from, to), normal.Normalized()), Vector3d.Dot(from, to));

    /// <summary>Circle centre through three points, or null when they are (nearly) collinear.</summary>
    public static Vector3d? Circumcenter(Vector3d a, Vector3d b, Vector3d c)
    {
        var ab = b - a;
        var ac = c - a;
        var n = Vector3d.Cross(ab, ac);
        var n2 = Vector3d.Dot(n, n);
        if (n2 < 1e-12 * Math.Max(1, ab.Length * ab.Length * ac.Length * ac.Length)) return null;
        var toCenter = (Vector3d.Cross(n, ab) * Vector3d.Dot(ac, ac) + Vector3d.Cross(ac, n) * Vector3d.Dot(ab, ab)) / (2 * n2);
        return a + toCenter;
    }

    /// <summary>Arc from <paramref name="a"/> through <paramref name="b"/> to <paramref name="c"/> (null when collinear).</summary>
    public static List<Vector3d>? ArcThrough(Vector3d a, Vector3d b, Vector3d c, int segments)
    {
        if (Circumcenter(a, b, c) is not { } center) return null;
        var normal = Vector3d.Cross(b - a, c - a).Normalized();
        // Measured about this normal the path a → b → c runs counter-clockwise.
        var toC = SignedAngle(a - center, c - center, normal);
        if (toC <= 0) toC += 2 * Math.PI;
        var pts = ArcAround(center, normal, a, toC, segments);
        pts[^1] = c;
        return pts;
    }

    /// <summary>Regular polygon (closed, no repeated point) with the first vertex toward <paramref name="toward"/>.</summary>
    public static List<Vector3d> RegularPolygon(Vector3d center, Vector3d normal, Vector3d toward, double radius, int sides)
    {
        normal = normal.Normalized();
        var start = toward - center;
        start -= normal * Vector3d.Dot(start, normal);
        var u = start.IsZero(1e-9) ? normal.AnyPerpendicular() : start.Normalized();
        var v = Vector3d.Cross(normal, u);
        var pts = new List<Vector3d>(sides);
        for (var i = 0; i < sides; i++)
        {
            var a = 2 * Math.PI * i / sides;
            pts.Add(center + (u * Math.Cos(a) + v * Math.Sin(a)) * radius);
        }
        return pts;
    }
}
