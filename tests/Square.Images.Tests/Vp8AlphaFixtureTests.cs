using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Square.Images.Tests;

public sealed class Vp8AlphaFixtureTests
{
    public static TheoryData<string, int, int, string, string, string> Fixtures
    {
        get
        {
            var data = new TheoryData<string, int, int, string, string, string>();
            foreach (var fixture in LoadManifest().Fixtures)
                data.Add(fixture.File, fixture.Width, fixture.Height, fixture.Golden, fixture.Sha256,
                    fixture.GoldenSha256);
            return data;
        }
    }

    [Fact]
    public void CommittedLossyVp8AlphaFixturesMatchManifest()
    {
        foreach (var fixture in LoadManifest().Fixtures)
        {
            var encoded = ReadFixture(fixture.File);
            var golden = ReadFixture(fixture.Golden);

            Assert.True(fixture.Alpha);
            Assert.Equal("ffmpeg bgra alpha channel", fixture.AlphaSource);
            Assert.Equal(fixture.Sha256, Sha256(encoded));
            Assert.Equal(fixture.GoldenSha256, Sha256(golden));
            Assert.Equal(checked(fixture.Width * fixture.Height * 4), golden.Length);
            Assert.Equal((fixture.Width, fixture.Height), ReadVp8XDimensions(encoded));
            Assert.True(HasChunk(encoded, "ALPH"u8));
            Assert.True(HasChunk(encoded, "VP8 "u8));
            Assert.Equal(0x10, encoded[20] & 0x10);
        }
    }

    [Fact]
    public void AlphaGoldensContainStraightAlphaCoverage()
    {
        var gradient = ReadFixture("vp8-alpha-gradient-17x15.bgra");
        var checker = ReadFixture("vp8-alpha-checker-detail-29x21.bgra");

        Assert.Contains(gradient.Where((_, index) => (index & 3) == 3), alpha => alpha == 0);
        Assert.Contains(gradient.Where((_, index) => (index & 3) == 3), alpha => alpha is > 0 and < 255);
        Assert.Contains(gradient.Where((_, index) => (index & 3) == 3), alpha => alpha == 255);
        Assert.All(checker.Where((_, index) => (index & 3) == 3), alpha => Assert.True(alpha is 0 or 255));
        Assert.Contains(checker.Where((_, index) => (index & 3) == 3), alpha => alpha == 0);
        Assert.Contains(checker.Where((_, index) => (index & 3) == 3), alpha => alpha == 255);
        Assert.Contains(Enumerable.Range(0, checker.Length / 4), index =>
            checker[index * 4 + 3] == 0 &&
            (checker[index * 4] != 0 || checker[index * 4 + 1] != 0 || checker[index * 4 + 2] != 0));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void DecodesLossyVp8AlphaFixturesExactly(string file, int width, int height, string golden,
        string sha256, string goldenSha256)
    {
        var encoded = ReadFixture(file);
        var expected = ReadFixture(golden);

        Assert.Equal(sha256, Sha256(encoded));
        Assert.Equal(goldenSha256, Sha256(expected));
        using var document = ImageDecoder.Decode(encoded);
        Assert.Equal((width, height), (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
        Assert.Equal(expected, document.PrimaryBitmap.Pixels);
    }

    [Fact]
    public void RejectsTruncatedLossyVp8Alpha()
    {
        var encoded = ReadFixture("vp8-alpha-gradient-17x15.webp");
        for (var length = 0; length < encoded.Length; length++)
            Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded.AsSpan(0, length)));
    }

    [Fact]
    public void EnforcesLossyVp8AlphaDimensionsDecodedBytesAndChunkLimits()
    {
        var encoded = ReadFixture("vp8-alpha-gradient-17x15.webp");
        var alphaLength = FindChunkLength(encoded, "ALPH"u8);

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded,
            new ImageDecoderOptions { MaxWidth = 16 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded,
            new ImageDecoderOptions { MaxDecodedBytes = 17 * 15 * 4 - 1 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded,
            new ImageDecoderOptions { MaxChunkBytes = alphaLength - 1 }));
    }

    private static Vp8AlphaFixtureManifest LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "vp8-alpha-manifest.json");
        return JsonSerializer.Deserialize<Vp8AlphaFixtureManifest>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static byte[] ReadFixture(string file)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));

    private static string Sha256(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static (int Width, int Height) ReadVp8XDimensions(ReadOnlySpan<byte> webp)
    {
        Assert.True(webp.Slice(12, 4).SequenceEqual("VP8X"u8));
        return (ReadUInt24(webp.Slice(24, 3)) + 1, ReadUInt24(webp.Slice(27, 3)) + 1);
    }

    private static int ReadUInt24(ReadOnlySpan<byte> value)
        => value[0] | value[1] << 8 | value[2] << 16;

    private static bool HasChunk(ReadOnlySpan<byte> webp, ReadOnlySpan<byte> type)
        => FindChunkLength(webp, type, throwIfMissing: false) >= 0;

    private static int FindChunkLength(ReadOnlySpan<byte> webp, ReadOnlySpan<byte> type,
        bool throwIfMissing = true)
    {
        var offset = 12;
        while (offset <= webp.Length - 8)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(webp.Slice(offset + 4, 4));
            if (webp.Slice(offset, 4).SequenceEqual(type)) return length;
            offset = checked(offset + 8 + length + (length & 1));
        }
        if (throwIfMissing) throw new InvalidOperationException("WebP chunk not found.");
        return -1;
    }

    private sealed class Vp8AlphaFixtureManifest
    {
        public List<Vp8AlphaFixtureEntry> Fixtures { get; init; } = [];
    }

    private sealed class Vp8AlphaFixtureEntry
    {
        public string File { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public int Width { get; init; }
        public int Height { get; init; }
        public string Golden { get; init; } = "";
        public string GoldenSha256 { get; init; } = "";
        public bool Alpha { get; init; }
        public string AlphaSource { get; init; } = "";
    }
}
