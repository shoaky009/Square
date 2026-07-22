using Xunit;

namespace Square.Images.Tests;

public sealed class JpegDecoderTests
{
    [Fact]
    public void DecodesGrayscaleJpegWithZeroDc()
    {
        var jpeg = CodecTestData.Jpeg(8, 8, 1, [new[] { 0 }]);

        using var document = ImageDecoder.Decode(jpeg);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        for (var i = 0; i < decoded.Pixels.Length; i += 4)
        {
            Assert.Equal(128, decoded.Pixels[i]);
            Assert.Equal(128, decoded.Pixels[i + 1]);
            Assert.Equal(128, decoded.Pixels[i + 2]);
            Assert.Equal(255, decoded.Pixels[i + 3]);
        }
    }

    [Fact]
    public void DecodesYcbcrJpegWithZeroDc()
    {
        var jpeg = CodecTestData.Jpeg(8, 8, 3, [new[] { 0 }, new[] { 0 }, new[] { 0 }]);

        using var document = ImageDecoder.Decode(jpeg);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        for (var i = 0; i < decoded.Pixels.Length; i += 4)
        {
            Assert.Equal(128, decoded.Pixels[i]);
            Assert.Equal(128, decoded.Pixels[i + 1]);
            Assert.Equal(128, decoded.Pixels[i + 2]);
            Assert.Equal(255, decoded.Pixels[i + 3]);
        }
    }

    [Fact]
    public void DecodesYcbcrJpegAndConvertsToRgb()
    {
        var jpeg = CodecTestData.Jpeg(8, 8, 3, [new[] { 64 }, new[] { 0 }, new[] { 0 }]);

        using var document = ImageDecoder.Decode(jpeg);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        for (var i = 0; i < decoded.Pixels.Length; i += 4)
        {
            Assert.Equal(255, decoded.Pixels[i + 3]);
        }
    }

    [Fact]
    public void DecodesGrayscaleJpegWithNonZeroDc()
    {
        var jpeg = CodecTestData.Jpeg(8, 8, 1, [new[] { 8 }]);

        using var document = ImageDecoder.Decode(jpeg);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        for (var i = 0; i < decoded.Pixels.Length; i += 4)
        {
            Assert.Equal(129, decoded.Pixels[i]);
            Assert.Equal(129, decoded.Pixels[i + 1]);
            Assert.Equal(129, decoded.Pixels[i + 2]);
            Assert.Equal(255, decoded.Pixels[i + 3]);
        }
    }

    [Fact]
    public void DecodesGrayscaleJpegWithMultipleMcus()
    {
        var jpeg = CodecTestData.Jpeg(16, 16, 1, [new[] { 8, 16, 24, 32 }]);

        using var document = ImageDecoder.Decode(jpeg);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(16, decoded.Width);
        Assert.Equal(16, decoded.Height);
        var expected = new[] { 129, 130, 131, 132 };
        var block = 0;
        for (var by = 0; by < 2; by++)
            for (var bx = 0; bx < 2; bx++)
            {
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                    {
                        var px = bx * 8 + x;
                        var py = by * 8 + y;
                        var idx = (py * 16 + px) * 4;
                        Assert.Equal(expected[block], decoded.Pixels[idx]);
                    }
                block++;
            }
    }

    [Fact]
    public void RejectsTruncatedAndUnsupportedJpeg()
    {
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(new byte[] { 0xFF, 0xD8 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0, 4, 8, 0 }));
    }
}
