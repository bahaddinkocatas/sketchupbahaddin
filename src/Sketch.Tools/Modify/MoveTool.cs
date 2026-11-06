using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;
using Sketch.Tools.Drawing;

namespace Sketch.Tools.Modify;

/// <summary>
/// Move (M): pick a start point, pick an end point; the selection (or the entity under the cursor) moves and
/// connected geometry stretches. Tap Ctrl to toggle copy mode. After a copy, type "5x" for an array or "/5" to
/// divide the distance. Type a distance to move exactly.
/// </summary>
public sealed class MoveTool : InferenceToolBase
{
    private Vector3d? _start;
    private readonly TransformPreview _preview = new();
    private List<int> _ids = new();
    private bool _copy;
    private bool _pressedAt;
    private double _downX, _downY;
    private Entity? _hover;

    // Last copy, for array input typed afterwards.
    private (List<int> Ids, Vector3d Delta)? _lastCopy;
    /// <summary>History state right after that copy: the array only replaces it if nothing happened since.</summary>
    private long _lastCopyMark;

    public override string Id => "tools/move";
    public override ToolCursor Cursor => ToolCursor.Move;
    public override string MeasurementLabel => "Distance";

    public override string StatusText => _start is null
        ? "Pick a point on the entity to move. Ctrl = toggle Copy."
        : (_copy ? "Pick the destination for the copy. " : "Pick the destination. ") + "You can enter a distance; Ctrl = toggle Copy, Esc = cancel.";

    public override string MeasurementValue =>
        _start is { } s && Current is { } c ? Units.Default.Format(Vector3d.Distance(s, c.Position)) : string.Empty;

    protected override Vector3d? From => _start;

    public override void Activate(IView view)
    {
        _lastCopy = null;
        Reset(view);
    }

    public override void Deactivate(IView view)
    {
        if (_start is not null) Cancel(view);
        base.Deactivate(view);
    }

    /// <summary>Entity under the cursor in the container being edited (geometry in a group picks the group).</summary>
    private static Entity? PickInActive(IView view, double x, double y)
    {
        var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, x, y));
        return deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_start is null)
        {
            UpdateInference(view, e.X, e.Y);
            _hover = view.Selection.IsEmpty ? PickInActive(view, e.X, e.Y) : null;
        }
        else
        {
            Preview(view, e.X, e.Y);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_start is null)
        {
            var r = UpdateInference(view, e.X, e.Y);
            if (view.Selection.IsEmpty)
            {
                var picked = PickInActive(view, e.X, e.Y);
                if (picked is null)
                {
                    RequestExit();
                    return;
                }
                view.Selection.Set(new[] { picked });
            }
            Begin(view, r.Position);
            _pressedAt = true;
            _downX = e.X;
            _downY = e.Y;
        }
        else
        {
            Preview(view, e.X, e.Y);
            Finish(view);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        // Drag-and-release also completes the move.
        if (_pressedAt && _start is not null && (Math.Abs(e.X - _downX) > 5 || Math.Abs(e.Y - _downY) > 5))
        {
            Preview(view, e.X, e.Y);
            Finish(view);
            NotifyStateChanged();
            view.Invalidate();
        }
        _pressedAt = false;
    }

    private void Begin(IView view, Vector3d start)
    {
        _start = start;
        _ids = view.Selection.Items.Select(i => i.Id).ToList();
        view.Model.StartOperation(_copy ? "Copy" : "Move");
        _preview.Begin(view, _ids);
        _lastCopy = null;
    }

    private Vector3d _delta;
    private List<Entity> _copied = new();

    private void Preview(IView view, double x, double y)
    {
        if (_preview.Container is null || _start is not { } start) return;
        var r = UpdateInference(view, x, y);
        Apply(view, r.Position - start);
    }

    private void Apply(IView view, Vector3d delta)
    {
        var ents = _preview.Container ?? view.Model.ActiveEntities;
        _delta = delta;
        if (_copy)
        {
            _preview.ResetToOriginal();
            _copied = MoveCopy.Copy(ents, ents.FindByIds(_ids).ToList(), delta);
        }
        else
        {
            _preview.Transform(Matrix4d.Translation(delta));
        }
        view.Selection.Set(ents.FindByIds(_ids));
    }

    private void Finish(IView view)
    {
        view.Model.CommitOperation();
        if (_copy)
        {
            _lastCopy = (_ids.ToList(), _delta);
            _lastCopyMark = view.Model.HistoryMark;
            view.Selection.Set(_copied.Where(c => !c.IsDeleted));
        }
        _start = null;
        _preview.End();
        Lock = null;
    }

    private void Cancel(IView view)
    {
        view.Model.AbortOperation();
        view.Selection.Set(view.Model.ActiveEntities.FindByIds(_ids));
        _start = null;
        _preview.End();
        Lock = null;
    }

    private bool _ctrlTap;

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        // Copy mode toggles on a Ctrl *tap*: pressing Ctrl+Z etc. must not toggle it.
        if (key == ViewKey.Ctrl)
        {
            _ctrlTap = true;
            return true;
        }
        _ctrlTap = false;
        return base.OnKeyDown(view, key, modifiers);
    }

    public override bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key == ViewKey.Ctrl && _ctrlTap)
        {
            _ctrlTap = false;
            _copy = !_copy;
            if (_start is not null) Preview(view, MouseX, MouseY);
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }
        return base.OnKeyUp(view, key, modifiers);
    }

    public override void Reset(IView view)
    {
        if (_start is not null) Cancel(view);
        _copy = false;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnUserText(IView view, string text)
    {
        // Array after a copy: "5x" / "*5" / "/5".
        if (_start is null && _lastCopy is { } last && MeasurementInput.TryParseArray(text, out var count, out var divide))
        {
            // Replace the single copy: "5x" = 5 copies spaced by the distance, "/5" = 5 copies dividing it.
            if (view.Model.HistoryMark != _lastCopyMark)
            {
                // Something else was done since the copy: the array would undo the wrong operation.
                _lastCopy = null;
                return false;
            }
            view.Model.Undo();
            var ents = view.Model.ActiveEntities;
            var delta = divide ? last.Delta / count : last.Delta;
            view.Model.Run("Copy Array", () =>
            {
                var copies = MoveCopy.Copy(ents, ents.FindByIds(last.Ids).ToList(), delta, count);
                view.Selection.Set(copies);
            });
            _lastCopy = last;
            _lastCopyMark = view.Model.HistoryMark;
            view.Invalidate();
            return true;
        }

        if (_start is not { } start || Current is null) return false;
        if (!Units.Default.TryParseLength(text, out var distance)) return false;
        var dir = (Current.Position - start).Normalized();
        if (dir.IsZero()) return false;
        Apply(view, dir * distance);
        Finish(view);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_start is { } s && Current is { } c)
        {
            g.Line(s, c.Position, RubberBandColor, 1, dashed: true);
            g.Point(s, DrawColor.Black, 7, PointShape.Circle, filled: false);
        }
        else if (_hover is Edge { IsDeleted: false } e)
        {
            g.Line(e.Start.Position, e.End.Position, DrawColor.Selection.WithAlpha(160), 2, depthTested: true);
        }
        else if (_hover is Face { IsDeleted: false } f)
        {
            foreach (var loop in f.Loops)
                foreach (var (a, b) in loop.Segments())
                    g.Line(a.Position, b.Position, DrawColor.Selection.WithAlpha(160), 2, depthTested: true);
        }
        DrawInference(g);
    }
}
