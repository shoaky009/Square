using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Square.Graphics;

namespace Square.Text.Glyph;

public sealed class RasterizedGlyph
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public required int OffsetX { get; init; }
    public required int OffsetY { get; init; }
    public required int AdvanceX { get; init; }
    public required byte[] Coverage { get; init; }
}

public sealed partial class SystemGlyphRasterizer
{
    private readonly Dictionary<GlyphKey, RasterizedGlyph?> _cache = [];
    private readonly StbGlyphRasterizer _stbRasterizer = new();

    public bool IsAvailable => OperatingSystem.IsWindows() || _stbRasterizer.IsAvailable;

    public RasterizedGlyph? Rasterize(Font font, char character)
    {
        if (!IsAvailable) return null;
        var family = ResolveFontFamily(font.Family, character);
        var effectiveFont = family == font.Family
            ? font
            : new Font(family, font.Size, font.Weight, font.Style);
        var key = new GlyphKey(effectiveFont.Family, effectiveFont.Size, effectiveFont.Weight, effectiveFont.Style, character);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var glyph = OperatingSystem.IsWindows()
            ? RasterizeWin32(effectiveFont, character)
            : _stbRasterizer.Rasterize(effectiveFont, character);
        _cache[key] = glyph;
        return glyph;
    }

    private static string ResolveFontFamily(string requestedFamily, char character)
    {
        if (!string.Equals(requestedFamily, "Segoe UI", StringComparison.OrdinalIgnoreCase))
            return requestedFamily;

        if (character is >= '\u3040' and <= '\u30ff' or >= '\uff66' and <= '\uff9f')
            return "Yu Gothic UI";
        if (character is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff')
            return "Microsoft YaHei UI";
        return requestedFamily;
    }

    private static RasterizedGlyph? RasterizeWin32(Font font, char character)
    {
#if PLATFORM_WIN32
        var dc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return null;

        var fontHandle = NativeMethods.CreateFont(
            -(int)MathF.Round(font.Size), 0, 0, 0, (int)font.Weight,
            font.Style == FontStyle.Italic ? 1u : 0u,
            0, 0, NativeMethods.DefaultCharset, 0, 0,
            NativeMethods.AntialiasedQuality, 0, font.Family);
        if (fontHandle == IntPtr.Zero)
        {
            NativeMethods.DeleteDC(dc);
            return null;
        }

        var oldFont = NativeMethods.SelectObject(dc, fontHandle);
        try
        {
            if (!NativeMethods.GetTextMetrics(dc, out var textMetrics)) return null;

            var transform = NativeMethods.Mat2.Identity;
            var size = NativeMethods.GetGlyphOutline(
                dc, character, NativeMethods.Gray8Bitmap,
                out var metrics, 0, IntPtr.Zero, ref transform);
            if (size == NativeMethods.GdiError) return null;

            var coverage = size == 0 ? [] : new byte[size];
            if (size > 0)
            {
                var handle = GCHandle.Alloc(coverage, GCHandleType.Pinned);
                try
                {
                    var written = NativeMethods.GetGlyphOutline(
                        dc, character, NativeMethods.Gray8Bitmap,
                        out metrics, size, handle.AddrOfPinnedObject(), ref transform);
                    if (written == NativeMethods.GdiError) return null;
                }
                finally
                {
                    handle.Free();
                }

                // GDI GGO_GRAY8_BITMAP coverage is 0..64; normalize to 0..255.
                for (var i = 0; i < coverage.Length; i++)
                    coverage[i] = (byte)Math.Min(255, coverage[i] * 255 / 64);
            }

            var width = (int)metrics.BlackBoxX;
            return new RasterizedGlyph
            {
                Width = width,
                Height = (int)metrics.BlackBoxY,
                Stride = (width + 3) & ~3,
                OffsetX = metrics.GlyphOrigin.X,
                OffsetY = textMetrics.Ascent - metrics.GlyphOrigin.Y,
                AdvanceX = metrics.CellIncrementX > 0
                    ? metrics.CellIncrementX
                    : Math.Max(1, (int)MathF.Round(font.Size * 0.5f)),
                Coverage = coverage
            };
        }
        finally
        {
            if (oldFont != IntPtr.Zero) NativeMethods.SelectObject(dc, oldFont);
            NativeMethods.DeleteObject(fontHandle);
            NativeMethods.DeleteDC(dc);
        }
#else
        return null;
#endif
    }

    private readonly record struct GlyphKey(
        string Family,
        float Size,
        FontWeight Weight,
        FontStyle Style,
        char Character);

#if PLATFORM_WIN32
    private static partial class NativeMethods
    {
        internal const uint DefaultCharset = 1;
        internal const uint AntialiasedQuality = 4;
        internal const uint Gray8Bitmap = 6;
        internal const uint GdiError = 0xFFFFFFFF;

        [LibraryImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
        internal static partial IntPtr CreateCompatibleDC(IntPtr dc);

        [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeleteDC(IntPtr dc);

        [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr CreateFont(
            int height, int width, int escapement, int orientation, int weight,
            uint italic, uint underline, uint strikeOut, uint charSet,
            uint outputPrecision, uint clipPrecision, uint quality,
            uint pitchAndFamily, string faceName);

        [LibraryImport("gdi32.dll", EntryPoint = "SelectObject")]
        internal static partial IntPtr SelectObject(IntPtr dc, IntPtr obj);

        [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeleteObject(IntPtr obj);

        [LibraryImport("gdi32.dll", EntryPoint = "GetTextMetricsW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetTextMetrics(IntPtr dc, out TextMetrics metrics);

        [LibraryImport("gdi32.dll", EntryPoint = "GetGlyphOutlineW")]
        internal static partial uint GetGlyphOutline(
            IntPtr dc, uint character, uint format, out GlyphMetrics metrics,
            uint bufferSize, IntPtr buffer, ref Mat2 transform);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GlyphMetrics
        {
            internal uint BlackBoxX;
            internal uint BlackBoxY;
            internal Point GlyphOrigin;
            internal short CellIncrementX;
            internal short CellIncrementY;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Fixed
        {
            internal ushort Fraction;
            internal short Value;

            internal static Fixed One => new() { Value = 1 };
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Mat2
        {
            internal Fixed M11;
            internal Fixed M12;
            internal Fixed M21;
            internal Fixed M22;

            internal static Mat2 Identity => new() { M11 = Fixed.One, M22 = Fixed.One };
        }

        [StructLayout(LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal struct TextMetrics
        {
            internal int Height;
            internal int Ascent;
            internal int Descent;
            internal int InternalLeading;
            internal int ExternalLeading;
            internal int AverageCharWidth;
            internal int MaxCharWidth;
            internal int Weight;
            internal int Overhang;
            internal int DigitizedAspectX;
            internal int DigitizedAspectY;
            internal ushort FirstChar;
            internal ushort LastChar;
            internal ushort DefaultChar;
            internal ushort BreakChar;
            internal byte Italic;
            internal byte Underlined;
            internal byte StruckOut;
            internal byte PitchAndFamily;
            internal byte CharSet;
        }
    }
#endif
}

internal static class SystemTextMeasurementRegistration
{
    private static readonly SystemGlyphRasterizer Rasterizer = new();
    private static readonly object Sync = new();

#pragma warning disable CA2255 // Square.Text installs the optional font metrics provider for Square.Graphics.
    [ModuleInitializer]
    internal static void Register()
        => TextLayout.RegisterAdvanceProvider(MeasureAdvance);
#pragma warning restore CA2255

    private static float? MeasureAdvance(Rune rune, Font font)
    {
        if (!rune.IsBmp || !Rasterizer.IsAvailable) return null;
        var family = font.Family.ToLowerInvariant() switch
        {
            "sans-serif" or "system-ui" or "ui-sans-serif" => OperatingSystem.IsWindows() ? "Segoe UI" : "DejaVu Sans",
            "serif" or "ui-serif" => OperatingSystem.IsWindows() ? "Times New Roman" : "DejaVu Serif",
            "monospace" or "ui-monospace" => OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono",
            _ => font.Family
        };
        var effectiveFont = family == font.Family
            ? font
            : new Font(family, font.Size, font.Weight, font.Style);
        lock (Sync)
            return Rasterizer.Rasterize(effectiveFont, (char)rune.Value)?.AdvanceX;
    }
}
