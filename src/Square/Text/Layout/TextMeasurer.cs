using Square.Graphics;
using System.Text;

namespace Square.Text.Layout;

/// <summary>文本测量器，提供尺寸测量、命中测试与光标定位。</summary>
public sealed class TextMeasurer
{
    private readonly Glyph.GlyphCache _glyphCache = new();

    /// <summary>测量文本在指定字体与最大尺寸下的占用大小。</summary>
    /// <param name="text">要测量的文本。</param>
    /// <param name="font">字体。</param>
    /// <param name="maxSize">最大尺寸约束。</param>
    /// <returns>文本占用尺寸；空文本返回 <see cref="Size.Zero"/>。</returns>
    public Size MeasureText(string text, Font font, Size maxSize)
    {
        if (string.IsNullOrEmpty(text)) return Size.Zero;

        return new TextLayout(text, font) { MaxSize = maxSize }.Measure();
    }

    /// <summary>命中测试：返回最接近指定水平位置的字符索引。</summary>
    /// <param name="text">文本。</param>
    /// <param name="font">字体。</param>
    /// <param name="x">水平位置。</param>
    /// <returns>字符索引；空文本返回 0。</returns>
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

    /// <summary>获取指定字符索引处的光标位置。</summary>
    /// <param name="text">文本。</param>
    /// <param name="font">字体。</param>
    /// <param name="index">字符索引。</param>
    /// <returns>光标位置；空文本返回 <see cref="Point.Zero"/>。</returns>
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
