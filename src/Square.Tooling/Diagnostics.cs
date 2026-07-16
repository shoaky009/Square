namespace Square.Tooling;

/// <summary>
/// Basic diagnostic output utilities for Square development.
/// </summary>
public static class Diagnostics
{
    public static void Info(string message) =>
        Console.WriteLine($"[Square] {message}");

    public static void Warn(string message) =>
        Console.WriteLine($"[Square WARNING] {message}");

    public static void Error(string message) =>
        Console.Error.WriteLine($"[Square ERROR] {message}");
}