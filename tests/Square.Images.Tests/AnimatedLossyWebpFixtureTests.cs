using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Square.Images.Tests;

public sealed class AnimatedLossyWebpFixtureTests
{
    [Fact]
    public void CommittedAnimatedLossyWebpFixturesMatchManifestAndContainerStructure()
    {
        foreach (var fixture in LoadManifest().Fixtures)
        {
            var encoded = ReadFixture(fixture.File);
            Assert.Equal(fixture.Sha256, Sha256(encoded));
            Assert.Equal(encoded.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(4, 4)));
            Assert.Equal("RIFF", Encoding.ASCII.GetString(encoded, 0, 4));
            Assert.Equal("WEBP", Encoding.ASCII.GetString(encoded, 8, 4));

            var chunks = ReadChunks(encoded.AsMemory(12));
            Assert.Equal(new[] { "VP8X", "ANIM" }.Concat(Enumerable.Repeat("ANMF", fixture.Frames.Count)),
                chunks.Select(static chunk => chunk.Type));
            var vp8x = chunks[0].Data.Span;
            Assert.Equal(10, vp8x.Length);
            Assert.Equal((fixture.Width, fixture.Height),
                (ReadUInt24(vp8x.Slice(4, 3)) + 1, ReadUInt24(vp8x.Slice(7, 3)) + 1));
            Assert.NotEqual(0, vp8x[0] & 2);
            Assert.Equal(fixture.Frames.Any(static frame => frame.Chunks.Any(static chunk => chunk.Type == "ALPH")),
                (vp8x[0] & 0x10) != 0);

            var anim = chunks[1].Data.Span;
            Assert.Equal(uint.Parse(fixture.BackgroundBgra, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                BinaryPrimitives.ReadUInt32LittleEndian(anim.Slice(0, 4)));
            Assert.Equal(fixture.LoopCount, BinaryPrimitives.ReadUInt16LittleEndian(anim.Slice(4, 2)));

            for (var index = 0; index < fixture.Frames.Count; index++)
            {
                var expected = fixture.Frames[index];
                Assert.Equal(index, expected.Index);
                var anmf = chunks[index + 2].Data;
                var header = anmf.Span.Slice(0, 16);
                Assert.Equal((expected.X, expected.Y),
                    (ReadUInt24(header.Slice(0, 3)) * 2, ReadUInt24(header.Slice(3, 3)) * 2));
                Assert.Equal((expected.Width, expected.Height),
                    (ReadUInt24(header.Slice(6, 3)) + 1, ReadUInt24(header.Slice(9, 3)) + 1));
                Assert.Equal(expected.DurationMilliseconds, ReadUInt24(header.Slice(12, 3)));
                Assert.Equal(expected.DisposeToBackground, (header[15] & 1) != 0);
                Assert.Equal(expected.Blend == "replace", (header[15] & 2) != 0);
                Assert.Equal(0, header[15] & 0xFC);

                var frameChunks = ReadChunks(anmf.Slice(16));
                Assert.Equal(expected.Chunks.Select(static chunk => chunk.Type),
                    frameChunks.Select(static chunk => chunk.Type));
                Assert.Equal("VP8 ", frameChunks[^1].Type);
                Assert.True(frameChunks.Count is 1 or 2);
                if (frameChunks.Count == 2) Assert.Equal("ALPH", frameChunks[0].Type);
                Assert.Equal((expected.Width, expected.Height), ReadVp8Dimensions(frameChunks[^1].Data.Span));
                for (var chunkIndex = 0; chunkIndex < frameChunks.Count; chunkIndex++)
                {
                    Assert.Equal(expected.Chunks[chunkIndex].Length, frameChunks[chunkIndex].Data.Length);
                    Assert.Equal(expected.Chunks[chunkIndex].Sha256, Sha256(frameChunks[chunkIndex].Data.Span));
                }

                var golden = ReadFixture(expected.Golden);
                Assert.Equal(checked(fixture.Width * fixture.Height * 4), golden.Length);
                Assert.Equal(expected.GoldenSha256, Sha256(golden));
            }
        }
    }

    [Fact]
    public void FixturesCoverOpaquePartialDisposalAndMixedAlphaFrames()
    {
        var manifest = LoadManifest();
        var opaque = Assert.Single(manifest.Fixtures, static fixture => fixture.File == "opaque-partial-dispose.webp");
        Assert.Equal(2, opaque.Frames.Count);
        Assert.All(opaque.Frames, static frame => Assert.Equal(new[] { "VP8 " },
            frame.Chunks.Select(static chunk => chunk.Type)));
        Assert.Contains(opaque.Frames, static frame => frame.DisposeToBackground);
        Assert.All(opaque.Frames, frame => Assert.True(frame.Width < opaque.Width || frame.Height < opaque.Height));
        var opaqueFirst = ReadFixture(opaque.Frames[0].Golden);
        var opaqueSecond = ReadFixture(opaque.Frames[1].Golden);
        Assert.NotEqual(new byte[] { 0x08, 0x10, 0x20, 0xFF }, Pixel(opaqueFirst, opaque.Width, 2, 2));
        Assert.Equal(new byte[] { 0x08, 0x10, 0x20, 0xFF }, Pixel(opaqueSecond, opaque.Width, 2, 2));

        var mixed = Assert.Single(manifest.Fixtures, static fixture => fixture.File == "mixed-alpha-partial-dispose.webp");
        Assert.Contains(mixed.Frames, static frame => frame.Chunks.Count == 1 && frame.Chunks[0].Type == "VP8 ");
        var alphaFrame = Assert.Single(mixed.Frames,
            static frame => frame.Chunks.Select(static chunk => chunk.Type).SequenceEqual(new[] { "ALPH", "VP8 " }));
        Assert.True(alphaFrame.DisposeToBackground);
        Assert.Equal("alpha-over", alphaFrame.Blend);

        var alphaGolden = ReadFixture(alphaFrame.Golden);
        Assert.Contains(Enumerable.Range(0, alphaGolden.Length / 4), index =>
            alphaGolden[index * 4 + 3] is > 0 and < 255);
        var finalGolden = ReadFixture(mixed.Frames[^1].Golden);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(finalGolden, mixed.Width, 20, 10));
    }

    [Fact]
    public void ImageDecoderMatchesAnimatedLossyWebpGoldens()
    {
        foreach (var fixture in LoadManifest().Fixtures)
        {
            using var document = ImageDecoder.Decode(ReadFixture(fixture.File));
            Assert.Equal((fixture.Width, fixture.Height),
                (document.PrimaryBitmap.Width, document.PrimaryBitmap.Height));
            Assert.Equal(fixture.Frames.Count, document.Items.Count);
            Assert.Equal(fixture.LoopCount == 0, document.Animation!.LoopsForever);
            Assert.Equal(fixture.LoopCount == 0 ? 0 : fixture.LoopCount, document.Animation.PlayCount);
            for (var index = 0; index < fixture.Frames.Count; index++)
            {
                Assert.Equal(TimeSpan.FromMilliseconds(fixture.Frames[index].DurationMilliseconds),
                    document.Items[index].Duration);
                Assert.Equal(ReadFixture(fixture.Frames[index].Golden), document.GetBitmap(index).Pixels);
            }
        }
    }

    private static FixtureManifest LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "animated-lossy-webp", "manifest.json");
        return JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static byte[] ReadFixture(string file)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "animated-lossy-webp", file));

    private static List<RiffChunk> ReadChunks(ReadOnlyMemory<byte> data)
    {
        var chunks = new List<RiffChunk>();
        var offset = 0;
        while (offset < data.Length)
        {
            Assert.True(offset <= data.Length - 8);
            var span = data.Span;
            var length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + 4, 4));
            Assert.True(length >= 0);
            var paddedLength = checked(length + (length & 1));
            Assert.True(offset + 8L + paddedLength <= data.Length);
            chunks.Add(new RiffChunk(Encoding.ASCII.GetString(span.Slice(offset, 4)),
                data.Slice(offset + 8, length)));
            if ((length & 1) != 0) Assert.Equal(0, span[offset + 8 + length]);
            offset += 8 + paddedLength;
        }
        return chunks;
    }

    private static (int Width, int Height) ReadVp8Dimensions(ReadOnlySpan<byte> vp8)
    {
        Assert.True(vp8.Length >= 10);
        Assert.True(vp8.Slice(3, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A }));
        return (BinaryPrimitives.ReadUInt16LittleEndian(vp8.Slice(6, 2)) & 0x3FFF,
            BinaryPrimitives.ReadUInt16LittleEndian(vp8.Slice(8, 2)) & 0x3FFF);
    }

    private static int ReadUInt24(ReadOnlySpan<byte> value)
        => value[0] | value[1] << 8 | value[2] << 16;

    private static byte[] Pixel(byte[] pixels, int width, int x, int y)
        => pixels.AsSpan((y * width + x) * 4, 4).ToArray();

    private static string Sha256(ReadOnlySpan<byte> data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private readonly record struct RiffChunk(string Type, ReadOnlyMemory<byte> Data);

    private sealed class FixtureManifest
    {
        public List<FixtureEntry> Fixtures { get; init; } = [];
    }

    private sealed class FixtureEntry
    {
        public string File { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public int Width { get; init; }
        public int Height { get; init; }
        public string BackgroundBgra { get; init; } = "";
        public ushort LoopCount { get; init; }
        public List<FrameEntry> Frames { get; init; } = [];
    }

    private sealed class FrameEntry
    {
        public int Index { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int DurationMilliseconds { get; init; }
        public bool DisposeToBackground { get; init; }
        public string Blend { get; init; } = "";
        public List<FrameChunkEntry> Chunks { get; init; } = [];
        public string Golden { get; init; } = "";
        public string GoldenSha256 { get; init; } = "";
    }

    private sealed class FrameChunkEntry
    {
        public string Type { get; init; } = "";
        public int Length { get; init; }
        public string Sha256 { get; init; } = "";
    }
}
