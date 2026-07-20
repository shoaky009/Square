namespace Square.Extensions.RichText;

public sealed class RichTextDocument
{
    public RichTextDocument(IEnumerable<RichTextBlock>? blocks = null)
    {
        if (blocks != null) Blocks.AddRange(blocks);
        if (Blocks.Count == 0) Blocks.Add(RichTextBlock.Paragraph());
        RichTextSchema.ValidateDocument(this);
    }

    public List<RichTextBlock> Blocks { get; } = [];

    public string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));

    public static RichTextDocument FromPlainText(string? text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return new RichTextDocument(lines.Select(line => RichTextBlock.Paragraph(new RichTextRun(line))));
    }

    public void Normalize()
    {
        if (Blocks.Count == 0) Blocks.Add(RichTextBlock.Paragraph());
        foreach (var block in Blocks) block.Normalize();
        RichTextSchema.ValidateDocument(this);
    }
}