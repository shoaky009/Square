using Square.Graphics;
using Square.Graphics.Codecs;
using Xunit;

namespace Square.Images.Tests;

public sealed class PngDecoderTests
{
    [Fact]
    public void DecodesPngProducedBySquareEncoder()
    {
        using var source = new Bitmap(2, 2);
        new byte[] { 30, 20, 10, 40, 60, 50, 40, 255, 90, 80, 70, 0, 120, 110, 100, 128 }
            .CopyTo(source.Pixels, 0);
        using var encoded = new MemoryStream();
        BitmapPngEncoder.Save(source, encoded);

        using var document = ImageDecoder.Decode(encoded.ToArray());
        var decoded = document.PrimaryBitmap;

        Assert.Equal(source.Pixels, decoded.Pixels);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DecodesAllPngFilters(int filter)
    {
        var first = new byte[] { 10, 20, 30, 255, 40, 50, 60, 128 };
        var second = new byte[] { 70, 80, 90, 64, 100, 110, 120, 32 };
        var raw = new byte[18];
        raw[0] = 0; first.CopyTo(raw, 1);
        raw[9] = (byte)filter;
        EncodeFiltered(second, first, raw.AsSpan(10), 4, filter);

        using var document = ImageDecoder.Decode(CodecTestData.Png(2, 2, 8, 6, 0, raw));
        var decoded = document.PrimaryBitmap;

        Assert.Equal([30, 20, 10, 255, 60, 50, 40, 128, 90, 80, 70, 64, 120, 110, 100, 32], decoded.Pixels);
    }

    [Fact]
    public void DecodesIndexedTransparencyAndPackedSamples()
    {
        var png = CodecTestData.Png(4, 1, 2, 3, 0, [0, 0b00011011],
            [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255], [0, 64, 128, 255]);

        using var document = ImageDecoder.Decode(png);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([0, 0, 255, 0, 0, 255, 0, 64, 255, 0, 0, 128, 255, 255, 255, 255], decoded.Pixels);
    }

    [Fact]
    public void DecodesAdam7Rgba()
    {
        var pixels = Enumerable.Range(0, 64).Select(i => new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2), 255 }).ToArray();
        var raw = new List<byte>();
        (int X, int Y, int Dx, int Dy)[] passes = [(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)];
        foreach (var pass in passes)
            for (var y = pass.Y; y < 8; y += pass.Dy)
            {
                raw.Add(0);
                for (var x = pass.X; x < 8; x += pass.Dx) raw.AddRange(pixels[y * 8 + x]);
            }

        using var document = ImageDecoder.Decode(CodecTestData.Png(8, 8, 8, 6, 1, raw.ToArray()));
        var decoded = document.PrimaryBitmap;

        for (var i = 0; i < 64; i++)
            Assert.Equal(new byte[] { (byte)(i + 2), (byte)(i + 1), (byte)i, 255 }, decoded.Pixels.AsSpan(i * 4, 4).ToArray());
    }

    [Fact]
    public void RejectsCrcErrorsAndConfiguredLimits()
    {
        var png = CodecTestData.Png(1, 1, 8, 6, 0, [0, 1, 2, 3, 4]);
        png[29] ^= 1;
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(png));
        var valid = CodecTestData.Png(2, 1, 8, 6, 0, [0, 1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(valid, new ImageDecoderOptions { MaxWidth = 1 }));
    }

    private static void EncodeFiltered(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> output, int bpp, int filter)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : 0;
            var above = previous[i];
            var upperLeft = i >= bpp ? previous[i - bpp] : 0;
            var predictor = filter switch
            {
                0 => 0, 1 => left, 2 => above, 3 => (left + above) / 2, 4 => Paeth(left, above, upperLeft), _ => 0
            };
            output[i] = unchecked((byte)(row[i] - predictor));
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c; var pa = Math.Abs(p - a); var pb = Math.Abs(p - b); var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
