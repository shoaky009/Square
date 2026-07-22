using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Square.Images.Tests;

public sealed class FixtureGoldenTests
{
    [Fact]
    public void CommittedAnimationFixturesMatchManifestAndGoldenFrames()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var manifest = JsonSerializer.Deserialize<FixtureManifest>(
            File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        foreach (var fixture in manifest.Fixtures)
        {
            var sourcePath = Path.Combine(fixtureRoot, fixture.File);
            Assert.Equal(fixture.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant());
            using var document = ImageDecoder.Decode(sourcePath);
            Assert.Equal(fixture.Frames.Count, document.Items.Count);
            for (var index = 0; index < fixture.Frames.Count; index++)
            {
                var expected = File.ReadAllBytes(Path.Combine(fixtureRoot, fixture.Frames[index].Golden));
                Assert.Equal(expected, document.GetBitmap(index).Pixels);
                Assert.Equal(TimeSpan.FromMilliseconds(fixture.Frames[index].DurationMilliseconds), document.Items[index].Duration);
            }
        }
    }

    private sealed class FixtureManifest
    {
        public List<FixtureEntry> Fixtures { get; init; } = [];
    }

    private sealed class FixtureEntry
    {
        public string File { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public List<FrameEntry> Frames { get; init; } = [];
    }

    private sealed class FrameEntry
    {
        public string Golden { get; init; } = "";
        public double DurationMilliseconds { get; init; }
    }
}
