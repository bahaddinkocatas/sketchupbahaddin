using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Drawing;

/// <summary>
/// 2 Point Arc (A): click the start, click the end, then click (or type) the bulge. On a face the arc lies on it,
/// otherwise it is horizontal (vertical when the chord is vertical). Snaps to a half circle. Type "12s" for the
/// segment count.
/// </summary>
public sealed class ArcTool : InferenceToolBase
{
    public const int DefaultSegments = 12;

    private Vector3d? _start;
    private Vector3d? _end;
    private Face? _startFace;
    private bool _startInsideFace;
    private Vector3d? _fixedNormal;
    private List<Vector3d> _planes = new();
    private Vector3d _normal = Vector3d.UnitZ;
    private Vector3d _defaultNormal = Vector3d.UnitZ;
    private bool _probing;
    private int _segments = DefaultSegments;
    private double _bulge;
    private bool _halfCircle;

    public override string Id => "draw/arc";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => _end is null ? "Length" : "Bulge";

    public override string StatusText => _start is null ? "Select start point."
        : _end is null ? "Select end point or enter chord length."
        : "Select bulge amount or enter value. Type e.g. 12s for the number of segments.";

    public override string MeasurementValue => _start is { } s && Current is { } c
        ? _end is null ? Units.Default.Format(Vector3d.Distance(s, c.Position)) : Units.Default.Format(Math.Abs(_bulge))
        : string.Empty;

    protected override Vector3d? From => _end is null ? _start : null;
    protected override Face? FromFace => _startFace;
    protected override Plane3d? DrawingPlane => _end is { } e && !_probing ? new Plane3d(e, _normal) : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _start = null;
        _end = null;
        _startFace = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    private Vector3d BulgeDir => Vector3d.Cross(_normal, (_end!.Value - _start!.Value).Normalized()).Normalized();

    private void UpdateBulge(IView view)
    {
        if (_start is not { } s || _end is not { } e || Current is null) return;
        var mid = (s + e) / 2;
        _bulge = Vector3d.Dot(Current.Position - mid, BulgeDir);
        // Half-circle snap within a few pixels.
        var half = Vector3d.Distance(s, e) / 2;
        var upp = view.Camera.UnitsPerPixel(mid, view.Height);
        _halfCircle = Math.Abs(Math.Abs(_bulge) - half) < 6 * upp;
        if (_halfCircle)
        {
            _bulge = Math.Sign(_bulge) * half;
            view.ShowTooltip("Half Circle",MouseX, MouseY);
        }
    }

    /// <summary>
    /// Inference for the cursor. While bulging, the arc's plane first follows the cursor: through the bulge point
    /// when it is on geometry (e.g. over the face the arc should lie on), else the default for the chord.
    /// </summary>
    private InferenceResult Track(IView view, double x, double y)
    {
        if (_start is { } s && _end is { } e && _fixedNormal is null)
        {
            _probing = true;
            var probe = UpdateInference(view, x, y);
            _probing = false;
            _normal = DrawingPlanes.ThroughCursor(s, e, probe, _planes) ?? _defaultNormal;
        }
        return UpdateInference(view, x, y);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        Track(view, e.X, e.Y);
        UpdateBulge(view);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = Track(view, e.X, e.Y);
        if (_start is null)
        {
            _start = r.Position;
            _startFace = r.Face;
            _startInsideFace = DrawingPlanes.FixedBy(r) is not null;
            _planes = DrawingPlanes.PlanesAt(r, view.Model.Axes);
        }
        else if (_end is null)
        {
            DrawingPlanes.AddFacePlanes(_planes, r);
            SetEnd(r.Position, view);
        }
        else
        {
            UpdateBulge(view);
            Create(view, _bulge);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void SetEnd(Vector3d end, IView view)
    {
        if (_start is not { } s || Vector3d.Distance(s, end) <= Tolerance.Length) return;
        _end = end;
        var chord = (end - s).Normalized();
        // Started inside a face that holds the chord: the arc stays on it. Otherwise the plane follows the bulge
        // point (see Track), falling back to the view-facing, horizontal or vertical plane through the chord.
        var inside = _startInsideFace && _startFace is { } f && Math.Abs(Vector3d.Dot(f.Normal, chord)) < 1e-6 ? f.Normal : (Vector3d?)null;
        _fixedNormal = inside;
        _defaultNormal = DrawingPlanes.ChordDefault(chord, inside, view.Camera, view.Model.Axes);
        _normal = _fixedNormal ?? _defaultNormal;
    }

    private void Create(IView view, double bulge)
    {
        if (_start is not { } s || _end is not { } e) return;
        var pts = Shapes.ArcByBulge(s, e, BulgeDir, bulge, _segments);
        view.Model.Run("Arc", () => view.Model.ActiveEntities.AddPolyline(pts, closed: false));
        _start = null;
        _end = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (MeasurementInput.TryParseSegments(text, out var segments))
        {
            _segments = segments;
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }
        if (!Units.Default.TryParseLength(text, out var length)) return false;
        if (_end is null && _start is { } s && Current is { } c)
        {
            var dir = (c.Position - s).Normalized();
            if (dir.IsZero()) return false;
            SetEnd(s + dir * length, view);
        }
        else if (_end is not null)
        {
            Create(view, (_bulge < 0 ? -1 : 1) * length);
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
        if (_start is { } s && Current is { } c)
        {
            if (_end is { } e)
            {
                var pts = Shapes.ArcByBulge(s, e, BulgeDir, _bulge, _segments);
                for (var i = 0; i + 1 < pts.Count; i++) g.Line(pts[i], pts[i + 1], _halfCircle ? DrawColor.Magenta : DrawColor.Preview, 1.5f);
                g.Line(s, e, DrawColor.Preview, 1, dashed: true);
            }
            else
            {
                g.Line(s, c.Position, RubberBandColor, 1, dashed: true);
            }
        }
        DrawInference(g);
    }
}

/// <summary>Polygon: like Circle but with few sides (default 6). Type "8s" for the number of sides.</summary>
public sealed class PolygonTool : InferenceToolBase
{
    private Vector3d? _center;
    private Vector3d _normal = Vector3d.UnitZ;
    private int _sides = 6;

    public override string Id => "draw/polygon";
    public override ToolCursor Cursor => ToolCursor.Pencil;
    public override string MeasurementLabel => _center is null ? "Sides" : "Radius";

    public override string StatusText => _center is null ? "Select center point. Type e.g. 8s for the number of sides." : "Select radius point or enter value.";

    public override string MeasurementValue => _center is { } c && Current is { } cur
        ? Units.Default.Format(Vector3d.Distance(c, new Plane3d(c, _normal).Project(cur.Position)))
        : _sides.ToString();

    protected override Vector3d? From => _center;
    protected override Plane3d? DrawingPlane => _center is { } c ? new Plane3d(c, _normal) : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _center = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_center is null)
        {
            _center = r.Position;
            _normal = r.Face?.Normal ?? r.Image?.Normal ?? Sketch.Inference.InferenceEngine.ViewPlaneNormal(view.Camera, view.Model.Axes);
        }
        else
        {
            var c = _center.Value;
            Create(view, Vector3d.Distance(c, new Plane3d(c, _normal).Project(r.Position)), r.Position);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void Create(IView view, double radius, Vector3d toward)
    {
        if (_center is not { } c || radius <= Tolerance.Length) return;
        var pts = Shapes.RegularPolygon(c, _normal, toward, radius, _sides);
        view.Model.Run("Polygon", () => view.Model.ActiveEntities.AddPolyline(pts, closed: true));
        _center = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (MeasurementInput.TryParseSegments(text, out var sides))
        {
            _sides = sides;
            NotifyStateChanged();
            return true;
        }
        if (_center is not { } c || !Units.Default.TryParseLength(text, out var radius)) return false;
        Create(view, radius, Current?.Position ?? c + _normal.AnyPerpendicular());
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_center is { } c && Current is { } cur)
        {
            var radius = Vector3d.Distance(c, new Plane3d(c, _normal).Project(cur.Position));
            if (radius > Tolerance.Length)
            {
                var pts = Shapes.RegularPolygon(c, _normal, cur.Position, radius, _sides);
                for (var i = 0; i < pts.Count; i++) g.Line(pts[i], pts[(i + 1) % pts.Count], DrawColor.Preview, 1.5f);
            }
            g.Point(c, DrawColor.Black, 7, PointShape.Cross);
        }
        DrawInference(g);
    }
}
