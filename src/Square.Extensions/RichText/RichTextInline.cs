namespace Square.Extensions.RichText;

public abstract class RichTextInline
{
    public abstract string PlainText { get; }
}

public sealed class RichTextRun : RichTextInline
{
    public RichTextRun(string text, RichTextMarks? marks = null)
    {
        Text = text ?? "";
        Marks = marks ?? RichTextMarks.Empty;
    }

    public string Text { get; set; }
    public RichTextMarks Marks { get; set; }
    public override string PlainText => Text;

    public RichTextRun Slice(int start, int length)
    {
        if (start < 0 || start > Text.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || start + length > Text.Length) throw new ArgumentOutOfRangeException(nameof(length));
        return new RichTextRun(Text.Substring(start, length), Marks);
    }
}