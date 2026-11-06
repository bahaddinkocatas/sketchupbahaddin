using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools.Construction;

/// <summary>
/// Dimension: click two points, then move to place the dimension line and click. On a face the dimension lies in
/// the face; otherwise horizontal measurements stay horizontal and others stand vertically.
/// </summary>
public sealed class DimensionTool : InferenceToolBase
{
    private Vector3d? _a;
    private Vector3d? _b;
    private Face? _face;
    private Vector3d _offset;

    public override string Id => "tools/dimension";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Length";

    public override string StatusText => _a is null ? "Select first point to dimension."
        : _b is null ? "Select second point." : "Place the dimension.";

    public override string MeasurementValue => _a is { } a
        ? Units.Default.Format(Vector3d.Distance(a, _b ?? Current?.Position ?? a))
        : string.Empty;

    protected override Vector3d? From => _b is null ? _a : null;

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _a = null;
        _b = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    /// <summary>Offset of the dimension line for a cursor point: perpendicular to the measured segment.</summary>
    private Vector3d OffsetFor(IView view, double x, double y)
    {
        var a = _a!.Value;
        var b = _b!.Value;
        var along = (b - a).Normalized();
        Vector3d normal;
        if (_face is { } f && Math.Abs(Vector3d.Dot(f.Normal, along)) < 1e-6) normal = f.Normal;
        else if (Math.Abs(along.Z) < 1e-6) normal = Vector3d.UnitZ;
        else normal = Vector3d.Cross(along, Vector3d.UnitZ).IsZero(1e-9) ? Vector3d.UnitY : Vector3d.Cross(along, Vector3d.UnitZ).Normalized();
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        var plane = new Plane3d(a, normal);
        var t = plane.Intersect(ray);
        if (t is not > 0) return _offset;
        var v = ray.PointAt(t.Value) - a;
        return v - along * Vector3d.Dot(v, along);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_b is null) UpdateInference(view, e.X, e.Y);
        else _offset = OffsetFor(view, e.X, e.Y);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_a is null)
        {
            var r = UpdateInference(view, e.X, e.Y);
            _a = r.Position;
            _face = r.Face;
        }
        else if (_b is null)
        {
            var r = UpdateInference(view, e.X, e.Y);
            if (Vector3d.Distance(r.Position, _a.Value) <= Tolerance.Length) return;
            _b = r.Position;
            _offset = Vector3d.Zero;
            ClearInference(view);
        }
        else
        {
            var (a, b, off) = (_a.Value, _b.Value, OffsetFor(view, e.X, e.Y));
            view.Model.Run("Dimension", () => view.Model.ActiveEntities.AddDimension(a, b, off));
            _a = null;
            _b = null;
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_a is { } a)
        {
            if (_b is { } b)
            {
                g.Line(a, a + _offset, DrawColor.Black, 1);
                g.Line(b, b + _offset, DrawColor.Black, 1);
                g.Line(a + _offset, b + _offset, DrawColor.Black, 1);
                g.Text((a + b) / 2 + _offset, Units.Default.Format(Vector3d.Distance(a, b)), DrawColor.Black);
            }
            else if (Current is { } c)
            {
                g.Line(a, c.Position, RubberBandColor, 1, dashed: true);
            }
        }
        if (_b is null) DrawInference(g);
    }
}

/// <summary>
/// Text: click on geometry to attach a leader note (the default text is the face area, edge length or point
/// coordinates), move to place the label and click. Clicking empty space places plain text.
/// </summary>
public sealed class TextTool : InferenceToolBase
{
    private Vector3d? _anchor;
    private string _defaultText = string.Empty;
    private Vector3d _labelOffset;

    public override string Id => "tools/text";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    public override string StatusText => _anchor is null
        ? "Select a point, edge or face to attach the text to."
        : "Place the text.";

    /// <summary>Asks the user for the text (UI supplies a dialog); null cancels.</summary>
    public Func<string, string?>? AskText { get; set; }

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _anchor = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    private Vector3d LabelOffsetFor(IView view, double x, double y)
    {
        var a = _anchor!.Value;
        var cam = view.Camera;
        var ray = cam.RayThroughPixel(x, y, view.Width, view.Height);
        // Label on the plane through the anchor facing the camera.
        var t = new Plane3d(a, cam.Direction).Intersect(ray);
        return t is > 0 ? ray.PointAt(t.Value) - a : _labelOffset;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_anchor is null) UpdateInference(view, e.X, e.Y);
        else _labelOffset = LabelOffsetFor(view, e.X, e.Y);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_anchor is null)
        {
            var r = UpdateInference(view, e.X, e.Y);
            var units = Units.Default;
            _defaultText = r.Kind switch
            {
                InferenceKind.OnFace when r.Face is { } f => units.FormatArea(Math.Abs(f.Area)),
                InferenceKind.OnEdge or InferenceKind.Midpoint when r.Edge is { } edge => units.Format(edge.Length),
                InferenceKind.None => string.Empty,
                _ => $"[{units.Format(r.Position.X)}; {units.Format(r.Position.Y)}; {units.Format(r.Position.Z)}]",
            };
            if (r.Kind == InferenceKind.None)
            {
                // Empty space: plain text at the point.
                Place(view, r.Position, Vector3d.Zero, leader: false);
                return;
            }
            _anchor = r.Position;
            _labelOffset = Vector3d.Zero;
            ClearInference(view);
        }
        else
        {
            Place(view, _anchor.Value, LabelOffsetFor(view, e.X, e.Y), leader: true);
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private void Place(IView view, Vector3d anchor, Vector3d offset, bool leader)
    {
        var text = AskText?.Invoke(_defaultText) ?? (AskText is null ? _defaultText : null);
        _anchor = null;
        if (string.IsNullOrWhiteSpace(text)) return;
        view.Model.Run("Text", () => view.Model.ActiveEntities.AddNote(anchor, offset, text, leader && !offset.IsZero()));
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_anchor is { } a)
        {
            g.Line(a, a + _labelOffset, DrawColor.Black, 1);
            g.Text(a + _labelOffset, string.IsNullOrEmpty(_defaultText) ? "Text" : _defaultText, DrawColor.Black);
        }
        else
        {
            DrawInference(g);
        }
    }
}
