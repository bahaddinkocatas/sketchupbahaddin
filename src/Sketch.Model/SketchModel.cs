namespace Sketch.Model;

/// <summary>
/// The document: geometry, tags, materials, scenes, display style and undo/redo. Every user action runs inside
/// one operation (<see cref="StartOperation"/> … <see cref="CommitOperation"/>) and becomes one undo step.
/// </summary>
public sealed class SketchModel
{
    private readonly Stack<(string Name, ModelSnapshot State)> _undo = new();
    private readonly Stack<(string Name, ModelSnapshot State)> _redo = new();
    private (string Name, ModelSnapshot State)? _pending;
    private readonly List<int> _editPath = new();
    private RenderStyle _style = new();

    public SketchModel()
    {
        Context = new ModelContext();
        Entities = new Entities(Context, null) { DefaultTag = () => ActiveTag };
        Tags.Changed += (_, _) =>
        {
            if (ActiveTag is not null && !Tags.Items.Contains(ActiveTag)) ActiveTag = null;
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        };
        Materials.Changed += (_, _) => DisplayChanged?.Invoke(this, EventArgs.Empty);
        Context.Isolation = IsolatedContainer;
    }

    public ModelContext Context { get; }

    /// <summary>Root geometry of the model.</summary>
    public Entities Entities { get; }

    public TagCollection Tags => Context.Tags;
    public MaterialCollection Materials => Context.Materials;
    public SceneCollection Scenes { get; } = new();

    /// <summary>Tag given to newly drawn geometry (null = Untagged).</summary>
    public Tag? ActiveTag { get; set; }

    private ModelAxes _axes = ModelAxes.World;

    /// <summary>Drawing axes (moved by the Axes tool).</summary>
    public ModelAxes Axes
    {
        get => _axes;
        set
        {
            _axes = value;
            MarkModified();
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public RenderStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when something that affects drawing but not geometry changes (tags, materials, style).</summary>
    public event EventHandler? DisplayChanged;

    public void RaiseDisplayChanged() => DisplayChanged?.Invoke(this, EventArgs.Empty);

    // ------------------------------------------------------------------ group editing context

    /// <summary>The groups being edited, outermost first (empty = editing the model root).</summary>
    public IReadOnlyList<Group> EditPath => ResolveEditPath();

    /// <summary>The container new geometry goes into and tools operate on.</summary>
    public Entities ActiveEntities => EditPath.Count == 0 ? Entities : EditPath[^1].Entities;

    /// <summary>Raised when entering or leaving a group.</summary>
    public event EventHandler? EditContextChanged;

    private bool _hideRest = true;

    /// <summary>While a group is edited, everything outside it is hidden (on) or drawn faded (off).</summary>
    public bool HideRestOfModel
    {
        get => _hideRest;
        set
        {
            _hideRest = value;
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private (long Version, int Count, int Last, Entities? Container) _isolation = (-1, 0, 0, null);

    /// <summary>
    /// The container being edited when the rest of the model is hidden. Asked for every entity drawn, so the lookup
    /// is cached until the geometry or the edit path changes (undo recreates group objects).
    /// </summary>
    private Entities? IsolatedContainer()
    {
        if (!_hideRest || _editPath.Count == 0) return null;
        if (_isolation.Version == Entities.Version && _isolation.Count == _editPath.Count && _isolation.Last == _editPath[^1])
            return _isolation.Container;
        var path = ResolveEditPath();
        var container = path.Count == 0 ? null : path[^1].Entities;
        _isolation = (Entities.Version, _editPath.Count, _editPath.Count > 0 ? _editPath[^1] : 0, container);
        return container;
    }

    public void EnterGroup(Group group)
    {
        if (!ReferenceEquals(group.Parent, ActiveEntities)) return;
        _editPath.Add(group.Id);
        EditContextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Leaves the innermost group being edited. Returns the group that was closed.</summary>
    public Group? ExitGroup()
    {
        var path = ResolveEditPath();
        if (path.Count == 0) return null;
        _editPath.RemoveAt(_editPath.Count - 1);
        EditContextChanged?.Invoke(this, EventArgs.Empty);
        return path[^1];
    }

    public void ExitAllGroups()
    {
        if (_editPath.Count == 0) return;
        _editPath.Clear();
        EditContextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Group objects are recreated by undo/redo; the path is stored as ids and looked up each time.</summary>
    private List<Group> ResolveEditPath()
    {
        var result = new List<Group>();
        var container = Entities;
        foreach (var id in _editPath)
        {
            var g = container.Groups.FirstOrDefault(x => x.Id == id);
            if (g is null)
            {
                _editPath.RemoveRange(result.Count, _editPath.Count - result.Count);
                break;
            }
            result.Add(g);
            container = g.Entities;
        }
        return result;
    }

    // ------------------------------------------------------------------ undo / redo

    /// <summary>True when the model changed since it was last saved or created.</summary>
    public bool IsModified { get; private set; }

    public string? UndoName => _undo.Count > 0 ? _undo.Peek().Name : null;
    public string? RedoName => _redo.Count > 0 ? _redo.Peek().Name : null;

    /// <summary>Raised after an operation is committed, aborted, undone or redone.</summary>
    public event EventHandler? HistoryChanged;

    /// <summary>Changes whenever the history does; a tool that wants to undo "its own last operation" compares it first.</summary>
    public long HistoryMark { get; private set; }

    private void OnHistoryChanged()
    {
        HistoryMark++;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StartOperation(string name)
    {
        if (_pending is not null) CommitOperation();
        _pending = (name, Capture());
    }

    /// <summary>
    /// Everything an undo step restores: the geometry plus the tag, material and scene lists and the drawing axes
    /// (so deleting a tag, recolouring a material or adding a scene can be undone too).
    /// </summary>
    public sealed record ModelSnapshot(EntitiesState Entities, IReadOnlyList<(string Name, Rgb Color, bool Visible)> Tags,
        IReadOnlyList<(string Name, Rgb Color, double Alpha)> Materials, IReadOnlyList<Scene> Scenes, ModelAxes Axes);

    private ModelSnapshot Capture() => new(Entities.CaptureState(), Tags.Capture(), Materials.Capture(), Scenes.Capture(), _axes);

    private void Restore(ModelSnapshot snapshot)
    {
        // Tags and materials first: the geometry refers to them by name.
        Tags.Restore(snapshot.Tags);
        Materials.Restore(snapshot.Materials);
        Entities.RestoreState(snapshot.Entities);
        Scenes.Restore(snapshot.Scenes);
        if (!ReferenceEquals(_axes, snapshot.Axes))
        {
            _axes = snapshot.Axes;
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void CommitOperation()
    {
        if (_pending is null) return;
        _undo.Push(_pending.Value);
        _redo.Clear();
        _pending = null;
        IsModified = true;
        OnHistoryChanged();
    }

    /// <summary>Discards the changes made since <see cref="StartOperation"/>.</summary>
    public void AbortOperation()
    {
        if (_pending is null) return;
        Restore(_pending.Value.State);
        _pending = null;
        OnHistoryChanged();
    }

    public bool IsOperationActive => _pending is not null;

    /// <summary>Runs an action as one undoable operation.</summary>
    public void Run(string name, Action action)
    {
        StartOperation(name);
        try
        {
            action();
            CommitOperation();
        }
        catch
        {
            AbortOperation();
            throw;
        }
    }

    public bool Undo()
    {
        if (_pending is not null) AbortOperation();
        if (_undo.Count == 0) return false;
        var (name, state) = _undo.Pop();
        _redo.Push((name, Capture()));
        Restore(state);
        IsModified = true;
        OnHistoryChanged();
        EditContextChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var (name, state) = _redo.Pop();
        _undo.Push((name, Capture()));
        Restore(state);
        IsModified = true;
        OnHistoryChanged();
        EditContextChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Clears undo history (after loading a file or starting a new model).</summary>
    public void ResetHistory(bool modified = false)
    {
        _undo.Clear();
        _redo.Clear();
        _pending = null;
        _editPath.Clear();
        IsModified = modified;
        OnHistoryChanged();
        EditContextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks the document as changed without an undo step (e.g. a tag shown or hidden).</summary>
    public void MarkModified()
    {
        IsModified = true;
        OnHistoryChanged();
    }

    public void MarkSaved()
    {
        IsModified = false;
        OnHistoryChanged();
    }

    // ------------------------------------------------------------------ scenes

    /// <summary>Applies a scene's tag visibility and style; the caller applies the camera to its view.</summary>
    public void ApplyScene(Scene scene)
    {
        if (scene.SavesTags)
        {
            foreach (var tag in Tags.Items)
                if (scene.TagVisibility.TryGetValue(tag.Name, out var visible))
                    tag.Visible = visible;
            Tags.RaiseChanged();
        }
        if (scene.SavesStyle && scene.Style is not null) Style = scene.Style;
    }

    /// <summary>Stores the current state into a scene.</summary>
    public void UpdateScene(Scene scene, Camera camera)
    {
        scene.Camera = camera.Clone();
        scene.TagVisibility = Tags.Items.ToDictionary(t => t.Name, t => t.Visible);
        scene.Style = Style;
        Scenes.RaiseChanged();
        MarkModified();
    }
}
