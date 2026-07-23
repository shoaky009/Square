using System.Buffers.Binary;
using System.Diagnostics;
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

var vp8Fixtures = new List<object>();
WriteVp8Fixture("vp8-solid-8x8.webp", 8, 8,
    "color=c=0xCC3311:s=8x8:r=1:d=1", 75);
WriteVp8Fixture("vp8-gradient-17x13.webp", 17, 13,
    "nullsrc=s=17x13:r=1:d=1,geq=r='255*X/(W-1)':g='255*Y/(H-1)':b='255*(X+Y)/(W+H-2)'", 75);
WriteVp8Fixture("vp8-detail-31x19.webp", 31, 19,
    "nullsrc=s=31x19:r=1:d=1,geq=r='mod(X*37+Y*17+X*Y*3,256)':g='mod(X*11+Y*53+X*Y*5,256)':b='if(lt(mod(floor(X/2)+floor(Y/2),2),1),24,232)'", 55);
File.WriteAllText(Path.Combine(root, "vp8-manifest.json"), JsonSerializer.Serialize(new { fixtures = vp8Fixtures },
    new JsonSerializerOptions { WriteIndented = true }));

var vp8AlphaFixtures = new List<object>();
WriteVp8AlphaFixture("vp8-alpha-gradient-17x15.webp", 17, 15, BuildAlphaGradient(17, 15), 72,
    "ffmpeg bgra alpha channel");
WriteVp8AlphaFixture("vp8-alpha-checker-detail-29x21.webp", 29, 21, BuildAlphaCheckerDetail(29, 21), 58,
    "ffmpeg bgra alpha channel");
File.WriteAllText(Path.Combine(root, "vp8-alpha-manifest.json"),
    JsonSerializer.Serialize(new { fixtures = vp8AlphaFixtures },
        new JsonSerializerOptions { WriteIndented = true }));

var animatedLossyRoot = Path.Combine(root, "animated-lossy-webp");
Directory.CreateDirectory(animatedLossyRoot);
var animatedLossyFixtures = new List<object>();
WriteAnimatedLossyFixture("opaque-partial-dispose.webp", 32, 20, 0xFF201008, 4,
[
    new LossyAnimationFrame(0, 0, 20, 14, 70, true, true,
        BuildOpaqueFrame(20, 14, 11), 68, false),
    new LossyAnimationFrame(14, 8, 18, 12, 130, false, true,
        BuildOpaqueFrame(18, 12, 73), 61, false)
]);
WriteAnimatedLossyFixture("mixed-alpha-partial-dispose.webp", 30, 18, 0x00000000, 0,
[
    new LossyAnimationFrame(0, 2, 22, 14, 90, false, true,
        BuildOpaqueFrame(22, 14, 29), 66, false),
    new LossyAnimationFrame(12, 4, 18, 12, 110, true, false,
        BuildTransparentFrame(18, 12, 47), 64, true),
    new LossyAnimationFrame(4, 8, 10, 8, 60, false, false,
        BuildOpaqueFrame(10, 8, 101), 70, false)
]);
File.WriteAllText(Path.Combine(animatedLossyRoot, "manifest.json"),
    JsonSerializer.Serialize(new
    {
        generator = "ffmpeg libwebp_anim one-frame encodes; deterministic repository ANMF muxer",
        colorGoldenSource = "ffmpeg-decoded yuv420p converted with Square's deterministic VP8 scalar YUV-to-BGRA formula",
        alphaGoldenSource = "straight alpha from ffmpeg BGRA decode of each one-frame ALPH+VP8 WebP",
        compositing = "WebP ANMF background, blend, and dispose-to-background rules in straight-alpha BGRA",
        fixtures = animatedLossyFixtures
    }, new JsonSerializerOptions { WriteIndented = true }));

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

void WriteVp8Fixture(string file, int width, int height, string input, int quality)
{
    var path = Path.Combine(root, file);
    RunFfmpeg(["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", input,
        "-frames:v", "1", "-c:v", "libwebp", "-lossless", "0", "-quality", quality.ToString(),
        "-compression_level", "6", "-preset", "picture", "-pix_fmt", "yuv420p", "-map_metadata", "-1",
        "-y", path]);
    var yuvPath = Path.Combine(Path.GetTempPath(), $"square-vp8-{Guid.NewGuid():N}.yuv");
    try
    {
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo",
            "-pix_fmt", "yuv420p", "-y", yuvPath]);
        var golden = Path.ChangeExtension(file, ".bgra");
        var encoded = File.ReadAllBytes(path);
        var pixels = ConvertVp8YuvToBgra(File.ReadAllBytes(yuvPath), width, height);
        File.WriteAllBytes(Path.Combine(root, golden), pixels);
        vp8Fixtures.Add(new
        {
            file,
            sha256 = Hash(encoded),
            width,
            height,
            golden,
            goldenSha256 = Hash(pixels)
        });
    }
    finally
    {
        File.Delete(yuvPath);
    }
}

void WriteVp8AlphaFixture(string file, int width, int height, byte[] source, int quality, string alphaSource)
{
    var path = Path.Combine(root, file);
    var tempPrefix = Path.Combine(Path.GetTempPath(), $"square-vp8-alpha-{Guid.NewGuid():N}");
    var sourcePath = tempPrefix + ".bgra";
    var yuvPath = tempPrefix + ".yuv";
    var decodedPath = tempPrefix + ".decoded.bgra";
    try
    {
        File.WriteAllBytes(sourcePath, source);
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra",
            "-video_size", $"{width}x{height}", "-framerate", "1", "-i", sourcePath, "-frames:v", "1",
            "-c:v", "libwebp", "-lossless", "0", "-quality", quality.ToString(), "-compression_level", "6",
            "-preset", "picture", "-pix_fmt", "yuva420p", "-map_metadata", "-1", "-y", path]);
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo",
            "-pix_fmt", "yuv420p", "-y", yuvPath]);
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo",
            "-pix_fmt", "bgra", "-y", decodedPath]);

        var encoded = File.ReadAllBytes(path);
        var pixels = ConvertVp8YuvToBgra(File.ReadAllBytes(yuvPath), width, height);
        var externallyDecoded = File.ReadAllBytes(decodedPath);
        if (externallyDecoded.Length != pixels.Length)
            throw new InvalidDataException("Unexpected decoded WebP BGRA length.");
        for (var offset = 3; offset < pixels.Length; offset += 4) pixels[offset] = externallyDecoded[offset];

        var golden = Path.ChangeExtension(file, ".bgra");
        File.WriteAllBytes(Path.Combine(root, golden), pixels);
        vp8AlphaFixtures.Add(new
        {
            file,
            sha256 = Hash(encoded),
            width,
            height,
            golden,
            goldenSha256 = Hash(pixels),
            alpha = true,
            alphaSource
        });
    }
    finally
    {
        File.Delete(sourcePath);
        File.Delete(yuvPath);
        File.Delete(decodedPath);
    }
}

void WriteAnimatedLossyFixture(string file, int width, int height, uint background, ushort loopCount,
    LossyAnimationFrame[] frameSpecs)
{
    var encodedFrames = new List<EncodedLossyAnimationFrame>();
    foreach (var frame in frameSpecs)
    {
        if ((frame.X & 1) != 0 || (frame.Y & 1) != 0)
            throw new InvalidOperationException("WebP animation frame offsets must be even.");
        if (frame.Source.Length != checked(frame.Width * frame.Height * 4))
            throw new InvalidOperationException("Animation frame source does not match its dimensions.");
        encodedFrames.Add(EncodeLossyAnimationFrame(frame));
    }

    var encoded = BuildAnimatedLossyWebp(width, height, background, loopCount, encodedFrames);
    File.WriteAllBytes(Path.Combine(animatedLossyRoot, file), encoded);
    var composited = CompositeLossyAnimation(width, height, background, encodedFrames);
    var frames = new List<object>();
    for (var index = 0; index < encodedFrames.Count; index++)
    {
        var frame = encodedFrames[index];
        var golden = $"{file}.frame{index}.bgra";
        File.WriteAllBytes(Path.Combine(animatedLossyRoot, golden), composited[index]);
        frames.Add(new
        {
            index,
            x = frame.Spec.X,
            y = frame.Spec.Y,
            width = frame.Spec.Width,
            height = frame.Spec.Height,
            durationMilliseconds = frame.Spec.DurationMilliseconds,
            disposeToBackground = frame.Spec.DisposeToBackground,
            blend = frame.Spec.NoBlend ? "replace" : "alpha-over",
            chunks = frame.Chunks.Select(chunk => new
            {
                type = chunk.Type,
                length = chunk.Data.Length,
                sha256 = Hash(chunk.Data)
            }),
            golden,
            goldenSha256 = Hash(composited[index])
        });
    }
    animatedLossyFixtures.Add(new
    {
        file,
        sha256 = Hash(encoded),
        width,
        height,
        backgroundBgra = $"{background:x8}",
        loopCount,
        frames
    });
}

EncodedLossyAnimationFrame EncodeLossyAnimationFrame(LossyAnimationFrame frame)
{
    var tempPrefix = Path.Combine(Path.GetTempPath(), $"square-animated-vp8-{Guid.NewGuid():N}");
    var sourcePath = tempPrefix + ".bgra";
    var webpPath = tempPrefix + ".webp";
    var yuvPath = tempPrefix + ".yuv";
    var decodedPath = tempPrefix + ".decoded.bgra";
    try
    {
        File.WriteAllBytes(sourcePath, frame.Source);
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra",
            "-video_size", $"{frame.Width}x{frame.Height}", "-framerate", "1", "-i", sourcePath,
            "-frames:v", "1", "-c:v", "libwebp_anim", "-lossless", "0", "-quality",
            frame.Quality.ToString(), "-preset", "picture", "-pix_fmt", frame.HasAlpha ? "yuva420p" : "yuv420p",
            "-map_metadata", "-1", "-y", webpPath]);
        RunFfmpeg(["-hide_banner", "-loglevel", "error", "-i", webpPath, "-frames:v", "1",
            "-f", "rawvideo", "-pix_fmt", "yuv420p", "-y", yuvPath]);
        if (frame.HasAlpha)
            RunFfmpeg(["-hide_banner", "-loglevel", "error", "-i", webpPath, "-frames:v", "1",
                "-f", "rawvideo", "-pix_fmt", "bgra", "-y", decodedPath]);

        var chunks = ReadStaticLossyChunks(File.ReadAllBytes(webpPath));
        var expectedTypes = frame.HasAlpha ? new[] { "ALPH", "VP8 " } : new[] { "VP8 " };
        if (!chunks.Select(static chunk => chunk.Type).SequenceEqual(expectedTypes))
            throw new InvalidDataException($"Unexpected one-frame WebP chunks: {string.Join(", ", chunks.Select(static c => c.Type))}.");
        var dimensions = ReadVp8Dimensions(chunks[^1].Data);
        if (dimensions != (frame.Width, frame.Height))
            throw new InvalidDataException("Encoded VP8 dimensions do not match the animation frame.");

        var pixels = ConvertVp8YuvToBgra(File.ReadAllBytes(yuvPath), frame.Width, frame.Height);
        if (frame.HasAlpha)
        {
            var decoded = File.ReadAllBytes(decodedPath);
            if (decoded.Length != pixels.Length) throw new InvalidDataException("Unexpected decoded frame length.");
            for (var offset = 3; offset < pixels.Length; offset += 4)
                pixels[offset] = decoded[offset];
        }
        return new EncodedLossyAnimationFrame(frame, chunks, pixels);
    }
    finally
    {
        File.Delete(sourcePath);
        File.Delete(webpPath);
        File.Delete(yuvPath);
        File.Delete(decodedPath);
    }
}

static List<WebpPayloadChunk> ReadStaticLossyChunks(byte[] webp)
{
    if (webp.Length < 12 || !webp.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
        !webp.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        throw new InvalidDataException("Invalid one-frame WebP output.");
    var result = new List<WebpPayloadChunk>();
    var offset = 12;
    while (offset < webp.Length)
    {
        if (offset > webp.Length - 8) throw new InvalidDataException("Truncated one-frame WebP chunk.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(webp.AsSpan(offset + 4, 4));
        if (length < 0 || offset + 8L + length + (length & 1) > webp.Length)
            throw new InvalidDataException("Invalid one-frame WebP chunk length.");
        var type = System.Text.Encoding.ASCII.GetString(webp, offset, 4);
        if (type is "ALPH" or "VP8 ")
            result.Add(new WebpPayloadChunk(type, webp.AsSpan(offset + 8, length).ToArray()));
        offset += 8 + length + (length & 1);
    }
    return result;
}

static byte[] BuildAnimatedLossyWebp(int width, int height, uint background, ushort loopCount,
    List<EncodedLossyAnimationFrame> frames)
{
    using var body = new MemoryStream();
    var vp8x = new byte[10];
    vp8x[0] = (byte)(2 | (frames.Any(static frame => frame.Spec.HasAlpha) ? 0x10 : 0));
    WriteUInt24(vp8x.AsSpan(4, 3), width - 1);
    WriteUInt24(vp8x.AsSpan(7, 3), height - 1);
    WriteWebpChunk(body, "VP8X", vp8x);
    var anim = new byte[6];
    BinaryPrimitives.WriteUInt32LittleEndian(anim.AsSpan(0, 4), background);
    BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(4, 2), loopCount);
    WriteWebpChunk(body, "ANIM", anim);
    foreach (var frame in frames)
    {
        using var payload = new MemoryStream();
        var header = new byte[16];
        WriteUInt24(header.AsSpan(0, 3), frame.Spec.X / 2);
        WriteUInt24(header.AsSpan(3, 3), frame.Spec.Y / 2);
        WriteUInt24(header.AsSpan(6, 3), frame.Spec.Width - 1);
        WriteUInt24(header.AsSpan(9, 3), frame.Spec.Height - 1);
        WriteUInt24(header.AsSpan(12, 3), frame.Spec.DurationMilliseconds);
        header[15] = (byte)((frame.Spec.DisposeToBackground ? 1 : 0) | (frame.Spec.NoBlend ? 2 : 0));
        payload.Write(header);
        foreach (var chunk in frame.Chunks) WriteWebpChunk(payload, chunk.Type, chunk.Data);
        WriteWebpChunk(body, "ANMF", payload.ToArray());
    }
    var result = new byte[checked(12 + (int)body.Length)];
    "RIFF"u8.CopyTo(result);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)(result.Length - 8));
    "WEBP"u8.CopyTo(result.AsSpan(8));
    body.ToArray().CopyTo(result, 12);
    return result;
}

static byte[][] CompositeLossyAnimation(int width, int height, uint background,
    List<EncodedLossyAnimationFrame> frames)
{
    var canvas = new byte[checked(width * height * 4)];
    FillRectangle(canvas, width, 0, 0, width, height, background);
    var result = new byte[frames.Count][];
    EncodedLossyAnimationFrame? previous = null;
    for (var index = 0; index < frames.Count; index++)
    {
        if (previous is { Spec.DisposeToBackground: true } disposed)
            FillRectangle(canvas, width, disposed.Spec.X, disposed.Spec.Y, disposed.Spec.Width,
                disposed.Spec.Height, background);
        var frame = frames[index];
        for (var y = 0; y < frame.Spec.Height; y++)
        for (var x = 0; x < frame.Spec.Width; x++)
        {
            var sourceOffset = (y * frame.Spec.Width + x) * 4;
            var destinationOffset = ((frame.Spec.Y + y) * width + frame.Spec.X + x) * 4;
            if (frame.Spec.NoBlend)
                frame.Pixels.AsSpan(sourceOffset, 4).CopyTo(canvas.AsSpan(destinationOffset, 4));
            else
                AlphaOver(frame.Pixels.AsSpan(sourceOffset, 4), canvas.AsSpan(destinationOffset, 4));
        }
        result[index] = (byte[])canvas.Clone();
        previous = frame;
    }
    return result;
}

static void AlphaOver(ReadOnlySpan<byte> source, Span<byte> destination)
{
    var sourceAlpha = source[3];
    if (sourceAlpha == 255) { source.CopyTo(destination); return; }
    if (sourceAlpha == 0) return;
    var destinationAlpha = destination[3];
    var outputAlpha = sourceAlpha + (destinationAlpha * (255 - sourceAlpha) + 127) / 255;
    for (var channel = 0; channel < 3; channel++)
    {
        var premultiplied = source[channel] * sourceAlpha +
            destination[channel] * destinationAlpha * (255 - sourceAlpha) / 255;
        destination[channel] = (byte)((premultiplied + outputAlpha / 2) / outputAlpha);
    }
    destination[3] = (byte)outputAlpha;
}

static void FillRectangle(byte[] pixels, int canvasWidth, int x, int y, int width, int height, uint color)
{
    for (var py = y; py < y + height; py++)
    for (var px = x; px < x + width; px++)
    {
        var offset = (py * canvasWidth + px) * 4;
        pixels[offset] = (byte)color;
        pixels[offset + 1] = (byte)(color >> 8);
        pixels[offset + 2] = (byte)(color >> 16);
        pixels[offset + 3] = (byte)(color >> 24);
    }
}

static byte[] BuildOpaqueFrame(int width, int height, int seed)
{
    var pixels = new byte[checked(width * height * 4)];
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = (byte)((seed + x * 31 + y * 17 + x * y * 3) & 255);
        pixels[offset + 1] = (byte)((seed * 3 + x * 7 + y * 47 + x * y * 5) & 255);
        pixels[offset + 2] = (byte)((seed * 5 + x * 43 + y * 11 + x * y * 7) & 255);
        pixels[offset + 3] = 255;
    }
    return pixels;
}

static byte[] BuildTransparentFrame(int width, int height, int seed)
{
    var pixels = BuildOpaqueFrame(width, height, seed);
    var denominator = width - 1 + height - 1;
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var alpha = (255 * (x + y) + denominator / 2) / denominator;
        if (((x / 3 + y / 2) & 3) == 0) alpha = 0;
        else if (x == width - 1 && y == height - 1) alpha = 255;
        pixels[(y * width + x) * 4 + 3] = (byte)alpha;
    }
    return pixels;
}

static (int Width, int Height) ReadVp8Dimensions(ReadOnlySpan<byte> vp8)
{
    if (vp8.Length < 10 || !vp8.Slice(3, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A }))
        throw new InvalidDataException("Invalid VP8 keyframe header.");
    return (BinaryPrimitives.ReadUInt16LittleEndian(vp8.Slice(6, 2)) & 0x3FFF,
        BinaryPrimitives.ReadUInt16LittleEndian(vp8.Slice(8, 2)) & 0x3FFF);
}

static void WriteWebpChunk(Stream stream, string type, ReadOnlySpan<byte> data)
{
    stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
    Span<byte> length = stackalloc byte[4];
    BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
    stream.Write(length);
    stream.Write(data);
    if ((data.Length & 1) != 0) stream.WriteByte(0);
}

static void WriteUInt24(Span<byte> destination, int value)
{
    destination[0] = (byte)value;
    destination[1] = (byte)(value >> 8);
    destination[2] = (byte)(value >> 16);
}

static byte[] BuildAlphaGradient(int width, int height)
{
    var pixels = new byte[checked(width * height * 4)];
    var alphaDenominator = width - 1 + 2 * (height - 1);
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = (byte)((x * 19 + y * 31 + x * y * 3) & 255);
        pixels[offset + 1] = (byte)((x * 47 + y * 13 + x * y * 5) & 255);
        pixels[offset + 2] = (byte)((x * 7 + y * 61 + x * y * 11) & 255);
        pixels[offset + 3] = (byte)((255 * (x + 2 * y) + alphaDenominator / 2) / alphaDenominator);
    }
    return pixels;
}

static byte[] BuildAlphaCheckerDetail(int width, int height)
{
    var pixels = new byte[checked(width * height * 4)];
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = (byte)((x * 43 + y * 17 + x * y * 7) & 255);
        pixels[offset + 1] = (byte)((x * 11 + y * 59 + x * y * 3) & 255);
        pixels[offset + 2] = (byte)((x * 71 + y * 23 + x * y * 5) & 255);
        pixels[offset + 3] = (byte)((((x / 3) + (y / 2)) & 1) == 0 ? 0 : 255);
    }
    return pixels;
}

static void RunFfmpeg(IEnumerable<string> arguments)
{
    var startInfo = new ProcessStartInfo("ffmpeg")
    {
        UseShellExecute = false
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffmpeg.");
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.");
}

static byte[] ConvertVp8YuvToBgra(byte[] source, int width, int height)
{
    var ySize = checked(width * height);
    var chromaWidth = (width + 1) / 2;
    var chromaHeight = (height + 1) / 2;
    var chromaSize = checked(chromaWidth * chromaHeight);
    if (source.Length != ySize + 2 * chromaSize) throw new InvalidDataException("Unexpected VP8 YUV plane length.");
    var output = new byte[checked(ySize * 4)];
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var yy = source[y * width + x];
        var chroma = (y / 2) * chromaWidth + x / 2;
        var u = source[ySize + chroma];
        var v = source[ySize + chromaSize + chroma];
        var offset = (y * width + x) * 4;
        output[offset] = Blue(yy, u);
        output[offset + 1] = Green(yy, u, v);
        output[offset + 2] = Red(yy, v);
        output[offset + 3] = 255;
    }
    return output;
}

static byte Red(int y, int v) => ClipYuv(MultiplyHigh(y, 19077) + MultiplyHigh(v, 26149) - 14234);
static byte Green(int y, int u, int v) => ClipYuv(
    MultiplyHigh(y, 19077) - MultiplyHigh(u, 6419) - MultiplyHigh(v, 13320) + 8708);
static byte Blue(int y, int u) => ClipYuv(MultiplyHigh(y, 19077) + MultiplyHigh(u, 33050) - 17685);
static int MultiplyHigh(int value, int coefficient) => value * coefficient >> 8;
static byte ClipYuv(int value) => (byte)((value & ~16383) == 0 ? value >> 6 : value < 0 ? 0 : 255);
static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

static void SetRed(byte[] pixels, int width, int x, int y)
{
    var offset = (y * width + x) * 4;
    pixels[offset + 2] = 254;
    pixels[offset + 3] = 255;
}

readonly record struct LossyAnimationFrame(int X, int Y, int Width, int Height, int DurationMilliseconds,
    bool DisposeToBackground, bool NoBlend, byte[] Source, int Quality, bool HasAlpha);
readonly record struct WebpPayloadChunk(string Type, byte[] Data);
sealed record EncodedLossyAnimationFrame(LossyAnimationFrame Spec, List<WebpPayloadChunk> Chunks, byte[] Pixels);
