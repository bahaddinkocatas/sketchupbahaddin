namespace Sketch.Tools;

/// <summary>
/// Base class for interactive tools. A tool is a small state machine: it previews on mouse move and
/// changes the model on click, as one undoable operation.
/// </summary>
public abstract class Tool
{
    /// <summary>Stable command key (matches the command catalog), e.g. "camera/orbit".</summary>
    public abstract string Id { get; }

    /// <summary>Instruction shown in the status bar.</summary>
    public virtual string StatusText => string.Empty;

    public virtual ToolCursor Cursor => ToolCursor.Arrow;

    /// <summary>Label of the Measurements box while this tool is active (e.g. "Uzunluk").</summary>
    public virtual string MeasurementLabel => "Measurements";

    /// <summary>Current value shown in the Measurements box (e.g. the length being drawn).</summary>
    public virtual string MeasurementValue => string.Empty;

    public virtual void Activate(IView view) { }
    public virtual void Deactivate(IView view) { }

    public virtual void OnMouseDown(IView view, ViewMouseEvent e) { }
    public virtual void OnMouseMove(IView view, ViewMouseEvent e) { }
    public virtual void OnMouseUp(IView view, ViewMouseEvent e) { }
    public virtual void OnDoubleClick(IView view, ViewMouseEvent e) { }

    /// <summary>Returns true when the key was consumed.</summary>
    public virtual bool OnKeyDown(IView view, ViewKey key, KeyModifiers modifiers) => false;

    public virtual bool OnKeyUp(IView view, ViewKey key, KeyModifiers modifiers) => false;

    /// <summary>Called with text typed into the Measurements box. Returns true when accepted.</summary>
    public virtual bool OnUserText(IView view, string text) => false;

    /// <summary>Draws the tool's preview and feedback on top of the model.</summary>
    public virtual void Draw(IView view, IViewDrawing g) { }

    /// <summary>Called by Esc: cancel the current operation but keep the tool active.</summary>
    public virtual void Reset(IView view) { }

    /// <summary>Raised when the status text or cursor changes.</summary>
    public event EventHandler? StateChanged;

    protected void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raised when the user clicks empty space while the tool is idle: the host clears the selection and
    /// returns to the Select tool.
    /// </summary>
    public event EventHandler? ExitRequested;

    protected void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
