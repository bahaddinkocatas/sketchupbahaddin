using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Modify;

/// <summary>
/// Push/Pull (P): click a face, move to extrude it along its normal, click to finish. Type a distance for an
/// exact value; double-click a face to repeat the last distance. The distance is measured from the clicked point;
/// the moving point is shown from the middle of the face, which reads better than the click position.
/// </summary>
public sealed class PushPullTool : InferenceToolBase
{
    private int? _faceId;
    private Vector3d _anchor;
    private Vector3d _center;
    private Vector3d _normal;
    private EntitiesState? _base;
    private Entities? _baseContainer; // the container the snapshot belongs to (the one being edited)
    private double _distance;
    private double? _lastDistance;
    private Face? _hover;
    private bool _pressedAt;
    private double _downX, _downY;

    public override string Id => "tools/pushpull";
    public override ToolCursor Cursor => ToolCursor.PushPull;
    public override string MeasurementLabel => "Distance";

    public override string StatusText => _faceId is null
        ? "Pick a face to push or pull. Double-click repeats the last distance."
        : "Pick the distance to push or pull, or enter a value. Esc = cancel.";

    public override string MeasurementValue => _faceId is null ? string.Empty : Units.Default.Format(_distance);

    protected override Vector3d? From => _faceId is null ? null : _anchor;

    // The push direction is locked internally; SketchUp shows no line for it.
    protected override bool DrawLockedLine => false;

    public override void Activate(IView view) => Reset(view);

    public override void Deactivate(IView view)
    {
        if (_faceId is not null) Cancel(view);
        base.Deactivate(view);
    }

    private Face? FaceUnder(IView view, double x, double y)
    {
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        var face = view.Inference.FaceInference(ray)?.Face;
        // Only faces in the container being edited can be pushed (open a group to push its faces).
        return face is not null && ReferenceEquals(face.Parent, view.Model.ActiveEntities) ? face : null;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_faceId is null)
        {
            _hover = FaceUnder(view, e.X, e.Y);
            view.ShowTooltip(null, 0, 0);
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
        if (_faceId is null)
        {
            var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
            var hit = view.Inference.FaceInference(ray);
            if (hit?.Face is not { } face)
            {
                RequestExit();
                return;
            }
            if (!ReferenceEquals(face.Parent, view.Model.ActiveEntities)) return;
            _faceId = face.Id;
            _anchor = hit.Position;
            _center = Middle(face, _anchor);
            _normal = face.Normal;
            _distance = 0;
            view.Model.StartOperation("Push/Pull");
            _baseContainer = view.Model.ActiveEntities;
            _base = _baseContainer.CaptureState();
            Lock = new LineLock(_anchor, _normal, InferenceAxis.None);
            _pressedAt = true;
            _downX = e.X;
            _downY = e.Y;
            // Replace the previous push/pull's last point at once (it would otherwise show until the mouse moves).
            Preview(view, e.X, e.Y);
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
        if (_pressedAt && _faceId is not null && (Math.Abs(e.X - _downX) > 5 || Math.Abs(e.Y - _downY) > 5))
        {
            Preview(view, e.X, e.Y);
            Finish(view);
            NotifyStateChanged();
            view.Invalidate();
        }
        _pressedAt = false;
    }

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        // The first click of the double-click started a push/pull; turn it into a repeat of the last distance.
        if (_lastDistance is not { } last || _faceId is null || _base is null) return;
        _baseContainer!.RestoreState(_base, keepUnchangedGroups: true);
        Apply(view, last);
        Finish(view);
        NotifyStateChanged();
        view.Invalidate();
    }

    /// <summary>The face's centroid when it lies on the face (not for e.g. an L shape), else the clicked point.</summary>
    private static Vector3d Middle(Face face, Vector3d clicked)
    {
        var pts = face.OuterLoop.Vertices.Select(v => v.Position).ToList();
        var n = face.Normal;
        var origin = pts[0];
        var sum = Vector3d.Zero;
        double area = 0;
        for (var i = 1; i + 1 < pts.Count; i++)
        {
            var a = Vector3d.Dot(Vector3d.Cross(pts[i] - origin, pts[i + 1] - origin), n) / 2;
            sum += (origin + pts[i] + pts[i + 1]) / 3 * a;
            area += a;
        }
        if (Math.Abs(area) < Tolerance.Length * Tolerance.Length) return clicked;
        var centroid = sum / area;
        return face.ContainsPoint(centroid) ? centroid : clicked;
    }

    private void Preview(IView view, double x, double y)
    {
        if (_base is null) return;
        _baseContainer!.RestoreState(_base, keepUnchangedGroups: true);
        var r = UpdateInference(view, x, y);
        Apply(view, Vector3d.Dot(r.Position - _anchor, _normal));
    }

    private void Apply(IView view, double distance)
    {
        _distance = distance;
        if (Math.Abs(distance) <= Tolerance.Length) return; // nothing to extrude yet
        if (view.Model.ActiveEntities.FindById(_faceId!.Value) is Face face)
            PushPull.Execute(view.Model.ActiveEntities, face, distance);
    }

    private void Finish(IView view)
    {
        if (Math.Abs(_distance) > Tolerance.Length)
        {
            view.Model.CommitOperation();
            _lastDistance = _distance;
        }
        else
        {
            view.Model.AbortOperation();
        }
        _faceId = null;
        _base = null;
        Lock = null;
    }

    private void Cancel(IView view)
    {
        view.Model.AbortOperation();
        _faceId = null;
        _base = null;
        Lock = null;
    }

    public override void Reset(IView view)
    {
        if (_faceId is not null) Cancel(view);
        ClearInference(view);
        NotifyStateChanged();
    }

    public override bool OnUserText(IView view, string text)
    {
        if (!Units.Default.TryParseLength(text, out var distance)) return false;
        if (_faceId is not null && _base is not null)
        {
            // Typed values follow the direction currently being dragged.
            var sign = _distance < 0 ? -1 : 1;
            _baseContainer!.RestoreState(_base, keepUnchangedGroups: true);
            Apply(view, sign * distance);
            Finish(view);
        }
        else if (_lastDistance is not null && view.Selection.Items.OfType<Face>().FirstOrDefault() is { } face)
        {
            view.Model.Run("Push/Pull", () => PushPull.Execute(view.Model.ActiveEntities, face, distance));
            _lastDistance = distance;
        }
        else
        {
            return false;
        }
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_faceId is null)
        {
            if (_hover is { IsDeleted: false } f)
            {
                foreach (var loop in f.Loops)
                    foreach (var (a, b) in loop.Segments())
                        g.Line(a.Position, b.Position, DrawColor.Selection, 2, depthTested: true);
            }
            return;
        }
        // The moving point, shown from the middle of the face; a referenced point gets its dotted alignment line.
        var p = _center + _normal * _distance;
        if (Current?.ReferencePoint is { } r && !r.IsEqual(p))
        {
            g.Line(r, p, DrawColor.Black, 1, dashed: true);
            g.Point(r, DrawColor.InferenceEndpoint, 15, PointShape.Circle);
        }
        g.Point(p, DrawColor.Black, 9, PointShape.Circle, filled: false);
    }
}
