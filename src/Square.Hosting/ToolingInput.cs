using Square.Graphics;
using Square.Platform;

namespace Square.Hosting;

public sealed record ToolingPointerInput(Point Position, MouseAction Action, KeyModifiers Modifiers = KeyModifiers.None);
public sealed record ToolingKeyInput(int KeyCode, KeyAction Action, KeyModifiers Modifiers = KeyModifiers.None);
public sealed record ToolingWheelInput(Point Position, int Delta, KeyModifiers Modifiers = KeyModifiers.None);