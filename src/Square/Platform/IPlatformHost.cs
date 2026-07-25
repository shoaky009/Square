using Square.Graphics;
using Square.Hosting;

namespace Square.Platform;

public interface IPlatformHost : IDisposable
{
    Size ClientSize { get; }
    float DpiScale { get; }
    bool IsRunning { get; }
    AppWindowState State => AppWindowState.Normal;
    string Title { get; set; }
    CursorKind Cursor { get; set; }
    KeyModifiers Modifiers { get; }

    event Action<Size>? SizeChanged;
    event Action<Point, MouseAction>? MouseEvent;
    event Action<Point, int>? WheelEvent;
    event Action<int, KeyAction>? KeyEvent;
    event Action<string>? TextInput;
    event Action? Tick;

    event Action<AppWindowState>? StateChanged
    {
        add { }
        remove { }
    }

    event Action? Closed
    {
        add { }
        remove { }
    }

    void Show();

    void ShowAfterFirstFrame()
    {
    }

    void Close();

    void Minimize()
    {
    }

    void Maximize()
    {
    }

    void Restore()
    {
    }

    void BeginMove()
    {
    }

    IRenderContext CreateRenderContext();
    void PumpEvents();
    void SetTextInputRect(Rect rect);
    string GetClipboardText();
    void SetClipboardText(string text);
}

internal interface IPlatformNativeWindow
{
    IntPtr Handle { get; }
}

public enum MouseAction
{
    Down,
    Up,
    Move,
    Wheel
}

public enum KeyAction
{
    Down,
    Up
}

public enum CursorKind
{
    Arrow,
    Text,
    Hand
}

public enum AppWindowState
{
    Normal,
    Minimized,
    Maximized
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4
}

public interface IPlatformFactory
{
    string Name { get; }
    IPlatformHost CreateHost(PlatformHostCreateInfo info);
}

public interface IPlatformScreenshotProvider
{
    bool TryCaptureByProcessId(int processId, out Bitmap? bitmap);
}

public enum SoftwareRenderSurfaceKind
{
    Auto,
    Bitmap
}

public sealed class PlatformHostCreateInfo
{
    public required string Title { get; set; }
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public string RenderBackend { get; set; } = "Software";
    public SoftwareRenderSurfaceKind SoftwareSurface { get; set; } = SoftwareRenderSurfaceKind.Auto;
    public TitleStyle TitleStyle { get; set; } = TitleStyle.System;
    public BorderStyle BorderStyle { get; set; } = BorderStyle.Resizable;
    public IntPtr OwnerHandle { get; set; }
    public bool IsModal { get; set; }
}
