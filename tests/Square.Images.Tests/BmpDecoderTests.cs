using Xunit;

namespace Square.Images.Tests;

public sealed class BmpDecoderTests
{
    [Fact]
    public void DecodesTopDown24BitBmpWithPadding()
    {
        var bmp = CodecTestData.Bmp(2, -1, 24, [30, 20, 10, 60, 50, 40, 0, 0]);

        using var document = ImageDecoder.Decode(bmp);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([30, 20, 10, 255, 60, 50, 40, 255], decoded.Pixels);
    }

    [Fact]
    public void DecodesBottomUpAndPreserves32BitAlpha()
    {
        var bmp = CodecTestData.Bmp(1, 2, 32, [3, 2, 1, 4, 30, 20, 10, 40]);

        using var document = ImageDecoder.Decode(bmp);
        var decoded = document.PrimaryBitmap;

        Assert.Equal([30, 20, 10, 40, 3, 2, 1, 4], decoded.Pixels);
    }

    [Fact]
    public void RejectsTruncatedAndUnsupportedBmp()
    {
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(new byte[] { (byte)'B', (byte)'M' }));
        var bmp = CodecTestData.Bmp(1, 1, 24, [1, 2, 3, 0]);
        bmp[28] = 8;
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(bmp));
    }
}
