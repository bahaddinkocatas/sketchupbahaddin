using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools;

/// <summary>
/// Base for tools that place points with inference: tracks the current inference, handles Shift and arrow-key
/// locks, and draws the inference feedback (markers, coloured axis lines, dotted alignment lines, tooltip).
/// </summary>
public abstract class InferenceToolBase : Tool
{
    protected InferenceResult? Current { get; private set; }
    protected InferenceLock? Lock { get; set; }
    protected double MouseX { get; private set; }
    protected double MouseY { get; private set; }

    private bool _shiftLock;

    /// <summary>Start point for linear inferences (null when idle).</summary>
    protected virtual Vector3d? From => null;

    protected virtual Face? FromFace => null;

    /// <summary>Plane that inferences are restricted to (e.g. rectangle after its first corner).</summary>
    protected virtual Plane3d? DrawingPlane => null;

    protected InferenceResult UpdateInference(IView view, double x, double y)
    {
        MouseX = x;
        MouseY = y;
        var q = new InferenceQuery(view.Camera, view.Width, view.Height, x, y)
        {
            From = From,
            FromFace = FromFace,
            Lock = Lock,
            DrawingPlane = DrawingPlane,
        };
        Current = view.Inference.Infer(q);
        view.ShowTooltip(string.IsNullOrEmpty(Current.Tooltip) ? null : Current.Tooltip, x, y);
        return Current;
    }

    protected void ClearInference(IView view)
    {
        Current = null;
        Lock = null;
        _shiftLock = false;
        view.ShowTooltip(null, 0, 0);
    }

    public override void Deactivate(IView view)
    {
        ClearInference(view);
        base.Deactivate(view);
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case ViewKey.Shift when Lock is null && Current is not null:
                Lock = LockFromCurrent();
                _shiftLock = Lock is not null;
                break;
            case ViewKey.Up:
                ToggleAxisLock(view.Model.Axes.Z, InferenceAxis.Blue);
                break;
            case ViewKey.Right:
                ToggleAxisLock(view.Model.Axes.X, InferenceAxis.Red);
                break;
            case ViewKey.Left:
                ToggleAxisLock(view.Model.Axes.Y, InferenceAxis.Green);
                break;
            default:
                return false;
        }
        UpdateInference(view, MouseX, MouseY);
        view.Invalidate();
        return true;
    }

    public override bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key != ViewKey.Shift || !_shiftLock) return false;
        Lock = null;
        _shiftLock = false;
        UpdateInference(view, MouseX, MouseY);
        view.Invalidate();
        return true;
    }

    private void ToggleAxisLock(Vector3d dir, InferenceAxis axis)
    {
        if (From is not { } from) return;
        if (DrawingPlane is { } plane && Math.Abs(Vector3d.Dot(dir, plane.Normal)) > 1e-6) return; // axis leaves the plane
        if (Lock is LineLock l && l.Axis == axis && !_shiftLock)
        {
            Lock = null;
            return;
        }
        Lock = new LineLock(from, dir, axis);
        _shiftLock = false;
    }

    /// <summary>Shift locks the current linear inference, or the plane of the face under the cursor.</summary>
    protected virtual InferenceLock? LockFromCurrent()
    {
        if (Current is null) return null;
        if (Current is { LineOrigin: { } o, LineDirection: { } d }) return new LineLock(o, d, Current.Axis);
        if (From is { } from && !Current.Position.IsEqual(from))
        {
            // Lock the direction from the start point through the current point.
            var dir = (Current.Position - from).Normalized();
            return new LineLock(from, dir, InferenceAxis.None);
        }
        return null;
    }

    public static DrawColor AxisColor(InferenceAxis axis) => axis switch
    {
        InferenceAxis.Red => DrawColor.AxisRed,
        InferenceAxis.Green => DrawColor.AxisGreen,
        InferenceAxis.Blue => DrawColor.AxisBlue,
        InferenceAxis.Parallel or InferenceAxis.Perpendicular => DrawColor.Magenta,
        _ => DrawColor.Black,
    };

    /// <summary>Colour for a rubber-band line from <see cref="From"/> to the current point.</summary>
    protected DrawColor RubberBandColor =>
        Current is { Kind: InferenceKind.OnLine or InferenceKind.LineFromPoint or InferenceKind.OnEdge, LineDirection: not null } c
            ? AxisColor(c.Axis)
            : DrawColor.Preview;

    /// <summary>Whether a line lock is drawn as a thick line (tools that lock internally, like Push/Pull, turn it off).</summary>
    protected virtual bool DrawLockedLine => true;

    protected void DrawInference(IViewDrawing g)
    {
        if (Current is not { } c) return;

        if (c.ReferencePoint is { } r && c.Kind is InferenceKind.FromPoint or InferenceKind.LineFromPoint)
        {
            g.Line(r, c.Position, AxisColor(c.ReferenceAxis), 1, dashed: true);
            g.Point(r, AxisColor(c.ReferenceAxis), 10, PointShape.Square, filled: false);
        }
        if (Lock is LineLock locked && DrawLockedLine)
        {
            // A locked line is drawn thicker through the start point.
            g.Line(locked.Origin, c.Position, locked.Axis == InferenceAxis.None ? DrawColor.Preview : AxisColor(locked.Axis), 2.5f);
        }

        switch (c.Kind)
        {
            case InferenceKind.Endpoint:
                g.Point(c.Position, DrawColor.InferenceEndpoint, 15, PointShape.Circle);
                break;
            case InferenceKind.Midpoint:
                g.Point(c.Position, DrawColor.InferenceMidpoint, 15, PointShape.Circle);
                break;
            case InferenceKind.Origin:
            case InferenceKind.Intersection:
                g.Point(c.Position, DrawColor.InferenceIntersection, 15, PointShape.Cross);
                break;
            case InferenceKind.OnEdge:
                g.Point(c.Position, DrawColor.InferenceOnEdge, 13, PointShape.Square);
                break;
            case InferenceKind.GuidePoint:
                g.Point(c.Position, DrawColor.Black, 15, PointShape.Cross);
                break;
            case InferenceKind.OnGuide:
                g.Point(c.Position, DrawColor.Guide, 13, PointShape.Square);
                break;
            case InferenceKind.OnFace:
                g.Point(c.Position, DrawColor.InferenceOnFace, 14, PointShape.Diamond);
                break;
            default:
                g.Point(c.Position, DrawColor.Black, 9, PointShape.Circle, filled: false);
                break;
        }
    }
}
