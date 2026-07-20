using System.Globalization;
using System.Text;

namespace Square.Graphics;

public sealed class TextLayout
{
    public const float DefaultLineHeight = 1.2f;

    public string Text { get; set; } = "";
    public Font Font { get; set; } = new();
    public Size MaxSize { get; set; } = new(float.MaxValue, float.MaxValue);
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public float LineHeight { get; set; } = DefaultLineHeight;

    public TextLayout() { }
    public TextLayout(string text, Font font) { Text = text; Font = font; }

    public Size Measure() => MeasureCore();

    public float MeasureOffset(int offset)
    {
        if (offset < 0 || offset > Text.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        var width = 0f;
        foreach (var rune in Text.EnumerateRunes())
        {
            if (rune.Value == '\n') break;
            if (rune.Utf16SequenceLength > offset) break;
            width += MeasureRuneAdvance(rune, Font.Size);
            offset -= rune.Utf16SequenceLength;
        }
        return width;
    }

    public int HitTestOffset(float x)
    {
        if (string.IsNullOrEmpty(Text) || x <= 0) return 0;

        var offset = 0;
        var width = 0f;
        foreach (var rune in Text.EnumerateRunes())
        {
            if (rune.Value == '\n') break;
            var advance = MeasureRuneAdvance(rune, Font.Size);
            if (x < width + advance / 2f) break;
            width += advance;
            offset += rune.Utf16SequenceLength;
        }
        return Math.Clamp(offset, 0, Text.Length);
    }

    private Size MeasureCore()
    {
        if (string.IsNullOrEmpty(Text))
            return Size.Zero;

        var lineHeight = Font.Size * LineHeight;
        var maxWidth = MaxSize.Width;
        var constrainWidth = !float.IsNaN(maxWidth) && !float.IsInfinity(maxWidth) && maxWidth > 0;
        var widestLine = 0f;
        var lineCount = 1;
        var currentWidth = 0f;

        foreach (var rune in Text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                widestLine = Math.Max(widestLine, currentWidth);
                currentWidth = 0;
                lineCount++;
                continue;
            }

            var advance = MeasureRuneAdvance(rune, Font.Size);
            if (constrainWidth && currentWidth > 0 && currentWidth + advance > maxWidth)
            {
                widestLine = Math.Max(widestLine, currentWidth);
                currentWidth = 0;
                lineCount++;
            }
            currentWidth += advance;
        }

        widestLine = Math.Max(widestLine, currentWidth);
        return new Size(constrainWidth ? Math.Min(maxWidth, widestLine) : widestLine, lineCount * lineHeight);
    }

    public static float MeasureRuneAdvance(Rune rune, float fontSize)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            return 0;

        return IsFullWidth(rune.Value) ? fontSize : fontSize * 0.5f;
    }

    private static bool IsFullWidth(int value)
    {
        return value is >= 0x1100 and <= 0x115f or
            0x231a or 0x231b or 0x2329 or 0x232a or
            >= 0x2e80 and <= 0xa4cf or
            >= 0xac00 and <= 0xd7a3 or
            >= 0xf900 and <= 0xfaff or
            >= 0xfe10 and <= 0xfe19 or
            >= 0xfe30 and <= 0xfe6f or
            >= 0xff01 and <= 0xff60 or
            >= 0xffe0 and <= 0xffe6 or
            >= 0x1f300 and <= 0x1faff or
            >= 0x20000 and <= 0x3fffd;
    }
}
