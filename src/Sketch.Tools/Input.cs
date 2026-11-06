using Sketch.Geometry;
using Sketch.Model;

namespace Sketch.Tools;

public enum MouseButton
{
    None,
    Left,
    Middle,
    Right,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,
}

/// <summary>Mouse event in viewport pixels (origin top-left, y down).</summary>
public readonly record struct ViewMouseEvent(double X, double Y, MouseButton Button, KeyModifiers Modifiers, int WheelDelta = 0);

/// <summary>Platform-neutral key identifiers the tools care about.</summary>
public enum ViewKey
{
    Other,
    Escape,
    Enter,
    Shift,
    Ctrl,
    Alt,
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Cursor shapes a tool can request; the host maps them to platform cursors.</summary>
public enum ToolCursor
{
    Arrow,
    Select,
    Pencil,
    Orbit,
    Pan,
    Zoom,
    Move,
    PushPull,
    Eraser,
    Crosshair,
}

/// <summary>What a tool needs from the 3D view hosting it.</summary>
public interface IView
{
    Camera Camera { get; }
    SketchModel Model { get; }
    Selection Selection { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>World point of the visible surface under a pixel, or null when nothing is there.</summary>
    Vector3d? PickSurfacePoint(double x, double y);

    /// <summary>Bounding sphere of the visible model (falls back to a default region when empty).</summary>
    (Vector3d Center, double Radius) ModelBounds { get; }

    Sketch.Inference.InferenceEngine Inference { get; }

    /// <summary>Shows a small tooltip next to the cursor (null hides it). Coordinates are view pixels.</summary>
    void ShowTooltip(string? text, double x, double y);

    /// <summary>Remembers the current camera so Camera &gt; Previous can return to it.</summary>
    void SaveCameraForPrevious();

    void Invalidate();
}
