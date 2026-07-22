using Xunit;

namespace Square.Images.Tests;

public sealed class GifDecoderTests
{
    private static readonly byte[] Palette = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255];

    [Fact]
    public void DecodesOpaqueGifAndLogicalScreenBackground()
    {
        var gif = CodecTestData.Gif(4, 2, Palette, 1, 1, 2, 1, [1, 2]);

        using var document = ImageDecoder.Decode(gif);
        var decoded = document.PrimaryBitmap;

        Assert.Equal((4, 2), (decoded.Width, decoded.Height));
        Assert.Equal([0, 0, 255, 255], decoded.Pixels.AsSpan(0, 4).ToArray());
        Assert.Equal([0, 255, 0, 255], decoded.GetPixel(1, 1).ToArray());
        Assert.Equal([255, 0, 0, 255], decoded.GetPixel(2, 1).ToArray());
    }

    [Fact]
    public void DecodesTransparentGifToTransparentCanvas()
    {
        var gif = CodecTestData.Gif(2, 1, Palette, 0, 0, 2, 1, [0, 1], transparent: true);

        using var document = ImageDecoder.Decode(gif);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([0, 0, 0, 0, 0, 255, 0, 255], decoded.Pixels);
    }

    [Fact]
    public void LocalPaletteOverridesGlobalPalette()
    {
        var gif = CodecTestData.Gif(1, 1, Palette, 0, 0, 1, 1, [0],
            localPalette: [10, 20, 30, 40, 50, 60]);

        using var document = ImageDecoder.Decode(gif);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([30, 20, 10, 255], decoded.Pixels);
    }

    [Fact]
    public void DecodesInterlacedRowsInTopDownOrder()
    {
        var indices = new byte[] { 0, 0, 0, 0, 2, 2, 2, 2, 1, 1, 1, 1, 3, 3, 3, 3 };
        var gif = CodecTestData.Gif(4, 4, Palette, 0, 0, 4, 4, indices, interlaced: true);

        using var document = ImageDecoder.Decode(gif);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([0, 0, 255, 255], decoded.GetPixel(0, 0).ToArray());
        Assert.Equal([0, 255, 0, 255], decoded.GetPixel(0, 1).ToArray());
        Assert.Equal([255, 0, 0, 255], decoded.GetPixel(0, 2).ToArray());
        Assert.Equal([255, 255, 255, 255], decoded.GetPixel(0, 3).ToArray());
    }

    [Fact]
    public void DecodesExistingMinimalGifFixture()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");

        using var document = ImageDecoder.Decode(gif);
        var decoded = document.PrimaryBitmap;

        Assert.Equal((1, 1), (decoded.Width, decoded.Height));
        Assert.Equal(255, decoded.Pixels[3]);
    }

    [Fact]
    public void RejectsInvalidGifAndLimits()
    {
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode("GIF89a"u8.ToArray()));
        var gif = CodecTestData.Gif(2, 1, Palette, 0, 0, 2, 1, [0, 1]);
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(gif, new ImageDecoderOptions { MaxWidth = 1 }));
        gif[^2] = 1;
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(gif));
    }
}
