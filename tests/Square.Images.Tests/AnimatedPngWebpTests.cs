using Xunit;

namespace Square.Images.Tests;

public sealed class AnimatedPngWebpTests
{
    [Fact]
    public void DecodesApngFramesTimingLoopingBlendAndDisposal()
    {
        var apng = CodecTestData.Apng(2, 1,
        [
            new CodecTestData.ApngFrameData(2, 1, 0, 0, [0, 255, 0, 0, 255, 0, 255, 0, 255], 50,
                Dispose: 1),
            new CodecTestData.ApngFrameData(1, 1, 1, 0, [0, 0, 0, 0, 128], 120, Blend: 1)
        ], playCount: 3);

        using var document = ImageDecoder.Decode(apng);

        Assert.Equal(ImageFormat.Png, document.Format);
        Assert.Equal(ImageDocumentKind.Animation, document.Kind);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(50), document.Items[0].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), document.Items[1].Duration);
        Assert.Equal(3, document.Animation!.PlayCount);
        Assert.False(document.Animation.LoopsForever);
        Assert.Equal([0, 0, 255, 255, 0, 255, 0, 255], document.GetBitmap(0).Pixels);
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 128], document.GetBitmap(1).Pixels);
    }

    [Fact]
    public void DecodesApngRestorePreviousAndInfiniteLoop()
    {
        var apng = CodecTestData.Apng(2, 1,
        [
            new CodecTestData.ApngFrameData(2, 1, 0, 0, [0, 255, 0, 0, 255, 0, 255, 0, 255], 10),
            new CodecTestData.ApngFrameData(1, 1, 0, 0, [0, 0, 0, 255, 255], 10, Dispose: 2),
            new CodecTestData.ApngFrameData(1, 1, 1, 0, [0, 255, 255, 255, 255], 10)
        ]);

        using var document = ImageDecoder.Decode(apng);

        Assert.True(document.Animation!.LoopsForever);
        Assert.Equal([0, 0, 255, 255, 255, 255, 255, 255], document.GetBitmap(2).Pixels);
    }

    [Fact]
    public void RejectsInvalidApngSequenceAndLimits()
    {
        var apng = CodecTestData.Apng(1, 1,
        [
            new CodecTestData.ApngFrameData(1, 1, 0, 0, [0, 1, 2, 3, 255], 1),
            new CodecTestData.ApngFrameData(1, 1, 0, 0, [0, 4, 5, 6, 255], 1)
        ]);
        var invalid = (byte[])apng.Clone();
        var fdat = FindChunk(invalid, "fdAT"u8);
        invalid[fdat + 8 + 3] ^= 1;

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(invalid,
            new ImageDecoderOptions { PngCrcPolicy = PngCrcPolicy.Ignore }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(apng,
            new ImageDecoderOptions { MaxItemCount = 1 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(apng,
            new ImageDecoderOptions { MaxTotalDecodedBytes = 4 }));
    }

    [Fact]
    public void DecodesAnimatedLosslessWebpFramesTimingLoopingAndDisposal()
    {
        var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var red = CodecTestData.ExtractVp8L(redWebp);
        var animated = CodecTestData.AnimatedWebp(16, 8,
        [
            new CodecTestData.WebpFrameData(0, 0, 8, 8, 40, red, DisposeToBackground: true),
            new CodecTestData.WebpFrameData(8, 0, 8, 8, 90, red)
        ], loopCount: 2);

        using var document = ImageDecoder.Decode(animated);

        Assert.Equal(ImageFormat.Webp, document.Format);
        Assert.Equal(ImageDocumentKind.Animation, document.Kind);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal(2, document.Animation!.PlayCount);
        Assert.Equal(TimeSpan.FromMilliseconds(130), document.Animation.TotalDuration);
        Assert.Equal(new byte[] { 0, 0, 254, 255 }, document.GetBitmap(0).GetPixel(0, 0).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, document.GetBitmap(1).GetPixel(0, 0).ToArray());
        Assert.Equal(new byte[] { 0, 0, 254, 255 }, document.GetBitmap(1).GetPixel(8, 0).ToArray());
    }

    [Fact]
    public void RejectsAnimatedWebpLimitsAndLossyFrames()
    {
        var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var red = CodecTestData.ExtractVp8L(redWebp);
        var animated = CodecTestData.AnimatedWebp(16, 8,
        [
            new CodecTestData.WebpFrameData(0, 0, 8, 8, 10, red),
            new CodecTestData.WebpFrameData(8, 0, 8, 8, 10, red)
        ]);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(animated,
            new ImageDecoderOptions { MaxTotalDecodedBytes = 4 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(animated,
            new ImageDecoderOptions { MaxItemCount = 1 }));
    }

    private static int FindChunk(ReadOnlySpan<byte> png, ReadOnlySpan<byte> type)
    {
        var offset = 8;
        while (offset <= png.Length - 12)
        {
            if (png.Slice(offset + 4, 4).SequenceEqual(type)) return offset;
            offset += 12 + System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.Slice(offset, 4));
        }
        throw new InvalidOperationException("Chunk not found.");
    }
}
