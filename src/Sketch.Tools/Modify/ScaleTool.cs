using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Modify;

/// <summary>
/// Scale (S): grips on the selection's bounding box. Corner grips scale uniformly, edge grips scale two axes,
/// face grips one axis, about the opposite grip (Ctrl = about the centre). Type a factor ("1.5"), per-axis
/// factors ("2;1;1") or a length ("3000mm") for the dragged dimension. A negative factor mirrors.
/// </summary>
public sealed class ScaleTool : Tool
{
    private readonly record struct Grip(Vector3d Position, Vector3d Mask);

    private List<Grip> _grips = new();
    private (Vector3d Min, Vector3d Max)? _box;
    private Grip? _hover;
    private Grip? _active;
    private Vector3d _anchor;
    private Vector3d _factor = new(1, 1, 1);
    private readonly TransformPreview _preview = new();
    private List<int> _ids = new();

    public override string Id => "tools/scale";
    public override ToolCursor Cursor => ToolCursor.Crosshair;
    public override string MeasurementLabel => "Scale";

    public override string StatusText => _box is null
        ? "Select the entities to scale (or click one)."
        : _active is null ? "Pick a grip. Corner = uniform, edge = two axes, face = one axis. Ctrl = about center."
        : "Pick the scale or enter a value (e.g. 1.5 or 3000mm).";

    public override string MeasurementValue => _active is null ? string.Empty : FactorText();

    private string FactorText()
    {
        var m = _active!.Value.Mask;
        var values = new List<string>();
        if (m.X != 0) values.Add($"{_factor.X:0.###}");
        if (m.Y != 0) values.Add($"{_factor.Y:0.###}");
        if (m.Z != 0) values.Add($"{_factor.Z:0.###}");
        return values.Distinct().Count() == 1 ? values[0] : string.Join("; ", values);
    }

    public override void Activate(IView view)
    {
        _active = null;
        RebuildGrips(view);
        view.Selection.Changed += OnSelectionChanged;
        _view = view;
    }

    private IView? _view;

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_active is null && _view is not null) RebuildGrips(_view);
    }

    public override void Deactivate(IView view)
    {
        view.Selection.Changed -= OnSelectionChanged;
        if (_active is not null) Cancel(view);
        base.Deactivate(view);
    }

    private void RebuildGrips(IView view)
    {
        _grips.Clear();
        var points = Entities.PointsOf(view.Selection.Items.Where(i => !i.IsDeleted));
        if (points.Count == 0)
        {
            _box = null;
            return;
        }
        var min = points[0];
        var max = min;
        foreach (var p in points)
        {
            min = new Vector3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vector3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        _box = (min, max);
        var size = max - min;
        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        for (var k = -1; k <= 1; k++)
        {
            if (i == 0 && j == 0 && k == 0) continue;
            // Flat boxes (e.g. a single face) have no grips across their zero thickness.
            if ((i != 0 && size.X < Tolerance.Length) || (j != 0 && size.Y < Tolerance.Length) || (k != 0 && size.Z < Tolerance.Length)) continue;
            var p = new Vector3d(Pick(min.X, max.X, i), Pick(min.Y, max.Y, j), Pick(min.Z, max.Z, k));
            _grips.Add(new Grip(p, new Vector3d(i, j, k)));
        }
        NotifyStateChanged();
    }

    private static double Pick(double min, double max, int s) => s < 0 ? min : s > 0 ? max : (min + max) / 2;

    private Grip? GripAt(IView view, double x, double y)
    {
        Grip? best = null;
        var bestD = 10.0;
        foreach (var g in _grips)
        {
            var s = view.Camera.WorldToScreen(g.Position, view.Width, view.Height);
            if (s is null) continue;
            var d = Math.Sqrt((s.Value.X - x) * (s.Value.X - x) + (s.Value.Y - y) * (s.Value.Y - y));
            if (d < bestD)
            {
                bestD = d;
                best = g;
            }
        }
        return best;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_active is { } grip)
        {
            var ray = view.Camera.RayThroughPixel(e.X, e.Y, view.Width, view.Height);
            var dir = grip.Position - _anchor;
            var len = dir.Length;
            if (len > Tolerance.Length)
            {
                var p = InferenceEngine.ClosestOnLineToRay(_anchor, dir / len, ray);
                var f = Vector3d.Dot(p - _anchor, dir / len) / len;
                if (Math.Abs(f) < 1e-3) f = 1e-3;
                Apply(view, MaskedFactor(grip.Mask, f, f, f));
            }
        }
        else
        {
            var hover = GripAt(view, e.X, e.Y);
            if (hover != _hover)
            {
                _hover = hover;
                view.Invalidate();
            }
        }
        NotifyStateChanged();
    }

    private static Vector3d MaskedFactor(Vector3d mask, double fx, double fy, double fz) =>
        new(mask.X != 0 ? fx : 1, mask.Y != 0 ? fy : 1, mask.Z != 0 ? fz : 1);

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        if (_active is not null)
        {
            Finish(view);
            return;
        }
        if (_box is null || GripAt(view, e.X, e.Y) is not { } grip)
        {
            // No grip under the cursor: pick what is there to scale.
            var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
            var picked = deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
            if (picked is null)
            {
                // Clicking empty space ends the tool.
                RequestExit();
                return;
            }
            view.Selection.Set(new[] { picked });
            RebuildGrips(view);
            view.Invalidate();
            return;
        }
        var (min, max) = _box.Value;
        var center = (min + max) / 2;
        _anchor = e.Modifiers.HasFlag(KeyModifiers.Ctrl)
            ? center
            : new Vector3d(Pick(min.X, max.X, -(int)grip.Mask.X), Pick(min.Y, max.Y, -(int)grip.Mask.Y), Pick(min.Z, max.Z, -(int)grip.Mask.Z));
        _active = grip;
        _ids = view.Selection.Items.Select(i => i.Id).ToList();
        view.Model.StartOperation("Scale");
        _preview.Begin(view, _ids);
        _factor = new Vector3d(1, 1, 1);
        NotifyStateChanged();
    }

    private Matrix4d ScaleMatrix(Vector3d f) =>
        Matrix4d.Translation(_anchor) * Matrix4d.Scaling(f.X, f.Y, f.Z) * Matrix4d.Translation(-_anchor);

    private void Apply(IView view, Vector3d factor)
    {
        if (_preview.Container is not { } ents) return;
        _factor = factor;
        _preview.Transform(ScaleMatrix(factor));
        view.Selection.Set(ents.FindByIds(_ids));
        view.Invalidate();
    }

    private void Finish(IView view)
    {
        view.Model.CommitOperation();
        _active = null;
        _preview.End();
        RebuildGrips(view);
        view.Invalidate();
    }

    private void Cancel(IView view)
    {
        view.Model.AbortOperation();
        view.Selection.Set(view.Model.ActiveEntities.FindByIds(_ids));
        _active = null;
        _preview.End();
        RebuildGrips(view);
    }

    public override void Reset(IView view)
    {
        if (_active is not null) Cancel(view);
        view.Invalidate();
    }

    public override bool OnUserText(IView view, string text)
    {
        if (_active is not { } grip || _box is null) return false;
        var parts = Units.SplitList(text);
        double[] factors;
        if (parts.Length == 1 && parts[0].Any(char.IsLetter) && Units.Default.TryParseLength(parts[0], out var length))
        {
            // A length: scale so the dragged dimension becomes that long.
            var current = (grip.Position - _anchor).Length;
            if (current < Tolerance.Length) return false;
            var f = length / current;
            factors = new[] { f, f, f };
        }
        else
        {
            var values = new List<double>();
            foreach (var p in parts)
            {
                if (!Units.TryParseNumber(p, out var v) || v == 0) return false;
                values.Add(v);
            }
            if (values.Count == 0) return false;
            // One value for all masked axes, or one per masked axis in X, Y, Z order.
            var masked = new[] { grip.Mask.X != 0, grip.Mask.Y != 0, grip.Mask.Z != 0 };
            factors = new double[3];
            var idx = 0;
            for (var i = 0; i < 3; i++)
                factors[i] = masked[i] ? values[Math.Min(idx++, values.Count - 1)] : 1;
        }
        Apply(view, MaskedFactor(grip.Mask, factors[0], factors[1], factors[2]));
        Finish(view);
        NotifyStateChanged();
        return true;
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_box is not { } box) return;
        if (_active is null)
        {
            var (n, x) = box;
            var c = new[]
            {
                new Vector3d(n.X, n.Y, n.Z), new Vector3d(x.X, n.Y, n.Z), new Vector3d(x.X, x.Y, n.Z), new Vector3d(n.X, x.Y, n.Z),
                new Vector3d(n.X, n.Y, x.Z), new Vector3d(x.X, n.Y, x.Z), new Vector3d(x.X, x.Y, x.Z), new Vector3d(n.X, x.Y, x.Z),
            };
            int[][] edges = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 },
                new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
            foreach (var e in edges) g.Line(c[e[0]], c[e[1]], new DrawColor(220, 60, 60, 200), 1);
            foreach (var grip in _grips)
                g.Point(grip.Position, grip == _hover ? new DrawColor(255, 0, 0) : new DrawColor(0, 150, 0), grip == _hover ? 11 : 8);
        }
        else
        {
            g.Point(_anchor, new DrawColor(220, 0, 0), 9, PointShape.Cross);
        }
    }
}
