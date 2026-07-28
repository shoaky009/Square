namespace Square.Extensions.Markdown;

public sealed class MarkdownDocument
{
    public MarkdownDocument(IEnumerable<MarkdownBlock>? blocks = null)
    {
        Blocks = blocks?.ToArray() ?? [];
    }

    public static MarkdownDocument Empty { get; } = new();

    public IReadOnlyList<MarkdownBlock> Blocks { get; }

    public string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));

    public static MarkdownDocument Parse(string? source) => MarkdownParser.Parse(source);
}
