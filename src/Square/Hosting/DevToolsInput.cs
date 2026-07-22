using Square.Graphics;
using Square.Platform;

namespace Square.Hosting;

public sealed record DevToolsPointerInput(Point Position, MouseAction Action, KeyModifiers Modifiers = KeyModifiers.None);
public sealed record DevToolsKeyInput(int KeyCode, KeyAction Action, KeyModifiers Modifiers = KeyModifiers.None);
public sealed record DevToolsWheelInput(Point Position, int Delta, KeyModifiers Modifiers = KeyModifiers.None);
