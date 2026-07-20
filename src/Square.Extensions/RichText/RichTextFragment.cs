namespace Square.Extensions.RichText;

public sealed class RichTextFragment
{
    public RichTextFragment(IEnumerable<RichTextBlock>? blocks = null)
    {
        if (blocks != null) Blocks.AddRange(blocks);
    }

    public List<RichTextBlock> Blocks { get; } = [];
    public bool IsEmpty => Blocks.Count == 0 || Blocks.All(block => block.PlainText.Length == 0);
    public string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));
}