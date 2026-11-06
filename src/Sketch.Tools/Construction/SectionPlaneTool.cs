using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools.Construction;

/// <summary>
/// Section Plane: hover a face to align the plane with it (the part in front of the face is cut away), click to
/// place it; it becomes the active cut. Arrow keys fix the orientation (↑ horizontal, → red, ← green); Shift
/// keeps the current orientation while moving. Move it later with the Move tool; right-click it to reverse it or
/// turn its cut on/off.
/// </summary>
public sealed class SectionPlaneTool : InferenceToolBase
{
    private Vector3d? _forced;
    private Vector3d? _held;
    private Vector3d _normal = -Vector3d.UnitZ;

    public override string Id => "tools/section plane";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    public override string StatusText =>
        "Click to place the section plane (hover over a face to align with it). ↑ horizontal, → red, ← green; Shift = lock orientation.";

    public override void Activate(IView view) => Reset(view);

    public override void Reset(IView view)
    {
        _forced = null;
        _held = null;
        ClearInference(view);
        view.Invalidate();
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        var axes = view.Model.Axes;
        Vector3d? n = key switch
        {
            ViewKey.Up => -axes.Z,
            ViewKey.Right => axes.X,
            ViewKey.Left => axes.Y,
            _ => null,
        };
        if (n is { } v)
        {
            _forced = _forced is { } f && f.IsEqual(v) ? null : v;
            view.Invalidate();
            return true;
        }
        if (key == ViewKey.Shift)
        {
            _held = _normal;
            return true;
        }
        return false;
    }

    public override bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key != ViewKey.Shift) return false;
        _held = null;
        return true;
    }

    private void UpdateNormal()
    {
        _normal = _held ?? _forced ?? (Current?.Face is { } f ? -f.Normal : -Vector3d.UnitZ);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        UpdateNormal();
        view.Invalidate();
    }

    private static double SizeFor(IView view) => Math.Max(view.ModelBounds.Radius * 0.6, 500);

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var r = UpdateInference(view, e.X, e.Y);
        UpdateNormal();
        var model = view.Model;
        var count = model.Entities.AllSections().Count() + 1;
        SectionPlane? created = null;
        model.Run("Section Plane", () => created = model.ActiveEntities.AddSectionPlane(r.Position, _normal, SizeFor(view), $"Section Plane {count}"));
        if (created is not null) view.Selection.Set(new[] { created });
        view.Invalidate();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (Current is { } c)
        {
            var size = SizeFor(view);
            var u = _normal.AnyPerpendicular();
            if (Math.Abs(_normal.Z) < 0.9) u = Vector3d.Cross(Vector3d.UnitZ, _normal).Normalized();
            var v = Vector3d.Cross(_normal, u).Normalized();
            var p = c.Position;
            var pts = new[] { p - u * size - v * size, p + u * size - v * size, p + u * size + v * size, p - u * size + v * size };
            g.Polygon(pts, new DrawColor(230, 120, 30, 35));
            for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], new DrawColor(230, 120, 30), 2);
            g.Line(p, p + _normal * size * 0.25, new DrawColor(230, 120, 30), 2);
        }
        DrawInference(g);
    }
}
