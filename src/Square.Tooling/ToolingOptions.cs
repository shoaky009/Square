namespace Square.Tooling;

public sealed class ToolingOptions
{
    public int Port { get; set; } = 5128;
    public string? AccessToken { get; set; }
    public bool AllowInputInjection { get; set; } = true;
}