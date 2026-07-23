using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Square.Images.Tests;

public sealed class Vp8FixtureTests
{
    public static TheoryData<string, int, int, string, string, string> Fixtures
    {
        get
        {
            var manifest = LoadManifest();
            var data = new TheoryData<string, int, int, string, string, string>();
            foreach (var fixture in manifest.Fixtures)
                data.Add(fixture.File, fixture.Width, fixture.Height, fixture.Golden, fixture.Sha256,
                    fixture.GoldenSha256);
            return data;
        }
    }

    [Fact]
    public void CommittedLossyVp8FixturesMatchManifest()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (var fixture in LoadManifest().Fixtures)
        {
            var encoded = File.ReadAllBytes(Path.Combine(fixtureRoot, fixture.File));
            var golden = File.ReadAllBytes(Path.Combine(fixtureRoot, fixture.Golden));

            Assert.Equal(fixture.Sha256, Sha256(encoded));
            Assert.Equal(fixture.GoldenSha256, Sha256(golden));
            Assert.Equal(checked(fixture.Width * fixture.Height * 4), golden.Length);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void DecodesLossyVp8Fixtures(string file, int width, int height, string golden,
        string sha256, string goldenSha256)
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var encoded = File.ReadAllBytes(Path.Combine(fixtureRoot, file));
        var expected = File.ReadAllBytes(Path.Combine(fixtureRoot, golden));

        Assert.Equal(sha256, Sha256(encoded));
        Assert.Equal(goldenSha256, Sha256(expected));
        Assert.Equal(checked(width * height * 4), expected.Length);

        using var document = ImageDecoder.Decode(encoded);
        var decoded = document.PrimaryBitmap;

        Assert.Equal((width, height), (decoded.Width, decoded.Height));
        Assert.Equal(expected, decoded.Pixels);
    }

    [Fact]
    public void RejectsTruncatedLossyVp8()
    {
        var encoded = ReadFixture("vp8-gradient-17x13.webp");

        for (var length = 0; length < encoded.Length; length++)
            Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded.AsSpan(0, length)));
    }

    [Fact]
    public void RejectsInvalidVp8FrameHeader()
    {
        var encoded = ReadFixture("vp8-solid-8x8.webp");
        var vp8Payload = FindChunkPayload(encoded, "VP8 "u8);
        encoded[vp8Payload + 3] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded));
    }

    [Fact]
    public void EnforcesLossyVp8DimensionAndDecodedByteLimits()
    {
        var encoded = ReadFixture("vp8-gradient-17x13.webp");

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded,
            new ImageDecoderOptions { MaxWidth = 16 }));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded,
            new ImageDecoderOptions { MaxDecodedBytes = 17 * 13 * 4 - 1 }));
    }

    [Fact]
    public void RejectsZeroSizedLossyVp8Frame()
    {
        var encoded = ReadFixture("vp8-solid-8x8.webp");
        var vp8Payload = FindChunkPayload(encoded, "VP8 "u8);
        encoded[vp8Payload + 6] = 0;
        encoded[vp8Payload + 7] = 0;

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(encoded));
    }

    private static Vp8FixtureManifest LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "vp8-manifest.json");
        return JsonSerializer.Deserialize<Vp8FixtureManifest>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static byte[] ReadFixture(string file)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));

    private static string Sha256(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static int FindChunkPayload(ReadOnlySpan<byte> webp, ReadOnlySpan<byte> type)
    {
        var offset = 12;
        while (offset <= webp.Length - 8)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(webp.Slice(offset + 4, 4));
            if (webp.Slice(offset, 4).SequenceEqual(type)) return offset + 8;
            offset = checked(offset + 8 + length + (length & 1));
        }
        throw new InvalidOperationException("WebP chunk not found.");
    }

    private sealed class Vp8FixtureManifest
    {
        public List<Vp8FixtureEntry> Fixtures { get; init; } = [];
    }

    private sealed class Vp8FixtureEntry
    {
        public string File { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public int Width { get; init; }
        public int Height { get; init; }
        public string Golden { get; init; } = "";
        public string GoldenSha256 { get; init; } = "";
    }
}
