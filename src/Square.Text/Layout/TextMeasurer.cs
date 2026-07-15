using Square.Graphics;

namespace Square.Text.Layout;

public sealed class TextMeasurer
{
    private readonly Glyph.GlyphCache _glyphCache = new();

    public Size MeasureText(string text, Font font, Size maxSize)
    {
        if (string.IsNullOrEmpty(text)) return Size.Zero;

        var lineHeight = font.Size * 1.2f;
        var charWidth = font.Size * 0.5f;
        var totalWidth = text.Length * charWidth;

        if (maxSize.Width < float.MaxValue && totalWidth > maxSize.Width && maxSize.Width > 0)
        {
            var charsPerLine = Math.Max(1, (int)(maxSize.Width / charWidth));
            var lines = (text.Length + charsPerLine - 1) / charsPerLine;
            return new Size(maxSize.Width, lines * lineHeight);
        }

        return new Size(totalWidth, lineHeight);
    }

    public int HitTest(string text, Font font, float x)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var charWidth = font.Size * 0.5f;
        var index = (int)(x / charWidth);
        return Math.Clamp(index, 0, text.Length);
    }

    public Point GetPosition(string text, Font font, int index)
    {
        if (string.IsNullOrEmpty(text)) return Point.Zero;
        var charWidth = font.Size * 0.5f;
        var lineHeight = font.Size * 1.2f;
        index = Math.Clamp(index, 0, text.Length);
        return new Point(index * charWidth, 0);
    }
}