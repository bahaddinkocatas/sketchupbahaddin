namespace Sketch.Geometry;

public readonly record struct Ray3d(Vector3d Origin, Vector3d Direction)
{
    public Vector3d PointAt(double t) => Origin + Direction * t;
}

public readonly record struct Plane3d(Vector3d Point, Vector3d Normal)
{
    public static readonly Plane3d Ground = new(Vector3d.Zero, Vector3d.UnitZ);

    public double SignedDistance(Vector3d p) => Vector3d.Dot(p - Point, Normal);

    public Vector3d Project(Vector3d p) => p - Normal * SignedDistance(p);

    /// <summary>Ray parameter of the intersection, or null when the ray is parallel to the plane.</summary>
    public double? Intersect(Ray3d ray)
    {
        var denom = Vector3d.Dot(ray.Direction, Normal);
        if (Math.Abs(denom) < 1e-12) return null;
        return Vector3d.Dot(Point - ray.Origin, Normal) / denom;
    }
}
