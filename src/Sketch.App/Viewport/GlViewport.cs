using System.Runtime.InteropServices;
using System.Windows.Interop;
using Silk.NET.OpenGL;
using Sketch.App.Interop;
using Sketch.Geometry;
using Sketch.Model;
using Sketch.Rendering;
using Sketch.Tools;

namespace Sketch.App.Viewport;

/// <summary>
/// Native child window hosting the OpenGL view. Implements <see cref="IView"/> for the tools and forwards
/// mouse input to the <see cref="ToolManager"/>. Coordinates are physical pixels.
/// </summary>
public sealed class GlViewport : HwndHost, IView
{
    private const string ClassName = "SketchGlViewport";
    private const string DummyClassName = "SketchGlDummy";
    private static bool _classesRegistered;
    private static Win32.WndProc? _classProc; // keeps the delegate alive for the process lifetime
    private static readonly Dictionary<IntPtr, GlViewport> Instances = new();

    private IntPtr _hwnd;
    private WglContext? _context;
    private GL? _gl;
    private Renderer? _renderer;
    private int _width = 1, _height = 1;
    private int _capturedButtons;
    private bool _trackingLeave;

    private long _sceneVersion;
    private long _displayVersion;

    public Camera Camera { get; } = NewParallelCamera();

    private static Camera NewParallelCamera()
    {
        var camera = new Camera();
        camera.SyncOrthoHeightFromPerspective();
        camera.IsPerspective = false;
        return camera;
    }

    public SketchModel Model { get; }
    public Selection Selection { get; } = new();
    public ToolManager? Tools { get; set; }

    public Sketch.Inference.InferenceEngine Inference { get; }

    /// <summary>Raised when a tool wants a tooltip next to the cursor (text null = hide). Pixel coordinates.</summary>
    public event Action<string?, double, double>? TooltipRequested;

    public void ShowTooltip(string? text, double x, double y) => TooltipRequested?.Invoke(text, x, y);

    private readonly List<Camera> _cameraHistory = new();

    public void SaveCameraForPrevious()
    {
        _cameraHistory.Add(Camera.Clone());
        if (_cameraHistory.Count > 50) _cameraHistory.RemoveAt(0);
    }

    /// <summary>Camera &gt; Previous: returns to the camera before the last navigation.</summary>
    public bool PreviousCamera()
    {
        if (_cameraHistory.Count == 0) return false;
        Camera.CopyFrom(_cameraHistory[^1]);
        _cameraHistory.RemoveAt(_cameraHistory.Count - 1);
        Invalidate();
        return true;
    }

    public GlViewport()
    {
        Model = new SketchModel();
        Inference = new Sketch.Inference.InferenceEngine(Model.Entities) { Frame = () => Model.Axes };
        Model.Entities.Changed += (_, _) =>
        {
            Selection.Prune();
            _sceneVersion++;
            Invalidate();
        };
        Selection.Changed += (_, _) =>
        {
            _sceneVersion++;
            Invalidate();
        };
        Model.DisplayChanged += (_, _) =>
        {
            // Tag visibility may have changed: what inference can snap to changes with it.
            Inference.Invalidate();
            _groupBounds.Clear();
            _displayVersion++;
            _sceneVersion++;
            Invalidate();
        };
        Model.EditContextChanged += (_, _) =>
        {
            Selection.Prune();
            // With the rest of the model hidden, what is visible (and can be snapped to) changes with the context.
            Inference.Invalidate();
            _groupBounds.Clear();
            _sceneVersion++;
            Invalidate();
        };
    }

    /// <summary>Supplies the model's bounding sphere; defaults to the model geometry or the area around the origin.</summary>
    public Func<(Vector3d Center, double Radius)>? BoundsProvider { get; set; }

    // Explicit: FrameworkElement already has Width/Height (layout size in DIPs).
    int IView.Width => _width;
    int IView.Height => _height;
    public int PixelWidth => _width;

    /// <summary>Last mouse position over the view (pixels).</summary>
    public double LastMouseX { get; private set; }
    public double LastMouseY { get; private set; }
    public int PixelHeight => _height;

    private (long Version, (Vector3d Center, double Radius) Bounds)? _boundsCache;
    private Dictionary<Group, (long Version, Vector3d Min, Vector3d Max, bool Any)> _groupBounds = new();

    /// <summary>
    /// Same result as <see cref="Entities.BoundingSphere"/> (visible edges and images), with each top-level group's
    /// box cached until that group changes.
    /// </summary>
    private (Vector3d Center, double Radius)? ComputeBounds()
    {
        var any = false;
        var min = new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = new Vector3d(double.MinValue, double.MinValue, double.MinValue);
        void Add(Vector3d p)
        {
            any = true;
            min = new Vector3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vector3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        (bool, Vector3d, Vector3d) BoxOf(IEnumerable<Edge> edges, IEnumerable<ImageEntity> images)
        {
            var (a, mn, mx) = (any, min, max);
            (any, min, max) = (false, new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue), new Vector3d(double.MinValue, double.MinValue, double.MinValue));
            foreach (var e in edges.Where(Entities.IsEffectivelyVisible))
            {
                Add(e.Start.Position);
                Add(e.End.Position);
            }
            foreach (var img in images.Where(Entities.IsEffectivelyVisible))
                foreach (var c in img.Corners) Add(c);
            var result = (any, min, max);
            (any, min, max) = (a, mn, mx);
            return result;
        }

        var root = Model.Entities;
        var (rootAny, rootMin, rootMax) = BoxOf(root.Edges, root.Images);
        if (rootAny)
        {
            Add(rootMin);
            Add(rootMax);
        }
        var next = new Dictionary<Group, (long, Vector3d, Vector3d, bool)>();
        foreach (var g in root.Groups)
        {
            if (!_groupBounds.TryGetValue(g, out var b) || b.Version != g.Entities.Version)
            {
                var (ga, gmin, gmax) = BoxOf(g.Entities.AllEdges(), g.Entities.AllImages());
                b = (g.Entities.Version, gmin, gmax, ga);
            }
            next[g] = b;
            if (b.Any && Entities.IsEffectivelyVisible(g))
            {
                Add(b.Min);
                Add(b.Max);
            }
        }
        _groupBounds = next;
        if (!any) return Model.Entities.BoundingSphere();
        var center = (min + max) / 2;
        return (center, Math.Max((max - min).Length / 2, Sketch.Geometry.Tolerance.Length));
    }

    /// <summary>Bounding sphere of the visible model; recomputed only after the model or its visibility changed.</summary>
    public (Vector3d Center, double Radius) ModelBounds
    {
        get
        {
            if (BoundsProvider?.Invoke() is { } provided) return provided;
            if (_boundsCache is { } c && c.Version == _sceneVersion) return c.Bounds;
            var bounds = ComputeBounds() ?? (new Vector3d(60, 60, 30), 150);
            _boundsCache = (_sceneVersion, bounds);
            return bounds;
        }
    }

    public string GraphicsInfo { get; private set; } = string.Empty;

    /// <summary>Raised when the mouse moves over the view (for status-bar feedback).</summary>
    public event EventHandler? ViewChanged;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        RegisterClasses();
        _hwnd = Win32.CreateWindowEx(0, ClassName, string.Empty,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS,
            0, 0, 1, 1, hwndParent.Handle, IntPtr.Zero, Win32.GetModuleHandle(null), IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("Could not create the viewport window.");
        Instances[_hwnd] = this;

        _context = new WglContext(_hwnd, DummyClassName);
        _gl = GL.GetApi(_context.GetProcAddress);
        _renderer = new Renderer(_gl)
        {
            TextRasterizer = new WpfTextRasterizer(System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX),
            ImageDecoder = new WpfImageDecoder(),
        };
        GraphicsInfo = $"OpenGL {_renderer.GlVersion} · {_renderer.GlRenderer} · MSAA {_context.Samples}x";
        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        _context?.MakeCurrent();
        _renderer?.Dispose();
        _context?.Dispose();
        Instances.Remove(hwnd.Handle);
        Win32.DestroyWindow(hwnd.Handle);
    }

    private static void RegisterClasses()
    {
        if (_classesRegistered) return;
        _classProc = StaticWndProc;
        var module = Win32.GetModuleHandle(null);
        foreach (var (name, style) in new[]
                 {
                     (ClassName, Win32.CS_OWNDC | Win32.CS_HREDRAW | Win32.CS_VREDRAW | Win32.CS_DBLCLKS),
                     (DummyClassName, Win32.CS_OWNDC),
                 })
        {
            var wc = new Win32.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
                style = style,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_classProc),
                hInstance = module,
                hCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW),
                lpszClassName = name,
            };
            if (Win32.RegisterClassEx(ref wc) == 0)
                throw new InvalidOperationException($"RegisterClassEx failed for {name}: {Marshal.GetLastWin32Error()}");
        }
        _classesRegistered = true;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (Instances.TryGetValue(hWnd, out var view))
        {
            try
            {
                var result = view.HandleMessage(msg, wParam, lParam);
                if (result.HasValue) return result.Value;
            }
            catch (Exception ex)
            {
                // Never let an exception unwind through native code.
                System.Diagnostics.Debug.WriteLine(ex);
                App.ReportError(ex);
            }
        }
        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr? HandleMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_ERASEBKGND:
                return 1;
            case Win32.WM_SIZE:
                _width = Math.Max(1, (int)((long)lParam & 0xFFFF));
                _height = Math.Max(1, (int)(((long)lParam >> 16) & 0xFFFF));
                Invalidate();
                return 0;
            case Win32.WM_PAINT:
                RenderFrame(swap: true);
                Win32.ValidateRect(_hwnd, IntPtr.Zero);
                return 0;
            case Win32.WM_MOUSEACTIVATE:
                return Win32.MA_ACTIVATE;
            case Win32.WM_SETCURSOR:
                if (((long)lParam & 0xFFFF) == Win32.HTCLIENT)
                {
                    Win32.SetCursor(CursorFor(Tools?.Cursor ?? ToolCursor.Arrow));
                    return 1;
                }
                return null;
            case 0x02A3: // WM_MOUSELEAVE
                _trackingLeave = false;
                ShowTooltip(null, 0, 0);
                return 0;
            case Win32.WM_MOUSEMOVE:
                if (!_trackingLeave)
                {
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(), dwFlags = Win32.TME_LEAVE, hwndTrack = _hwnd,
                    };
                    _trackingLeave = Win32.TrackMouseEvent(ref tme);
                }
                LastMouseX = Win32.LoWordSigned(lParam);
                LastMouseY = Win32.HiWordSigned(lParam);
                Tools?.MouseMove(MouseEvent(MouseButton.None, wParam, lParam));
                ViewChanged?.Invoke(this, EventArgs.Empty);
                return 0;
            case Win32.WM_LBUTTONDOWN:
                return ButtonDown(MouseButton.Left, wParam, lParam);
            case Win32.WM_MBUTTONDOWN:
                return ButtonDown(MouseButton.Middle, wParam, lParam);
            case Win32.WM_RBUTTONDOWN:
                return ButtonDown(MouseButton.Right, wParam, lParam);
            case Win32.WM_LBUTTONUP:
                return ButtonUp(MouseButton.Left, wParam, lParam);
            case Win32.WM_MBUTTONUP:
                return ButtonUp(MouseButton.Middle, wParam, lParam);
            case Win32.WM_RBUTTONUP:
                return ButtonUp(MouseButton.Right, wParam, lParam);
            case Win32.WM_LBUTTONDBLCLK:
                return DoubleClick(MouseButton.Left, wParam, lParam);
            case Win32.WM_MBUTTONDBLCLK:
                return DoubleClick(MouseButton.Middle, wParam, lParam);
            case Win32.WM_RBUTTONDBLCLK:
                return ButtonDown(MouseButton.Right, wParam, lParam);
            case Win32.WM_MOUSEWHEEL:
            {
                // Wheel coordinates are in screen space.
                var pt = new Win32.POINT { X = Win32.LoWordSigned(lParam), Y = Win32.HiWordSigned(lParam) };
                Win32.ScreenToClient(_hwnd, ref pt);
                var delta = Win32.HiWordSigned(wParam);
                Tools?.MouseWheel(new ViewMouseEvent(pt.X, pt.Y, MouseButton.None, Modifiers(wParam), delta));
                ViewChanged?.Invoke(this, EventArgs.Empty);
                return 0;
            }
            case Win32.WM_CAPTURECHANGED:
                _capturedButtons = 0;
                return 0;
            case Win32.WM_KEYDOWN:
            case Win32.WM_SYSKEYDOWN:
                // Normally the main window has keyboard focus; if this child ever gets it, pass keys on.
                KeyForwarded?.Invoke((int)(long)wParam, true);
                return 0;
            case Win32.WM_KEYUP:
            case Win32.WM_SYSKEYUP:
                KeyForwarded?.Invoke((int)(long)wParam, false);
                return 0;
            case Win32.WM_CHAR:
                CharForwarded?.Invoke((char)(long)wParam);
                return 0;
        }
        return null;
    }

    /// <summary>Raised before a mouse button press is routed to the tools (used to move keyboard focus).</summary>
    public event EventHandler? ViewMouseDown;

    /// <summary>Key pressed (true) or released (false) while this child window had focus; virtual-key code.</summary>
    public event Action<int, bool>? KeyForwarded;

    public event Action<char>? CharForwarded;

    private IntPtr ButtonDown(MouseButton button, IntPtr wParam, IntPtr lParam)
    {
        // Keep Win32 keyboard focus on the main window so shortcuts and Esc always reach it.
        var root = Win32.GetAncestor(_hwnd, Win32.GA_ROOT);
        if (root != IntPtr.Zero) Win32.SetFocus(root);
        ViewMouseDown?.Invoke(this, EventArgs.Empty);
        if (_capturedButtons++ == 0) Win32.SetCapture(_hwnd);
        Tools?.MouseDown(MouseEvent(button, wParam, lParam));
        Win32.SetCursor(CursorFor(Tools?.Cursor ?? ToolCursor.Arrow));
        return 0;
    }

    /// <summary>Raised on a right-click in the view (pixel coordinates) to show the context menu.</summary>
    public event Action<double, double>? ContextMenuRequested;

    private IntPtr ButtonUp(MouseButton button, IntPtr wParam, IntPtr lParam)
    {
        if (button == MouseButton.Right)
        {
            var ev = MouseEvent(button, wParam, lParam);
            if (_capturedButtons > 0 && --_capturedButtons == 0) Win32.ReleaseCapture();
            ContextMenuRequested?.Invoke(ev.X, ev.Y);
            return 0;
        }
        Tools?.MouseUp(MouseEvent(button, wParam, lParam));
        if (_capturedButtons > 0 && --_capturedButtons == 0) Win32.ReleaseCapture();
        Win32.SetCursor(CursorFor(Tools?.Cursor ?? ToolCursor.Arrow));
        return 0;
    }

    private IntPtr DoubleClick(MouseButton button, IntPtr wParam, IntPtr lParam)
    {
        // With CS_DBLCLKS the second press arrives as a double-click instead of a button-down.
        ButtonDown(button, wParam, lParam);
        Tools?.DoubleClick(MouseEvent(button, wParam, lParam));
        return 0;
    }

    private static ViewMouseEvent MouseEvent(MouseButton button, IntPtr wParam, IntPtr lParam) =>
        new(Win32.LoWordSigned(lParam), Win32.HiWordSigned(lParam), button, Modifiers(wParam));

    private static KeyModifiers Modifiers(IntPtr wParam)
    {
        var w = (long)wParam;
        var m = KeyModifiers.None;
        if ((w & Win32.MK_SHIFT) != 0) m |= KeyModifiers.Shift;
        if ((w & Win32.MK_CONTROL) != 0) m |= KeyModifiers.Ctrl;
        if (Win32.GetKeyState(Win32.VK_MENU) < 0) m |= KeyModifiers.Alt;
        return m;
    }

    private static IntPtr CursorFor(ToolCursor cursor) => Win32.LoadCursor(IntPtr.Zero, cursor switch
    {
        ToolCursor.Pan or ToolCursor.Move => Win32.IDC_SIZEALL,
        ToolCursor.Pencil or ToolCursor.Crosshair or ToolCursor.PushPull or ToolCursor.Eraser => Win32.IDC_CROSS,
        ToolCursor.Orbit or ToolCursor.Zoom => Win32.IDC_HAND,
        _ => Win32.IDC_ARROW,
    });

    /// <summary>The app only uses parallel projection; a perspective camera from a file or scene is converted.</summary>
    private void EnsureParallel()
    {
        if (!Camera.IsPerspective) return;
        Camera.SyncOrthoHeightFromPerspective();
        Camera.IsPerspective = false;
    }

    private long _lastRootVersion = -1;

    /// <summary>Drawn over the model after the active tool's preview (e.g. DEAP opening labels).</summary>
    public Action<IViewDrawing>? ExtraOverlay { get; set; }

    private void DrawOverlays(IViewDrawing g)
    {
        Tools?.Draw(g);
        ExtraOverlay?.Invoke(g);
    }

    private void RenderFrame(bool swap)
    {
        if (_context is null || _renderer is null) return;
        // Entity property changes (tag, hidden, material) bump the model version without a Changed event.
        if (Model.Entities.Version != _lastRootVersion)
        {
            _lastRootVersion = Model.Entities.Version;
            _sceneVersion++;
        }
        EnsureParallel();
        _context.MakeCurrent();
        var (center, radius) = ModelBounds;
        _renderer.Render(Camera, _width, _height, center, radius, new RenderScene
        {
            Entities = Model.Entities,
            Axes = Model.Axes,
            Selection = Selection.Items,
            Images = Model.Context.Images,
            Style = Model.Style,
            ActiveEntities = Model.ActiveEntities,
            Version = _sceneVersion,
            DisplayVersion = _displayVersion,
            Overlay = DrawOverlays,
        });
        if (swap) _context.SwapBuffers();
    }

    /// <summary>Saves the current view as a PNG image (rendered off-screen at the view's size).</summary>
    public void SaveImage(string path)
    {
        if (_context is null || _renderer is null) throw new InvalidOperationException("The view is not ready.");
        _context.MakeCurrent();
        var (center, radius) = ModelBounds;
        var pixels = _renderer.RenderToImage(Camera, _width, _height, center, radius, new RenderScene
        {
            Entities = Model.Entities,
            Axes = Model.Axes,
            Selection = Selection.Items,
            Images = Model.Context.Images,
            Style = Model.Style,
            ActiveEntities = Model.ActiveEntities,
            Version = _sceneVersion,
            DisplayVersion = _displayVersion,
            Overlay = DrawOverlays,
        });
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(_width, _height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, pixels, _width * 4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
        Invalidate();
    }

    /// <summary>Nearest visible model surface under a pixel (axes, guides and previews are ignored).</summary>
    /// <summary>Test helper: average milliseconds of a full frame (render without swapping).</summary>
    public double BenchRender(int frames)
    {
        if (_context is null || _renderer is null) return -1;
        RenderFrame(swap: false);
        _renderer.Finish();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < frames; i++) RenderFrame(swap: false);
        _renderer.Finish();
        return sw.Elapsed.TotalMilliseconds / frames;
    }

    /// <summary>Test helper: forces the model mesh to be rebuilt on the next frame (as after an edit).</summary>
    public void BumpSceneVersion() => _sceneVersion++;

    /// <summary>
    /// Test helper: renders a frame and compares, on a pixel grid, the surface the depth buffer shows with the
    /// surface the CPU picking finds. Returns the number of compared pixels and a list of mismatches.
    /// </summary>
    public (int Compared, List<string> Mismatches) CheckPicking(int step)
    {
        var mismatches = new List<string>();
        var compared = 0;
        if (_context is null || _renderer is null) return (0, mismatches);
        RenderFrame(swap: false);
        for (var y = step / 2; y < _height; y += step)
        for (var x = step / 2; x < _width; x += step)
        {
            var shown = _renderer.ReadSurfacePoint(x, y);
            var ray = Camera.RayThroughPixel(x + 0.5, y + 0.5, _width, _height);
            var hit = Inference.FaceInference(ray);
            if (shown is null || hit is null) continue;
            compared++;
            var tShown = Vector3d.Dot(shown.Value - ray.Origin, ray.Direction);
            var tHit = Vector3d.Dot(hit.Position - ray.Origin, ray.Direction);
            // An image (floor plan) in front of the faces is what is shown there: not a picking error.
            if (Inference.ImageHit(ray) is { } image && image.T < tHit) continue;
            // Lines (edges, axes) also write depth: only pixels where a face is shown count.
            var tol = Math.Max(0.5, Camera.OrthoHeight * 0.002);
            var shownFace = Model.Entities.AllFaces().FirstOrDefault(f => Entities.IsEffectivelyVisible(f)
                && Math.Abs(f.Plane.SignedDistance(shown.Value)) < tol && f.ContainsPoint(f.Plane.Project(shown.Value), tol));
            if (shownFace is null) continue;
            if (tHit - tShown > Math.Max(1, Camera.OrthoHeight * 0.01))
                mismatches.Add($"({x},{y}) gösterilen {shownFace} t={tShown:0.#} seçilen {hit.Face} t={tHit:0.#}");
        }
        // The layers above face picking: the Select tool's pick and the drawing tools' inference.
        for (var y = step / 2; y < _height; y += step)
        for (var x = step / 2; x < _width; x += step)
        {
            var ray = Camera.RayThroughPixel(x, y, _width, _height);
            var visible = Inference.FaceInference(ray);
            if (visible is null) continue;
            if (Inference.ImageHit(ray) is { } image && image.T < Vector3d.Dot(visible.Position - ray.Origin, ray.Direction)) continue;
            var tVisible = Vector3d.Dot(visible.Position - ray.Origin, ray.Direction);
            var tol = Math.Max(1, Camera.OrthoHeight * 0.01);
            var q = new Sketch.Inference.InferenceQuery(Camera, _width, _height, x, y);
            var picked = Inference.PickEntity(q);
            if (picked is Face pf && !ReferenceEquals(pf, visible.Face))
                mismatches.Add($"({x},{y}) Select: {pf} seçildi, görünen {visible.Face}");
            if (picked is Edge pe && !visible.Face!.Edges.Contains(pe))
            {
                var pt = Sketch.Inference.InferenceEngine.ClosestOnLineToRay(pe.Start.Position, pe.Direction, ray);
                if (Vector3d.Dot(pt - ray.Origin, ray.Direction) > tVisible + tol)
                    mismatches.Add($"({x},{y}) Select: arkadaki kenar {pe} seçildi, görünen {visible.Face}");
            }
            var r = Inference.Infer(q);
            var tr = Vector3d.Dot(r.Position - ray.Origin, ray.Direction);
            var onVisible = Math.Abs(visible.Face!.Plane.SignedDistance(r.Position)) < tol * 0.5;
            if (tr > tVisible + tol && !onVisible)
                mismatches.Add($"({x},{y}) Çıkarım: {r.Kind} noktası görünen yüzün {tr - tVisible:0.#} arkasında, yüz={r.Face}");
            else if (r.Face is { } rf && !ReferenceEquals(rf, visible.Face) && r.Kind is Sketch.Inference.InferenceKind.OnFace)
                mismatches.Add($"({x},{y}) Çıkarım: OnFace {rf}, görünen {visible.Face}");
        }
        return (compared, mismatches);
    }

    public Vector3d? PickSurfacePoint(double x, double y)
    {
        var ray = Camera.RayThroughPixel(x, y, _width, _height);
        return Inference.SurfaceHit(ray);
    }

    public void Invalidate()
    {
        EnsureParallel();
        if (_hwnd != IntPtr.Zero) Win32.InvalidateRect(_hwnd, IntPtr.Zero, false);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
