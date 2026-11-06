using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;
using Sketch.Tools.Drawing;

namespace Sketch.Tools.Modify;

/// <summary>
/// Rotate (Q): click the centre (on a face its normal is the axis, else blue; arrow keys choose the axis), click
/// a reference point, then click or type an angle. Angles snap to 15°. Tap Ctrl to rotate a copy; after a copy,
/// type "6x" for a polar array or "/6" to divide.
/// </summary>
public sealed class RotateTool : InferenceToolBase
{
    private enum Stage { Center, Reference, Angle }

    private Stage _stage = Stage.Center;
    private Vector3d _center;
    private Vector3d _axis = Vector3d.UnitZ;
    private Vector3d? _forcedAxis;
    private Vector3d _reference;
    private double _angle;
    private readonly TransformPreview _preview = new();
    private List<int> _ids = new();
    private bool _copy, _ctrlTap;
    private List<Entity> _copied = new();
    private (List<int> Ids, Matrix4d Step, double Angle)? _lastCopy;
    /// <summary>History state right after that copy: the array only replaces it if nothing happened since.</summary>
    private long _lastCopyMark;

    public override string Id => "tools/rotate";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Angle";
    public override string MeasurementValue => _stage == Stage.Angle ? $"{_angle:0.#}°" : string.Empty;

    public override string StatusText => _stage switch
    {
        Stage.Center => "Pick the center of rotation. Arrows lock the axis (↑ blue, → red, ← green). Ctrl = toggle Copy.",
        Stage.Reference => "Pick a point to start the rotation.",
        _ => (_copy ? "Pick the angle for the copy" : "Pick the rotation angle") + " or enter a value.",
    };

    protected override Vector3d? From => _stage == Stage.Reference ? _center : null;
    protected override Plane3d? DrawingPlane => _stage == Stage.Center ? null : new Plane3d(_center, _axis);

    public override void Activate(IView view)
    {
        _lastCopy = null;
        Reset(view);
    }

    public override void Deactivate(IView view)
    {
        if (_stage == Stage.Angle) Cancel(view);
        base.Deactivate(view);
    }

    public override void Reset(IView view)
    {
        if (_stage == Stage.Angle) Cancel(view);
        _stage = Stage.Center;
        _copy = false;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key == ViewKey.Ctrl)
        {
            _ctrlTap = true;
            return true;
        }
        _ctrlTap = false;
        if (_stage == Stage.Center)
        {
            // Arrow keys follow the drawing axes (moved by the Axes tool), like the other tools.
            var axes = view.Model.Axes;
            Vector3d? axis = key switch
            {
                ViewKey.Up => axes.Z,
                ViewKey.Right => axes.X,
                ViewKey.Left => axes.Y,
                _ => null,
            };
            if (axis is not null)
            {
                _forcedAxis = _forcedAxis is { } f && f.IsEqual(axis.Value) ? null : axis;
                view.Invalidate();
                return true;
            }
        }
        return base.OnKeyDown(view, key, modifiers);
    }

    public override bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key == ViewKey.Ctrl && _ctrlTap)
        {
            _ctrlTap = false;
            _copy = !_copy;
            if (_stage == Stage.Angle) Apply(view, _angle);
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }
        return base.OnKeyUp(view, key, modifiers);
    }

    private static Entity? PickInActive(IView view, double x, double y)
    {
        var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, x, y));
        return deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var r = UpdateInference(view, e.X, e.Y);
        if (_stage == Stage.Angle)
        {
            _angle = SnappedAngle(r.Position);
            Apply(view, _angle);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        switch (_stage)
        {
            case Stage.Center:
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
                _center = r.Position;
                _axis = _forcedAxis ?? r.Face?.Normal ?? Sketch.Inference.InferenceEngine.ViewPlaneNormal(view.Camera, view.Model.Axes);
                _stage = Stage.Reference;
                break;
            case Stage.Reference:
                var p = new Plane3d(_center, _axis).Project(r.Position);
                if (Vector3d.Distance(p, _center) <= Tolerance.Length) return;
                _reference = p;
                _ids = view.Selection.Items.Select(i => i.Id).ToList();
                view.Model.StartOperation("Rotate");
                _preview.Begin(view, _ids);
                _angle = 0;
                _stage = Stage.Angle;
                break;
            case Stage.Angle:
                _angle = SnappedAngle(r.Position);
                Apply(view, _angle);
                Finish(view);
                break;
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private double SnappedAngle(Vector3d p)
    {
        var b = (_reference - _center).Normalized();
        var v = new Plane3d(_center, _axis).Project(p) - _center;
        if (v.IsZero()) return _angle;
        var a = Math.Atan2(Vector3d.Dot(Vector3d.Cross(b, v), _axis), Vector3d.Dot(b, v)) * 180 / Math.PI;
        var snapped = Math.Round(a / 15) * 15;
        return Math.Abs(a - snapped) < 2.5 ? snapped : a;
    }

    private Matrix4d RotationBy(double degrees) =>
        Matrix4d.Translation(_center) * Matrix4d.Rotation(_axis, degrees * Math.PI / 180) * Matrix4d.Translation(-_center);

    private void Apply(IView view, double degrees)
    {
        if (_preview.Container is not { } ents) return;
        if (_copy)
        {
            _preview.ResetToOriginal();
            _copied = MoveCopy.CopyTransformed(ents, ents.FindByIds(_ids).ToList(), RotationBy(degrees));
        }
        else
        {
            _preview.Transform(RotationBy(degrees));
        }
        view.Selection.Set(ents.FindByIds(_ids));
    }

    private void Finish(IView view)
    {
        view.Model.CommitOperation();
        if (_copy)
        {
            _lastCopy = (_ids.ToList(), RotationBy(_angle), _angle);
            _lastCopyMark = view.Model.HistoryMark;
            view.Selection.Set(_copied.Where(c => !c.IsDeleted));
        }
        _preview.End();
        _stage = Stage.Center;
        Lock = null;
    }

    private void Cancel(IView view)
    {
        view.Model.AbortOperation();
        view.Selection.Set(view.Model.ActiveEntities.FindByIds(_ids));
        _preview.End();
        _stage = Stage.Center;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_stage == Stage.Center && _lastCopy is { } last && MeasurementInput.TryParseArray(text, out var count, out var divide))
        {
            if (view.Model.HistoryMark != _lastCopyMark)
            {
                // Something else was done since the copy: the array would undo the wrong operation.
                _lastCopy = null;
                return false;
            }
            view.Model.Undo();
            var ents = view.Model.ActiveEntities;
            var angle = divide ? last.Angle / count : last.Angle;
            var step = RotationBy(angle);
            view.Model.Run("Radial Array", () =>
                view.Selection.Set(MoveCopy.CopyTransformed(ents, ents.FindByIds(last.Ids).ToList(), step, count)));
            _lastCopy = last;
            _lastCopyMark = view.Model.HistoryMark;
            view.Invalidate();
            return true;
        }
        if (_stage != Stage.Angle || !Units.TryParseNumber(text.Trim().TrimEnd('°'), out var degrees)) return false;
        _angle = degrees;
        Apply(view, degrees);
        Finish(view);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_stage != Stage.Center || Current is not null)
        {
            var c = _stage == Stage.Center ? Current!.Position : _center;
            var axis = _stage == Stage.Center ? _forcedAxis ?? Current!.Face?.Normal ?? Sketch.Inference.InferenceEngine.ViewPlaneNormal(view.Camera, view.Model.Axes) : _axis;
            var r = view.Camera.UnitsPerPixel(c, view.Height) * 55;
            var u = (_stage == Stage.Center ? axis.AnyPerpendicular() : (_reference - c).Normalized());
            if (u.IsZero()) u = axis.AnyPerpendicular();
            var v = Vector3d.Cross(axis, u);
            var color = Math.Abs(axis.Z) > 0.99 ? DrawColor.AxisBlue : Math.Abs(axis.X) > 0.99 ? DrawColor.AxisRed
                : Math.Abs(axis.Y) > 0.99 ? DrawColor.AxisGreen : DrawColor.Black;
            for (var i = 0; i < 24; i++)
            {
                var a0 = 2 * Math.PI * i / 24;
                var a1 = 2 * Math.PI * (i + 1) / 24;
                g.Line(c + (u * Math.Cos(a0) + v * Math.Sin(a0)) * r, c + (u * Math.Cos(a1) + v * Math.Sin(a1)) * r, color.WithAlpha(170), 1);
            }
            if (_stage == Stage.Reference && Current is { } cur) g.Line(c, cur.Position, DrawColor.Preview, 1, dashed: true);
            if (_stage == Stage.Angle)
            {
                g.Line(c, _reference, DrawColor.Preview, 1, dashed: true);
                var dir = (_reference - c).RotateAround(axis.Normalized(), _angle * Math.PI / 180);
                g.Line(c, c + dir, DrawColor.Preview, 1, dashed: true);
            }
        }
        DrawInference(g);
    }
}
