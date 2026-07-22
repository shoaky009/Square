using System.Security.Cryptography;
using System.Text.Json;
using Square.Images.Tests;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    "tests", "Square.Images.Tests", "Fixtures"));
Directory.CreateDirectory(root);

var fixtures = new List<object>();

var palette = new byte[] { 255, 0, 0, 0, 255, 0 };
var gif = CodecTestData.GifAnimation(2, 1, palette,
[
    new CodecTestData.GifFrameData(0, 0, 2, 1, [0, 0], Delay: 5),
    new CodecTestData.GifFrameData(1, 0, 1, 1, [1], Delay: 12)
]);
WriteFixture("animation.gif", gif,
[
    ("animation.gif.frame0.bgra", new byte[] { 0, 0, 255, 255, 0, 0, 255, 255 }, 50d),
    ("animation.gif.frame1.bgra", new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, 120d)
]);

var apng = CodecTestData.Apng(2, 1,
[
    new CodecTestData.ApngFrameData(2, 1, 0, 0, [0, 255, 0, 0, 255, 0, 255, 0, 255], 50, Dispose: 1),
    new CodecTestData.ApngFrameData(1, 1, 1, 0, [0, 0, 0, 0, 128], 120, Blend: 1)
], playCount: 3);
WriteFixture("animation.apng", apng,
[
    ("animation.apng.frame0.bgra", new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, 50d),
    ("animation.apng.frame1.bgra", new byte[] { 0, 0, 0, 0, 0, 0, 0, 128 }, 120d)
]);

var redWebp = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TA8AAAAvB8ABAAcQ9Y/+ByKi/wEA");
var red = CodecTestData.ExtractVp8L(redWebp);
var webp = CodecTestData.AnimatedWebp(16, 8,
[
    new CodecTestData.WebpFrameData(0, 0, 8, 8, 40, red, DisposeToBackground: true),
    new CodecTestData.WebpFrameData(8, 0, 8, 8, 90, red)
], loopCount: 2);
var webpFrame0 = new byte[16 * 8 * 4];
var webpFrame1 = new byte[16 * 8 * 4];
for (var y = 0; y < 8; y++)
{
    for (var x = 0; x < 8; x++) SetRed(webpFrame0, 16, x, y);
    for (var x = 8; x < 16; x++) SetRed(webpFrame1, 16, x, y);
}
WriteFixture("animation.webp", webp,
[
    ("animation.webp.frame0.bgra", webpFrame0, 40d),
    ("animation.webp.frame1.bgra", webpFrame1, 90d)
]);

File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new { fixtures },
    new JsonSerializerOptions { WriteIndented = true }));

void WriteFixture(string file, byte[] bytes, (string Golden, byte[] Pixels, double DurationMilliseconds)[] frames)
{
    File.WriteAllBytes(Path.Combine(root, file), bytes);
    foreach (var frame in frames) File.WriteAllBytes(Path.Combine(root, frame.Golden), frame.Pixels);
    fixtures.Add(new
    {
        file,
        sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        frames = frames.Select(frame => new { golden = frame.Golden, durationMilliseconds = frame.DurationMilliseconds })
    });
}

static void SetRed(byte[] pixels, int width, int x, int y)
{
    var offset = (y * width + x) * 4;
    pixels[offset + 2] = 254;
    pixels[offset + 3] = 255;
}
