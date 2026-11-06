namespace Sketch.Geometry;

/// <summary>Double-precision 3D vector, also used for points. Units are inches (SketchUp-compatible).</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static readonly Vector3d Zero = new(0, 0, 0);
    public static readonly Vector3d UnitX = new(1, 0, 0);
    public static readonly Vector3d UnitY = new(0, 1, 0);
    public static readonly Vector3d UnitZ = new(0, 0, 1);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator *(double s, Vector3d a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3d operator /(Vector3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3d Cross(Vector3d a, Vector3d b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static double Distance(Vector3d a, Vector3d b) => (a - b).Length;

    public static Vector3d Lerp(Vector3d a, Vector3d b, double t) => a + (b - a) * t;

    /// <summary>Returns the unit vector, or <see cref="Zero"/> when the length is below tolerance.</summary>
    public Vector3d Normalized()
    {
        var len = Length;
        return len < Tolerance.Length * 1e-6 ? Zero : this / len;
    }

    public bool IsZero(double tolerance = Tolerance.Length) => LengthSquared <= tolerance * tolerance;

    public bool IsEqual(Vector3d other, double tolerance = Tolerance.Length) => (this - other).IsZero(tolerance);

    /// <summary>Rotates the vector around a unit axis through the origin (Rodrigues' formula).</summary>
    public Vector3d RotateAround(Vector3d unitAxis, double radians)
    {
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return this * cos + Cross(unitAxis, this) * sin + unitAxis * (Dot(unitAxis, this) * (1 - cos));
    }

    /// <summary>Any unit vector perpendicular to this one.</summary>
    public Vector3d AnyPerpendicular()
    {
        var helper = Math.Abs(X) < 0.9 ? UnitX : UnitY;
        return Cross(this, helper).Normalized();
    }

    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}
