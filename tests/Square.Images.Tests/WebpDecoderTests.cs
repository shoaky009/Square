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
}
