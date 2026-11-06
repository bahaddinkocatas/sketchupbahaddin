using Sketch.Geometry;
using Sketch.Model;
using Sketch.Operations;

namespace Sketch.Tools.Modify;

/// <summary>
/// Live preview for Move, Rotate and Scale. The container being edited (not the whole model) is restored before each
/// new preview step. A selection of groups only (plus guides, dimensions, texts, images, section planes) is instead
/// moved by the difference to the previous step, since groups never merge with other geometry: this keeps dragging
/// a dwelling fluid in a model with 100 000+ edges.
/// </summary>
internal sealed class TransformPreview
{
    private Entities? _container;
    private EntitiesState? _base;
    private IReadOnlyList<int> _ids = Array.Empty<int>();
    private Matrix4d? _applied;
    private bool _incremental;

    public Entities? Container => _container;

    public void Begin(IView view, IReadOnlyList<int> ids)
    {
        _container = view.Model.ActiveEntities;
        _ids = ids;
        _applied = null;
        _incremental = _container.FindByIds(ids).All(e => e is Group or Guide or Dimension or Note or ImageEntity or SectionPlane);
        _base = _incremental ? null : _container.CaptureState();
    }

    public void End()
    {
        _container = null;
        _base = null;
        _applied = null;
    }

    private List<Entity> Items() => _container!.FindByIds(_ids).ToList();

    /// <summary>Puts everything back where it was when the operation began (and keeps restoring from now on).</summary>
    public void ResetToOriginal()
    {
        if (_container is null) return;
        if (_incremental)
        {
            if (_applied is { } a) MoveCopy.Transform(_container, Items(), a.Inverse());
            _applied = null;
            // Copies made from here on cannot be undone by a transform: switch to restoring a snapshot.
            _incremental = false;
            _base = _container.CaptureState();
            return;
        }
        _container.RestoreState(_base!, keepUnchangedGroups: true);
    }

    /// <summary>Shows the selection transformed by <paramref name="m"/> from its original position.</summary>
    public void Transform(Matrix4d m)
    {
        if (_container is null) return;
        if (_incremental)
        {
            var step = _applied is { } a ? m * a.Inverse() : m;
            MoveCopy.Transform(_container, Items(), step);
            _applied = m;
            return;
        }
        _container.RestoreState(_base!, keepUnchangedGroups: true);
        MoveCopy.Transform(_container, Items(), m);
    }
}
