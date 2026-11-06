using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools.Modify;

/// <summary>
/// Eraser (E): click or drag over edges to erase them (faces they bound disappear; an edge between coplanar
/// faces heals them). Clicking a group erases the whole group. Shift = hide instead, Ctrl = soften/smooth,
/// Ctrl+Shift = unsoften.
/// </summary>
public sealed class EraserTool : Tool
{
    private readonly List<Entity> _marked = new();
    private bool _down;
    private KeyModifiers _modifiers;

    public override string Id => "tools/eraser";
    public override ToolCursor Cursor => ToolCursor.Eraser;
    public override string StatusText => "Click or drag over edges to erase. Shift = hide, Ctrl = soften/smooth, Ctrl+Shift = unsoften.";

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _down = true;
        _modifiers = e.Modifiers;
        _marked.Clear();
        Mark(view, e);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_down)
        {
            _modifiers = e.Modifiers;
            Mark(view, e);
        }
    }

    private void Mark(IView view, ViewMouseEvent e)
    {
        var edge = view.Inference.PickEdge(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y));
        if (edge is null) return;
        // Inside the active container the edge itself is erased; geometry in a group erases the group.
        var target = Entities.AncestorIn(edge, view.Model.ActiveEntities);
        if (target is not null && !_marked.Contains(target))
        {
            _marked.Add(target);
            view.Invalidate();
        }
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (!_down || e.Button != MouseButton.Left) return;
        _down = false;
        if (_marked.Count == 0)
        {
            if (view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, e.X, e.Y)) is null) RequestExit();
            return;
        }
        var items = _marked.Where(x => !x.IsDeleted).ToList();
        _marked.Clear();
        var ctrl = _modifiers.HasFlag(KeyModifiers.Ctrl);
        var shift = _modifiers.HasFlag(KeyModifiers.Shift);
        var model = view.Model;
        var edges = items.OfType<Edge>().ToList();

        if (ctrl && shift)
            model.Run("Unsoften", () => SetFlags(model, edges, soft: false, smooth: false));
        else if (ctrl)
            model.Run("Soften", () => SetFlags(model, edges, soft: true, smooth: true));
        else if (shift)
            model.Run("Hide", () =>
            {
                foreach (var item in items) item.Hidden = true;
                model.Entities.Touch();
            });
        else
            model.Run("Erase", () => model.ActiveEntities.Erase(items));
        view.Invalidate();
    }

    private static void SetFlags(SketchModel model, IEnumerable<Edge> edges, bool soft, bool smooth)
    {
        foreach (var e in edges)
        {
            e.Soft = soft;
            e.Smooth = smooth;
        }
        model.Entities.Touch();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        foreach (var item in _marked.Where(x => !x.IsDeleted))
        {
            var edges = item switch
            {
                Edge e => new[] { e },
                Group grp => grp.Entities.AllEdges().ToArray(),
                _ => Array.Empty<Edge>(),
            };
            foreach (var e in edges) g.Line(e.Start.Position, e.End.Position, DrawColor.Selection, 3);
        }
    }

    public override void Reset(IView view)
    {
        _down = false;
        _marked.Clear();
    }
}
