using Xunit;

namespace Square.Images.Tests;

public sealed class ImageDocumentTests
{
    private static readonly byte[] Palette = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255];

    [Fact]
    public void DecodesGifFramesTimingLoopingAndComposition()
    {
        var gif = CodecTestData.GifAnimation(2, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 2, 1, [0, 0], Delay: 5),
            new CodecTestData.GifFrameData(1, 0, 1, 1, [1], Delay: 12)
        ], repeatCount: 2);

        using var document = ImageDecoder.Decode(gif);

        Assert.Equal(ImageFormat.Gif, document.Format);
        Assert.Equal(ImageDocumentKind.Animation, document.Kind);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(50), document.Items[0].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), document.Items[1].Duration);
        Assert.Equal(32, document.Items[0].BitDepth);
        Assert.NotNull(document.Animation);
        Assert.False(document.Animation.LoopsForever);
        Assert.Equal(3, document.Animation.PlayCount);
        Assert.Equal(TimeSpan.FromMilliseconds(170), document.Animation.TotalDuration);
        Assert.Equal([0, 0, 255, 255, 0, 0, 255, 255], document.GetBitmap(0).Pixels);
        Assert.Equal([0, 0, 255, 255, 0, 255, 0, 255], document.GetBitmap(1).Pixels);
    }

    [Fact]
    public void WrapsStaticImagesAndIconVariants()
    {
        var png = CodecTestData.Png(1, 1, 8, 6, 0, [0, 1, 2, 3, 4]);
        using var pngDocument = ImageDecoder.Decode(png);
        var ico = CodecTestData.Ico(1, 1, 32, [1, 2, 3, 4], [0, 0, 0, 0]);
        using var icoDocument = ImageDecoder.Decode(ico);

        Assert.Equal(ImageFormat.Png, pngDocument.Format);
        Assert.Equal(ImageDocumentKind.Still, pngDocument.Kind);
        Assert.Single(pngDocument.Items);
        Assert.Equal(32, pngDocument.Items[0].BitDepth);
        Assert.Same(pngDocument.GetBitmap(pngDocument.PrimaryIndex), pngDocument.PrimaryBitmap);
        Assert.Equal(ImageFormat.Ico, icoDocument.Format);
        Assert.Equal(ImageDocumentKind.Variants, icoDocument.Kind);
    }

    [Fact]
    public void AppliesGifRestoreBackgroundDisposal()
    {
        var gif = CodecTestData.GifAnimation(2, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 2, 1, [1, 1]),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [2], Disposal: 2),
            new CodecTestData.GifFrameData(1, 0, 1, 1, [3])
        ]);

        using var document = ImageDecoder.Decode(gif);

        Assert.Equal([255, 0, 0, 255, 0, 255, 0, 255], document.GetBitmap(1).Pixels);
        Assert.Equal([0, 0, 255, 255, 255, 255, 255, 255], document.GetBitmap(2).Pixels);
    }

    [Fact]
    public void AppliesGifRestorePreviousDisposal()
    {
        var gif = CodecTestData.GifAnimation(2, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 2, 1, [1, 1]),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [2], Disposal: 3),
            new CodecTestData.GifFrameData(1, 0, 1, 1, [3])
        ]);

        using var document = ImageDecoder.Decode(gif);

        Assert.Equal([255, 0, 0, 255, 0, 255, 0, 255], document.GetBitmap(1).Pixels);
        Assert.Equal([0, 255, 0, 255, 255, 255, 255, 255], document.GetBitmap(2).Pixels);
    }

    [Fact]
    public void PrimaryBitmapReturnsFirstGifFrame()
    {
        var gif = CodecTestData.GifAnimation(1, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 1, 1, [1]),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [2])
        ]);

        using var document = ImageDecoder.Decode(gif);

        Assert.Equal([0, 255, 0, 255], document.PrimaryBitmap.Pixels);
        Assert.Equal(2, document.Items.Count);
    }

    [Fact]
    public void RejectsInvalidDataAfterFirstGifFrame()
    {
        var gif = CodecTestData.GifAnimation(1, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 1, 1, [1]),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [2])
        ]);
        gif[^2] = 0x7F;

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(gif));
    }

    [Fact]
    public void DocumentOwnsDecodedBitmaps()
    {
        var gif = CodecTestData.GifAnimation(1, 1, Palette,
            [new CodecTestData.GifFrameData(0, 0, 1, 1, [0])]);
        var document = ImageDecoder.Decode(gif);
        var bitmap = document.GetBitmap(0);

        document.Dispose();

        Assert.True(bitmap.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => document.GetBitmap(0));
        Assert.Throws<ObjectDisposedException>(() => _ = document.PrimaryBitmap);
    }

    [Fact]
    public void EnforcesGifDocumentLimits()
    {
        var gif = CodecTestData.GifAnimation(1, 1, Palette,
        [
            new CodecTestData.GifFrameData(0, 0, 1, 1, [0]),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [1])
        ]);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(gif,
            new ImageDecoderOptions { MaxItemCount = 1 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(gif,
            new ImageDecoderOptions { MaxTotalDecodedBytes = 4 }));
    }
}
