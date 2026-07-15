using Square.Graphics;
using Square.Runtime;

namespace Square.Platform;

public interface IPlatformHost
{
    Size ClientSize { get; }
    float DpiScale { get; }
    bool IsRunning { get; }

    event Action<Size>? SizeChanged;
    event Action<Point, MouseAction>? MouseEvent;
    event Action<int, KeyAction>? KeyEvent;

    void Show();
    void Close();
    IRenderContext CreateRenderContext();
    void PumpEvents();
}

public enum MouseAction { Down, Up, Move, Wheel }
public enum KeyAction { Down, Up }

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
}