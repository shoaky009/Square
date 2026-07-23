namespace Square.Extensions.Markdown;

internal static class MarkdownParser
{
    public static MarkdownDocument Parse(string? source) => MarkdigMarkdownParser.Parse(source);
}
