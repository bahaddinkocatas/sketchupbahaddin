using Sketch.Geometry;
using Sketch.Tools.Navigation;

namespace Sketch.Tools;

/// <summary>
/// Routes view input to the active tool. Middle-mouse navigation (orbit; Shift = pan) and wheel zoom work
/// in every tool without changing the active tool, as in SketchUp.
/// </summary>
public sealed class ToolManager
{
    private readonly IView _view;
    private Tool _active;
    private Tool? _previous;
    private readonly Tool _default;

    private enum MiddleMode { None, Orbit, Pan }
    private MiddleMode _middle;
    private double _middleLastX, _middleLastY;
    private Vector3d _middlePivot, _middleAnchor;

    public ToolManager(IView view, Tool initial)
    {
        _view = view;
        _active = initial;
        _default = initial;
        _active.StateChanged += OnToolStateChanged;
        _active.ExitRequested += OnToolExitRequested;
        _active.Activate(view);
    }

    public Tool ActiveTool => _active;

    /// <summary>Raised when the active tool or its status/cursor changes.</summary>
    public event EventHandler? StateChanged;

    public string StatusText => _active.StatusText;

    public ToolCursor Cursor => _middle switch
    {
        MiddleMode.Orbit => ToolCursor.Orbit,
        MiddleMode.Pan => ToolCursor.Pan,
        _ => _active.Cursor,
    };

    public void Select(Tool tool)
    {
        if (ReferenceEquals(tool, _active)) return;
        _active.Deactivate(_view);
        _active.StateChanged -= OnToolStateChanged;
        _active.ExitRequested -= OnToolExitRequested;
        _previous = _active;
        _active = tool;
        _active.StateChanged += OnToolStateChanged;
        _active.ExitRequested += OnToolExitRequested;
        _active.Activate(_view);
        _view.Invalidate();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cancels whatever the active tool is in the middle of (before undo, delete, etc.).</summary>
    public void CancelActive()
    {
        _active.Reset(_view);
        _view.Invalidate();
    }

    /// <summary>Returns to the tool that was active before the current one (used after one-shot actions).</summary>
    public void SelectPrevious()
    {
        if (_previous is not null) Select(_previous);
    }

    public void MouseDown(ViewMouseEvent e)
    {
        if (e.Button == MouseButton.Middle)
        {
            _view.SaveCameraForPrevious();
            _middleLastX = e.X;
            _middleLastY = e.Y;
            if (e.Modifiers.HasFlag(KeyModifiers.Shift))
            {
                _middle = MiddleMode.Pan;
                _middleAnchor = NavigationMath.PanAnchor(_view, e.X, e.Y);
            }
            else
            {
                _middle = MiddleMode.Orbit;
                _middlePivot = NavigationMath.OrbitPivot(_view);
                _middleAnchor = NavigationMath.PanAnchor(_view, e.X, e.Y);
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        Guarded(() => _active.OnMouseDown(_view, e));
    }

    public void MouseMove(ViewMouseEvent e)
    {
        if (_middle != MiddleMode.None)
        {
            // Pressing/releasing Shift mid-drag switches between orbit and pan.
            var wantPan = e.Modifiers.HasFlag(KeyModifiers.Shift);
            if (wantPan && _middle == MiddleMode.Orbit)
            {
                _middle = MiddleMode.Pan;
                _middleAnchor = NavigationMath.PanAnchor(_view, e.X, e.Y);
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (!wantPan && _middle == MiddleMode.Pan)
            {
                _middle = MiddleMode.Orbit;
                _middlePivot = NavigationMath.OrbitPivot(_view);
                StateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (_middle == MiddleMode.Pan)
                NavigationMath.PanTo(_view, _middleAnchor, e.X, e.Y);
            else
                _view.Camera.Orbit(_middlePivot,
                    -(e.X - _middleLastX) * NavigationMath.OrbitRadiansPerPixel,
                    -(e.Y - _middleLastY) * NavigationMath.OrbitRadiansPerPixel);

            _middleLastX = e.X;
            _middleLastY = e.Y;
            _view.Invalidate();
            return;
        }
        Guarded(() => _active.OnMouseMove(_view, e));
    }

    public void MouseUp(ViewMouseEvent e)
    {
        if (e.Button == MouseButton.Middle && _middle != MiddleMode.None)
        {
            _middle = MiddleMode.None;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        Guarded(() => _active.OnMouseUp(_view, e));
    }

    public void DoubleClick(ViewMouseEvent e)
    {
        if (e.Button == MouseButton.Middle)
        {
            // Middle double-click centres the view on the clicked point.
            var p = _view.PickSurfacePoint(e.X, e.Y);
            if (p is { } point)
            {
                _view.Camera.Translate(point - _view.Camera.Target);
                _view.Invalidate();
            }
            return;
        }
        Guarded(() => _active.OnDoubleClick(_view, e));
    }

    public void MouseWheel(ViewMouseEvent e) => NavigationMath.WheelZoom(_view, e.X, e.Y, e.WheelDelta);

    public bool KeyDown(ViewKey key, KeyModifiers modifiers)
    {
        if (_active.OnKeyDown(_view, key, modifiers)) return true;
        if (key == ViewKey.Escape)
        {
            // Esc cancels whatever is in progress and returns to the Select tool.
            _active.Reset(_view);
            if (!ReferenceEquals(_active, _default)) Select(_default);
            _view.Invalidate();
            return true;
        }
        return false;
    }

    public bool KeyUp(ViewKey key, KeyModifiers modifiers) => _active.OnKeyUp(_view, key, modifiers);

    public bool UserText(string text)
    {
        var ok = false;
        Guarded(() => ok = _active.OnUserText(_view, text));
        return ok;
    }

    public void Draw(IViewDrawing g) => _active.Draw(_view, g);

    /// <summary>
    /// Runs a tool callback so that a failure inside it cannot leave a half-done operation behind: the pending
    /// operation is rolled back and the tool reset before the error is reported.
    /// </summary>
    private void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            _view.Model.AbortOperation();
            try
            {
                _active.Reset(_view);
            }
            catch (Exception)
            {
                // the original error is the one worth reporting
            }
            _view.Invalidate();
            throw;
        }
    }

    private void OnToolStateChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnToolExitRequested(object? sender, EventArgs e)
    {
        _active.Reset(_view);
        _view.Selection.Clear();
        if (!ReferenceEquals(_active, _default)) Select(_default);
        _view.Invalidate();
    }
}
