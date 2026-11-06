using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools.Drawing;

/// <summary>
/// Line (L): click a start point, click an end point; drawing continues from the end point until Esc or until
/// the line closes a face. Type a length (or [x,y,z] / &lt;x,y,z&gt;) in the Measurements box.
/// </summary>
public sealed class LineTool : InferenceToolBase
{
    private Vector3d? _start;
    private Face? _startFace;

    public override string Id => "draw/line";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => "Length";

    public override string StatusText => _start is null
        ? "Select start point."
        : "Select end point or enter value. Shift = lock direction, arrows = lock to axis, Esc = finish.";

    public override string MeasurementValue =>
        _start is { } s && Current is { } c ? Units.Default.Format(Vector3d.Distance(s, c.Position)) : string.Empty;

    protected override Vector3d? From => _start;
    protected override Face? FromFace => _startFace;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _start = null;
        _startFace = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var r = UpdateInference(view, e.X, e.Y);
        if (_start is { } s)
        {
            // Live length next to the cursor (the Measurements box shows it too).
            var length = Units.Default.Format(Vector3d.Distance(s, r.Position));
            view.ShowTooltip(string.IsNullOrEmpty(r.Tooltip) ? length : $"{length}  ·  {r.Tooltip}", e.X, e.Y);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_start is null)
        {
            _start = r.Position;
            _startFace = r.Face;
            view.Inference.ClearReferences();
        }
        else
        {
            AddSegment(view, r.Position);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void AddSegment(IView view, Vector3d end)
    {
        if (_start is not { } start || Vector3d.Distance(start, end) <= Tolerance.Length) return;
        var facesBefore = view.Model.ActiveEntities.Faces.Count;
        view.Model.Run("Line", () => view.Model.ActiveEntities.AddEdge(start, end));
        var closedFace = view.Model.ActiveEntities.Faces.Count > facesBefore;

        Lock = null;
        if (closedFace)
        {
            // Closing a face ends the chain, as in SketchUp.
            _start = null;
            _startFace = null;
        }
        else
        {
            _start = end;
        }
    }

    public override bool OnUserText(IView view, string text)
    {
        if (MeasurementInput.TryParseCoordinates(text, out var coords, out var relative))
        {
            if (_start is null)
            {
                _start = coords; // with no start point, relative coordinates are relative to the origin
            }
            else
            {
                AddSegment(view, relative ? _start.Value + coords : coords);
            }
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }

        if (_start is not { } start || Current is not { } c) return false;
        if (!Units.Default.TryParseLength(text, out var length) || Math.Abs(length) <= Tolerance.Length) return false;
        var dir = (c.Position - start).Normalized();
        if (dir.IsZero()) return false;
        AddSegment(view, start + dir * length);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_start is { } s && Current is { } c)
            g.Line(s, c.Position, RubberBandColor, 1.5f);
        DrawInference(g);
    }
}
