using System.Runtime.InteropServices;
using System.Text;
using Square.Graphics;
using StbTrueTypeSharp;

namespace Square.Text.Glyph;

internal sealed class StbGlyphRasterizer
{
    private readonly Dictionary<GlyphKey, RasterizedGlyph?> _cache = [];
    private readonly FontCollection _fonts = new();

    public bool IsAvailable => _fonts.HasAnyFont;

    public RasterizedGlyph? Rasterize(Font font, char character)
    {
        if (!IsAvailable) return null;
        var entry = _fonts.Resolve(font.Family, character);
        if (entry == null) return null;

        var effectiveFont = entry.Family == font.Family
            ? font
            : new Font(entry.Family, font.Size, font.Weight, font.Style);
        var key = new GlyphKey(effectiveFont.Family, effectiveFont.Size, effectiveFont.Weight, effectiveFont.Style, character);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var glyph = RasterizeStb(entry, effectiveFont, character);
        _cache[key] = glyph;
        return glyph;
    }

    private static unsafe RasterizedGlyph? RasterizeStb(FontEntry entry, Font font, char character)
    {
        var info = entry.AcquireFontInfo();
        if (info == null) return null;

        // GDI CreateFont(-size) uses character height; ScaleForPixelHeight matches that
        // for most fonts, but UI fonts can look slightly smaller than Segoe UI optically.
        // A modest boost keeps Linux text closer to Windows without changing layout CSS.
        var pixelHeight = font.Size * 1.12f;
        var scale = StbTrueType.stbtt_ScaleForPixelHeight(info, pixelHeight);
        if (scale <= 0) return null;

        var codepoint = (int)character;
        var glyphIndex = StbTrueType.stbtt_FindGlyphIndex(info, codepoint);
        if (glyphIndex == 0) return null;

        int advanceWidth, leftSideBearing;
        StbTrueType.stbtt_GetCodepointHMetrics(info, codepoint, &advanceWidth, &leftSideBearing);

        int width, height, xoff, yoff;
        byte* bitmap = StbTrueType.stbtt_GetCodepointBitmap(info, scale, scale, codepoint, &width, &height, &xoff, &yoff);
        try
        {
            if (bitmap == null || width <= 0 || height <= 0)
            {
                return new RasterizedGlyph
                {
                    Width = 0,
                    Height = 0,
                    Stride = 0,
                    OffsetX = xoff,
                    OffsetY = yoff,
                    AdvanceX = (int)MathF.Round(advanceWidth * scale),
                    Coverage = []
                };
            }

            var stride = (width + 3) & ~3;
            var coverage = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy((IntPtr)(bitmap + y * width), coverage, y * stride, width);
            }

            int ascent, descent, lineGap;
            StbTrueType.stbtt_GetFontVMetrics(info, &ascent, &descent, &lineGap);
            return new RasterizedGlyph
            {
                Width = width,
                Height = height,
                Stride = stride,
                OffsetX = xoff,
                OffsetY = (int)MathF.Round(ascent * scale) + yoff,
                AdvanceX = (int)MathF.Round(advanceWidth * scale),
                Coverage = coverage
            };
        }
        finally
        {
            if (bitmap != null) StbTrueType.stbtt_FreeBitmap(bitmap, null);
        }
    }

    private readonly record struct GlyphKey(
        string Family,
        float Size,
        FontWeight Weight,
        FontStyle Style,
        char Character);
}

internal sealed class FontEntry
{
    private readonly byte[] _data;
    private readonly int _offset;
    private StbTrueType.stbtt_fontinfo? _info;

    public string Family { get; }

    public FontEntry(string family, byte[] data, int offset = 0)
    {
        Family = family;
        _data = data;
        _offset = offset;
    }

    public StbTrueType.stbtt_fontinfo? AcquireFontInfo()
    {
        if (_info != null) return _info;
        _info = StbTrueType.CreateFont(_data, _offset);
        return _info;
    }
}

internal sealed class FontCollection
{
    private readonly Dictionary<string, FontEntry> _byFamily = new(NormalizedComparer.Instance);
    private readonly List<FontEntry> _fallbacks = [];
    private readonly Dictionary<char, string> _scriptFallbacks = [];

    public bool HasAnyFont { get; private set; }

    public FontCollection()
    {
        try
        {
            LoadSystemFonts();
        }
        catch
        {
        }
        ConfigureScriptFallbacks();
    }

    public FontEntry? Resolve(string requestedFamily, char character)
    {
        if (_byFamily.Count == 0) return null;

        var normRequested = Normalize(requestedFamily);
        if (_scriptFallbacks.TryGetValue(character, out var scriptFamily)
            && _byFamily.TryGetValue(Normalize(scriptFamily), out var scriptEntry))
            return scriptEntry;

        if (!string.IsNullOrEmpty(requestedFamily) && _byFamily.TryGetValue(normRequested, out var entry))
            return entry;

        foreach (var fb in _fallbacks)
        {
            if (Normalize(fb.Family) != normRequested) return fb;
        }
        return _fallbacks.Count > 0 ? _fallbacks[0] : null;
    }

    private void ConfigureScriptFallbacks()
    {
        if (_byFamily.Count == 0) return;

        string Pick(params string[] candidates)
        {
            foreach (var c in candidates)
            {
                var n = Normalize(c);
                if (_byFamily.ContainsKey(n)) return c;
            }
            return _fallbacks.Count > 0 ? _fallbacks[0].Family : _byFamily.First().Key;
        }

        var cjk = Pick("NotoSansCJK", "NotoSansCJKsc", "NotoSansCJKtc", "NotoSansCJKjp",
                       "SourceHanSansSC", "SourceHanSansCN", "WenQuanYiZenHei",
                       "DroidSansFallback", "MicrosoftYaHeiUI", "YuGothicUI");
        var japanese = Pick("NotoSansCJKjp", "YuGothicUI", "YuGothic", cjk);
        var korean = Pick("NotoSansCJKkr", "MalgunGothic", cjk);

        foreach (var c in EnumerateRange('\u3040', '\u30ff')) _scriptFallbacks.TryAdd(c, Normalize(japanese));
        foreach (var c in EnumerateRange('\uff66', '\uff9f')) _scriptFallbacks.TryAdd(c, Normalize(japanese));
        foreach (var c in EnumerateRange('\u3400', '\u4dbf')) _scriptFallbacks.TryAdd(c, Normalize(cjk));
        foreach (var c in EnumerateRange('\u4e00', '\u9fff')) _scriptFallbacks.TryAdd(c, Normalize(cjk));
        foreach (var c in EnumerateRange('\uac00', '\ud7af')) _scriptFallbacks.TryAdd(c, Normalize(korean));
    }

    private static IEnumerable<char> EnumerateRange(char lo, char hi)
    {
        for (var c = lo; c <= hi; c++) yield return c;
    }

    private void LoadSystemFonts()
    {
        var roots = GetPlatformFontRoots();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    continue;

                var nameKey = Path.GetFileNameWithoutExtension(file);
                if (!seen.Add(nameKey)) continue;

                var family = GuessFamilyFromName(nameKey);
                if (family == null) continue;

                var normFamily = Normalize(family);
                if (_byFamily.ContainsKey(normFamily)) continue;

                try
                {
                    var data = File.ReadAllBytes(file);
                    int offset = 0;
                    if (file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                    {
                        offset = GetTtcOffset(data, 0);
                        if (offset < 0) continue;
                    }
                    var entry = new FontEntry(family, data, offset);
                    _byFamily[normFamily] = entry;
                    _fallbacks.Add(entry);
                    HasAnyFont = true;
                }
                catch
                {
                }
            }
        }

        RegisterAliases();
    }

    private void RegisterAliases()
    {
        void Alias(string alias, string target)
        {
            var normTarget = Normalize(target);
            if (_byFamily.TryGetValue(normTarget, out var entry))
            {
                var normAlias = Normalize(alias);
                if (!_byFamily.ContainsKey(normAlias))
                    _byFamily[normAlias] = entry;
            }
        }

        string FirstAvailable(params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (_byFamily.ContainsKey(Normalize(c))) return c;
            }
            return _fallbacks.Count > 0 ? _fallbacks[0].Family : candidates[0];
        }

        if (_byFamily.Count == 0) return;

        if (OperatingSystem.IsWindows())
        {
            Alias("sans-serif", "Segoe UI");
            Alias("serif", "Times New Roman");
            Alias("monospace", "Consolas");
            Alias("Arial", "Segoe UI");
        }
        else
        {
            // Prefer modern UI fonts with metrics closer to Segoe UI when available.
            var sans = FirstAvailable("Ubuntu", "UbuntuSans", "NotoSans", "DejaVuSans");
            var serif = FirstAvailable("NotoSerif", "DejaVuSerif");
            var mono = FirstAvailable("UbuntuMono", "UbuntuSansMono", "NotoSansMono", "DejaVuSansMono");
            Alias("Segoe UI", sans);
            Alias("sans-serif", sans);
            Alias("Arial", sans);
            Alias("serif", serif);
            Alias("Times New Roman", serif);
            Alias("monospace", mono);
            Alias("Consolas", mono);
        }
    }

    private static unsafe int GetTtcOffset(byte[] data, int index)
    {
        fixed (byte* p = data)
            return StbTrueType.stbtt_GetFontOffsetForIndex(p, index);
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (!char.IsWhiteSpace(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private sealed class NormalizedComparer : IEqualityComparer<string>
    {
        public static readonly NormalizedComparer Instance = new();
        public bool Equals(string? x, string? y) => Normalize(x ?? "") == Normalize(y ?? "");
        public int GetHashCode(string obj) => Normalize(obj ?? "").GetHashCode();
    }

    private static string? GuessFamilyFromName(string fileName)
    {
        var name = fileName.Replace('-', ' ').Replace('_', ' ');
        var cleaned = string.Join(' ', name.Split(' ')
            .Where(t => !t.Equals("Regular", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("Book", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("Normal", StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static IEnumerable<string> GetPlatformFontRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return [Path.Combine(winDir, "Fonts")];
        }

        if (OperatingSystem.IsLinux())
        {
            return
            [
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "fonts")
            ];
        }

        if (OperatingSystem.IsMacOS())
        {
            return
            [
                "/System/Library/Fonts",
                "/Library/Fonts",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts")
            ];
        }

        return [];
    }
}