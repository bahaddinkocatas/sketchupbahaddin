using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools.Construction;

/// <summary>
/// Axes: click the new origin, click a point along the new red axis, then a point toward the green axis.
/// Double-clicking the origin just moves the axes there (keeping their directions). Esc cancels.
/// </summary>
public sealed class AxesTool : InferenceToolBase
{
    private Vector3d? _origin;
    private Vector3d? _red;

    public override string Id => "tools/axes";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    public override string StatusText => _origin is null
        ? "Select a point to place the origin (double-click = move only)."
        : _red is null ? "Select a point to define the red axis direction." : "Select a point to define the green axis direction.";

    protected override Vector3d? From => _origin;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _origin = null;
        _red = null;
        ClearInference(view);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_origin is null) _origin = r.Position;
        else if (_red is null)
        {
            if (Vector3d.Distance(r.Position, _origin.Value) > Tolerance.Length) _red = r.Position;
        }
        else if (ModelAxes.FromPoints(_origin.Value, _red.Value, r.Position) is { } axes)
        {
            view.Model.Run("Axes", () => view.Model.Axes = axes);
            _origin = null;
            _red = null;
            Lock = null;
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        if (_origin is not { } o || _red is not null) return;
        view.Model.Run("Axes", () => view.Model.Axes = view.Model.Axes with { Origin = o });
        Reset(view);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_origin is { } o && Current is { } c)
        {
            var size = view.Camera.UnitsPerPixel(o, view.Height) * 80;
            if (_red is null)
            {
                var x = c.Position - o;
                if (!x.IsZero(1e-9)) g.Line(o, o + x.Normalized() * Math.Max(size, x.Length), DrawColor.AxisRed, 2);
            }
            else if (ModelAxes.FromPoints(o, _red.Value, c.Position) is { } a)
            {
                g.Line(o, o + a.X * size, DrawColor.AxisRed, 2);
                g.Line(o, o + a.Y * size, DrawColor.AxisGreen, 2);
                g.Line(o, o + a.Z * size, DrawColor.AxisBlue, 2);
            }
            g.Point(o, DrawColor.Black, 9, PointShape.Cross);
        }
        DrawInference(g);
    }
}
