using Sketch.Geometry;

namespace Sketch.Tools;

public readonly record struct DrawColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly DrawColor Black = new(0, 0, 0);
    public static readonly DrawColor AxisRed = new(214, 0, 0);
    public static readonly DrawColor AxisGreen = new(0, 168, 0);
    public static readonly DrawColor AxisBlue = new(0, 0, 230);
    public static readonly DrawColor Magenta = new(220, 0, 220);
    public static readonly DrawColor Selection = new(0, 92, 230);
    public static readonly DrawColor InferenceEndpoint = new(0, 170, 0);
    public static readonly DrawColor InferenceMidpoint = new(0, 190, 230);
    public static readonly DrawColor InferenceOnEdge = new(220, 0, 0);
    public static readonly DrawColor InferenceOnFace = new(0, 0, 230);
    public static readonly DrawColor InferenceIntersection = new(0, 0, 0);
    public static readonly DrawColor Guide = new(60, 60, 60);

    /// <summary>Tool previews and rubber-band lines: a soft dark grey rather than solid black.</summary>
    public static readonly DrawColor Preview = new(72, 76, 84, 220);

    public DrawColor WithAlpha(byte a) => this with { A = a };
}

public enum PointShape
{
    Square,
    Circle,
    Diamond,
    Cross,
}

/// <summary>Immediate-mode overlay drawing that tools use to preview their work. Coordinates are world space.</summary>
public interface IViewDrawing
{
    /// <summary>Line with a width in pixels. Overlay lines are drawn on top of the model unless <paramref name="depthTested"/>.</summary>
    void Line(Vector3d a, Vector3d b, DrawColor color, float widthPx = 1, bool dashed = false, bool depthTested = false);

    /// <summary>Screen-sized marker at a world point.</summary>
    void Point(Vector3d p, DrawColor color, float sizePx = 9, PointShape shape = PointShape.Square, bool filled = true);

    /// <summary>Translucent filled planar polygon (e.g. a rectangle preview).</summary>
    void Polygon(IReadOnlyList<Vector3d> points, DrawColor color);

    /// <summary>Screen-facing text centred on a world point, shifted by (dx, dy) pixels (y down).</summary>
    void Text(Vector3d anchor, string text, DrawColor color, double dx = 0, double dy = 0);
}
