using System.Buffers.Binary;
using Square.Graphics;

namespace Square.Images.Webp;

internal static class WebpDecoder
{
    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        ValidateHeader(data, out var end);
        var offset = 12;
        int? canvasWidth = null, canvasHeight = null;
        var animated = false;
        byte[]? stillImage = null;
        uint background = 0;
        ushort loopCount = 1;
        var hasAnim = false;
        var frames = new List<WebpFrame>();
        while (offset < end)
        {
            var chunk = ReadChunk(data, ref offset, end, options);
            if (chunk.Type.SequenceEqual("VP8X"u8))
            {
                if (chunk.Data.Length != 10 || canvasWidth != null) throw new InvalidDataException("Invalid WebP VP8X chunk.");
                var flags = chunk.Data[0];
                if ((flags & 0xC1) != 0 || chunk.Data[1] != 0 || chunk.Data[2] != 0 || chunk.Data[3] != 0)
                    throw new InvalidDataException("Invalid WebP VP8X flags.");
                animated = (flags & 2) != 0;
                canvasWidth = UInt24(chunk.Data[4..7]) + 1; canvasHeight = UInt24(chunk.Data[7..10]) + 1;
                options.ValidateDimensions(canvasWidth.Value, canvasHeight.Value);
            }
            else if (chunk.Type.SequenceEqual("ANIM"u8))
            {
                if (!animated || hasAnim || chunk.Data.Length != 6 || frames.Count != 0)
                    throw new InvalidDataException("Invalid WebP ANIM chunk.");
                background = BinaryPrimitives.ReadUInt32LittleEndian(chunk.Data[0..4]);
                loopCount = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Data[4..6]);
                hasAnim = true;
            }
            else if (chunk.Type.SequenceEqual("ANMF"u8))
            {
                if (!animated || !hasAnim || canvasWidth == null || chunk.Data.Length < 24)
                    throw new InvalidDataException("Invalid WebP ANMF chunk.");
                if (frames.Count >= options.MaxItemCount) throw new InvalidDataException("WebP frame count exceeds the configured limit.");
                frames.Add(ParseFrame(chunk.Data, canvasWidth.Value, canvasHeight!.Value, options));
            }
            else if (chunk.Type.SequenceEqual("VP8L"u8))
            {
                if (animated || stillImage != null) throw new InvalidDataException("WebP contains an unexpected VP8L bitstream.");
                stillImage = chunk.Data.ToArray();
            }
            else if (chunk.Type.SequenceEqual("VP8 "u8) || chunk.Type.SequenceEqual("ALPH"u8))
                throw new InvalidDataException("Lossy WebP and separate ALPH chunks are not supported yet.");
        }

        if (animated)
        {
            if (!hasAnim || frames.Count == 0) throw new InvalidDataException("Animated WebP is missing animation frames.");
            return Compose(canvasWidth!.Value, canvasHeight!.Value, background, loopCount, frames, options);
        }
        if (stillImage == null) throw new InvalidDataException("WebP is missing a VP8L image chunk.");
        var bitmap = Vp8LDecoder.Decode(stillImage, options);
        if (canvasWidth != null && (bitmap.Width != canvasWidth || bitmap.Height != canvasHeight))
        {
            bitmap.Dispose(); throw new InvalidDataException("WebP canvas does not match its VP8L image.");
        }
        return new ImageDocument(ImageFormat.Webp, ImageDocumentKind.Still,
            [new ImageItem(0, bitmap, 32, TimeSpan.Zero)], 0);
    }

    private static WebpFrame ParseFrame(ReadOnlySpan<byte> data, int canvasWidth, int canvasHeight,
        ImageDecoderOptions options)
    {
        var x = checked(UInt24(data[0..3]) * 2); var y = checked(UInt24(data[3..6]) * 2);
        var width = UInt24(data[6..9]) + 1; var height = UInt24(data[9..12]) + 1;
        var duration = UInt24(data[12..15]); var flags = data[15];
        if ((flags & 0xFC) != 0) throw new InvalidDataException("WebP ANMF flags are invalid.");
        if ((long)x + width > canvasWidth || (long)y + height > canvasHeight)
            throw new InvalidDataException("WebP frame rectangle is outside the canvas.");
        var offset = 16;
        byte[]? vp8l = null;
        while (offset < data.Length)
        {
            var chunk = ReadChunk(data, ref offset, data.Length, options);
            if (chunk.Type.SequenceEqual("VP8L"u8))
            {
                if (vp8l != null) throw new InvalidDataException("WebP frame contains multiple VP8L chunks.");
                vp8l = chunk.Data.ToArray();
            }
            else if (chunk.Type.SequenceEqual("VP8 "u8) || chunk.Type.SequenceEqual("ALPH"u8))
                throw new InvalidDataException("Animated lossy WebP frames are not supported yet.");
        }
        if (vp8l == null) throw new InvalidDataException("WebP frame is missing a VP8L chunk.");
        var bitmap = Vp8LDecoder.Decode(vp8l, options);
        if (bitmap.Width != width || bitmap.Height != height)
        {
            bitmap.Dispose(); throw new InvalidDataException("WebP frame dimensions do not match ANMF.");
        }
        return new WebpFrame(x, y, width, height, duration, (flags & 1) != 0, (flags & 2) == 0, bitmap);
    }

    private static ImageDocument Compose(int width, int height, uint background, ushort loopCount,
        List<WebpFrame> frames, ImageDecoderOptions options)
    {
        var canvas = new Bitmap(width, height);
        Fill(canvas, background);
        var items = new ImageItem[frames.Count];
        WebpFrame? previous = null;
        long totalBytes = 0, totalTicks = 0;
        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                if (previous is { DisposeToBackground: true } disposed)
                    FillRect(canvas, disposed.X, disposed.Y, disposed.Width, disposed.Height, background);
                var frame = frames[i];
                Blend(canvas, frame);
                var snapshot = Clone(canvas);
                totalBytes = checked(totalBytes + snapshot.Pixels.Length);
                if (totalBytes > options.MaxTotalDecodedBytes)
                {
                    snapshot.Dispose(); throw new InvalidDataException("WebP decoded frames exceed the configured total byte limit.");
                }
                var duration = TimeSpan.FromMilliseconds(frame.DurationMilliseconds);
                totalTicks = checked(totalTicks + duration.Ticks);
                items[i] = new ImageItem(i, snapshot, 32, duration);
                previous = frame;
            }
            canvas.Dispose();
            foreach (var frame in frames) frame.Bitmap.Dispose();
            var animation = new ImageAnimationInfo(loopCount == 0, loopCount == 0 ? 0 : loopCount,
                TimeSpan.FromTicks(totalTicks));
            return new ImageDocument(ImageFormat.Webp, frames.Count > 1 ? ImageDocumentKind.Animation : ImageDocumentKind.Still,
                items, 0, frames.Count > 1 ? animation : null);
        }
        catch
        {
            canvas.Dispose();
            foreach (var frame in frames) frame.Bitmap.Dispose();
            foreach (var item in items) item?.Dispose();
            throw;
        }
    }

    private static void Blend(Bitmap canvas, WebpFrame frame)
    {
        for (var y = 0; y < frame.Height; y++)
            for (var x = 0; x < frame.Width; x++)
            {
                var source = frame.Bitmap.GetPixel(x, y);
                var destination = canvas.GetPixel(frame.X + x, frame.Y + y);
                if (!frame.BlendAlpha) source.CopyTo(destination);
                else AlphaOver(source, destination);
            }
    }

    private static void AlphaOver(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var sa = source[3];
        if (sa == 255) { source.CopyTo(destination); return; }
        if (sa == 0) return;
        var da = destination[3];
        var outA = sa + (da * (255 - sa) + 127) / 255;
        for (var channel = 0; channel < 3; channel++)
        {
            var premul = source[channel] * sa + destination[channel] * da * (255 - sa) / 255;
            destination[channel] = (byte)((premul + outA / 2) / outA);
        }
        destination[3] = (byte)outA;
    }

    private static void Fill(Bitmap bitmap, uint color)
        => FillRect(bitmap, 0, 0, bitmap.Width, bitmap.Height, color);
    private static void FillRect(Bitmap bitmap, int x, int y, int width, int height, uint color)
    {
        var blue = (byte)color; var green = (byte)(color >> 8); var red = (byte)(color >> 16); var alpha = (byte)(color >> 24);
        for (var py = y; py < y + height; py++)
            for (var px = x; px < x + width; px++)
            {
                var pixel = bitmap.GetPixel(px, py);
                pixel[0] = blue; pixel[1] = green; pixel[2] = red; pixel[3] = alpha;
            }
    }
    private static Bitmap Clone(Bitmap source)
    {
        var bitmap = new Bitmap(source.Width, source.Height); source.Pixels.CopyTo(bitmap.Pixels, 0); return bitmap;
    }
    private static void ValidateHeader(ReadOnlySpan<byte> data, out long end)
    {
        if (data.Length < 20 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WEBP"u8))
            throw new InvalidDataException("Invalid WebP RIFF header.");
        end = checked(8L + BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]));
        if (end != data.Length) throw new InvalidDataException("WebP RIFF size does not match the input length.");
    }
    private static WebpChunk ReadChunk(ReadOnlySpan<byte> data, ref int offset, long end, ImageDecoderOptions options)
    {
        if (end - offset < 8) throw new InvalidDataException("WebP chunk header is truncated.");
        var type = data.Slice(offset, 4);
        var sizeValue = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
        if (sizeValue > int.MaxValue || sizeValue > options.MaxChunkBytes) throw new InvalidDataException("WebP chunk exceeds the configured limit.");
        var size = (int)sizeValue; var payloadStart = offset + 8;
        var payloadEnd = checked((long)payloadStart + size); var chunkEnd = checked(payloadEnd + (size & 1));
        if (chunkEnd > end) throw new InvalidDataException("WebP chunk is truncated.");
        if ((size & 1) != 0 && data[(int)payloadEnd] != 0) throw new InvalidDataException("WebP chunk padding must be zero.");
        var result = new WebpChunk(type, data.Slice(payloadStart, size));
        offset = checked((int)chunkEnd); return result;
    }
    private static int UInt24(ReadOnlySpan<byte> value) => value[0] | value[1] << 8 | value[2] << 16;

    private readonly ref struct WebpChunk
    {
        public ReadOnlySpan<byte> Type { get; }
        public ReadOnlySpan<byte> Data { get; }
        public WebpChunk(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data) { Type = type; Data = data; }
    }
    private sealed record WebpFrame(int X, int Y, int Width, int Height, int DurationMilliseconds,
        bool DisposeToBackground, bool BlendAlpha, Bitmap Bitmap);
}
