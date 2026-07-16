using Square.Graphics;
using Square.Runtime;
using System.Runtime.InteropServices;
using System.Text;

namespace Square.Platform.Win32;

internal sealed class Win32Host : IPlatformHost
{
    private IntPtr _hwnd;
    private bool _running;
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;
    private Size _clientSize;
    private float _dpiScale = 1f;
    private IRenderContext? _renderContext;
    private Bitmap? _lastFrame;
    private char? _pendingHighSurrogate;
    private CursorKind _cursor = CursorKind.Arrow;
    private Rect _textInputRect;

    private static Win32Host? s_current;
    private static WndProcDelegate? s_wndProc;
    private static bool s_classRegistered;
    private static bool s_dpiAwarenessInitialized;

    public Size ClientSize => _clientSize;
    public float DpiScale => _dpiScale;
    public bool IsRunning => _running;
    public KeyModifiers Modifiers
    {
        get
        {
            var modifiers = KeyModifiers.None;
            if (Win32Api.GetKeyState(Win32Api.VK_SHIFT) < 0) modifiers |= KeyModifiers.Shift;
            if (Win32Api.GetKeyState(Win32Api.VK_CONTROL) < 0) modifiers |= KeyModifiers.Control;
            if (Win32Api.GetKeyState(Win32Api.VK_MENU) < 0) modifiers |= KeyModifiers.Alt;
            return modifiers;
        }
    }
    public CursorKind Cursor
    {
        get => _cursor;
        set
        {
            if (_cursor == value) return;
            _cursor = value;
            if (_hwnd != IntPtr.Zero) ApplyCursor();
        }
    }

    public event Action<Size>? SizeChanged;
    public event Action<Point, MouseAction>? MouseEvent;
    public event Action<Point, int>? WheelEvent;
    public event Action<int, KeyAction>? KeyEvent;
    public event Action<string>? TextInput;
    public event Action? Tick;

    public Win32Host(PlatformHostCreateInfo info)
    {
        _title = info.Title;
        _width = info.Width;
        _height = info.Height;
        s_current = this;
    }

    public void Show()
    {
        if (!s_dpiAwarenessInitialized)
        {
            // Square currently lays out and rasterizes in physical pixels. Declaring
            // DPI awareness prevents Windows from scaling the completed bitmap again.
            Win32Api.SetProcessDpiAwarenessContext(Win32Api.DpiAwarenessContextPerMonitorAwareV2);
            s_dpiAwarenessInitialized = true;
        }

        if (!s_classRegistered)
        {
            s_wndProc = WndProc;
            var wc = new Win32Api.WNDCLASSEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32Api.WNDCLASSEX>(),
                style = Win32Api.CS_HREDRAW | Win32Api.CS_VREDRAW,
                lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = Win32Api.GetModuleHandle(null),
                hCursor = Win32Api.LoadCursor(IntPtr.Zero, new IntPtr(Win32Api.IDC_ARROW)),
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = "SquareWindow",
                hIconSm = IntPtr.Zero
            };
            Win32Api.RegisterClassEx(ref wc);
            s_classRegistered = true;
        }

        _hwnd = Win32Api.CreateWindowEx(
            0, "SquareWindow", _title,
            Win32Api.WS_OVERLAPPEDWINDOW | Win32Api.WS_VISIBLE,
            100, 100, _width, _height,
            IntPtr.Zero, IntPtr.Zero, Win32Api.GetModuleHandle(null), IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            var err = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"CreateWindowEx failed: {err}");
        }

        Win32Api.GetClientRect(_hwnd, out var rect);
        _clientSize = new Size(rect.Width, rect.Height);

        Win32Api.ShowWindow(_hwnd, Win32Api.SW_SHOW);
        Win32Api.UpdateWindow(_hwnd);
        Win32Api.SetTimer(_hwnd, new UIntPtr(1), 530, IntPtr.Zero);
        _running = true;
    }

    public void Close()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
        {
            Win32Api.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        Win32Api.PostQuitMessage(0);
    }

    public IRenderContext CreateRenderContext()
    {
        if (_renderContext != null) return _renderContext;
        var factory = RenderBackendRegistry.Default;
        _renderContext = factory.CreateContext(new RenderContextCreateInfo
        {
            CanvasSize = _clientSize,
            DpiScale = _dpiScale,
            PresentFrame = PresentFrame
        });
        return _renderContext;
    }

    public void PumpEvents()
    {
        while (_running)
        {
            var result = Win32Api.GetMessage(out var msg, IntPtr.Zero, 0, 0);
            if (result <= 0) { _running = false; break; }
            Win32Api.TranslateMessage(ref msg);
            Win32Api.DispatchMessage(ref msg);
        }
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var host = s_current;
        if (host == null) return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);

        switch (msg)
        {
            case Win32Api.WM_SIZE:
                Win32Api.GetClientRect(hWnd, out var rect);
                var newSize = new Size(rect.Width, rect.Height);
                if (newSize == host._clientSize) break;
                host._clientSize = newSize;
                if (host._renderContext is IResizableRenderContext resizable)
                    resizable.Resize(newSize);
                host.SizeChanged?.Invoke(host._clientSize);
                break;
            case Win32Api.WM_DPICHANGED:
                {
                    var suggested = Marshal.PtrToStructure<Win32Api.RECT>(lParam);
                    Win32Api.SetWindowPos(
                        hWnd, IntPtr.Zero,
                        suggested.Left, suggested.Top, suggested.Width, suggested.Height,
                        Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE);
                }
                return IntPtr.Zero;
            case Win32Api.WM_LBUTTONDOWN:
                {
                    var x = (short)(lParam.ToInt64() & 0xFFFF);
                    var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    host.MouseEvent?.Invoke(new Point(x, y), MouseAction.Down);
                    Win32Api.SetCapture(hWnd);
                }
                break;
            case Win32Api.WM_LBUTTONUP:
                {
                    var x = (short)(lParam.ToInt64() & 0xFFFF);
                    var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    host.MouseEvent?.Invoke(new Point(x, y), MouseAction.Up);
                    Win32Api.ReleaseCapture();
                }
                break;
            case Win32Api.WM_MOUSEMOVE:
                {
                    var x = (short)(lParam.ToInt64() & 0xFFFF);
                    var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    host.MouseEvent?.Invoke(new Point(x, y), MouseAction.Move);
                }
                break;
            case Win32Api.WM_MOUSEWHEEL:
                {
                    var wheelDelta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    var lParam64 = lParam.ToInt64();
                    var x = (short)(lParam64 & 0xFFFF);
                    var y = (short)((lParam64 >> 16) & 0xFFFF);
                    var screenPoint = new Win32Api.POINT { X = x, Y = y };
                    Win32Api.ScreenToClient(hWnd, ref screenPoint);
                    host.WheelEvent?.Invoke(new Point(screenPoint.X, screenPoint.Y), wheelDelta);
                }
                return IntPtr.Zero;
            case Win32Api.WM_KEYDOWN:
                host.KeyEvent?.Invoke(wParam.ToInt32(), KeyAction.Down);
                break;
            case Win32Api.WM_KEYUP:
                host.KeyEvent?.Invoke(wParam.ToInt32(), KeyAction.Up);
                break;
            case Win32Api.WM_CHAR:
                host.DispatchUtf16Character((char)wParam.ToInt32());
                return IntPtr.Zero;
            case Win32Api.WM_UNICHAR:
                if (wParam.ToInt32() == Win32Api.UNICODE_NOCHAR) return new IntPtr(1);
                if (Rune.IsValid(wParam.ToInt32()))
                    host.TextInput?.Invoke(char.ConvertFromUtf32(wParam.ToInt32()));
                return IntPtr.Zero;
            case Win32Api.WM_IME_STARTCOMPOSITION:
                host.ApplyTextInputRect(hWnd);
                break;
            case Win32Api.WM_TIMER:
                host.Tick?.Invoke();
                return IntPtr.Zero;
            case Win32Api.WM_IME_COMPOSITION:
                if ((lParam.ToInt64() & Win32Api.GCS_RESULTSTR) != 0)
                {
                    host.DispatchImeResult(hWnd);
                    return IntPtr.Zero;
                }
                break;
            case Win32Api.WM_SETCURSOR:
                if ((lParam.ToInt64() & 0xffff) == Win32Api.HTCLIENT)
                {
                    if (Win32Api.GetCursorPos(out var cursorPoint))
                    {
                        Win32Api.ScreenToClient(hWnd, ref cursorPoint);
                        host.MouseEvent?.Invoke(new Point(cursorPoint.X, cursorPoint.Y), MouseAction.Move);
                    }
                    host.ApplyCursor();
                    return new IntPtr(1);
                }
                break;
            case Win32Api.WM_PAINT:
                {
                    var paint = new Win32Api.PAINTSTRUCT();
                    Win32Api.BeginPaint(hWnd, ref paint);
                    if (host._lastFrame != null) host.PresentFrame(host._lastFrame);
                    Win32Api.EndPaint(hWnd, ref paint);
                }
                return IntPtr.Zero;
            case Win32Api.WM_DESTROY:
                Win32Api.KillTimer(hWnd, new UIntPtr(1));
                host._running = false;
                Win32Api.PostQuitMessage(0);
                break;
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public IntPtr Handle => _hwnd;

    public void SetTextInputRect(Rect rect)
    {
        _textInputRect = rect;
        if (_hwnd != IntPtr.Zero) ApplyTextInputRect(_hwnd);
    }

    public string GetClipboardText()
    {
        if (!Win32Api.OpenClipboard(_hwnd)) return "";
        try
        {
            var memory = Win32Api.GetClipboardData(Win32Api.CF_UNICODETEXT);
            if (memory == IntPtr.Zero) return "";
            var pointer = Win32Api.GlobalLock(memory);
            if (pointer == IntPtr.Zero) return "";
            try { return Marshal.PtrToStringUni(pointer) ?? ""; }
            finally { Win32Api.GlobalUnlock(memory); }
        }
        finally
        {
            Win32Api.CloseClipboard();
        }
    }

    public void SetClipboardText(string text)
    {
        if (!Win32Api.OpenClipboard(_hwnd)) return;
        IntPtr memory = IntPtr.Zero;
        try
        {
            Win32Api.EmptyClipboard();
            var characters = (text ?? "").ToCharArray();
            var byteCount = (characters.Length + 1) * sizeof(char);
            memory = Win32Api.GlobalAlloc(
                Win32Api.GMEM_MOVEABLE | Win32Api.GMEM_ZEROINIT,
                new UIntPtr((uint)byteCount));
            if (memory == IntPtr.Zero) return;
            var pointer = Win32Api.GlobalLock(memory);
            if (pointer == IntPtr.Zero) return;
            try
            {
                Marshal.Copy(characters, 0, pointer, characters.Length);
                Marshal.WriteInt16(pointer, characters.Length * sizeof(char), 0);
            }
            finally
            {
                Win32Api.GlobalUnlock(memory);
            }

            if (Win32Api.SetClipboardData(Win32Api.CF_UNICODETEXT, memory) != IntPtr.Zero)
                memory = IntPtr.Zero;
        }
        finally
        {
            if (memory != IntPtr.Zero) Win32Api.GlobalFree(memory);
            Win32Api.CloseClipboard();
        }
    }

    private void DispatchUtf16Character(char character)
    {
        if (char.IsControl(character)) return;

        if (char.IsHighSurrogate(character))
        {
            _pendingHighSurrogate = character;
            return;
        }

        if (char.IsLowSurrogate(character) && _pendingHighSurrogate is char high)
        {
            TextInput?.Invoke(new string([high, character]));
            _pendingHighSurrogate = null;
            return;
        }

        _pendingHighSurrogate = null;
        TextInput?.Invoke(character.ToString());
    }

    private void ApplyCursor()
    {
        var cursorId = _cursor == CursorKind.Text ? Win32Api.IDC_IBEAM : Win32Api.IDC_ARROW;
        Win32Api.SetCursor(Win32Api.LoadCursor(IntPtr.Zero, new IntPtr(cursorId)));
    }

    private void ApplyTextInputRect(IntPtr hWnd)
    {
        if (_textInputRect.IsEmpty) return;
        var inputContext = Win32Api.ImmGetContext(hWnd);
        if (inputContext == IntPtr.Zero) return;
        try
        {
            var x = (int)MathF.Round(_textInputRect.X);
            var y = (int)MathF.Round(_textInputRect.Y);
            var bottom = (int)MathF.Round(_textInputRect.Bottom);
            var composition = new Win32Api.COMPOSITIONFORM
            {
                Style = Win32Api.CFS_POINT,
                CurrentPosition = new Win32Api.POINT { X = x, Y = y }
            };
            var candidate = new Win32Api.CANDIDATEFORM
            {
                Style = Win32Api.CFS_EXCLUDE,
                CurrentPosition = new Win32Api.POINT { X = x, Y = bottom },
                Area = new Win32Api.RECT { Left = x, Top = y, Right = x + 2, Bottom = bottom }
            };
            Win32Api.ImmSetCompositionWindow(inputContext, ref composition);
            Win32Api.ImmSetCandidateWindow(inputContext, ref candidate);
        }
        finally
        {
            Win32Api.ImmReleaseContext(hWnd, inputContext);
        }
    }

    private void DispatchImeResult(IntPtr hWnd)
    {
        var inputContext = Win32Api.ImmGetContext(hWnd);
        if (inputContext == IntPtr.Zero) return;

        try
        {
            var byteCount = Win32Api.ImmGetCompositionString(
                inputContext, Win32Api.GCS_RESULTSTR, IntPtr.Zero, 0);
            if (byteCount <= 0) return;

            var buffer = Marshal.AllocHGlobal(byteCount);
            try
            {
                var written = Win32Api.ImmGetCompositionString(
                    inputContext, Win32Api.GCS_RESULTSTR, buffer, byteCount);
                if (written > 0)
                {
                    var text = Marshal.PtrToStringUni(buffer, written / sizeof(char));
                    if (!string.IsNullOrEmpty(text)) TextInput?.Invoke(text);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            Win32Api.ImmReleaseContext(hWnd, inputContext);
        }
    }

    private void PresentFrame(Bitmap bitmap)
    {
        if (_hwnd == IntPtr.Zero) return;
        _lastFrame = bitmap;
        var info = new Win32Api.BITMAPINFO
        {
            bmiHeader = new Win32Api.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>(),
                biWidth = bitmap.Width,
                biHeight = -bitmap.Height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32Api.BI_RGB,
                biSizeImage = (uint)bitmap.Pixels.Length
            }
        };

        var handle = GCHandle.Alloc(bitmap.Pixels, GCHandleType.Pinned);
        var dc = Win32Api.GetDC(_hwnd);
        try
        {
            Win32Api.StretchDIBits(
                dc,
                0, 0, (int)_clientSize.Width, (int)_clientSize.Height,
                0, 0, bitmap.Width, bitmap.Height,
                handle.AddrOfPinnedObject(), ref info,
                Win32Api.DIB_RGB_COLORS, Win32Api.SRCCOPY);
        }
        finally
        {
            if (dc != IntPtr.Zero) Win32Api.ReleaseDC(_hwnd, dc);
            handle.Free();
        }
    }
}
