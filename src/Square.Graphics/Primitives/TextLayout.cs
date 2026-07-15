namespace Square.Graphics;

public sealed class TextLayout
{
    public string Text { get; set; } = "";
    public Font Font { get; set; } = new();
    public Size MaxSize { get; set; } = new(float.MaxValue, float.MaxValue);
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public float LineHeight { get; set; } = 1.2f;

    public TextLayout() { }
    public TextLayout(string text, Font font) { Text = text; Font = font; }

    public Size Measure() => MeasureCore();

    private Size MeasureCore()
    {
        if (string.IsNullOrEmpty(Text))
            return Size.Zero;

        var lineHeight = Font.Size * LineHeight;
        var charWidth = Font.Size * 0.5f;
        var totalWidth = Text.Length * charWidth;

        var maxWidth = MaxSize.Width;
        if (!float.IsNaN(maxWidth) && totalWidth > maxWidth && maxWidth > 0)
        {
            var charsPerLine = (int)(maxWidth / charWidth);
            if (charsPerLine <= 0) charsPerLine = 1;
            var lines = (Text.Length + charsPerLine - 1) / charsPerLine;
            return new Size(maxWidth, lines * lineHeight);
        }

        return new Size(totalWidth, lineHeight);
    }
}