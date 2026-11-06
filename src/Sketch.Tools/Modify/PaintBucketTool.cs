using Sketch.Inference;
using Sketch.Model;

namespace Sketch.Tools.Modify;

/// <summary>
/// Paint Bucket (B): click a face to paint it with the current material (a group gets the material for all its
/// unpainted faces). Alt+click samples a material. Ctrl paints all connected faces, Shift all faces in the
/// container with the same material.
/// </summary>
public sealed class PaintBucketTool : Tool
{
    private Entity? _hover;
    private List<Entity> _flash = new();
    private DateTime _flashUntil;

    public override string Id => "tools/paint";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    /// <summary>Material to apply; null paints the default (removes the material).</summary>
    public Material? Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            NotifyStateChanged();
        }
    }

    private Material? _current;

    /// <summary>Raised when Alt+click picks a material, so the Materials panel can show it.</summary>
    public event EventHandler? CurrentChanged;

    public override string StatusText =>
        $"Material: {Current?.Name ?? "Default"}. Click a face or group to paint it (with a selection, the whole selection is painted). Pick materials in the Materials panel. Alt = sample, Ctrl = fill connected, Shift = replace all.";

    private static Entity? Pick(IView view, double x, double y)
    {
        // Faces first (the surface under the cursor), then edges, so a group made only of edges can be painted too.
        var ray = view.Camera.RayThroughPixel(x, y, view.Width, view.Height);
        Entity? deep = view.Inference.FaceInference(ray)?.Face
                       ?? view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, x, y));
        var target = deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
        return target is Face or Group ? target : null;
    }

    /// <summary>With a preselection, clicking any selected entity paints the whole selection (as in SketchUp).</summary>
    private static List<Entity> Targets(IView view, Entity clicked) =>
        view.Selection.Contains(clicked)
            ? view.Selection.Items.Where(e => e is Face or Group && !e.IsDeleted).ToList()
            : new List<Entity> { clicked };

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var hover = Pick(view, e.X, e.Y);
        if (!ReferenceEquals(hover, _hover))
        {
            _hover = hover;
            view.ShowTooltip(hover is null ? null : Current?.Name ?? "Default", e.X, e.Y);
            view.Invalidate();
        }
    }

    public override void Deactivate(IView view)
    {
        view.ShowTooltip(null, 0, 0);
        base.Deactivate(view);
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var target = Pick(view, e.X, e.Y);
        if (target is null)
        {
            RequestExit();
            return;
        }

        if (e.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            Current = target switch
            {
                Face f => Entities.EffectiveMaterial(f),
                Group g => g.Material,
                _ => Current,
            };
            return;
        }

        var model = view.Model;
        var material = Current;
        var targets = Targets(view, target);
        model.Run("Paint", () =>
        {
            foreach (var t in targets)
            {
                switch (t)
                {
                    case Group g:
                        g.Material = material;
                        break;
                    case Face f when e.Modifiers.HasFlag(KeyModifiers.Ctrl):
                    {
                        // Fill (Ctrl) / replace on object (Ctrl+Shift): connected faces with the same material.
                        var old = f.Material;
                        foreach (var face in Entities.ConnectedGeometry(f).OfType<Face>().Where(x => x.Material == old)) face.Material = material;
                        break;
                    }
                    case Face f when e.Modifiers.HasFlag(KeyModifiers.Shift):
                    {
                        // Replace: every face in the context with the same material.
                        var old = f.Material;
                        foreach (var face in f.Parent!.Faces.Where(x => x.Material == old)) face.Material = material;
                        break;
                    }
                    case Face f:
                        f.Material = material;
                        break;
                }
            }
            model.Entities.Touch();
        });
        _flash = targets;
        _flashUntil = DateTime.UtcNow.AddMilliseconds(350);
        view.Invalidate();
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        var highlight = DateTime.UtcNow < _flashUntil ? _flash : _hover is null ? new List<Entity>() : new List<Entity> { _hover };
        foreach (var e in OutlineEdges(highlight))
            g.Line(e.Start.Position, e.End.Position, DrawColor.Selection.WithAlpha(150), 2, depthTested: true);
    }

    internal static IEnumerable<Edge> OutlineEdges(IEnumerable<Entity> entities)
    {
        foreach (var x in entities)
        {
            var edges = x switch
            {
                Face { IsDeleted: false } f => f.Edges,
                Edge { IsDeleted: false } edge => new[] { edge },
                Group { IsDeleted: false } grp => grp.Entities.AllEdges(),
                _ => Enumerable.Empty<Edge>(),
            };
            foreach (var e in edges) yield return e;
        }
    }
}

/// <summary>
/// Tag tool: click a face, edge or group to give it the active tag. Alt+click samples the tag of what you click
/// (making it the active tag). Shift tags every entity in the container that has the same tag as the clicked one.
/// </summary>
public sealed class TagTool : Tool
{
    private Entity? _hover;
    private List<Entity> _flash = new();
    private DateTime _flashUntil;

    public override string Id => "tools/tag";
    public override ToolCursor Cursor => ToolCursor.Crosshair;

    /// <summary>Name of the tag that clicks apply (shown in the status bar).</summary>
    public Func<string>? ActiveTagName { get; set; }

    public override string StatusText =>
        $"Tag: {ActiveTagName?.Invoke() ?? TagCollection.UntaggedName}. Click an entity to apply the tag (with a selection, the whole selection is tagged). Pick the tag in the Tag box or the Tags panel. Alt = sample, Shift = replace all.";

    private static Entity? Pick(IView view, double x, double y)
    {
        var deep = view.Inference.PickEntity(new InferenceQuery(view.Camera, view.Width, view.Height, x, y));
        return deep is null ? null : Entities.AncestorIn(deep, view.Model.ActiveEntities);
    }

    public override void OnMouseMove(IView view, ViewMouseEvent e)
    {
        var hover = Pick(view, e.X, e.Y);
        if (!ReferenceEquals(hover, _hover))
        {
            _hover = hover;
            view.ShowTooltip(hover is null ? null : hover.Tag?.Name ?? TagCollection.UntaggedName, e.X, e.Y);
            view.Invalidate();
        }
    }

    public override void OnMouseDown(IView view, ViewMouseEvent e)
    {
        if (e.Button != MouseButton.Left) return;
        var target = Pick(view, e.X, e.Y);
        if (target is null)
        {
            RequestExit();
            return;
        }
        var model = view.Model;
        if (e.Modifiers.HasFlag(KeyModifiers.Alt))
        {
            model.ActiveTag = target.Tag;
            model.RaiseDisplayChanged();
            view.ShowTooltip(target.Tag?.Name ?? TagCollection.UntaggedName, e.X, e.Y);
            NotifyStateChanged();
            return;
        }
        var tag = model.ActiveTag;
        var targets = view.Selection.Contains(target)
            ? view.Selection.Items.Where(x => x is not Vertex && !x.IsDeleted).ToList()
            : new List<Entity> { target };
        if (e.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            var old = target.Tag;
            var container = target.Parent!;
            targets = container.Faces.Cast<Entity>().Concat(container.Edges).Concat(container.Groups).Where(x => x.Tag == old).ToList();
        }
        model.Run("Tag", () =>
        {
            foreach (var x in targets) x.Tag = tag;
            model.Entities.Touch();
        });
        // The tagged entities flash magenta, as in SketchUp.
        _flash = targets;
        _flashUntil = DateTime.UtcNow.AddMilliseconds(350);
        view.Invalidate();
    }

    public override void Deactivate(IView view)
    {
        view.ShowTooltip(null, 0, 0);
        base.Deactivate(view);
    }

    public override void Draw(IView view, IViewDrawing g)
    {
        var highlight = DateTime.UtcNow < _flashUntil ? _flash : _hover is null ? new List<Entity>() : new List<Entity> { _hover };
        foreach (var e in PaintBucketTool.OutlineEdges(highlight))
            g.Line(e.Start.Position, e.End.Position, DrawColor.Magenta.WithAlpha(170), 2, depthTested: true);
    }
}
