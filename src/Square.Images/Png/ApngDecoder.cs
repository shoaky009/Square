using System.Buffers.Binary;
using Square.Graphics;

namespace Square.Images.Png;

internal static class ApngDecoder
{
    private const uint IHDR = 0x49484452, PLTE = 0x504C5445, IDAT = 0x49444154, IEND = 0x49454E44,
        tRNS = 0x74524E53, acTL = 0x6163544C, fcTL = 0x6663544C, fdAT = 0x66644154;

    public static bool IsAnimated(ReadOnlySpan<byte> data)
    {
        if (!data.StartsWith(PngDecoder.Signature)) return false;
        var offset = PngDecoder.Signature.Length;
        while (offset <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > int.MaxValue || offset + 12L + length > data.Length) return false;
            var type = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 4, 4));
            if (type == acTL) return true;
            if (type is IDAT or IEND) return false;
            offset = checked((int)(offset + 12L + length));
        }
        return false;
    }

    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (!data.StartsWith(PngDecoder.Signature)) throw new InvalidDataException("Invalid PNG signature.");
        var state = new State();
        using var defaultData = new MemoryStream();
        FrameControl? current = null;
        MemoryStream? currentData = null;
        var frames = new List<EncodedFrame>();
        var offset = PngDecoder.Signature.Length;
        uint expectedSequence = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 12) throw new InvalidDataException("APNG chunk is truncated.");
            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (lengthValue > int.MaxValue || lengthValue > options.MaxChunkBytes)
                throw new InvalidDataException("APNG chunk exceeds the configured limit.");
            var length = (int)lengthValue;
            var end = checked((long)offset + 12 + length);
            if (end > data.Length) throw new InvalidDataException("APNG chunk data is truncated.");
            var typeBytes = data.Slice(offset + 4, 4);
            var chunk = data.Slice(offset + 8, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + length, 4));
            ValidateCrc(typeBytes, chunk, expectedCrc, options.PngCrcPolicy);
            var type = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);
            switch (type)
            {
                case IHDR:
                    if (state.HasHeader || length != 13) throw new InvalidDataException("Invalid APNG IHDR chunk.");
                    state.Width = ReadDimension(chunk[0..4]); state.Height = ReadDimension(chunk[4..8]);
                    state.BitDepth = chunk[8]; state.ColorType = chunk[9]; state.Interlace = chunk[12];
                    if (chunk[10] != 0 || chunk[11] != 0 || state.Interlace > 1)
                        throw new InvalidDataException("Unsupported APNG compression, filter, or interlace method.");
                    options.ValidateDimensions(state.Width, state.Height); state.HasHeader = true;
                    break;
                case PLTE:
                    if (!state.HasHeader || state.SeenImageData || state.Palette != null) throw new InvalidDataException("Invalid APNG PLTE order.");
                    state.Palette = chunk.ToArray();
                    break;
                case tRNS:
                    if (!state.HasHeader || state.SeenImageData || state.Transparency != null) throw new InvalidDataException("Invalid APNG tRNS order.");
                    state.Transparency = chunk.ToArray();
                    break;
                case acTL:
                    if (!state.HasHeader || state.SeenImageData || state.HasAnimation || length != 8)
                        throw new InvalidDataException("Invalid APNG acTL chunk.");
                    state.FrameCount = BinaryPrimitives.ReadUInt32BigEndian(chunk[0..4]);
                    state.PlayCount = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..8]);
                    if (state.FrameCount == 0 || state.FrameCount > options.MaxItemCount)
                        throw new InvalidDataException("APNG frame count exceeds the configured limit.");
                    state.HasAnimation = true;
                    break;
                case fcTL:
                    if (!state.HasAnimation || length != 26) throw new InvalidDataException("Invalid APNG fcTL chunk.");
                    FinishCurrent();
                    var sequence = BinaryPrimitives.ReadUInt32BigEndian(chunk[0..4]);
                    if (sequence != expectedSequence++) throw new InvalidDataException("APNG sequence number is invalid.");
                    current = ParseControl(chunk, state.Width, state.Height);
                    currentData = new MemoryStream();
                    break;
                case IDAT:
                    if (!state.HasAnimation) throw new InvalidDataException("APNG is missing acTL before IDAT.");
                    state.SeenImageData = true;
                    defaultData.Write(chunk);
                    if (current != null && frames.Count == 0) currentData!.Write(chunk);
                    break;
                case fdAT:
                    if (current == null || length < 4) throw new InvalidDataException("APNG fdAT has no frame control.");
                    state.SeenImageData = true;
                    var dataSequence = BinaryPrimitives.ReadUInt32BigEndian(chunk[0..4]);
                    if (dataSequence != expectedSequence++) throw new InvalidDataException("APNG sequence number is invalid.");
                    currentData!.Write(chunk[4..]);
                    break;
                case IEND:
                    if (length != 0) throw new InvalidDataException("Invalid APNG IEND chunk.");
                    FinishCurrent();
                    if ((uint)frames.Count != state.FrameCount) throw new InvalidDataException("APNG frame count does not match acTL.");
                    offset = checked((int)end);
                    if (offset != data.Length) throw new InvalidDataException("APNG contains trailing data.");
                    return Compose(state, frames, options);
                default:
                    if ((typeBytes[0] & 0x20) == 0) throw new InvalidDataException("APNG contains an unsupported critical chunk.");
                    break;
            }
            offset = checked((int)end);
        }
        throw new InvalidDataException("APNG is missing IEND.");

        void FinishCurrent()
        {
            if (current == null) return;
            if (currentData == null || currentData.Length == 0) throw new InvalidDataException("APNG frame contains no image data.");
            frames.Add(new EncodedFrame(current.Value, currentData.ToArray()));
            currentData.Dispose(); currentData = null; current = null;
        }
    }

    private static ImageDocument Compose(State state, List<EncodedFrame> frames, ImageDecoderOptions options)
    {
        var canvas = new Bitmap(state.Width, state.Height);
        Bitmap? restore = null;
        FrameControl? previous = null;
        var items = new ImageItem[frames.Count];
        long totalBytes = 0; long totalTicks = 0;
        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                if (previous != null) ApplyDisposal(canvas, previous.Value, restore);
                restore?.Dispose(); restore = null;
                var encoded = frames[i];
                if (encoded.Control.Dispose == 2) restore = Clone(canvas);
                Bitmap frame;
                try
                {
                    frame = PngDecoder.DecodeFrame(encoded.Control.Width, encoded.Control.Height,
                        state.BitDepth, state.ColorType, state.Interlace, state.Palette, state.Transparency,
                        encoded.Data, options);
                }
                catch (InvalidDataException error)
                {
                    throw new InvalidDataException($"APNG frame {i} could not be decoded.", error);
                }
                using (frame)
                {
                    Blend(canvas, frame, encoded.Control);
                }
                var snapshot = Clone(canvas);
                totalBytes = checked(totalBytes + snapshot.Pixels.Length);
                if (totalBytes > options.MaxTotalDecodedBytes)
                {
                    snapshot.Dispose();
                    throw new InvalidDataException("APNG decoded frames exceed the configured total byte limit.");
                }
                var duration = Duration(encoded.Control.DelayNumerator, encoded.Control.DelayDenominator);
                totalTicks = checked(totalTicks + duration.Ticks);
                items[i] = new ImageItem(i, snapshot, 32, duration);
                previous = encoded.Control;
            }
            canvas.Dispose(); restore?.Dispose();
            var animation = new ImageAnimationInfo(state.PlayCount == 0,
                state.PlayCount == 0 ? 0 : checked((int)state.PlayCount), TimeSpan.FromTicks(totalTicks));
            return new ImageDocument(ImageFormat.Png, frames.Count > 1 ? ImageDocumentKind.Animation : ImageDocumentKind.Still,
                items, 0, frames.Count > 1 ? animation : null);
        }
        catch
        {
            canvas.Dispose(); restore?.Dispose();
            foreach (var item in items) item?.Dispose();
            throw;
        }
    }

    private static FrameControl ParseControl(ReadOnlySpan<byte> chunk, int canvasWidth, int canvasHeight)
    {
        var width = ReadDimension(chunk[4..8]); var height = ReadDimension(chunk[8..12]);
        var x = checked((int)BinaryPrimitives.ReadUInt32BigEndian(chunk[12..16]));
        var y = checked((int)BinaryPrimitives.ReadUInt32BigEndian(chunk[16..20]));
        if ((long)x + width > canvasWidth || (long)y + height > canvasHeight)
            throw new InvalidDataException("APNG frame rectangle is outside the canvas.");
        var dispose = chunk[24]; var blend = chunk[25];
        if (dispose > 2 || blend > 1) throw new InvalidDataException("APNG frame operation is invalid.");
        return new FrameControl(width, height, x, y, BinaryPrimitives.ReadUInt16BigEndian(chunk[20..22]),
            BinaryPrimitives.ReadUInt16BigEndian(chunk[22..24]), dispose, blend);
    }

    private static void Blend(Bitmap canvas, Bitmap frame, FrameControl control)
    {
        for (var y = 0; y < control.Height; y++)
            for (var x = 0; x < control.Width; x++)
            {
                var source = frame.GetPixel(x, y);
                var destination = canvas.GetPixel(control.X + x, control.Y + y);
                if (control.Blend == 0) source.CopyTo(destination);
                else AlphaOver(source, destination);
            }
    }

    private static void ApplyDisposal(Bitmap canvas, FrameControl frame, Bitmap? restore)
    {
        if (frame.Dispose == 0) return;
        if (frame.Dispose == 2 && restore != null) { restore.Pixels.CopyTo(canvas.Pixels, 0); return; }
        for (var y = 0; y < frame.Height; y++) canvas.GetRow(frame.Y + y).Slice(frame.X * 4, frame.Width * 4).Clear();
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

    private static Bitmap Clone(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height); source.Pixels.CopyTo(result.Pixels, 0); return result;
    }

    private static TimeSpan Duration(ushort numerator, ushort denominator)
        => TimeSpan.FromSeconds((double)numerator / (denominator == 0 ? 100 : denominator));
    private static int ReadDimension(ReadOnlySpan<byte> value)
    {
        var result = BinaryPrimitives.ReadUInt32BigEndian(value);
        if (result is 0 or > int.MaxValue) throw new InvalidDataException("Invalid APNG dimensions.");
        return (int)result;
    }
    private static void ValidateCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data, uint expected, PngCrcPolicy policy)
    {
        if (policy == PngCrcPolicy.Ignore || policy == PngCrcPolicy.CriticalChunksOnly && (type[0] & 0x20) != 0) return;
        if (Crc32.Compute(type, data) != expected) throw new InvalidDataException("APNG chunk CRC mismatch.");
    }

    private sealed class State
    {
        public int Width, Height, BitDepth, ColorType, Interlace;
        public bool HasHeader, HasAnimation, SeenImageData;
        public uint FrameCount, PlayCount;
        public byte[]? Palette, Transparency;
    }
    private readonly record struct FrameControl(int Width, int Height, int X, int Y, ushort DelayNumerator,
        ushort DelayDenominator, byte Dispose, byte Blend);
    private readonly record struct EncodedFrame(FrameControl Control, byte[] Data);
}
