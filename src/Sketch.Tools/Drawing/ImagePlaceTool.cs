using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools.Drawing;

/// <summary>
/// Places an imported image: click the first corner (on a face the image lies on it, otherwise on the ground),
/// move to size it (proportions are kept), click again. Type a width to place it exactly. Afterwards use the
/// Tape Measure to rescale the drawing to a known dimension.
/// </summary>
public sealed class ImagePlaceTool : InferenceToolBase
{
    private string? _key;
    private string _name = string.Empty;
    private double _aspect = 1; // height / width
    private Vector3d? _origin;
    private Vector3d _normal = Vector3d.UnitZ;
    private Vector3d _u = Vector3d.UnitX, _v = Vector3d.UnitY;
    private double _width;

    public override string Id => "file/import image";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Width";

    public override string StatusText => _origin is null
        ? "Select the image corner."
        : "Select the size or enter width. You can rescale it to the real size afterwards with the Tape Measure.";

    public override string MeasurementValue => _origin is null ? string.Empty : Units.Default.Format(Math.Abs(_width));

    protected override Plane3d? DrawingPlane => _origin is { } o ? new Plane3d(o, _normal) : null;

    /// <summary>Raised after the image is placed (the host switches back to Select).</summary>
    public event EventHandler? Placed;

    /// <summary>Prepares placement of an image already in the model's image store.</summary>
    public void Begin(string key, int pixelWidth, int pixelHeight, string name)
    {
        _key = key;
        _name = name;
        _aspect = pixelWidth > 0 ? (double)pixelHeight / pixelWidth : 1;
        _origin = null;
    }

    public override void Activate(IView view)
    {
        _origin = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override void Reset(IView view)
    {
        _origin = null;
        ClearInference(view);
        NotifyStateChanged();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var r = UpdateInference(view, e.X, e.Y);
        if (_origin is { } o) _width = Vector3d.Dot(r.Position - o, _u);
        NotifyStateChanged();
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left || _key is null) return;
        var r = UpdateInference(view, e.X, e.Y);
        if (_origin is null)
        {
            _origin = r.Position;
            _normal = r.Face?.Normal ?? Vector3d.UnitZ;
            // Picture "right" runs along red projected onto the plane; "up" is perpendicular to it.
            var u = Vector3d.UnitX - _normal * Vector3d.Dot(Vector3d.UnitX, _normal);
            if (u.Length < 0.1) u = Vector3d.UnitY - _normal * Vector3d.Dot(Vector3d.UnitY, _normal);
            _u = u.Normalized();
            _v = Vector3d.Cross(_normal, _u).Normalized();
        }
        else
        {
            Place(view, Vector3d.Dot(r.Position - _origin.Value, _u));
        }
        NotifyStateChanged();
        view.Invalidate();
    }

    private (Vector3d Origin, Vector3d U, Vector3d V) Frame(double width)
    {
        var o = _origin!.Value;
        var w = Math.Abs(width);
        // Dragging to the left places the picture to the left of the first corner.
        var origin = width < 0 ? o - _u * w : o;
        return (origin, _u * w, _v * (w * _aspect));
    }

    private void Place(IView view, double width)
    {
        if (_key is null || Math.Abs(width) <= Tolerance.Length) return;
        var (origin, u, v) = Frame(width);
        var key = _key;
        var name = _name;
        view.Model.Run("Place Image", () => view.Model.ActiveEntities.AddImage(key, origin, u, v, name));
        _origin = null;
        Placed?.Invoke(this, EventArgs.Empty);
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_origin is null || !Units.Default.TryParseLength(text, out var width) || width <= 0) return false;
        Place(view, (_width < 0 ? -1 : 1) * width);
        NotifyStateChanged();
        view.Invalidate();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_origin is not null && Math.Abs(_width) > Tolerance.Length)
        {
            var (o, u, v) = Frame(_width);
            var pts = new[] { o, o + u, o + u + v, o + v };
            g.Polygon(pts, new DrawColor(80, 130, 220, 50));
            for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], DrawColor.Preview, 1.5f);
            g.Line(pts[0], pts[2], DrawColor.Preview, 1, dashed: true);
        }
        DrawInference(g);
    }
}
