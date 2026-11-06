using Sketch.Geometry;
using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools;

/// <summary>
/// Select (Space). Click to select; Ctrl adds, Shift toggles, Ctrl+Shift removes. Drag left-to-right for a window
/// selection (fully inside), right-to-left for a crossing selection (touching). Double-click a face to add its
/// edges, triple-click to select all connected geometry.
/// </summary>
public sealed class SelectTool : Tool
{
    private bool _down;
    private bool _dragging;
    private double _x0, _y0, _x1, _y1;
    private KeyModifiers _modifiers;
    private int _clickCount;
    private DateTime _lastClick = DateTime.MinValue;
    private double _lastClickX, _lastClickY;
    private Entity? _hover;

    public override string Id => "tools/select";
    public override string StatusText => "Select objects. Ctrl = add, Shift = add/subtract, Ctrl+Shift = subtract. Drag to select multiple.";
    public override ToolCursor Cursor => ToolCursor.Select;

    private static InferenceQuery Query(IView view, double x, double y) => new(view.Camera, view.Width, view.Height, x, y);

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        _down = true;
        _dragging = false;
        _x0 = _x1 = e.X;
        _y0 = _y1 = e.Y;
        _modifiers = e.Modifiers;
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        if (_down)
        {
            _x1 = e.X;
            _y1 = e.Y;
            if (!_dragging && (Math.Abs(_x1 - _x0) > 3 || Math.Abs(_y1 - _y0) > 3)) _dragging = true;
            if (_dragging) view.Invalidate();
            return;
        }
        var deep = view.Inference.PickEntity(Query(view, e.X, e.Y));
        var hover = deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
        if (!ReferenceEquals(hover, _hover))
        {
            _hover = hover;
            view.Invalidate();
        }
    }

    public override void OnMouseUp(IView view, ViewMouseEvent e)
    {
        if (!_down || e.Button != MouseButton.Left) return;
        _down = false;
        if (_dragging)
        {
            _dragging = false;
            var crossing = _x1 < _x0;
            Apply(view, ScreenRegion.Rectangle(_x0, _y0, _x1, _y1).Pick(view, crossing), _modifiers);
            view.Invalidate();
            return;
        }

        var now = DateTime.UtcNow;
        // Only clicks at (nearly) the same spot make a double/triple click; two quick clicks on different faces
        // are two separate selections.
        var samePlace = Math.Abs(e.X - _lastClickX) <= 4 && Math.Abs(e.Y - _lastClickY) <= 4;
        _clickCount = (now - _lastClick).TotalMilliseconds < 450 && samePlace ? _clickCount + 1 : 1;
        _lastClick = now;
        _lastClickX = e.X;
        _lastClickY = e.Y;

        var deep = view.Inference.PickEntity(Query(view, e.X, e.Y));
        var active = view.Model.ActiveEntities;
        var picked = deep is null ? null : Entities.AncestorIn(deep, active);
        if (picked is null)
        {
            // Clicking empty space or geometry outside the group being edited closes the group.
            if (view.Model.EditPath.Count > 0 && _modifiers == KeyModifiers.None)
            {
                view.Model.ExitGroup();
                view.Selection.Clear();
                view.Invalidate();
                return;
            }
            if (_modifiers == KeyModifiers.None) view.Selection.Clear();
            return;
        }

        if (_clickCount == 2 && picked is Group group)
        {
            // Double-click a group to edit its content.
            view.Model.EnterGroup(group);
            view.Selection.Clear();
            view.Invalidate();
            return;
        }

        IEnumerable<Entity> items = _clickCount switch
        {
            1 => new[] { picked },
            2 => Neighbourhood(picked),
            _ => picked is Group ? new[] { picked } : Entities.ConnectedGeometry(picked),
        };
        if (_clickCount > 1 && _modifiers == KeyModifiers.None)
            view.Selection.Set(items);
        else
            Apply(view, items, _modifiers);
    }

    /// <summary>Double-click: a face with its edges, or an edge with its faces.</summary>
    private static IEnumerable<Entity> Neighbourhood(Entity e) => e switch
    {
        Face f => f.Edges.Cast<Entity>().Prepend(f),
        Edge edge => edge.Faces.Cast<Entity>().Prepend(edge),
        _ => new[] { e },
    };

    internal static void Apply(IView view, IEnumerable<Entity> items, KeyModifiers m)
    {
        var ctrl = m.HasFlag(KeyModifiers.Ctrl);
        var shift = m.HasFlag(KeyModifiers.Shift);
        if (ctrl && shift) view.Selection.Remove(items);
        else if (ctrl) view.Selection.Add(items);
        else if (shift) view.Selection.Toggle(items);
        else view.Selection.Set(items);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        if (_dragging)
        {
            var cam = view.Camera;
            // Any depth inside the clip range works (drawn without depth test); the target depth always is.
            var depth = cam.Distance;
            Vector3d W(double x, double y) => cam.RayThroughPixel(x, y, view.Width, view.Height).PointAt(depth);
            var pts = new[] { W(_x0, _y0), W(_x1, _y0), W(_x1, _y1), W(_x0, _y1) };
            var crossing = _x1 < _x0;
            for (var i = 0; i < 4; i++) g.Line(pts[i], pts[(i + 1) % 4], DrawColor.Black, 1, dashed: crossing);
            return;
        }
        // Hover highlight.
        switch (_hover)
        {
            case Edge { IsDeleted: false } e when !view.Selection.Contains(e):
                g.Line(e.Start.Position, e.End.Position, DrawColor.Selection.WithAlpha(160), 2, depthTested: true);
                break;
            case Face { IsDeleted: false } f when !view.Selection.Contains(f):
                foreach (var loop in f.Loops)
                    foreach (var (a, b) in loop.Segments())
                        g.Line(a.Position, b.Position, DrawColor.Selection.WithAlpha(160), 2, depthTested: true);
                break;
            case Group { IsDeleted: false } grp when !view.Selection.Contains(grp):
                // A group lights up as a whole: its edges and the outlines of any pictures in it (no box while
                // hovering; the box only appears once the group is selected).
                var c = DrawColor.Selection.WithAlpha(110);
                foreach (var e in grp.Entities.AllEdges().Where(Entities.IsEffectivelyVisible))
                    g.Line(e.Start.Position, e.End.Position, c, 2, depthTested: true);
                foreach (var img in grp.Entities.AllImages().Where(Entities.IsEffectivelyVisible))
                {
                    // Drawn on top: a depth-tested line in the picture's own plane would fight with it.
                    var k = img.Corners;
                    for (var i = 0; i < 4; i++) g.Line(k[i], k[(i + 1) % 4], c, 2);
                }
                break;
        }
    }

    public override void Reset(IView view)
    {
        _down = false;
        _dragging = false;
        // Esc with nothing selected closes the group being edited.
        if (view.Selection.IsEmpty && view.Model.EditPath.Count > 0) view.Model.ExitGroup();
        view.Selection.Clear();
    }
}
