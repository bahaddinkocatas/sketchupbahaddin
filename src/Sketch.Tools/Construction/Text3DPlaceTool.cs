using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Construction;

/// <summary>
/// Places 3D Text prepared by the host (outlines from the "3D Metin" dialog): the text follows the cursor and
/// stands on the face under it (lies flat on horizontal faces and on the ground); click to place it as a group.
/// </summary>
public sealed class Text3DPlaceTool : InferenceToolBase
{
    private IReadOnlyList<IReadOnlyList<Vector2d>> _loops = Array.Empty<IReadOnlyList<Vector2d>>();
    private bool _filled;
    private double _extrude;
    private string _name = "3D Text";

    public override string Id => "tools/3d text";
    public override ToolCursor Cursor => ToolCursor.Move;
    public override string StatusText => "Click to place the text (hover over a face to place it on the face). Esc = cancel.";

    /// <summary>Raised after the text was placed (the host returns to the Select tool).</summary>
    public event EventHandler? Placed;

    /// <summary>Glyph outlines in model units with x to the right and y up; the origin is the text's insertion point.</summary>
    public void Prepare(IReadOnlyList<IReadOnlyList<Vector2d>> loops, bool filled, double extrude, string name)
    {
        _loops = loops;
        _filled = filled;
        _extrude = extrude;
        _name = name;
    }

    private (Vector3d Origin, Vector3d X, Vector3d Y, Vector3d N) Frame()
    {
        var p = Current?.Position ?? Vector3d.Zero;
        var n = Current?.Face?.Normal ?? Vector3d.UnitZ;
        if (Math.Abs(n.Z) < 0.5)
        {
            var x = Vector3d.Cross(Vector3d.UnitZ, n).Normalized();
            return (p, x, Vector3d.Cross(n, x).Normalized(), n);
        }
        var ex = Vector3d.UnitX;
        return (p, ex, Vector3d.Cross(n, ex).Normalized(), n);
    }

    private List<IReadOnlyList<Vector3d>> WorldLoops()
    {
        var (o, x, y, _) = Frame();
        return _loops.Select(l => (IReadOnlyList<Vector3d>)l.Select(q => o + x * q.X + y * q.Y).ToList()).ToList();
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        UpdateInference(view, e.X, e.Y);
        view.Invalidate();
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left || _loops.Count == 0) return;
        UpdateInference(view, e.X, e.Y);
        var loops = WorldLoops();
        var normal = Frame().N;
        Group? group = null;
        view.Model.Run("3D Text", () => group = Text3D.Create(view.Model.ActiveEntities, loops, normal, _filled, _extrude, _name));
        if (group is not null) view.Selection.Set(new[] { group });
        _loops = Array.Empty<IReadOnlyList<Vector2d>>();
        Placed?.Invoke(this, EventArgs.Empty);
    }

    public override void Reset(IView view)
    {
        _loops = Array.Empty<IReadOnlyList<Vector2d>>();
        ClearInference(view);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (Current is null) return;
        foreach (var loop in WorldLoops())
            for (var i = 0; i < loop.Count; i++) g.Line(loop[i], loop[(i + 1) % loop.Count], DrawColor.Black, 1);
        DrawInference(g);
    }
}
