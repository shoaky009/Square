namespace Square.Text.Glyph;

public readonly struct GlyphInfo
{
    public readonly int CodePoint;
    public readonly float AdvanceWidth;
    public readonly float AdvanceHeight;
    public readonly float LeftBearing;
    public readonly float TopBearing;

    public GlyphInfo(int codePoint, float advanceWidth, float advanceHeight, float leftBearing, float topBearing)
    {
        CodePoint = codePoint;
        AdvanceWidth = advanceWidth;
        AdvanceHeight = advanceHeight;
        LeftBearing = leftBearing;
        TopBearing = topBearing;
    }
}

public sealed class GlyphCache
{
    private readonly Dictionary<(string, int, float), GlyphInfo> _cache = new();

    public GlyphInfo GetOrCompute(string family, float size, int codePoint)
    {
        var key = (family, codePoint, size);
        if (_cache.TryGetValue(key, out var info)) return info;

        var charWidth = size * 0.5f;
        info = new GlyphInfo(codePoint, charWidth, size * 1.2f, 0, 0);
        _cache[key] = info;
        return info;
    }

    public void Clear() => _cache.Clear();
}