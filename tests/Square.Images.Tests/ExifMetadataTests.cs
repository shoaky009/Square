using System.Buffers.Binary;
using Xunit;

namespace Square.Images.Tests;

public sealed class ExifMetadataTests
{
    public static TheoryData<int, int, int, byte[]> Orientations => new()
    {
        { 1, 16, 16, [129, 130, 131, 132] },
        { 2, 16, 16, [130, 129, 132, 131] },
        { 3, 16, 16, [132, 131, 130, 129] },
        { 4, 16, 16, [131, 132, 129, 130] },
        { 5, 16, 16, [129, 131, 130, 132] },
        { 6, 16, 16, [131, 129, 132, 130] },
        { 7, 16, 16, [132, 130, 131, 129] },
        { 8, 16, 16, [130, 132, 129, 131] }
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public void DocumentAppliesAllExifOrientations(int orientation, int width, int height, byte[] corners)
    {
        var jpeg = OrientedJpeg(orientation, littleEndian: true);

        using var document = ImageDecoder.Decode(jpeg);
        var bitmap = document.GetBitmap(0);

        Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
        Assert.Equal(corners[0], bitmap.GetPixel(0, 0)[0]);
        Assert.Equal(corners[1], bitmap.GetPixel(width - 1, 0)[0]);
        Assert.Equal(corners[2], bitmap.GetPixel(0, height - 1)[0]);
        Assert.Equal(corners[3], bitmap.GetPixel(width - 1, height - 1)[0]);
        Assert.Equal((ImageOrientation)orientation, document.Metadata.OriginalOrientation);
        Assert.Equal(orientation != 1, document.Metadata.OrientationApplied);
    }

    [Fact]
    public void ReadsBigEndianExifOrientation()
    {
        var jpeg = OrientedJpeg(3, littleEndian: false);

        using var document = ImageDecoder.Decode(jpeg);

        Assert.Equal(ImageOrientation.Rotate180, document.Metadata.OriginalOrientation);
        Assert.Equal([132, 131, 130, 129], Corners(document.GetBitmap(0)));
    }

    [Fact]
    public void DecoderAppliesOrientationByDefault()
    {
        var jpeg = OrientedJpeg(6, littleEndian: true);

        using var document = ImageDecoder.Decode(jpeg);

        Assert.Equal([131, 129, 132, 130], Corners(document.PrimaryBitmap));
    }

    [Fact]
    public void DocumentCanExplicitlyIgnoreOrientation()
    {
        var jpeg = OrientedJpeg(6, littleEndian: true);

        using var document = ImageDecoder.Decode(jpeg,
            new ImageDecoderOptions { ExifOrientationPolicy = ExifOrientationPolicy.Ignore });

        Assert.Equal([129, 130, 131, 132], Corners(document.GetBitmap(0)));
        Assert.Equal(ImageOrientation.Rotate90, document.Metadata.OriginalOrientation);
        Assert.False(document.Metadata.OrientationApplied);
    }

    [Fact]
    public void DocumentExplicitApplyDoesNotApplyOrientationTwice()
    {
        var jpeg = OrientedJpeg(6, littleEndian: true);

        using var document = ImageDecoder.Decode(jpeg,
            new ImageDecoderOptions { ExifOrientationPolicy = ExifOrientationPolicy.Apply });

        Assert.Equal([131, 129, 132, 130], Corners(document.GetBitmap(0)));
        Assert.True(document.Metadata.OrientationApplied);
    }

    [Fact]
    public void RejectsCyclicAndExcessiveExifIfds()
    {
        var cycle = CodecTestData.ExifTiff(1, littleEndian: true);
        BinaryPrimitives.WriteUInt16LittleEndian(cycle.AsSpan(8, 2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(cycle.AsSpan(10, 4), 8);
        var cyclicJpeg = CodecTestData.JpegWithExif(8, 8, 1, [new[] { 0 }], cycle);
        var tags = new byte[38];
        tags[0] = tags[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tags.AsSpan(2, 2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tags.AsSpan(4, 4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tags.AsSpan(8, 2), 2);
        WriteOrientationEntry(tags.AsSpan(10, 12), 1);
        WriteOrientationEntry(tags.AsSpan(22, 12), 1);
        var taggedJpeg = CodecTestData.JpegWithExif(8, 8, 1, [new[] { 0 }], tags);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(cyclicJpeg));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(taggedJpeg,
            new ImageDecoderOptions { MaxExifTagCount = 1 }));
    }

    private static byte[] OrientedJpeg(int orientation, bool littleEndian)
        => CodecTestData.JpegWithExifOrientation(16, 16, 1, [new[] { 8, 16, 24, 32 }], orientation, littleEndian);

    private static byte[] Corners(Square.Graphics.Bitmap bitmap)
        => [bitmap.GetPixel(0, 0)[0], bitmap.GetPixel(bitmap.Width - 1, 0)[0],
            bitmap.GetPixel(0, bitmap.Height - 1)[0], bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1)[0]];

    private static void WriteOrientationEntry(Span<byte> entry, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(entry[0..2], 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..4], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..8], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[8..10], value);
    }
}
