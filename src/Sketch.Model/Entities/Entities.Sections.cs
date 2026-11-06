using Sketch.Geometry;

namespace Sketch.Model;

public sealed partial class Entities
{
    private readonly List<SectionPlane> _sections = new();

    public IReadOnlyList<SectionPlane> Sections => _sections;

    public IEnumerable<SectionPlane> AllSections() => _sections.Concat(_groups.SelectMany(g => g.Entities.AllSections()));

    /// <summary>The section plane that currently cuts the view (one at a time), or null.</summary>
    public SectionPlane? ActiveSectionPlane() => AllSections().FirstOrDefault(s => s.IsActive && !s.IsDeleted);

    /// <summary>Adds a section plane and makes it the active cut (others are deactivated, as in SketchUp).</summary>
    public SectionPlane AddSectionPlane(Vector3d point, Vector3d normal, double size, string name)
    {
        foreach (var s in Root.AllSections()) s.IsActive = false;
        var sp = new SectionPlane
        {
            Id = Context.NextId(), Point = point, Normal = normal.Normalized(), Size = size, Name = name, IsActive = true,
            Parent = this, Tag = NewEntityTag(),
        };
        _sections.Add(sp);
        RaiseChanged();
        return sp;
    }

    /// <summary>Turns a section plane's cut on (turning every other one off) or off.</summary>
    public void SetActiveSection(SectionPlane plane, bool active)
    {
        foreach (var s in Root.AllSections()) s.IsActive = false;
        plane.IsActive = active;
        RaiseChanged();
    }

    /// <summary>Flips which side of the section plane stays visible.</summary>
    public void ReverseSection(SectionPlane plane)
    {
        plane.Normal = -plane.Normal;
        RaiseChanged();
    }

    private Entities Root
    {
        get
        {
            var e = this;
            while (e.Owner?.Parent is { } parent) e = parent;
            return e;
        }
    }

    private IEnumerable<EntitiesState.SectionData> CaptureSections() =>
        _sections.Select(s => new EntitiesState.SectionData(s.Id, s.Point, s.Normal, s.Size, s.Name, s.IsActive, s.Tag?.Name, s.Hidden));

    private void LoadSections(EntitiesState state, Func<int, int> id, Vector3d offset, List<Entity> added)
    {
        foreach (var sd in state.Sections ?? Array.Empty<EntitiesState.SectionData>())
        {
            var s = new SectionPlane
            {
                Id = id(sd.Id), Point = sd.Point + offset, Normal = sd.Normal, Size = sd.Size, Name = sd.Name, IsActive = sd.Active,
                Parent = this, Tag = Context.Tags.Find(sd.Tag), Hidden = sd.Hidden,
            };
            _sections.Add(s);
            added.Add(s);
        }
    }
}
