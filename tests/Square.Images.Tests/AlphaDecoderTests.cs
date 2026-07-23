using Square.Images.Webp;
using Xunit;

namespace Square.Images.Tests;

public sealed class AlphaDecoderTests
{
    [Theory]
    [InlineData(0, new byte[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(1, new byte[] { 1, 3, 6, 5, 10, 16 })]
    [InlineData(2, new byte[] { 1, 3, 6, 5, 8, 12 })]
    [InlineData(3, new byte[] { 1, 3, 6, 5, 12, 21 })]
    public void DecodesRawFilters(byte filter, byte[] expected)
    {
        var payload = new byte[] { (byte)(filter << 2), 1, 2, 3, 4, 5, 6 };

        var alpha = AlphaDecoder.Decode(payload, 3, 2, new ImageDecoderOptions());

        Assert.Equal(expected, alpha);
    }

    [Fact]
    public void AcceptsPreprocessingWithoutDequantization()
    {
        var alpha = AlphaDecoder.Decode([0x10, 7, 8], 2, 1, new ImageDecoderOptions());

        Assert.Equal(new byte[] { 7, 8 }, alpha);
    }

    [Fact]
    public void DecodesLosslessCompressedGreenChannel()
    {
        var webp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var vp8l = CodecTestData.ExtractVp8L(webp);
        var payload = new byte[vp8l.Length - 4];
        payload[0] = 1;
        vp8l.AsSpan(5).CopyTo(payload.AsSpan(1));

        var alpha = AlphaDecoder.Decode(payload, 8, 8, new ImageDecoderOptions());

        Assert.Equal(new byte[64], alpha);
    }

    [Fact]
    public void RejectsTrailingLosslessData()
    {
        var webp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var vp8l = CodecTestData.ExtractVp8L(webp);
        var payload = new byte[vp8l.Length - 3];
        payload[0] = 1;
        vp8l.AsSpan(5).CopyTo(payload.AsSpan(1));

        Assert.Throws<InvalidDataException>(() =>
            AlphaDecoder.Decode(payload, 8, 8, new ImageDecoderOptions()));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 2 })]
    [InlineData(new byte[] { 3 })]
    [InlineData(new byte[] { 0x20 })]
    [InlineData(new byte[] { 0x30 })]
    [InlineData(new byte[] { 0x40 })]
    [InlineData(new byte[] { 0, 1 })]
    [InlineData(new byte[] { 0, 1, 2, 3 })]
    [InlineData(new byte[] { 1 })]
    public void RejectsMalformedHeadersAndPayloads(byte[] payload)
    {
        Assert.Throws<InvalidDataException>(() =>
            AlphaDecoder.Decode(payload, 2, 1, new ImageDecoderOptions()));
    }

    [Fact]
    public void EnforcesDecodedAndChunkLimits()
    {
        Assert.Throws<InvalidDataException>(() => AlphaDecoder.Decode([0, 1, 2], 2, 1,
            new ImageDecoderOptions { MaxDecodedBytes = 1 }));
        Assert.Throws<InvalidDataException>(() => AlphaDecoder.Decode([0, 1, 2], 2, 1,
            new ImageDecoderOptions { MaxChunkBytes = 2 }));
    }
}
