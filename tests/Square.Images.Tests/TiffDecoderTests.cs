using Xunit;

namespace Square.Images.Tests;

public sealed class TiffDecoderTests
{
    [Fact]
    public void DecodesLittleEndianMultiPageGrayscaleAndRgbStrips()
    {
        var tiff = CodecTestData.Tiff(
        [
            new CodecTestData.TiffPageData(2, 2, 1, 1, 8, [10, 20, 30, 40], RowsPerStrip: 1),
            new CodecTestData.TiffPageData(2, 1, 2, 3, 8, [255, 0, 0, 0, 255, 0])
        ]);

        using var document = ImageDecoder.Decode(tiff);

        Assert.Equal(ImageFormat.Tiff, document.Format);
        Assert.Equal(ImageDocumentKind.Pages, document.Kind);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal([10, 10, 10, 255, 20, 20, 20, 255, 30, 30, 30, 255, 40, 40, 40, 255],
            document.GetBitmap(0).Pixels);
        Assert.Equal([0, 0, 255, 255, 0, 255, 0, 255], document.GetBitmap(1).Pixels);
        Assert.Equal((8, 24), (document.Items[0].SourceBitDepth, document.Items[1].SourceBitDepth));
    }

    [Fact]
    public void DecodesBigEndianPaletteAndOneBitGrayscale()
    {
        var map = new ushort[6];
        map[0] = 0; map[1] = ushort.MaxValue;
        map[2] = 0; map[3] = 0;
        map[4] = 0; map[5] = 0;
        var tiff = CodecTestData.Tiff(
        [
            new CodecTestData.TiffPageData(2, 1, 3, 1, 1, [0b01000000], ColorMap: map),
            new CodecTestData.TiffPageData(2, 1, 0, 1, 1, [0b01000000])
        ], littleEndian: false);

        using var document = ImageDecoder.Decode(tiff);

        Assert.Equal([0, 0, 0, 255, 0, 0, 255, 255], document.GetBitmap(0).Pixels);
        Assert.Equal([255, 255, 255, 255, 0, 0, 0, 255], document.GetBitmap(1).Pixels);
    }

    [Fact]
    public void DecodesRgbaExtraSamplesAndPageOrientation()
    {
        var tiff = CodecTestData.Tiff(
        [
            new CodecTestData.TiffPageData(2, 1, 2, 4, 8,
                [100, 50, 25, 128, 10, 20, 30, 255], Orientation: 2, ExtraSample: 2)
        ]);

        using var document = ImageDecoder.Decode(tiff);

        Assert.Equal([30, 20, 10, 255, 25, 50, 100, 128], document.PrimaryBitmap.Pixels);
        Assert.Equal(ImageOrientation.MirrorHorizontal, document.PrimaryItem.Metadata.OriginalOrientation);
        Assert.True(document.PrimaryItem.Metadata.OrientationApplied);
        Assert.Equal(document.PrimaryItem.Metadata.OriginalOrientation, document.Metadata.OriginalOrientation);
    }

    [Fact]
    public void CanIgnoreTiffPageOrientation()
    {
        var tiff = CodecTestData.Tiff(
        [
            new CodecTestData.TiffPageData(2, 1, 1, 1, 8, [10, 20], Orientation: 2)
        ]);

        using var document = ImageDecoder.Decode(tiff,
            new ImageDecoderOptions { ExifOrientationPolicy = ExifOrientationPolicy.Ignore });

        Assert.Equal([10, 10, 10, 255, 20, 20, 20, 255], document.PrimaryBitmap.Pixels);
        Assert.False(document.PrimaryItem.Metadata.OrientationApplied);
    }

    [Fact]
    public void RejectsUnsupportedCompressionCyclesAndLimits()
    {
        var tiff = CodecTestData.Tiff(
        [
            new CodecTestData.TiffPageData(1, 1, 1, 1, 8, [10]),
            new CodecTestData.TiffPageData(1, 1, 1, 1, 8, [20])
        ]);
        var compressed = (byte[])tiff.Clone();
        compressed[8 + 2 + 3 * 12 + 8] = 5;
        var cyclic = (byte[])tiff.Clone();
        var firstEntryCount = BitConverter.ToUInt16(cyclic, 8);
        var nextOffset = 8 + 2 + firstEntryCount * 12;
        BitConverter.GetBytes((uint)8).CopyTo(cyclic, nextOffset);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(compressed));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(cyclic));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(tiff,
            new ImageDecoderOptions { MaxItemCount = 1 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(tiff,
            new ImageDecoderOptions { MaxTotalDecodedBytes = 4 }));
    }
}
