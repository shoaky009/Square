using System.Runtime.InteropServices;

namespace Square.Platform.Win32;

internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

internal static partial class Win32Api
{
    public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int WS_VISIBLE = 0x10000000;
    public const int SW_SHOW = 5;
    public const int WM_DESTROY = 0x0002;
    public const int WM_PAINT = 0x000F;
    public const int WM_SIZE = 0x0005;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_QUIT = 0x0012;
    public const int CS_HREDRAW = 0x0002;
    public const int CS_VREDRAW = 0x0001;

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll", EntryPoint = "UpdateWindow")]
    public static partial bool UpdateWindow(IntPtr hWnd);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow")]
    public static partial bool DestroyWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    public static partial int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    public static partial bool TranslateMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial IntPtr DispatchMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "PostQuitMessage")]
    public static partial void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "BeginPaint")]
    public static extern IntPtr BeginPaint(IntPtr hwnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", EntryPoint = "EndPaint")]
    public static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT lpPaint);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll", EntryPoint = "GetClientRect")]
    public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        public int reserved1;
        public int reserved2;
        public int reserved3;
        public int reserved4;
        public int reserved5;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
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
}