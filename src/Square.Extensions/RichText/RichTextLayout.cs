using System.Text;
using Square.Graphics;
using Square.Text.Glyph;

namespace Square.Extensions.RichText;

public sealed record RichTextLayoutFragment(
    RichTextRun Run,
    int StartOffset,
    int EndOffset,
    Font Font,
    Rect Bounds);

public sealed record RichTextLayoutLine(
    int StartOffset,
    int EndOffset,
    Rect Bounds,
    IReadOnlyList<RichTextLayoutFragment> Fragments);

public sealed class RichTextBlockLayout
{
    private static readonly SystemGlyphRasterizer GlyphRasterizer = new();

    public RichTextBlockLayout(RichTextBlock block, Rect bounds, IReadOnlyList<RichTextLayoutLine> lines)
    {
        Block = block;
        Bounds = bounds;
        Lines = lines;
    }

    public RichTextBlock Block { get; }
    public Rect Bounds { get; }
    public IReadOnlyList<RichTextLayoutLine> Lines { get; }

    public int HitTestOffset(Point point)
    {
        var line = FindNearestLine(point.Y);
        if (line.Fragments.Count == 0) return line.StartOffset;
        foreach (var fragment in line.Fragments)
        {
            if (point.X > fragment.Bounds.Right) continue;
            var localX = Math.Max(0, point.X - fragment.Bounds.X);
            var localOffset = HitTestOffset(fragment.Run.Text, fragment.Font, localX);
            return Math.Clamp(fragment.StartOffset + localOffset, fragment.StartOffset, fragment.EndOffset);
        }
        return line.EndOffset;
    }

    public Rect GetCaretRect(int offset)
    {
        if (offset < 0 || offset > Block.PlainText.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        var line = Lines[GetLineIndex(offset)];
        if (line.Fragments.Count == 0) return new Rect(line.Bounds.X, line.Bounds.Y, 1, line.Bounds.Height);

        foreach (var fragment in line.Fragments)
        {
            if (offset > fragment.EndOffset) continue;
            var localOffset = Math.Clamp(offset - fragment.StartOffset, 0, fragment.Run.Text.Length);
            var x = fragment.Bounds.X + MeasureOffset(fragment.Run.Text, fragment.Font, localOffset);
            return new Rect(x, line.Bounds.Y, 1, line.Bounds.Height);
        }
        return new Rect(line.Bounds.Right, line.Bounds.Y, 1, line.Bounds.Height);
    }

    public int GetLineIndex(int offset)
    {
        if (offset < 0 || offset > Block.PlainText.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        for (var i = 0; i < Lines.Count; i++)
        {
            if (offset < Lines[i].EndOffset || i == Lines.Count - 1)
                return i;
        }
        return Lines.Count - 1;
    }

    public IReadOnlyList<Rect> GetSelectionRects(int startOffset, int endOffset)
    {
        if (startOffset < 0 || endOffset < startOffset || endOffset > Block.PlainText.Length)
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        if (startOffset == endOffset) return [];

        var rects = new List<Rect>();
        foreach (var line in Lines)
        {
            var start = Math.Max(startOffset, line.StartOffset);
            var end = Math.Min(endOffset, line.EndOffset);
            if (end <= start) continue;
            var startX = MeasureOffsetOnLine(line, start);
            var endX = MeasureOffsetOnLine(line, end);
            rects.Add(new Rect(startX, line.Bounds.Y, Math.Max(1, endX - startX), line.Bounds.Height));
        }
        return rects;
    }

    private static float MeasureOffsetOnLine(RichTextLayoutLine line, int offset)
    {
        if (line.Fragments.Count == 0) return line.Bounds.X;
        foreach (var fragment in line.Fragments)
        {
            if (offset > fragment.EndOffset) continue;
            var localOffset = Math.Clamp(offset - fragment.StartOffset, 0, fragment.Run.Text.Length);
            return fragment.Bounds.X + MeasureOffset(fragment.Run.Text, fragment.Font, localOffset);
        }
        return line.Bounds.Right;
    }

    internal static float MeasureAdvance(Font font, Rune rune)
    {
        if (GlyphRasterizer.IsAvailable && rune.Value <= char.MaxValue)
        {
            var glyph = GlyphRasterizer.Rasterize(font, (char)rune.Value);
            if (glyph != null) return glyph.AdvanceX;
        }
        return TextLayout.MeasureRuneAdvance(rune, font);
    }

    internal static float MeasureText(string text, Font font)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes()) width += MeasureAdvance(font, rune);
        return width;
    }

    private static float MeasureOffset(string text, Font font, int offset)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Utf16SequenceLength > offset) break;
            width += MeasureAdvance(font, rune);
            offset -= rune.Utf16SequenceLength;
        }
        return width;
    }

    private static int HitTestOffset(string text, Font font, float x)
    {
        if (string.IsNullOrEmpty(text) || x <= 0) return 0;
        var offset = 0;
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var advance = MeasureAdvance(font, rune);
            if (x < width + advance / 2f) break;
            width += advance;
            offset += rune.Utf16SequenceLength;
        }
        return Math.Clamp(offset, 0, text.Length);
    }

    private RichTextLayoutLine FindNearestLine(float y)
    {
        foreach (var line in Lines)
            if (y <= line.Bounds.Bottom) return line;
        return Lines[^1];
    }
}

public static class RichTextLayoutEngine
{
    public static RichTextBlockLayout LayoutBlock(
        RichTextBlock block,
        Font baseFont,
        Point origin,
        float maxWidth,
        float lineHeight)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(baseFont);
        maxWidth = float.IsFinite(maxWidth) && maxWidth > 0 ? maxWidth : float.PositiveInfinity;
        lineHeight = lineHeight > 0 ? lineHeight : baseFont.Size * TextLayout.DefaultLineHeight;

        var lines = new List<RichTextLayoutLine>();
        var fragments = new List<RichTextLayoutFragment>();
        var lineStart = 0;
        var lineOffset = 0;
        var lineX = origin.X;
        var lineY = origin.Y;
        var fragmentStart = 0;
        var fragmentX = lineX;
        RichTextRun? activeRun = null;
        Font? activeFont = null;
        var activeText = new StringBuilder();

        void FlushFragment()
        {
            if (activeRun == null || activeFont == null || activeText.Length == 0) return;
            var text = activeText.ToString();
            var width = RichTextBlockLayout.MeasureText(text, activeFont);
            fragments.Add(new RichTextLayoutFragment(
                new RichTextRun(text, activeRun.Marks),
                fragmentStart,
                fragmentStart + text.Length,
                activeFont,
                new Rect(fragmentX, lineY, width, lineHeight)));
            activeText.Clear();
        }

        void FlushLine()
        {
            FlushFragment();
            lines.Add(new RichTextLayoutLine(
                lineStart,
                lineOffset,
                new Rect(origin.X, lineY, Math.Max(0, lineX - origin.X), lineHeight),
                fragments.ToArray()));
            fragments.Clear();
            lineStart = lineOffset;
            lineX = origin.X;
            lineY += lineHeight;
            activeRun = null;
            activeFont = null;
            activeText.Clear();
        }

        foreach (var inline in block.Inlines)
        {
            if (inline is not RichTextRun run) continue;
            var font = ApplyMarks(baseFont, run.Marks);
            foreach (var rune in run.Text.EnumerateRunes())
            {
                var advance = RichTextBlockLayout.MeasureAdvance(font, rune);
                if (lineX > origin.X && lineX - origin.X + advance > maxWidth)
                    FlushLine();

                if (!ReferenceEquals(activeRun, run) || activeFont?.Weight != font.Weight || activeFont?.Style != font.Style)
                {
                    FlushFragment();
                    activeRun = run;
                    activeFont = font;
                    fragmentStart = lineOffset;
                    fragmentX = lineX;
                }

                activeText.Append(rune.ToString());
                lineX += advance;
                lineOffset += rune.Utf16SequenceLength;
            }
        }

        FlushLine();
        var width = lines.Count == 0 ? 0 : lines.Max(line => line.Bounds.Width);
        var bounds = new Rect(origin.X, origin.Y, width, lines.Count * lineHeight);
        return new RichTextBlockLayout(block, bounds, lines);
    }

    public static Font ApplyMarks(Font baseFont, RichTextMarks marks) => new(
        baseFont.Family,
        baseFont.Size,
        marks.Bold ? FontWeight.Bold : baseFont.Weight,
        marks.Italic ? FontStyle.Italic : baseFont.Style);
}