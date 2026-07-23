namespace Square.Extensions.Markdown;

public abstract record MarkdownInline
{
    public abstract string PlainText { get; }
}

public sealed record MarkdownText(string Text) : MarkdownInline
{
    public override string PlainText => Text;
}

public sealed record MarkdownCode(string Code) : MarkdownInline
{
    public override string PlainText => Code;
}

public sealed record MarkdownLink : MarkdownInline
{
    public MarkdownLink(string destination, string? title, IEnumerable<MarkdownInline>? inlines = null)
    {
        Destination = destination ?? "";
        Title = title;
        Inlines = inlines?.ToArray() ?? [];
    }

    public string Destination { get; }
    public string? Title { get; }
    public IReadOnlyList<MarkdownInline> Inlines { get; }
    public override string PlainText => string.Concat(Inlines.Select(inline => inline.PlainText));
}

public sealed record MarkdownImage(
    string Source,
    string AltText,
    string? Title = null) : MarkdownInline
{
    public override string PlainText => AltText;
}

public enum MarkdownEmphasisKind
{
    Italic,
    Bold,
    Strikethrough
}

public sealed record MarkdownEmphasis : MarkdownInline
{
    public MarkdownEmphasis(MarkdownEmphasisKind kind, IEnumerable<MarkdownInline>? inlines = null)
    {
        Kind = kind;
        Inlines = inlines?.ToArray() ?? [];
    }

    public MarkdownEmphasisKind Kind { get; }
    public IReadOnlyList<MarkdownInline> Inlines { get; }
    public override string PlainText => string.Concat(Inlines.Select(inline => inline.PlainText));
}

public sealed record MarkdownLineBreak(bool IsHard) : MarkdownInline
{
    public override string PlainText => "\n";
}
