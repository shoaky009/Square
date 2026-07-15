using Square.Graphics;
using Square.Runtime;

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

    private static Win32Host? s_current;
    private static WndProcDelegate? s_wndProc;
    private static bool s_classRegistered;

    public Size ClientSize => _clientSize;
    public float DpiScale => _dpiScale;
    public bool IsRunning => _running;

    public event Action<Size>? SizeChanged;
    public event Action<Point, MouseAction>? MouseEvent;
    public event Action<int, KeyAction>? KeyEvent;

    public Win32Host(PlatformHostCreateInfo info)
    {
        _title = info.Title;
        _width = info.Width;
        _height = info.Height;
        s_current = this;
    }

    public void Show()
    {
        if (!s_classRegistered)
        {
            s_wndProc = WndProc;
            var wc = new Win32Api.WNDCLASSEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32Api.WNDCLASSEX>(),
                style = Win32Api.CS_HREDRAW | Win32Api.CS_VREDRAW,
                lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = Win32Api.GetModuleHandle(null),
                hCursor = IntPtr.Zero,
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
            DpiScale = _dpiScale
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
                host._clientSize = new Size(rect.Width, rect.Height);
                host.SizeChanged?.Invoke(host._clientSize);
                break;
            case Win32Api.WM_LBUTTONDOWN:
                {
                    var x = (short)(lParam.ToInt64() & 0xFFFF);
                    var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    host.MouseEvent?.Invoke(new Point(x, y), MouseAction.Down);
                }
                break;
            case Win32Api.WM_LBUTTONUP:
                host.MouseEvent?.Invoke(new Point(0, 0), MouseAction.Up);
                break;
            case Win32Api.WM_MOUSEMOVE:
                {
                    var x = (short)(lParam.ToInt64() & 0xFFFF);
                    var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    host.MouseEvent?.Invoke(new Point(x, y), MouseAction.Move);
                }
                break;
            case Win32Api.WM_KEYDOWN:
                host.KeyEvent?.Invoke(wParam.ToInt32(), KeyAction.Down);
                break;
            case Win32Api.WM_KEYUP:
                host.KeyEvent?.Invoke(wParam.ToInt32(), KeyAction.Up);
                break;
            case Win32Api.WM_DESTROY:
                host._running = false;
                Win32Api.PostQuitMessage(0);
                break;
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public IntPtr Handle => _hwnd;
}