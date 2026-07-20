using Square.Graphics;
using System.Text;

namespace Square.Text.Layout;

public sealed class TextMeasurer
{
    private readonly Glyph.GlyphCache _glyphCache = new();

    public Size MeasureText(string text, Font font, Size maxSize)
    {
        if (string.IsNullOrEmpty(text)) return Size.Zero;

        return new TextLayout(text, font) { MaxSize = maxSize }.Measure();
    }

    public int HitTest(string text, Font font, float x)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var width = 0f;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var advance = TextLayout.MeasureRuneAdvance(rune, font);
            if (x < width + advance / 2f) return index;
            width += advance;
            index += rune.Utf16SequenceLength;
        }
        return text.Length;
    }

    public Point GetPosition(string text, Font font, int index)
    {
        if (string.IsNullOrEmpty(text)) return Point.Zero;
        index = Math.Clamp(index, 0, text.Length);
        var width = 0f;
        var consumed = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (consumed >= index) break;
            width += TextLayout.MeasureRuneAdvance(rune, font);
            consumed += rune.Utf16SequenceLength;
        }
        return new Point(width, 0);
    }
}
