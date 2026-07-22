using Xunit;

namespace Square.Images.Tests;

public sealed class IconDecoderTests
{
    private static byte[] Solid32(int width, int height, byte blue, byte green, byte red, byte alpha)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = blue; pixels[offset + 1] = green;
            pixels[offset + 2] = red; pixels[offset + 3] = alpha;
        }
        return pixels;
    }

    private static byte[] EmptyMask(int width, int height) => new byte[((width + 31) / 32 * 4) * height];

    [Fact]
    public void Decodes32BitIcoWithAlpha()
    {
        var xor = new byte[] { 10, 20, 30, 64, 40, 50, 60, 128 };
        var and = new byte[] { 0, 0, 0, 0 };
        var ico = CodecTestData.Ico(2, 1, 32, xor, and);

        using var document = ImageDecoder.Decode(ico);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(2, decoded.Width);
        Assert.Equal(1, decoded.Height);
        Assert.Equal([10, 20, 30, 64, 40, 50, 60, 128], decoded.Pixels);
    }

    [Fact]
    public void Decodes24BitIcoUsingAndMaskAsAlpha()
    {
        var xor = new byte[] { 10, 20, 30, 40, 50, 60, 0, 0, 70, 80, 90, 100, 110, 120, 0, 0 };
        var and = new byte[] { 0b10100000, 0, 0, 0, 0b11000000, 0, 0, 0 };
        var ico = CodecTestData.Ico(2, 2, 24, xor, and);

        using var document = ImageDecoder.Decode(ico);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal([70, 80, 90, 0, 100, 110, 120, 0, 10, 20, 30, 0, 40, 50, 60, 255], decoded.Pixels);
    }

    [Fact]
    public void Decodes8BitIndexedIcoWithPalette()
    {
        var palette = new byte[256 * 4];
        palette[0] = 10; palette[1] = 20; palette[2] = 30;
        palette[4] = 40; palette[5] = 50; palette[6] = 60;
        palette[8] = 70; palette[9] = 80; palette[10] = 90;
        palette[12] = 100; palette[13] = 110; palette[14] = 120;
        var xor = new byte[] { 0, 1, 0, 0, 2, 3, 0, 0 };
        var and = new byte[] { 0b11000000, 0, 0, 0, 0b10100000, 0, 0, 0 };
        var ico = CodecTestData.Ico(2, 2, 8, xor, and, palette);

        using var document = ImageDecoder.Decode(ico);
        var decoded = document.PrimaryBitmap;

        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal([70, 80, 90, 0, 100, 110, 120, 255, 10, 20, 30, 0, 40, 50, 60, 0], decoded.Pixels);
    }

    [Fact]
    public void RejectsTruncatedAndInvalidIco()
    {
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(new byte[] { 0, 0, 1, 0 }));
        var ico = CodecTestData.Ico(1, 1, 32, [1, 2, 3, 4], [0]);
        ico[2] = 3;
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ico));
    }

    [Fact]
    public void DocumentExposesAllIconVariantsAndSelectsLargestPrimary()
    {
        var ico = CodecTestData.IconContainer(1,
        [
            new CodecTestData.IconVariantData(1, 1, 32, Solid32(1, 1, 1, 2, 3, 255), EmptyMask(1, 1)),
            new CodecTestData.IconVariantData(2, 2, 24,
                [10, 20, 30, 10, 20, 30, 0, 0, 10, 20, 30, 10, 20, 30, 0, 0], EmptyMask(2, 2))
        ]);

        using var document = ImageDecoder.Decode(ico);

        Assert.Equal(ImageFormat.Ico, document.Format);
        Assert.Equal(ImageDocumentKind.Variants, document.Kind);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal(1, document.PrimaryIndex);
        Assert.Equal((1, 1, 32), (document.Items[0].Width, document.Items[0].Height, document.Items[0].SourceBitDepth));
        Assert.Equal((2, 2, 24), (document.Items[1].Width, document.Items[1].Height, document.Items[1].SourceBitDepth));
        Assert.Equal((2, 2), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
    }

    [Fact]
    public void DocumentSelectsHigherBitDepthForEqualIconSize()
    {
        var ico = CodecTestData.IconContainer(1,
        [
            new CodecTestData.IconVariantData(1, 1, 24, [1, 2, 3, 0], EmptyMask(1, 1)),
            new CodecTestData.IconVariantData(1, 1, 32, Solid32(1, 1, 4, 5, 6, 7), EmptyMask(1, 1))
        ]);

        using var document = ImageDecoder.Decode(ico);

        Assert.Equal(1, document.PrimaryIndex);
        Assert.Equal(32, document.PrimaryItem.SourceBitDepth);
    }

    [Fact]
    public void DocumentExposesCursorHotspots()
    {
        var cur = CodecTestData.IconContainer(2,
        [
            new CodecTestData.IconVariantData(2, 2, 32, Solid32(2, 2, 1, 2, 3, 255), EmptyMask(2, 2),
                HotspotX: 1, HotspotY: 0)
        ]);

        using var document = ImageDecoder.Decode(cur);

        Assert.Equal(ImageFormat.Cur, document.Format);
        Assert.Equal(new Square.Graphics.Point(1, 0), document.Items[0].Hotspot);
    }

    [Fact]
    public void RejectsCursorHotspotsOutsideBoundsAndDocumentLimits()
    {
        var cur = CodecTestData.IconContainer(2,
        [
            new CodecTestData.IconVariantData(1, 1, 32, Solid32(1, 1, 1, 2, 3, 255), EmptyMask(1, 1),
                HotspotX: 1, HotspotY: 0)
        ]);
        var ico = CodecTestData.IconContainer(1,
        [
            new CodecTestData.IconVariantData(1, 1, 32, Solid32(1, 1, 1, 2, 3, 255), EmptyMask(1, 1)),
            new CodecTestData.IconVariantData(1, 1, 32, Solid32(1, 1, 4, 5, 6, 255), EmptyMask(1, 1))
        ]);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(cur));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ico,
            new ImageDecoderOptions { MaxItemCount = 1 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ico,
            new ImageDecoderOptions { MaxTotalDecodedBytes = 4 }));
    }
}
