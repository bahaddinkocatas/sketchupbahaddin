using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    private readonly List<Dimension> _dimensions = new();
    private readonly List<Note> _notes = new();

    public IReadOnlyList<Dimension> Dimensions => _dimensions;
    public IReadOnlyList<Note> Notes => _notes;

    public IEnumerable<Dimension> AllDimensions() => _dimensions.Concat(_groups.SelectMany(g => g.Entities.AllDimensions()));
    public IEnumerable<Note> AllNotes() => _notes.Concat(_groups.SelectMany(g => g.Entities.AllNotes()));

    public Dimension AddDimension(Vector3d start, Vector3d end, Vector3d offset)
    {
        var d = new Dimension { Id = Context.NextId(), Start = start, End = end, Offset = offset, Parent = this, Tag = NewEntityTag() };
        _dimensions.Add(d);
        RaiseChanged();
        return d;
    }

    public Note AddNote(Vector3d anchor, Vector3d labelOffset, string text, bool leader)
    {
        var n = new Note { Id = Context.NextId(), Anchor = anchor, LabelOffset = labelOffset, Text = text, HasLeader = leader, Parent = this, Tag = NewEntityTag() };
        _notes.Add(n);
        RaiseChanged();
        return n;
    }

    private bool RemoveAnnotation(Entity e)
    {
        var removed = e switch
        {
            Dimension d => _dimensions.Remove(d),
            Note n => _notes.Remove(n),
            SectionPlane sp => _sections.Remove(sp),
            _ => false,
        };
        if (removed) e.IsDeleted = true;
        return removed;
    }

    private void ClearAnnotations()
    {
        foreach (var d in _dimensions) d.IsDeleted = true;
        foreach (var n in _notes) n.IsDeleted = true;
        _dimensions.Clear();
        _notes.Clear();
        foreach (var sp in _sections) sp.IsDeleted = true;
        _sections.Clear();
    }

    /// <summary>Moves/rotates/scales annotations with the rest of a selection.</summary>
    public void TransformAnnotations(IEnumerable<Entity> items, Matrix4d transform)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case Dimension d:
                    d.Start = transform.TransformPoint(d.Start);
                    d.End = transform.TransformPoint(d.End);
                    d.Offset = transform.TransformVector(d.Offset);
                    break;
                case Note n:
                    n.Anchor = transform.TransformPoint(n.Anchor);
                    break;
                case SectionPlane sp:
                    sp.Point = transform.TransformPoint(sp.Point);
                    sp.Normal = transform.TransformVector(sp.Normal).Normalized();
                    break;
                case ImageEntity img:
                    TransformImage(img, transform);
                    break;
                case Group g:
                    // Annotations and images inside groups move with the group.
                    g.Entities.TransformAnnotations(g.Entities.Dimensions.Cast<Entity>().Concat(g.Entities.Notes)
                        .Concat(g.Entities.Images).Concat(g.Entities.Sections).Concat(g.Entities.Groups).ToList(), transform);
                    break;
            }
        }
        RaiseChanged();
    }

    private IEnumerable<EntitiesState.DimensionData> CaptureDimensions() =>
        _dimensions.Select(d => new EntitiesState.DimensionData(d.Id, d.Start, d.End, d.Offset, d.Tag?.Name, d.Hidden));

    private IEnumerable<EntitiesState.NoteData> CaptureNotes() =>
        _notes.Select(n => new EntitiesState.NoteData(n.Id, n.Anchor, n.LabelOffset, n.Text, n.HasLeader, n.Tag?.Name, n.Hidden));

    private void LoadAnnotations(EntitiesState state, Func<int, int> id, Vector3d offset, List<Entity> added)
    {
        foreach (var dd in state.Dimensions ?? Array.Empty<EntitiesState.DimensionData>())
        {
            var d = new Dimension
            {
                Id = id(dd.Id), Start = dd.Start + offset, End = dd.End + offset, Offset = dd.Offset, Parent = this,
                Tag = Context.Tags.Find(dd.Tag), Hidden = dd.Hidden,
            };
            _dimensions.Add(d);
            added.Add(d);
        }
        foreach (var nd in state.Notes ?? Array.Empty<EntitiesState.NoteData>())
        {
            var n = new Note
            {
                Id = id(nd.Id), Anchor = nd.Anchor + offset, LabelOffset = nd.LabelOffset, Text = nd.Text, HasLeader = nd.HasLeader,
                Parent = this, Tag = Context.Tags.Find(nd.Tag), Hidden = nd.Hidden,
            };
            _notes.Add(n);
            added.Add(n);
        }
    }
}
