using System.Runtime.InteropServices;

namespace Sketch.App.Interop;

/// <summary>
/// Creates an OpenGL 3.3 core context with MSAA on a window (8x on discrete GPUs, 4x on integrated Intel graphics),
/// using a throw-away window to load the WGL_ARB extensions first (a window's pixel format can only be set once).
/// Frames are drawn on demand, so vsync is off: SwapBuffers must not block the UI thread for a display refresh.
/// </summary>
internal sealed class WglContext : IDisposable
{
    private const int WGL_DRAW_TO_WINDOW_ARB = 0x2001, WGL_ACCELERATION_ARB = 0x2003, WGL_SUPPORT_OPENGL_ARB = 0x2010;
    private const int WGL_DOUBLE_BUFFER_ARB = 0x2011, WGL_PIXEL_TYPE_ARB = 0x2013, WGL_COLOR_BITS_ARB = 0x2014;
    private const int WGL_DEPTH_BITS_ARB = 0x2022, WGL_STENCIL_BITS_ARB = 0x2023, WGL_FULL_ACCELERATION_ARB = 0x2027;
    private const int WGL_TYPE_RGBA_ARB = 0x202B, WGL_SAMPLE_BUFFERS_ARB = 0x2041, WGL_SAMPLES_ARB = 0x2042;
    private const int WGL_CONTEXT_MAJOR_VERSION_ARB = 0x2091, WGL_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    private const int WGL_CONTEXT_PROFILE_MASK_ARB = 0x9126, WGL_CONTEXT_CORE_PROFILE_BIT_ARB = 0x0001;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool ChoosePixelFormatArb(IntPtr hdc, int[] attribs, float[]? fAttribs, uint maxFormats, int[] formats, out uint numFormats);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr CreateContextAttribsArb(IntPtr hdc, IntPtr shareContext, int[] attribs);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool SwapIntervalExt(int interval);

    private readonly IntPtr _hwnd;
    private readonly IntPtr _opengl32;

    public IntPtr Hdc { get; }
    public IntPtr Hglrc { get; }
    public int Samples { get; }

    /// <summary>The OpenGL renderer (graphics adapter) name, as reported by the driver.</summary>
    public string Renderer { get; } = string.Empty;

    public WglContext(IntPtr hwnd, string dummyClassName)
    {
        _hwnd = hwnd;
        _opengl32 = Win32.LoadLibrary("opengl32.dll");

        ChoosePixelFormatArb? choose = null;
        CreateContextAttribsArb? createAttribs = null;
        SwapIntervalExt? swapInterval = null;

        // 1) Dummy window + legacy context to get the ARB entry points.
        var dummy = Win32.CreateWindowEx(0, dummyClassName, "dummy", Win32.WS_OVERLAPPED, 0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandle(null), IntPtr.Zero);
        var dummyDc = Win32.GetDC(dummy);
        var pfd = BasicDescriptor();
        var dummyFormat = Win32.ChoosePixelFormat(dummyDc, ref pfd);
        Win32.SetPixelFormat(dummyDc, dummyFormat, ref pfd);
        var dummyRc = Win32.wglCreateContext(dummyDc);
        if (dummyRc != IntPtr.Zero && Win32.wglMakeCurrent(dummyDc, dummyRc))
        {
            choose = Load<ChoosePixelFormatArb>("wglChoosePixelFormatARB");
            createAttribs = Load<CreateContextAttribsArb>("wglCreateContextAttribsARB");
            swapInterval = Load<SwapIntervalExt>("wglSwapIntervalEXT");
            Renderer = Marshal.PtrToStringAnsi(Win32.glGetString(Win32.GL_RENDERER)) ?? string.Empty;
            Win32.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Win32.wglDeleteContext(dummyRc);
        }
        Win32.ReleaseDC(dummy, dummyDc);
        Win32.DestroyWindow(dummy);

        // 2) Real pixel format: 8x MSAA, 4x, then none. Integrated Intel graphics pay a lot for 8x at full-screen
        //    sizes, so they start at 4x.
        Hdc = Win32.GetDC(hwnd);
        var format = 0;
        var integrated = Renderer.Contains("Intel", StringComparison.OrdinalIgnoreCase);
        if (choose is not null)
        {
            foreach (var samples in integrated ? new[] { 4, 0 } : new[] { 8, 4, 0 })
            {
                var attribs = new List<int>
                {
                    WGL_DRAW_TO_WINDOW_ARB, 1, WGL_SUPPORT_OPENGL_ARB, 1, WGL_DOUBLE_BUFFER_ARB, 1,
                    WGL_ACCELERATION_ARB, WGL_FULL_ACCELERATION_ARB, WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
                    WGL_COLOR_BITS_ARB, 32, WGL_DEPTH_BITS_ARB, 24, WGL_STENCIL_BITS_ARB, 8,
                };
                if (samples > 0) attribs.AddRange(new[] { WGL_SAMPLE_BUFFERS_ARB, 1, WGL_SAMPLES_ARB, samples });
                attribs.Add(0);
                var formats = new int[1];
                if (choose(Hdc, attribs.ToArray(), null, 1, formats, out var count) && count > 0)
                {
                    format = formats[0];
                    Samples = samples;
                    break;
                }
            }
        }
        pfd = BasicDescriptor();
        if (format == 0) format = Win32.ChoosePixelFormat(Hdc, ref pfd);
        Win32.DescribePixelFormat(Hdc, format, (uint)Marshal.SizeOf<Win32.PIXELFORMATDESCRIPTOR>(), ref pfd);
        if (!Win32.SetPixelFormat(Hdc, format, ref pfd))
            throw new InvalidOperationException("SetPixelFormat failed.");

        // 3) Core 3.3 context (fall back to legacy if the driver refuses).
        if (createAttribs is not null)
        {
            Hglrc = createAttribs(Hdc, IntPtr.Zero, new[]
            {
                WGL_CONTEXT_MAJOR_VERSION_ARB, 3, WGL_CONTEXT_MINOR_VERSION_ARB, 3,
                WGL_CONTEXT_PROFILE_MASK_ARB, WGL_CONTEXT_CORE_PROFILE_BIT_ARB, 0,
            });
        }
        if (Hglrc == IntPtr.Zero) Hglrc = Win32.wglCreateContext(Hdc);
        if (Hglrc == IntPtr.Zero) throw new InvalidOperationException("Could not create an OpenGL context.");
        MakeCurrent();
        // No vsync: the view redraws only when something changes, and the desktop compositor already prevents tearing.
        swapInterval?.Invoke(0);
    }

    private static Win32.PIXELFORMATDESCRIPTOR BasicDescriptor() => new()
    {
        nSize = (ushort)Marshal.SizeOf<Win32.PIXELFORMATDESCRIPTOR>(),
        nVersion = 1,
        dwFlags = Win32.PFD_DRAW_TO_WINDOW | Win32.PFD_SUPPORT_OPENGL | Win32.PFD_DOUBLEBUFFER,
        iPixelType = Win32.PFD_TYPE_RGBA,
        cColorBits = 32,
        cDepthBits = 24,
        cStencilBits = 8,
    };

    private static T? Load<T>(string name) where T : Delegate
    {
        var p = Win32.wglGetProcAddress(name);
        return IsValid(p) ? Marshal.GetDelegateForFunctionPointer<T>(p) : null;
    }

    private static bool IsValid(IntPtr p) => p != IntPtr.Zero && p != 1 && p != 2 && p != 3 && p != -1;

    /// <summary>Resolves a GL function: extension/core entry points via WGL, GL 1.1 ones from opengl32.dll.</summary>
    public IntPtr GetProcAddress(string name)
    {
        var p = Win32.wglGetProcAddress(name);
        return IsValid(p) ? p : Win32.GetProcAddress(_opengl32, name);
    }

    public void MakeCurrent() => Win32.wglMakeCurrent(Hdc, Hglrc);

    public void SwapBuffers() => Win32.SwapBuffers(Hdc);

    public void Dispose()
    {
        Win32.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Win32.wglDeleteContext(Hglrc);
        Win32.ReleaseDC(_hwnd, Hdc);
    }
}
