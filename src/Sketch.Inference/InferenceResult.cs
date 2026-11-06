using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Inference;

public enum InferenceKind
{
    /// <summary>Free point (on the drawing plane), no snapping.</summary>
    None,
    Endpoint,
    Midpoint,
    Intersection,
    Origin,
    OnEdge,
    OnFace,
    OnGuide,
    GuidePoint,
    OnImage,

    /// <summary>On a line through the start point along an axis/parallel direction.</summary>
    OnLine,

    /// <summary>Aligned with a reference point along an axis (dotted "from point" line).</summary>
    FromPoint,

    /// <summary>On a line from the start point and aligned with a reference point at the same time.</summary>
    LineFromPoint,
}

/// <summary>Which axis a linear inference follows (determines its colour).</summary>
public enum InferenceAxis
{
    None,
    Red,
    Green,
    Blue,
    Parallel,
    Perpendicular,
}

public sealed record InferenceResult(Vector3d Position, InferenceKind Kind)
{
    public Vertex? Vertex { get; init; }
    public Edge? Edge { get; init; }
    public Guide? Guide { get; init; }
    public ImageEntity? Image { get; init; }
    public Face? Face { get; init; }

    /// <summary>For linear inferences: line origin and unit direction.</summary>
    public Vector3d? LineOrigin { get; init; }
    public Vector3d? LineDirection { get; init; }
    public InferenceAxis Axis { get; init; }

    /// <summary>For from-point inferences: the reference point the dotted line comes from.</summary>
    public Vector3d? ReferencePoint { get; init; }
    public InferenceAxis ReferenceAxis { get; init; }

    public bool IsPointInference => Kind is InferenceKind.Endpoint or InferenceKind.Midpoint
        or InferenceKind.Intersection or InferenceKind.Origin or InferenceKind.GuidePoint;

    /// <summary>Short user-facing name shown next to the cursor (SketchUp's wording).</summary>
    public string Tooltip => Kind switch
    {
        InferenceKind.Endpoint => "Endpoint",
        InferenceKind.Midpoint => "Midpoint",
        InferenceKind.Intersection => "Intersection",
        InferenceKind.Origin => "Origin",
        InferenceKind.OnEdge when LineDirection is not null && Axis != InferenceAxis.None => "On Edge · " + AxisName(Axis, on: true),
        InferenceKind.OnEdge => "On Edge",
        InferenceKind.OnFace => "On Face",
        InferenceKind.OnGuide => "On Line",
        InferenceKind.GuidePoint => "Guide Point",
        InferenceKind.OnImage => "On Face in Image",
        InferenceKind.OnLine => AxisName(Axis, on: true),
        InferenceKind.FromPoint => AxisName(ReferenceAxis, on: false),
        InferenceKind.LineFromPoint => AxisName(Axis, on: true) + " · " + AxisName(ReferenceAxis, on: false),
        _ => string.Empty,
    };

    private static string AxisName(InferenceAxis axis, bool on) => axis switch
    {
        InferenceAxis.Red => on ? "On Red Axis" : "From Point",
        InferenceAxis.Green => on ? "On Green Axis" : "From Point",
        InferenceAxis.Blue => on ? "On Blue Axis" : "From Point",
        InferenceAxis.Parallel => "Parallel to Edge",
        InferenceAxis.Perpendicular => "Perpendicular to Edge",
        _ => string.Empty,
    };
}

/// <summary>A constraint set by Shift or the arrow keys.</summary>
public abstract record InferenceLock;

/// <summary>Points are constrained to a line.</summary>
public sealed record LineLock(Vector3d Origin, Vector3d Direction, InferenceAxis Axis) : InferenceLock;

/// <summary>Points are constrained to a plane.</summary>
public sealed record PlaneLock(Plane3d Plane) : InferenceLock;
