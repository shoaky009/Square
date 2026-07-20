namespace Square.Tooling;

public sealed class ToolingOptions
{
    public int Port { get; set; }
    public string? AccessToken { get; set; }
    public bool AllowInputInjection { get; set; } = true;
    public bool AllowInspector { get; set; } = true;
    public bool IncludeSourcePaths { get; set; } = true;
    public bool IncludeTextContent { get; set; } = true;
}
