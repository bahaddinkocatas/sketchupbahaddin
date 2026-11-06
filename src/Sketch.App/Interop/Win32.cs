using System.Runtime.InteropServices;

namespace Sketch.App.Interop;

internal static class Win32
{
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift;
        public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
        public byte cDepthBits;
        public byte cStencilBits;
        public byte cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask, dwVisibleMask, dwDamageMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    public const uint CS_VREDRAW = 0x0001, CS_HREDRAW = 0x0002, CS_DBLCLKS = 0x0008, CS_OWNDC = 0x0020;
    public const uint WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
    public const uint WS_OVERLAPPED = 0x00000000;

    public const uint WM_SIZE = 0x0005, WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014, WM_SETCURSOR = 0x0020, WM_MOUSEACTIVATE = 0x0021;
    public const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206;
    public const uint WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_MBUTTONDBLCLK = 0x0209, WM_MOUSEWHEEL = 0x020A;
    public const uint WM_CAPTURECHANGED = 0x0215;
    public const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_CHAR = 0x0102, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr hwndTrack;
        public uint dwHoverTime;
    }

    public const uint TME_LEAVE = 0x00000002;
    [DllImport("user32.dll")] public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    public const uint GA_ROOT = 2;
    public const int MA_ACTIVATE = 1;
    public const int MK_SHIFT = 0x0004, MK_CONTROL = 0x0008;
    public const int VK_MENU = 0x12;
    public const int HTCLIENT = 1;

    public const uint PFD_DRAW_TO_WINDOW = 0x00000004, PFD_SUPPORT_OPENGL = 0x00000020, PFD_DOUBLEBUFFER = 0x00000001;
    public const byte PFD_TYPE_RGBA = 0;

    public const int IDC_ARROW = 32512, IDC_CROSS = 32515, IDC_SIZEALL = 32646, IDC_HAND = 32649;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("user32.dll")] public static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
    [DllImport("user32.dll")] public static extern bool ValidateRect(IntPtr hWnd, IntPtr rect);
    [DllImport("user32.dll")] public static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadLibrary(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("gdi32.dll")] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
    [DllImport("gdi32.dll")] public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
    [DllImport("gdi32.dll")] public static extern int DescribePixelFormat(IntPtr hdc, int format, uint size, ref PIXELFORMATDESCRIPTOR pfd);
    [DllImport("gdi32.dll")] public static extern bool SwapBuffers(IntPtr hdc);

    [DllImport("opengl32.dll")] public static extern IntPtr wglCreateContext(IntPtr hdc);
    [DllImport("opengl32.dll")] public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
    [DllImport("opengl32.dll")] public static extern bool wglDeleteContext(IntPtr hglrc);
    [DllImport("opengl32.dll", CharSet = CharSet.Ansi)] public static extern IntPtr wglGetProcAddress(string name);
    [DllImport("opengl32.dll")] public static extern IntPtr glGetString(uint name);
    public const uint GL_RENDERER = 0x1F01;

    public static short LoWordSigned(IntPtr value) => unchecked((short)((long)value & 0xFFFF));
    public static short HiWordSigned(IntPtr value) => unchecked((short)(((long)value >> 16) & 0xFFFF));
}
