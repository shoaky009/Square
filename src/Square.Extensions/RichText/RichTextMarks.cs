namespace Square.Extensions.RichText;

public sealed record RichTextMarks(
    bool Bold = false,
    bool Italic = false,
    bool Underline = false,
    string? Link = null,
    string? Foreground = null,
    string? Background = null)
{
    public static RichTextMarks Empty { get; } = new();

    public bool IsEmpty =>
        !Bold &&
        !Italic &&
        !Underline &&
        string.IsNullOrEmpty(Link) &&
        string.IsNullOrEmpty(Foreground) &&
        string.IsNullOrEmpty(Background);
}