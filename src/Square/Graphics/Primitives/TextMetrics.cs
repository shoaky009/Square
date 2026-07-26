using System.Globalization;
using System.Text;

namespace Square.Graphics;

public readonly record struct FontMetrics(float Top, float Ascent, float Descent, float Bottom, float Leading)
{
    public float Height => Math.Max(0, Bottom - Top);
}

public readonly record struct GlyphMetrics(float AdvanceX, Rect InkBounds);

public interface ITextMetricsProvider
{
    bool TryGetFontMetrics(Font font, out FontMetrics metrics);
    bool TryGetGlyphMetrics(Font font, Rune rune, out GlyphMetrics metrics);
}

public static class TextMetrics
{
    private static ITextMetricsProvider? _provider;

    public static void RegisterProvider(ITextMetricsProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public static FontMetrics GetFontMetrics(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (_provider?.TryGetFontMetrics(font, out var metrics) == true && IsValid(metrics))
            return metrics;

        var height = Math.Max(1, font.Size * TextLayout.DefaultLineHeight);
        var ascent = font.Size * 0.8f;
        return new FontMetrics(-ascent, -ascent, height - ascent, height - ascent, 0);
    }

    public static GlyphMetrics GetGlyphMetrics(Font font, Rune rune)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (IsZeroAdvanceCategory(rune)) return new GlyphMetrics(0, Rect.Empty);
        if (_provider?.TryGetGlyphMetrics(font, rune, out var metrics) == true && IsValid(metrics))
            return metrics;

        var advance = TextLayout.MeasureRuneAdvanceFallback(rune, font);
        var fontMetrics = GetFontMetrics(font);
        return new GlyphMetrics(advance, new Rect(0, fontMetrics.Top, advance, fontMetrics.Height));
    }

    public static float GetLineHeight(Font font, float lineHeightMultiplier)
        => Math.Max(1, font.Size * lineHeightMultiplier);

    public static float GetBaselineOffset(Font font, float lineHeight)
    {
        var metrics = GetFontMetrics(font);
        return (lineHeight - metrics.Height) / 2f - metrics.Top;
    }

    public static Rect GetGlyphBoundsInLine(Font font, Rune rune, float lineHeight)
    {
        var glyph = GetGlyphMetrics(font, rune);
        if (glyph.InkBounds.IsEmpty) return Rect.Empty;
        return glyph.InkBounds.Offset(0, GetBaselineOffset(font, lineHeight));
    }

    public static Rect MeasureInkBounds(TextLayout layout, Point origin)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (string.IsNullOrEmpty(layout.Text)) return Rect.Empty;

        var lineHeight = GetLineHeight(layout.Font, layout.LineHeight);
        var lines = TextWrapping.Wrap(layout.Text, layout.MaxSize.Width,
            (_, rune) => GetGlyphMetrics(layout.Font, rune).AdvanceX);
        var result = Rect.Empty;
        var hasBounds = false;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var x = origin.X;
            var lineTop = origin.Y + lineIndex * lineHeight;
            for (var offset = line.StartOffset; offset < line.EndOffset;)
            {
                var status = Rune.DecodeFromUtf16(layout.Text.AsSpan(offset), out var rune, out var consumed);
                if (status != System.Buffers.OperationStatus.Done) break;
                var glyph = GetGlyphMetrics(layout.Font, rune);
                var ink = GetGlyphBoundsInLine(layout.Font, rune, lineHeight).Offset(x, lineTop);
                if (!ink.IsEmpty)
                {
                    result = hasBounds ? Rect.Union(result, ink) : ink;
                    hasBounds = true;
                }
                x += glyph.AdvanceX;
                offset += consumed;
            }
        }

        var layoutBounds = new Rect(origin, layout.Measure());
        return hasBounds ? Rect.Union(layoutBounds, result) : layoutBounds;
    }

    internal static bool IsZeroAdvanceCategory(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format;
    }

    private static bool IsValid(FontMetrics metrics)
        => float.IsFinite(metrics.Top) && float.IsFinite(metrics.Ascent) &&
           float.IsFinite(metrics.Descent) && float.IsFinite(metrics.Bottom) &&
           float.IsFinite(metrics.Leading) && metrics.Bottom >= metrics.Top;

    private static bool IsValid(GlyphMetrics metrics)
        => float.IsFinite(metrics.AdvanceX) && metrics.AdvanceX >= 0 &&
           float.IsFinite(metrics.InkBounds.X) && float.IsFinite(metrics.InkBounds.Y) &&
           float.IsFinite(metrics.InkBounds.Width) && float.IsFinite(metrics.InkBounds.Height);
}
