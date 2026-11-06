using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Modify;

/// <summary>
/// Flip (mirror). Select geometry (or click it with the tool), then click one of the three planes through the
/// selection's centre to mirror it about that plane. Drag a plane along its axis first to mirror about another
/// position. Hovering a face offers that face's plane (magenta). Ctrl toggles copy mode (the original stays);
/// → / ← / ↑ choose the red / green / blue plane, then click or press Enter. Esc resets the planes.
/// </summary>
public sealed class FlipTool : Tool
{
    private static readonly Vector3d[] Normals = { Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ };
    private static readonly DrawColor[] Colors = { new(214, 0, 0), new(0, 168, 0), new(0, 0, 230) };
    private static readonly string[] Names = { "Red", "Green", "Blue" };

    private (Vector3d Min, Vector3d Max)? _box;
    private readonly double[] _offset = new double[3];
    private int? _hoverPlane;
    private int? _lockedPlane;
    private (Vector3d Point, Vector3d Normal, Face Face)? _custom;
    private Entity? _hoverEntity;
    private bool _copy;
    private bool _ctrlTap;

    private int? _pressPlane;
    private (Vector3d Point, Vector3d Normal)? _pressCustom;
    private double _pressX, _pressY;
    private bool _dragging;
    private IView? _view;

    public override string Id => "tools/flip";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    public override string StatusText => _box is null
        ? "Select objects to flip, or click one."
        : $"Click a plane to flip about (drag to move the plane). Hover over a face = that face's plane. " +
          $"Ctrl = copy ({(_copy ? "ON" : "off")}), → red, ← green, ↑ blue plane.";

    public override void Activate(IView view)
    {
        _view = view;
        view.Selection.Changed += OnSelectionChanged;
        Rebuild(view);
    }

    public override void Deactivate(IView view)
    {
        view.Selection.Changed -= OnSelectionChanged;
        base.Deactivate(view);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_view is not null && !_dragging) Rebuild(_view);
    }

    private void Rebuild(IView view)
    {
        var vertices = Entities.PointsOf(view.Selection.Items.Where(i => !i.IsDeleted));
        if (vertices.Count == 0)
        {
            _box = null;
            NotifyStateChanged();
            view.Invalidate();
            return;
        }
        var min = vertices[0];
        var max = min;
        foreach (var p in vertices)
        {
            min = new Vector3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vector3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        _box = (min, max);
        var c = (min + max) / 2;
        _offset[0] = c.X;
        _offset[1] = c.Y;
        _offset[2] = c.Z;
        NotifyStateChanged();
        view.Invalidate();
    }

    private Vector3d PlanePoint(int axis)
    {
        var (min, max) = _box!.Value;
        var c = (min + max) / 2;
        return axis switch
        {
            0 => c with { X = _offset[0] },
            1 => c with { Y = _offset[1] },
            _ => c with { Z = _offset[2] },
        };
    }

    /// <summary>Corners of a plane, padded around the selection so that flat selections still show it.</summary>
    private Vector3d[] PlaneCorners(int axis)
    {
        var (min, max) = _box!.Value;
        var size = max - min;
        var pad = Math.Max(Math.Max(size.X, Math.Max(size.Y, size.Z)) * 0.12, 10);
        var lo = min - new Vector3d(pad, pad, pad);
        var hi = max + new Vector3d(pad, pad, pad);
        var o = _offset[axis];
        return axis switch
        {
            0 => new[] { new Vector3d(o, lo.Y, lo.Z), new Vector3d(o, hi.Y, lo.Z), new Vector3d(o, hi.Y, hi.Z), new Vector3d(o, lo.Y, hi.Z) },
            1 => new[] { new Vector3d(lo.X, o, lo.Z), new Vector3d(hi.X, o, lo.Z), new Vector3d(hi.X, o, hi.Z), new Vector3d(lo.X, o, hi.Z) },
            _ => new[] { new Vector3d(lo.X, lo.Y, o), new Vector3d(hi.X, lo.Y, o), new Vector3d(hi.X, hi.Y, o), new Vector3d(lo.X, hi.Y, o) },
        };
    }

    private int? PlaneUnderCursor(IView view, double x, double y)
    {
        if (_box is null) return null;
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        int? best = null;
        var bestT = double.MaxValue;
        for (var axis = 0; axis < 3; axis++)
        {
            var n = Normals[axis];
            var denom = Vector3d.Dot(ray.Direction, n);
            if (Math.Abs(denom) < 1e-6) continue;
            var t = Vector3d.Dot(PlanePoint(axis) - ray.Origin, n) / denom;
            var p = ray.PointAt(t);
            var corners = PlaneCorners(axis);
            var lo = corners[0];
            var hi = corners[2];
            bool Within(double v, double a, double b) => v >= Math.Min(a, b) - 1e-6 && v <= Math.Max(a, b) + 1e-6;
            var inside = axis switch
            {
                0 => Within(p.Y, lo.Y, hi.Y) && Within(p.Z, lo.Z, hi.Z),
                1 => Within(p.X, lo.X, hi.X) && Within(p.Z, lo.Z, hi.Z),
                _ => Within(p.X, lo.X, hi.X) && Within(p.Y, lo.Y, hi.Y),
            };
            if (inside && t < bestT)
            {
                bestT = t;
                best = axis;
            }
        }
        return best;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_pressPlane is { } axis)
        {
            if (!_dragging && Math.Abs(e.X - _pressX) + Math.Abs(e.Y - _pressY) > 4) _dragging = true;
            if (_dragging)
            {
                // The plane slides along its axis; snapping to a point under the cursor places it exactly there.
                var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
                var inferred = view.Inference.Infer(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
                var onAxis = inferred.Kind is InferenceKind.Endpoint or InferenceKind.Midpoint or InferenceKind.OnEdge
                    ? inferred.Position
                    : InferenceEngine.ClosestOnLineToRay(PlanePoint(axis), Normals[axis], ray);
                _offset[axis] = axis switch { 0 => onAxis.X, 1 => onAxis.Y, _ => onAxis.Z };
                view.Invalidate();
            }
            return;
        }
        if (_box is null)
        {
            var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
            var hover = deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
            if (!ReferenceEquals(hover, _hoverEntity))
            {
                _hoverEntity = hover;
                view.Invalidate();
            }
            return;
        }
        var plane = PlaneUnderCursor(view, e.X, e.Y);
        (Vector3d, Vector3d, Face)? custom = null;
        if (plane is null)
        {
            var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
            if (view.Inference.FaceInference(ray) is { Face: { } f } hit) custom = (hit.Position, f.Normal, f);
        }
        if (plane != _hoverPlane || !Equals(custom?.Item3, _custom?.Face))
        {
            _hoverPlane = plane;
            _custom = custom;
            view.Invalidate();
        }
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_box is null)
        {
            if (_hoverEntity is { IsDeleted: false } picked)
            {
                view.Selection.Set(new[] { picked });
                Rebuild(view);
            }
            else RequestExit();
            return;
        }
        _pressX = e.X;
        _pressY = e.Y;
        _dragging = false;
        _pressPlane = PlaneUnderCursor(view, e.X, e.Y) ?? _lockedPlane;
        _pressCustom = _pressPlane is null && _custom is { } c ? (c.Point, c.Normal) : null;
        if (_pressPlane is null && _pressCustom is null)
        {
            // Clicking elsewhere picks new geometry to flip.
            var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
            var picked = deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
            if (picked is not null) view.Selection.Set(new[] { picked });
            else RequestExit();
        }
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var axis = _pressPlane;
        var custom = _pressCustom;
        _pressPlane = null;
        _pressCustom = null;
        _dragging = false;
        if (axis is { } a) Flip(view, PlanePoint(a), Normals[a]);
        else if (custom is { } c) Flip(view, c.Point, c.Normal);
    }

    public override bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers)
    {
        // Copy mode toggles on a Ctrl *tap* (as in Move and Rotate): Ctrl+Z etc. must not toggle it.
        if (key == ViewKey.Ctrl)
        {
            _ctrlTap = true;
            return true;
        }
        _ctrlTap = false;
        switch (key)
        {
            case ViewKey.Right:
                LockPlane(view, 0);
                return true;
            case ViewKey.Left:
                LockPlane(view, 1);
                return true;
            case ViewKey.Up:
                LockPlane(view, 2);
                return true;
            case ViewKey.Enter when _lockedPlane is { } a && _box is not null:
                Flip(view, PlanePoint(a), Normals[a]);
                return true;
        }
        return false;
    }

    public override bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers)
    {
        if (key == ViewKey.Ctrl && _ctrlTap)
        {
            _ctrlTap = false;
            _copy = !_copy;
            NotifyStateChanged();
            view.Invalidate();
            return true;
        }
        return base.OnKeyUp(view, key, modifiers);
    }

    private void LockPlane(IView view, int axis)
    {
        _lockedPlane = _lockedPlane == axis ? null : axis;
        view.ShowTooltip(_lockedPlane is null ? null : $"{Names[axis]} Plane", _pressX, _pressY);
        view.Invalidate();
    }

    private void Flip(IView view, Vector3d point, Vector3d normal)
    {
        var model = view.Model;
        var items = view.Selection.Items.Where(i => !i.IsDeleted).ToList();
        if (items.Count == 0) return;
        var mirror = Matrix4d.Reflection(point, normal);
        List<Entity>? copies = null;
        model.Run(_copy ? "Flip Copy" : "Flip", () =>
        {
            if (_copy) copies = MoveCopy.CopyTransformed(model.ActiveEntities, items, mirror);
            else MoveCopy.Transform(model.ActiveEntities, items, mirror);
        });
        if (copies is not null) view.Selection.Set(copies);
        // Copy mode lasts for one copy, as in SketchUp.
        _copy = false;
        _lockedPlane = null;
        Rebuild(view);
    }

    /// <summary>Mirrors the selection about the plane through its centre perpendicular to an axis (Flip Red/Green/Blue).</summary>
    public static void FlipSelection(IView view, int axis)
    {
        var items = view.Selection.Items.Where(i => !i.IsDeleted).ToList();
        var vertices = Entities.PointsOf(items);
        if (vertices.Count == 0) return;
        var min = vertices.Aggregate((a, b) => new Vector3d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
        var max = vertices.Aggregate((a, b) => new Vector3d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
        var mirror = Matrix4d.Reflection((min + max) / 2, Normals[axis]);
        view.Model.Run("Flip", () => MoveCopy.Transform(view.Model.ActiveEntities, items, mirror));
        view.Invalidate();
    }

    public override void Reset(IView view)
    {
        _copy = false;
        _lockedPlane = null;
        _pressPlane = null;
        _dragging = false;
        Rebuild(view);
        view.ShowTooltip(null, 0, 0);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_box is null)
        {
            if (_hoverEntity is not null)
                foreach (var e in PaintBucketTool.OutlineEdges(new[] { _hoverEntity }))
                    g.Line(e.Start.Position, e.End.Position, DrawColor.Selection.WithAlpha(150), 2, depthTested: true);
            return;
        }
        var active = _pressPlane ?? _hoverPlane ?? _lockedPlane;
        for (var axis = 0; axis < 3; axis++)
        {
            var corners = PlaneCorners(axis);
            var strong = axis == active;
            g.Polygon(corners, Colors[axis].WithAlpha(strong ? (byte)95 : (byte)38));
            for (var i = 0; i < 4; i++)
                g.Line(corners[i], corners[(i + 1) % 4], Colors[axis].WithAlpha(strong ? (byte)230 : (byte)120), strong ? 2 : 1);
        }
        if (active is null && _custom is { } c)
        {
            foreach (var loop in c.Face.Loops)
                foreach (var (a, b) in loop.Segments())
                    g.Line(a.Position, b.Position, DrawColor.Magenta, 2, depthTested: true);
            var pts = c.Face.OuterLoop.Vertices.Select(v => v.Position).ToList();
            g.Polygon(pts, DrawColor.Magenta.WithAlpha(60));
        }
        if (_copy) g.Text(PlanePoint(2), "+", DrawColor.Black, 0, -18);
    }
}
