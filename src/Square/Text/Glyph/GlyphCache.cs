namespace Square.Text.Glyph;

/// <summary>字形度量缓存项，记录单个码点的步进与承载信息。</summary>
public readonly struct GlyphInfo
{
    /// <summary>Unicode 码点。</summary>
    public readonly int CodePoint;
    /// <summary>水平步进宽度。</summary>
    public readonly float AdvanceWidth;
    /// <summary>垂直步进高度。</summary>
    public readonly float AdvanceHeight;
    /// <summary>左侧承载（字形左边缘到步进原点的距离）。</summary>
    public readonly float LeftBearing;
    /// <summary>顶部承载（字形顶边缘到步进原点的距离）。</summary>
    public readonly float TopBearing;

    /// <summary>初始化字形信息。</summary>
    /// <param name="codePoint">Unicode 码点。</param>
    /// <param name="advanceWidth">水平步进宽度。</param>
    /// <param name="advanceHeight">垂直步进高度。</param>
    /// <param name="leftBearing">左侧承载。</param>
    /// <param name="topBearing">顶部承载。</param>
    public GlyphInfo(int codePoint, float advanceWidth, float advanceHeight, float leftBearing, float topBearing)
    {
        CodePoint = codePoint;
        AdvanceWidth = advanceWidth;
        AdvanceHeight = advanceHeight;
        LeftBearing = leftBearing;
        TopBearing = topBearing;
    }
}

/// <summary>按字体族、字号与码点缓存字形度量信息。</summary>
public sealed class GlyphCache
{
    private readonly Dictionary<(string, int, float), GlyphInfo> _cache = new();

    /// <summary>获取或计算指定字体族、字号与码点的字形信息。</summary>
    /// <param name="family">字体族名称。</param>
    /// <param name="size">字号。</param>
    /// <param name="codePoint">Unicode 码点。</param>
    /// <returns>命中缓存或新计算得到的字形信息。</returns>
    public GlyphInfo GetOrCompute(string family, float size, int codePoint)
    {
        var key = (family, codePoint, size);
        if (_cache.TryGetValue(key, out var info)) return info;

        var charWidth = size * 0.5f;
        info = new GlyphInfo(codePoint, charWidth, size * 1.2f, 0, 0);
        _cache[key] = info;
        return info;
    }

    /// <summary>清空缓存。</summary>
    public void Clear() => _cache.Clear();
}
