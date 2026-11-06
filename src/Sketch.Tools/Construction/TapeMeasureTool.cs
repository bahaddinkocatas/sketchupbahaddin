using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Construction;

/// <summary>
/// Tape Measure (T): measures distances and creates guides. Starting on an edge creates a guide line parallel to
/// that edge; starting on a point creates a guide point. Ctrl toggles guide creation. Double-click an edge for a
/// guide along it. Type a length to place the guide at an exact distance.
/// </summary>
public sealed class TapeMeasureTool : InferenceToolBase
{
    private Vector3d? _start;
    private Vector3d? _edgeDir;
    private bool _createGuides = true;
    private bool _ctrlTap;

    // The last completed point-to-point measurement: typing a length afterwards rescales the model.
    private (Vector3d A, Vector3d B)? _lastMeasure;

    /// <summary>
    /// Asks whether to resize the model (current length, new length, both in inches). Supplied by the UI;
    /// without it the resize happens without asking.
    /// </summary>
    public Func<double, double, bool>? ConfirmResize { get; set; }

    public override string Id => "tools/tape";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Length";

    public override string StatusText => _start is null
        ? "Select point to measure from. Start on an edge to create a parallel guide. After measuring, type the real length to resize the model. Ctrl = toggle create guides."
        : _edgeDir is null
            ? "Select point to measure to" + (_createGuides ? " (a guide point is created)." : ".")
            : "Select the guide line position or enter a distance.";

    public override string MeasurementValue => _start is { } s && Current is { } c ? Units.Default.Format(Distance(s, c.Position)) : string.Empty;

    protected override Vector3d? From => _start;

    /// <summary>Distance shown: perpendicular to the start edge in guide-line mode, otherwise point to point.</summary>
    private double Distance(Vector3d start, Vector3d p)
    {
        if (_edgeDir is not { } d) return Vector3d.Distance(start, p);
        var v = p - start;
        return (v - d * Vector3d.Dot(v, d)).Length;
    }

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _start = null;
        _edgeDir = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var r = UpdateInference(view, e.X, e.Y);
        if (_start is { } s)
        {
            // Show the measured length right at the cursor (with the inference name, if any).
            var length = Units.Default.Format(Distance(s, r.Position));
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
            _lastMeasure = null;
            _start = r.Position;
            // From an edge (but not its endpoints) the guide is a line parallel to the edge.
            _edgeDir = r.Kind is InferenceKind.OnEdge or InferenceKind.Midpoint && r.Edge is { } edge ? edge.Direction
                : r.Kind == InferenceKind.OnGuide && r.Guide is { } g ? g.Direction
                : null;
        }
        else
        {
            Finish(view, r.Position);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnDoubleClick(IView view, ViewMouseEvent e)
    {
        // Double-click on an edge: an infinite guide along it.
        var edge = view.Inference.PickEdge(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
        if (edge is null) return;
        Reset(view);
        view.Model.Run("Guide", () => view.Model.ActiveEntities.AddGuideLine(edge.Start.Position, edge.Direction));
    }

    private void Finish(IView view, Vector3d end)
    {
        if (_start is not { } start) return;
        if (_edgeDir is null && Vector3d.Distance(start, end) > Tolerance.Length) _lastMeasure = (start, end);
        if (_createGuides && Distance(start, end) > Tolerance.Length)
        {
            var dir = _edgeDir;
            view.Model.Run("Guide", () =>
            {
                if (dir is { } d) view.Model.ActiveEntities.AddGuideLine(end, d);
                else view.Model.ActiveEntities.AddGuidePoint(end);
            });
        }
        _start = null;
        _edgeDir = null;
        Lock = null;
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
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
            _createGuides = !_createGuides;
            NotifyStateChanged();
            return true;
        }
        return base.OnKeyUp(view, key, modifiers);
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_start is null && _lastMeasure is { } m && Units.Default.TryParseLength(text, out var newLength) && newLength > 0)
            return Resize(view, m.A, m.B, newLength);
        if (_start is not { } start || Current is null || !Units.Default.TryParseLength(text, out var length)) return false;
        Vector3d end;
        if (_edgeDir is { } d)
        {
            // Offset the guide perpendicular to the start edge, toward the cursor.
            var v = Current.Position - start;
            var perp = (v - d * Vector3d.Dot(v, d)).Normalized();
            if (perp.IsZero()) return false;
            end = start + perp * length;
        }
        else
        {
            var dir = (Current.Position - start).Normalized();
            if (dir.IsZero()) return false;
            end = start + dir * length;
        }
        Finish(view, end);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    /// <summary>Scales everything in the container being edited so the measured distance becomes the typed one.</summary>
    private bool Resize(IView view, Vector3d a, Vector3d b, double newLength)
    {
        var current = Vector3d.Distance(a, b);
        if (current <= Tolerance.Length) return false;
        if (ConfirmResize is not null && !ConfirmResize(current, newLength)) return true;
        var f = newLength / current;
        var transform = Matrix4d.Translation(a) * Matrix4d.Scaling(f, f, f) * Matrix4d.Translation(-a);
        view.Model.Run("Rescale Model", () =>
        {
            var c = view.Model.ActiveEntities;
            var all = c.Edges.Cast<Entity>().Concat(c.Faces).Concat(c.Groups).Concat(c.Guides).Concat(c.Dimensions)
                .Concat(c.Notes).Concat(c.Images).ToList();
            MoveCopy.Transform(c, all, transform);
        });
        _lastMeasure = null;
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_start is { } s && Current is { } c)
        {
            g.Text((s + c.Position) / 2, Units.Default.Format(Distance(s, c.Position)), DrawColor.Black);
            if (_edgeDir is { } d)
            {
                var v = c.Position - s;
                var foot = s + d * Vector3d.Dot(v, d);
                g.Line(foot, c.Position, DrawColor.Preview, 1, dashed: true);
                if (_createGuides)
                {
                    var reach = view.Camera.Distance * 4;
                    g.Line(c.Position - d * reach, c.Position + d * reach, DrawColor.Guide, 1, dashed: true);
                }
            }
            else
            {
                g.Line(s, c.Position, RubberBandColor, 1, dashed: true);
            }
            g.Point(s, DrawColor.Black, 7, PointShape.Cross);
        }
        DrawInference(g);
    }
}

/// <summary>
/// Protractor: click the centre, click a point on the base line, then click (or type an angle) to create an
/// angled guide line. Angles snap to 15° steps.
/// </summary>
public sealed class ProtractorTool : InferenceToolBase
{
    private Vector3d? _center;
    private Vector3d? _base;
    private Vector3d _normal = Vector3d.UnitZ;

    public override string Id => "tools/protractor";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Angle";

    public override string StatusText => _center is null ? "Place protractor center."
        : _base is null ? "Select point on the base line." : "Select angle or enter value (degrees).";

    public override string MeasurementValue => _base is not null && Current is not null ? $"{AngleDegrees(Current.Position):0.#}°" : string.Empty;

    protected override Vector3d? From => _center;
    protected override Plane3d? DrawingPlane => _center is { } c && _base is not null ? new Plane3d(c, _normal) : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _center = null;
        _base = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    private double RawAngle(Vector3d p)
    {
        var c = _center!.Value;
        var b = (_base!.Value - c).Normalized();
        var v = new Plane3d(c, _normal).Project(p) - c;
        return Math.Atan2(Vector3d.Dot(Vector3d.Cross(b, v), _normal), Vector3d.Dot(b, v)) * 180 / Math.PI;
    }

    /// <summary>Angle from the base line, snapped to 15° steps when close.</summary>
    private double AngleDegrees(Vector3d p)
    {
        var a = RawAngle(p);
        var snapped = Math.Round(a / 15) * 15;
        return Math.Abs(a - snapped) < 2.5 ? snapped : a;
    }

    private Vector3d DirectionAt(double degrees)
    {
        var b = (_base!.Value - _center!.Value).Normalized();
        return b.RotateAround(_normal, degrees * Math.PI / 180);
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
            _normal = r.Face?.Normal ?? Vector3d.UnitZ;
        }
        else if (_base is null)
        {
            var p = new Plane3d(_center.Value, _normal).Project(r.Position);
            if (Vector3d.Distance(p, _center.Value) > Tolerance.Length) _base = p;
        }
        else
        {
            Create(view, AngleDegrees(r.Position));
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void Create(IView view, double degrees)
    {
        var c = _center!.Value;
        var dir = DirectionAt(degrees);
        view.Model.Run("Angle Guide", () => view.Model.ActiveEntities.AddGuideLine(c, dir));
        _center = null;
        _base = null;
        Lock = null;
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_base is null || !Units.TryParseNumber(text.Trim().TrimEnd('°'), out var degrees)) return false;
        Create(view, degrees);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_center is { } c)
        {
            // Protractor disc: a circle with 15° ticks, sized to the screen.
            var r = view.Camera.UnitsPerPixel(c, view.Height) * 60;
            var u = (_base is { } b ? b - c : _normal.AnyPerpendicular()).Normalized();
            var v = Vector3d.Cross(_normal, u);
            for (var i = 0; i < 24; i++)
            {
                var a0 = 2 * Math.PI * i / 24;
                var a1 = 2 * Math.PI * (i + 1) / 24;
                var p0 = c + (u * Math.Cos(a0) + v * Math.Sin(a0)) * r;
                var p1 = c + (u * Math.Cos(a1) + v * Math.Sin(a1)) * r;
                g.Line(p0, p1, DrawColor.AxisBlue.WithAlpha(160), 1);
                g.Line(p0, c + (p0 - c) * 0.88, DrawColor.AxisBlue.WithAlpha(160), 1);
            }
            if (_base is { } bp) g.Line(c, bp, DrawColor.Preview, 1, dashed: true);
            if (_base is not null && Current is { } cur)
            {
                var reach = view.Camera.Distance * 4;
                g.Line(c, c + DirectionAt(AngleDegrees(cur.Position)) * reach, DrawColor.Guide, 1, dashed: true);
            }
            else if (Current is { } cur2)
            {
                g.Line(c, cur2.Position, DrawColor.Preview, 1, dashed: true);
            }
        }
        DrawInference(g);
    }
}
