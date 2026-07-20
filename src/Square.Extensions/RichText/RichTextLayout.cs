using System.Text;
using Square.Graphics;

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
            var localOffset = new TextLayout(fragment.Run.Text, fragment.Font).HitTestOffset(localX);
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
            var x = fragment.Bounds.X + new TextLayout(fragment.Run.Text, fragment.Font).MeasureOffset(localOffset);
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
            return fragment.Bounds.X + new TextLayout(fragment.Run.Text, fragment.Font).MeasureOffset(localOffset);
        }
        return line.Bounds.Right;
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
            var width = new TextLayout(text, activeFont).Measure().Width;
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
                var advance = TextLayout.MeasureRuneAdvance(rune, font.Size);
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