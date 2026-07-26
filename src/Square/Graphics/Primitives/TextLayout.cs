using System.Globalization;
using System.Text;

namespace Square.Graphics;

public sealed class TextLayout
{
    public const float DefaultLineHeight = 1.2f;
    private static Func<Rune, Font, float?>? _advanceProvider;

    public string Text { get; set; } = "";
    public Font Font { get; set; } = new();
    public Size MaxSize { get; set; } = new(float.MaxValue, float.MaxValue);
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public float LineHeight { get; set; } = DefaultLineHeight;

    public TextLayout() { }
    public TextLayout(string text, Font font) { Text = text; Font = font; }

    public static void RegisterAdvanceProvider(Func<Rune, Font, float?> provider)
        => _advanceProvider = provider ?? throw new ArgumentNullException(nameof(provider));

    public Size Measure() => MeasureCore();

    public float MeasureOffset(int offset)
    {
        if (offset < 0 || offset > Text.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        var width = 0f;
        foreach (var rune in Text.EnumerateRunes())
        {
            if (rune.Value == '\n') break;
            if (rune.Utf16SequenceLength > offset) break;
            width += MeasureRuneAdvance(rune, Font);
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
            var advance = MeasureRuneAdvance(rune, Font);
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

        var lineHeight = TextMetrics.GetLineHeight(Font, LineHeight);
        var maxWidth = MaxSize.Width;
        var lines = TextWrapping.Wrap(Text, maxWidth, (_, rune) => MeasureRuneAdvance(rune, Font));
        var widestLine = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        var constrainWidth = float.IsFinite(maxWidth) && maxWidth > 0;
        var wrapped = lines.Count > Text.Count(character => character == '\n') + 1;
        return new Size(constrainWidth && wrapped ? maxWidth : widestLine, lines.Count * lineHeight);
    }

    public static float MeasureRuneAdvance(Rune rune, float fontSize)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            return 0;

        return IsFullWidth(rune.Value) ? fontSize : fontSize * 0.5f;
    }

    public static float MeasureRuneAdvance(Rune rune, Font font)
    {
        if (TextMetrics.IsZeroAdvanceCategory(rune)) return 0;
        var provided = TextMetrics.GetGlyphMetrics(font, rune).AdvanceX;
        if (provided >= 0 && float.IsFinite(provided)) return provided;
        var measured = _advanceProvider?.Invoke(rune, font);
        if (measured is >= 0 and float value && float.IsFinite(value)) return value;
        return MeasureRuneAdvanceFallback(rune, font);
    }

    internal static float MeasureRuneAdvanceFallback(Rune rune, Font font)
    {
        var measured = _advanceProvider?.Invoke(rune, font);
        if (measured is >= 0 and float value && float.IsFinite(value)) return value;
        var advance = MeasureRuneAdvance(rune, font.Size);
        return font.Weight >= FontWeight.Bold ? advance * 1.08f : advance;
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

public readonly record struct TextLineRange(int StartOffset, int EndOffset, float Width);

public static class TextWrapping
{
    public static IReadOnlyList<TextLineRange> Wrap(
        string text,
        float maxWidth,
        Func<int, Rune, float> measureAdvance)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measureAdvance);
        if (text.Length == 0) return [];

        var constrainWidth = float.IsFinite(maxWidth) && maxWidth > 0;
        var lines = new List<TextLineRange>();
        var tokens = new List<Token>();
        var paragraphStart = 0;
        for (var offset = 0; offset < text.Length;)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out var consumed);
            if (status != System.Buffers.OperationStatus.Done) break;
            var end = offset + consumed;
            if (rune.Value == '\n')
            {
                WrapParagraph(tokens, paragraphStart, maxWidth, constrainWidth, lines);
                tokens.Clear();
                paragraphStart = end;
            }
            else
            {
                tokens.Add(new Token(offset, end, rune, Math.Max(0, measureAdvance(offset, rune))));
            }
            offset = end;
        }
        WrapParagraph(tokens, paragraphStart, maxWidth, constrainWidth, lines);
        return lines;
    }

    private static void WrapParagraph(
        List<Token> tokens,
        int paragraphStart,
        float maxWidth,
        bool constrainWidth,
        List<TextLineRange> lines)
    {
        if (tokens.Count == 0)
        {
            lines.Add(new TextLineRange(paragraphStart, paragraphStart, 0));
            return;
        }

        var lineStart = 0;
        while (lineStart < tokens.Count)
        {
            var width = 0f;
            var lastBreak = -1;
            var widthAtBreak = 0f;
            var wrapped = false;
            for (var index = lineStart; index < tokens.Count; index++)
            {
                if (index > lineStart && CanBreakBetween(tokens[index - 1].Rune, tokens[index].Rune))
                {
                    lastBreak = index;
                    widthAtBreak = width;
                }

                var advance = tokens[index].Advance;
                if (constrainWidth && width > 0 && width + advance > maxWidth)
                {
                    var lineEnd = lastBreak > lineStart ? lastBreak : index;
                    var lineWidth = lastBreak > lineStart ? widthAtBreak : width;
                    lines.Add(new TextLineRange(tokens[lineStart].Start, tokens[lineEnd - 1].End, lineWidth));
                    lineStart = lineEnd;
                    wrapped = true;
                    break;
                }
                width += advance;
            }

            if (wrapped) continue;
            lines.Add(new TextLineRange(tokens[lineStart].Start, tokens[^1].End, width));
            break;
        }
    }

    private static bool CanBreakBetween(Rune previous, Rune current)
    {
        if (previous.Value == 0x200b) return true;
        if (Rune.IsWhiteSpace(previous)) return true;
        if (previous.Value is '-' or '/' or '\\') return true;
        if (!IsCjk(previous) && !IsCjk(current)) return false;
        return !IsOpeningPunctuation(previous.Value) && !IsClosingPunctuation(current.Value);
    }

    private static bool IsCjk(Rune rune) => rune.Value is
        >= 0x2e80 and <= 0x9fff or
        >= 0xac00 and <= 0xd7af or
        >= 0xf900 and <= 0xfaff or
        >= 0xff01 and <= 0xff60 or
        >= 0x20000 and <= 0x3fffd;

    private static bool IsOpeningPunctuation(int value) => value is
        '(' or '[' or '{' or 0x2018 or 0x201c or 0x3008 or 0x300a or 0x300c or 0x300e or 0x3010 or 0x3014 or 0xff08 or 0xff3b;

    private static bool IsClosingPunctuation(int value) => value is
        ')' or ']' or '}' or ',' or '.' or '!' or '?' or ':' or ';' or
        0x2019 or 0x201d or 0x3001 or 0x3002 or 0x3009 or 0x300b or 0x300d or 0x300f or 0x3011 or 0x3015 or 0xff09 or 0xff0c or 0xff0e or 0xff01 or 0xff1f;

    private readonly record struct Token(int Start, int End, Rune Rune, float Advance);
}
