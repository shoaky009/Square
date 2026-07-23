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

    [Fact]
    public void AppliesWebpExifOrientationToEveryAnimationFrame()
    {
        var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var red = CodecTestData.ExtractVp8L(redWebp);
        var animated = CodecTestData.AnimatedWebp(16, 8,
        [
            new CodecTestData.WebpFrameData(0, 0, 8, 8, 40, red),
            new CodecTestData.WebpFrameData(8, 0, 8, 8, 90, red)
        ], exif: CodecTestData.ExifTiff(6, littleEndian: true));

        using var document = ImageDecoder.Decode(animated);

        Assert.Equal((8, 16), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
        Assert.Equal(ImageOrientation.Rotate90, document.Metadata.OriginalOrientation);
        Assert.True(document.Metadata.OrientationApplied);
        Assert.All(document.Items, item => Assert.Same(document.Metadata, item.Metadata));
        Assert.Equal(new byte[] { 0, 0, 254, 255 }, document.GetBitmap(0).GetPixel(7, 0).ToArray());
        Assert.Equal(new byte[] { 0, 0, 254, 255 }, document.GetBitmap(1).GetPixel(7, 15).ToArray());
    }

    [Fact]
    public void RejectsAnimationFramesAfterTrailingMetadata()
    {
        var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var red = CodecTestData.ExtractVp8L(redWebp);
        var animated = CodecTestData.AnimatedWebp(8, 8,
            [new CodecTestData.WebpFrameData(0, 0, 8, 8, 10, red)],
            exif: CodecTestData.ExifTiff(1, littleEndian: true));
        var anmfOffset = FindRiffChunk(animated, "ANMF"u8);
        var anmfLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(animated.AsSpan(anmfOffset + 4, 4));
        var paddedLength = 8 + anmfLength + (anmfLength & 1);
        var frameChunk = animated.AsSpan(anmfOffset, paddedLength).ToArray();
        var malformed = new byte[animated.Length + frameChunk.Length];
        animated.CopyTo(malformed, 0);
        frameChunk.CopyTo(malformed, animated.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(4, 4),
            (uint)(malformed.Length - 8));

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(malformed));
    }

    [Fact]
    public void OneFrameWebpAnimationRetainsLoopMetadata()
    {
        var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var animated = CodecTestData.AnimatedWebp(8, 8,
            [new CodecTestData.WebpFrameData(0, 0, 8, 8, 25, CodecTestData.ExtractVp8L(redWebp))],
            loopCount: 2);

        using var document = ImageDecoder.Decode(animated);

        Assert.Equal(ImageDocumentKind.Animation, document.Kind);
        Assert.NotNull(document.Animation);
        Assert.False(document.Animation.LoopsForever);
        Assert.Equal(2, document.Animation.PlayCount);
        Assert.Equal(TimeSpan.FromMilliseconds(25), document.Animation.TotalDuration);
    }

    [Fact]
    public void RejectsInvalidLossyAnimationFrameChunkOrder()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "animated-lossy-webp");
        var encoded = File.ReadAllBytes(Path.Combine(fixtureRoot, "mixed-alpha-partial-dispose.webp"));
        var anmfOffset = FindRiffChunk(encoded, "ANMF"u8, occurrence: 1);
        var anmfLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(anmfOffset + 4, 4));
        var frameData = encoded.AsSpan(anmfOffset + 8, anmfLength).ToArray();
        var alphaOffset = FindFrameChunk(frameData, "ALPH"u8);
        var vp8Offset = FindFrameChunk(frameData, "VP8 "u8);
        var alphaLength = FrameChunkLength(frameData, alphaOffset);
        var vp8Length = FrameChunkLength(frameData, vp8Offset);
        var header = frameData.AsSpan(0, 16).ToArray();
        var alpha = frameData.AsSpan(alphaOffset, alphaLength).ToArray();
        var vp8 = frameData.AsSpan(vp8Offset, vp8Length).ToArray();

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ReplaceAnmf(encoded, anmfOffset,
            [.. header, .. vp8, .. alpha])));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ReplaceAnmf(encoded, anmfOffset,
            [.. header, .. alpha, .. alpha, .. vp8])));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(ReplaceAnmf(encoded, anmfOffset,
            [.. header, .. alpha])));
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

    private static int FindRiffChunk(ReadOnlySpan<byte> webp, ReadOnlySpan<byte> type, int occurrence = 0)
    {
        var offset = 12;
        while (offset <= webp.Length - 8)
        {
            if (webp.Slice(offset, 4).SequenceEqual(type) && occurrence-- == 0) return offset;
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(webp.Slice(offset + 4, 4));
            offset += 8 + length + (length & 1);
        }
        throw new InvalidOperationException("Chunk not found.");
    }

    private static int FindFrameChunk(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> type)
    {
        var offset = 16;
        while (offset <= frame.Length - 8)
        {
            if (frame.Slice(offset, 4).SequenceEqual(type)) return offset;
            offset += FrameChunkLength(frame, offset);
        }
        throw new InvalidOperationException("Frame chunk not found.");
    }

    private static int FrameChunkLength(ReadOnlySpan<byte> frame, int offset)
    {
        var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(offset + 4, 4));
        return 8 + length + (length & 1);
    }

    private static byte[] ReplaceAnmf(byte[] webp, int anmfOffset, byte[] payload)
    {
        var oldLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(webp.AsSpan(anmfOffset + 4, 4));
        var oldTotal = 8 + oldLength + (oldLength & 1);
        var newTotal = 8 + payload.Length + (payload.Length & 1);
        var output = new byte[webp.Length - oldTotal + newTotal];
        webp.AsSpan(0, anmfOffset).CopyTo(output);
        "ANMF"u8.CopyTo(output.AsSpan(anmfOffset));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(anmfOffset + 4, 4), payload.Length);
        payload.CopyTo(output, anmfOffset + 8);
        webp.AsSpan(anmfOffset + oldTotal).CopyTo(output.AsSpan(anmfOffset + newTotal));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), (uint)(output.Length - 8));
        return output;
    }
}
