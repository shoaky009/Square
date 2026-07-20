namespace Square.Extensions.RichText;

public enum RichTextBlockKind
{
    Paragraph,
    Heading,
    Quote,
    CodeBlock
}

public sealed class RichTextBlock
{
    public RichTextBlock(RichTextBlockKind kind, IEnumerable<RichTextInline>? inlines = null, int headingLevel = 0)
    {
        Kind = kind;
        HeadingLevel = headingLevel;
        if (inlines != null) Inlines.AddRange(inlines);
        RichTextSchema.ValidateBlock(this);
    }

    public RichTextBlockKind Kind { get; }
    public int HeadingLevel { get; }
    public List<RichTextInline> Inlines { get; } = [];
    public string PlainText => string.Concat(Inlines.Select(inline => inline.PlainText));

    public static RichTextBlock Paragraph(params RichTextInline[] inlines) =>
        new(RichTextBlockKind.Paragraph, inlines);

    public static RichTextBlock Heading(int level, params RichTextInline[] inlines) =>
        new(RichTextBlockKind.Heading, inlines, level);

    public static RichTextBlock Quote(params RichTextInline[] inlines) =>
        new(RichTextBlockKind.Quote, inlines);

    public static RichTextBlock CodeBlock(string text) =>
        new(RichTextBlockKind.CodeBlock, [new RichTextRun(text)]);

    public void Normalize()
    {
        RichTextSchema.ValidateBlock(this);
        for (var i = Inlines.Count - 1; i >= 0; i--)
        {
            if (Inlines[i] is RichTextRun { Text.Length: 0 })
                Inlines.RemoveAt(i);
        }

        for (var i = 1; i < Inlines.Count;)
        {
            if (Inlines[i - 1] is RichTextRun previous &&
                Inlines[i] is RichTextRun current &&
                previous.Marks == current.Marks)
            {
                previous.Text += current.Text;
                Inlines.RemoveAt(i);
                continue;
            }

            i++;
        }
    }
}