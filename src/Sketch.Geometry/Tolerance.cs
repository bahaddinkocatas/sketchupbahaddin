namespace Sketch.Geometry;

/// <summary>Geometric tolerances. Internal unit is the inch; 0.001" matches SketchUp's documented point tolerance.</summary>
public static class Tolerance
{
    public const double Length = 0.001;

    /// <summary>Angular tolerance in radians used for parallel/perpendicular tests.</summary>
    public const double Angle = 1e-6;
}
