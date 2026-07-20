namespace Square.Extensions.RichText;

public static class RichTextSchema
{
    public static void ValidateDocument(RichTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Blocks.Count == 0)
            throw new InvalidOperationException("A rich text document must contain at least one block.");
        foreach (var block in document.Blocks)
            ValidateBlock(block);
    }

    public static void ValidateBlock(RichTextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Kind == RichTextBlockKind.Heading && block.HeadingLevel is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(block.HeadingLevel), "Heading level must be between 1 and 6.");
        if (block.Kind != RichTextBlockKind.Heading && block.HeadingLevel != 0)
            throw new InvalidOperationException("Only heading blocks can have a heading level.");

        foreach (var inline in block.Inlines)
        {
            if (inline is not RichTextRun)
                throw new InvalidOperationException($"Unsupported rich text inline '{inline.GetType().Name}'.");
        }
    }
}