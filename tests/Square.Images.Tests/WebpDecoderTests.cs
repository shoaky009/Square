using Xunit;

namespace Square.Images.Tests;

public sealed class WebpDecoderTests
{
    [Fact]
    public void DecodesRealLosslessWebp()
    {
        var webp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");

        using var document = ImageDecoder.Decode(webp);
        var decoded = document.PrimaryBitmap;

        Assert.Equal((8, 8), (decoded.Width, decoded.Height));
        for (var offset = 0; offset < decoded.Pixels.Length; offset += 4)
            Assert.Equal(new byte[] { 0, 0, 254, 255 }, decoded.Pixels.AsSpan(offset, 4).ToArray());
    }

    [Theory]
    [InlineData("UklGRh4AAABXRUJQVlA4TBEAAAAvBIAAEAdQiirUo4CBiOh/AAA=", "HhQKgB4UCoAeFAqAHhQKgB4UCoAeFAqAHhQKgB4UCoAeFAqAHhQKgB4UCoAeFAqAHhQKgB4UCoAeFAqA")]
    [InlineData("UklGRn4AAABXRUJQVlA4THIAAAAvEAADAGdAIGnjk533D/UaBtK2ibNN+q7sBIIQWWa7ZeY/+IPwBJwG5SgYYQOMattWgoxcKrBoQIUXAM3iDT4FbKwVPnO9uZAGEf0P8lxfDlJHfBpXzvKsVf4CeQadxmu7631QaARnQPtyN8CpP3MGtAo=", "AAD//wAA//8AAP//AP8A/wD/AP8A/wD/AP///wD///8A/////wAA//8AAP//AP///wD///8A/////wD///8A////AP8AAP//AAD//wAA//8A/wD/AP8A/wD/AP8A////AP///wD/////AAD//wAA//8A////AP///wD/////AP///wD///8A/wAA//8AAP//AAA//wA/AP8APwD/AD8A/wA/P/8APz//AD8//z8AAP8/AAD/PwA//z8AP/8/AD//Pz8A/z8/AP8/PwD/AAD//wAA//8AAD//AD8A/wA/AP8APwD/AD8//wA/P/8APz//PwAA/z8AAP8/AD//PwA//z8AP/8/PwD/Pz8A/z8/AP8AAP//AAD//wAAP/8APwD/AD8A/wA/AP8APz//AD8//wA/P/8/AAD/PwAA/z8AP/8/AD//PwA//z8/AP8/PwD/Pz8A/wAA//8AAP//AAA//wA/AP8APwD/AD8A/wA/P/8APz//AD8//z8AAP8/AAD/PwA//z8AP/8/AD//Pz8A/z8/AP8/PwD/AAD//wAA//8AAD//AD8A/wA/AP8APwD/AID//wCA//8AgP//AID//z8AAP8/AD//PwA//z8AP/8AgP//AID//wCA//8AAP//AAD//wAAP/8APwD/AD8A/wCA//8AgP//AD8//wA/P/8AgP//AID//z8AP/8/AD//AID//wCA//8/PwD/Pz8A/wAA//8AAP//AAA//wA/AP8AgP//AID//wA/P/8APz//AD8//z8AAP8AgP//AID//wCA//8AgP//Pz8A/z8/AP8/PwD/AAD//wAA//8AAD//AD8A/wCA//8AgP//AD8//wA/P/8APz//PwAA/wCA//8AgP//AID//wCA//8/PwD/Pz8A/z8/AP8AAP//AAD//wAAP/8APwD/AID//wCA//8APz//AID//wCA//8/AAD/AID//wCA//8AgP//AID//z8/AP8AgP//AID//wAA//8AAP//AAA//wA/AP8AgP//AID//wA/P/8AgP//AID//z8AAP8AgP//AID//wCA//8AgP//Pz8A/wCA//8AgP//AAD//wAA//8AAD//AD8A/wCA//8AgP//AD8//wA/P/8APz//PwAA/wCA//8AgP//AID//wCA//8/PwD/Pz8A/z8/AP8=")]
    public void MatchesLibwebpGoldenOutput(string encoded, string expected)
    {
        using var document = ImageDecoder.Decode(Convert.FromBase64String(encoded));
        var decoded = document.PrimaryBitmap;
        Assert.Equal(Convert.FromBase64String(expected), decoded.Pixels);
    }

    [Fact]
    public void RejectsLossyAndMalformedWebp()
    {
        var lossy = new byte[20];
        "RIFF"u8.CopyTo(lossy); System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(lossy.AsSpan(4, 4), 12);
        "WEBPVP8 "u8.CopyTo(lossy.AsSpan(8));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(lossy));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode("RIFF\0\0\0\0WEBP"u8.ToArray()));
    }

    [Fact]
    public void EnforcesLimitsAndRejectsTruncation()
    {
        var webp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(webp, new ImageDecoderOptions { MaxWidth = 7 }));
        for (var length = 0; length < webp.Length; length++)
            Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(webp.AsSpan(0, length)));
    }

    [Fact]
    public void DecodesExtendedLosslessWebp()
    {
        var simple = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var vp8l = simple.AsSpan(12).ToArray();
        var extended = new byte[12 + 18 + vp8l.Length];
        "RIFF"u8.CopyTo(extended); System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(4, 4), (uint)(extended.Length - 8));
        "WEBPVP8X"u8.CopyTo(extended.AsSpan(8));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(16, 4), 10);
        extended[24] = 7; extended[27] = 7;
        vp8l.CopyTo(extended, 30);

        using var document = ImageDecoder.Decode(extended);
        var decoded = document.PrimaryBitmap;
        Assert.Equal((8, 8), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void AppliesWebpExifOrientationAndExposesMetadata()
    {
        var simple = Convert.FromBase64String("UklGRn4AAABXRUJQVlA4THIAAAAvEAADAGdAIGnjk533D/UaBtK2ibNN+q7sBIIQWWa7ZeY/+IPwBJwG5SgYYQOMattWgoxcKrBoQIUXAM3iDT4FbKwVPnO9uZAGEf0P8lxfDlJHfBpXzvKsVf4CeQadxmu7631QaARnQPtyN8CpP3MGtAo=");
        var vp8l = CodecTestData.ExtractVp8L(simple);
        var webp = CodecTestData.ExtendedLosslessWebp(vp8l, 17, 13,
            CodecTestData.ExifTiff(6, littleEndian: true));

        using var original = ImageDecoder.Decode(simple);
        using var document = ImageDecoder.Decode(webp);

        Assert.Equal((13, 17), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
        for (var y = 0; y < 17; y++)
            for (var x = 0; x < 13; x++)
                Assert.Equal(original.PrimaryBitmap.GetPixel(y, 12 - x).ToArray(),
                    document.PrimaryBitmap.GetPixel(x, y).ToArray());
        Assert.Equal(ImageOrientation.Rotate90, document.Metadata.OriginalOrientation);
        Assert.True(document.Metadata.OrientationApplied);
        Assert.Same(document.Metadata, document.PrimaryItem.Metadata);
    }

    [Fact]
    public void CanIgnoreWebpExifOrientationWithPrefixedPayload()
    {
        var simple = Convert.FromBase64String("UklGRn4AAABXRUJQVlA4THIAAAAvEAADAGdAIGnjk533D/UaBtK2ibNN+q7sBIIQWWa7ZeY/+IPwBJwG5SgYYQOMattWgoxcKrBoQIUXAM3iDT4FbKwVPnO9uZAGEf0P8lxfDlJHfBpXzvKsVf4CeQadxmu7631QaARnQPtyN8CpP3MGtAo=");
        var exif = "Exif\0\0"u8.ToArray().Concat(CodecTestData.ExifTiff(8, littleEndian: false)).ToArray();
        var webp = CodecTestData.ExtendedLosslessWebp(CodecTestData.ExtractVp8L(simple), 17, 13, exif);

        using var document = ImageDecoder.Decode(webp,
            new ImageDecoderOptions { ExifOrientationPolicy = ExifOrientationPolicy.Ignore });

        Assert.Equal((17, 13), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
        Assert.Equal(ImageOrientation.Rotate270, document.Metadata.OriginalOrientation);
        Assert.False(document.Metadata.OrientationApplied);
    }

    [Fact]
    public void AcceptsFlaggedIccpAndXmpChunks()
    {
        var simple = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var webp = CodecTestData.ExtendedLosslessWebp(CodecTestData.ExtractVp8L(simple), 8, 8,
            iccp: [1, 2, 3], xmp: "<x:xmpmeta/>"u8.ToArray());

        using var document = ImageDecoder.Decode(webp);

        Assert.Equal((8, 8), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
    }

    [Fact]
    public void RejectsWebpMetadataFlagMismatchAndLimits()
    {
        var simple = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var vp8l = CodecTestData.ExtractVp8L(simple);
        var missingExif = CodecTestData.ExtendedLosslessWebp(vp8l, 8, 8, additionalFlags: 8);
        var oversizedIccp = CodecTestData.ExtendedLosslessWebp(vp8l, 8, 8, iccp: [1, 2, 3, 4]);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(missingExif));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(oversizedIccp,
            new ImageDecoderOptions { MaxMetadataBytes = 3 }));
    }

    [Fact]
    public void RejectsMisorderedExtendedWebpChunks()
    {
        var simple = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var vp8l = CodecTestData.ExtractVp8L(simple);
        var vp8x = CodecTestData.Vp8X(8, 8, 0x20);
        var profile = new byte[] { 1, 2, 3 };
        var vp8xAfterUnknown = CodecTestData.Webp(("JUNK", Array.Empty<byte>()), ("VP8X", vp8x),
            ("ICCP", profile), ("VP8L", vp8l));
        var profileAfterImage = CodecTestData.Webp(("VP8X", vp8x), ("VP8L", vp8l), ("ICCP", profile));
        var imageAfterXmp = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(8, 8, 4)),
            ("VP8L", vp8l), ("XMP ", "x"u8.ToArray()), ("VP8L", vp8l));

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(vp8xAfterUnknown));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(profileAfterImage));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(imageAfterXmp));
    }

    [Fact]
    public void EnforcesCombinedWebpMetadataLimit()
    {
        var simple = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var webp = CodecTestData.ExtendedLosslessWebp(CodecTestData.ExtractVp8L(simple), 8, 8,
            iccp: [1, 2, 3], xmp: [4, 5, 6]);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(webp,
            new ImageDecoderOptions { MaxMetadataBytes = 5 }));
    }

    [Fact]
    public void RejectsInvalidStaticLossyAlphaContainerCombinations()
    {
        var alphaFixture = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "vp8-alpha-gradient-17x15.webp"));
        var alpha = ExtractChunk(alphaFixture, "ALPH"u8);
        var vp8 = ExtractChunk(alphaFixture, "VP8 "u8);
        var noFlag = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(17, 15, 0)),
            ("ALPH", alpha), ("VP8 ", vp8));
        var missingAlpha = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(17, 15, 0x10)),
            ("VP8 ", vp8));
        var alphaAfterImage = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(17, 15, 0x10)),
            ("VP8 ", vp8), ("ALPH", alpha));
        var duplicateAlpha = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(17, 15, 0x10)),
            ("ALPH", alpha), ("ALPH", alpha), ("VP8 ", vp8));
        var lossless = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
        var alphaWithVp8L = CodecTestData.Webp(("VP8X", CodecTestData.Vp8X(8, 8, 0x10)),
            ("ALPH", alpha.AsSpan(0, Math.Min(alpha.Length, 65)).ToArray()),
            ("VP8L", CodecTestData.ExtractVp8L(lossless)));

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(noFlag));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(missingAlpha));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(alphaAfterImage));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(duplicateAlpha));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(alphaWithVp8L));
    }

    private static byte[] ExtractChunk(ReadOnlySpan<byte> webp, ReadOnlySpan<byte> type)
    {
        var offset = 12;
        while (offset <= webp.Length - 8)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(webp.Slice(offset + 4, 4));
            if (webp.Slice(offset, 4).SequenceEqual(type)) return webp.Slice(offset + 8, length).ToArray();
            offset = checked(offset + 8 + length + (length & 1));
        }
        throw new InvalidOperationException("WebP chunk not found.");
    }
}
