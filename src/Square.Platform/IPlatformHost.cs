using Square.Graphics;
namespace Square.Platform;

public interface IPlatformHost : IDisposable
{
    Size ClientSize { get; }
    float DpiScale { get; }
    bool IsRunning { get; }
    string Title { get; set; }
    CursorKind Cursor { get; set; }
    KeyModifiers Modifiers { get; }

    event Action<Size>? SizeChanged;
    event Action<Point, MouseAction>? MouseEvent;
    event Action<Point, int>? WheelEvent;
    event Action<int, KeyAction>? KeyEvent;
    event Action<string>? TextInput;
    event Action? Tick;

    void Show();
    void Close();
    IRenderContext CreateRenderContext();
    void PumpEvents();
    void SetTextInputRect(Rect rect);
    string GetClipboardText();
    void SetClipboardText(string text);
}

public enum MouseAction { Down, Up, Move, Wheel }
public enum KeyAction { Down, Up }
public enum CursorKind { Arrow, Text }
[Flags]
public enum KeyModifiers { None = 0, Shift = 1, Control = 2, Alt = 4 }

public interface IPlatformFactory
{
    string Name { get; }
    IPlatformHost CreateHost(PlatformHostCreateInfo info);
}

public sealed class PlatformHostCreateInfo
{
    public required string Title { get; set; }
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public string RenderBackend { get; set; } = "Software";
}
