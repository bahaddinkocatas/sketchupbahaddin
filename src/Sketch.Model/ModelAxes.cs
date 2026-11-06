using Sketch.Geometry;

namespace Sketch.Model;

/// <summary>
/// The drawing axes (red X, green Y, blue Z) and their origin. They start on the world axes; the Axes tool can
/// move and rotate them so that inference, arrow-key locks and new rectangles follow a rotated building.
/// </summary>
public sealed record ModelAxes(Vector3d Origin, Vector3d X, Vector3d Y, Vector3d Z)
{
    public static readonly ModelAxes World = new(Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ);

    public bool IsWorld => Origin.IsEqual(Vector3d.Zero) && X.IsEqual(Vector3d.UnitX) && Y.IsEqual(Vector3d.UnitY);

    /// <summary>Axes from an origin, a point along red and a point toward green (Z completes a right-handed set).</summary>
    public static ModelAxes? FromPoints(Vector3d origin, Vector3d onRed, Vector3d towardGreen)
    {
        var x = onRed - origin;
        if (x.IsZero(1e-9)) return null;
        x = x.Normalized();
        var y = towardGreen - origin;
        y -= x * Vector3d.Dot(y, x);
        if (y.IsZero(1e-9)) return null;
        y = y.Normalized();
        return new ModelAxes(origin, x, y, Vector3d.Cross(x, y).Normalized());
    }
}
