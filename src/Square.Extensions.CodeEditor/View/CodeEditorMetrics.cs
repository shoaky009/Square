using System.Text;
using Square.Graphics;
using Square.Text;

namespace Square.Extensions.CodeEditor;

/// <summary>等宽度量与 tab 展开。</summary>
internal static class CodeEditorMetrics
{
    public static float MeasureLineWidth(string line, Font font, int tabSize)
    {
        var width = 0f;
        var col = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (rune.Value == '\t')
            {
                var spaces = tabSize - col % tabSize;
                var spaceAdvance = TextMetrics.GetGlyphMetrics(font, new Rune(' ')).AdvanceX;
                width += spaceAdvance * spaces;
                col += spaces;
            }
            else
            {
                width += TextMetrics.GetGlyphMetrics(font, rune).AdvanceX;
                col += rune.IsAscii && rune.Value < 128 ? 1 : 2;
            }
        }
        return width;
    }

    public static int ColumnAtX(string line, Font font, int tabSize, float x)
    {
        if (x <= 0) return 0;
        var width = 0f;
        var col = 0;
        var index = 0;
        while (index < line.Length)
        {
            var rune = Decode(line, index, out var len);
            float advance;
            int colAdvance;
            if (rune.Value == '\t')
            {
                colAdvance = tabSize - col % tabSize;
                advance = TextMetrics.GetGlyphMetrics(font, new Rune(' ')).AdvanceX * colAdvance;
            }
            else
            {
                advance = TextMetrics.GetGlyphMetrics(font, rune).AdvanceX;
                colAdvance = rune.IsAscii && rune.Value < 128 ? 1 : 2;
            }
            if (x < width + advance / 2f) return index;
            width += advance;
            col += colAdvance;
            index += len;
        }
        return line.Length;
    }

    public static float XAtColumn(string line, Font font, int tabSize, int charIndex)
    {
        charIndex = Math.Clamp(charIndex, 0, line.Length);
        var width = 0f;
        var col = 0;
        var index = 0;
        while (index < charIndex)
        {
            var rune = Decode(line, index, out var len);
            if (index + len > charIndex) break;
            if (rune.Value == '\t')
            {
                var spaces = tabSize - col % tabSize;
                width += TextMetrics.GetGlyphMetrics(font, new Rune(' ')).AdvanceX * spaces;
                col += spaces;
            }
            else
            {
                width += TextMetrics.GetGlyphMetrics(font, rune).AdvanceX;
                col += rune.IsAscii && rune.Value < 128 ? 1 : 2;
            }
            index += len;
        }
        return width;
    }

    public static string ExpandTabs(string line, int tabSize)
    {
        if (!line.Contains('\t')) return line;
        var sb = new StringBuilder(line.Length + 8);
        var col = 0;
        foreach (var ch in line)
        {
            if (ch == '\t')
            {
                var spaces = tabSize - col % tabSize;
                sb.Append(' ', spaces);
                col += spaces;
            }
            else
            {
                sb.Append(ch);
                col += char.IsAscii(ch) ? 1 : 2;
            }
        }
        return sb.ToString();
    }

    private static Rune Decode(string text, int index, out int length)
    {
        Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out length);
        return rune;
    }
}
