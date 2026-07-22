using System.Buffers.Binary;
using System.IO.Compression;
using Square.Graphics;

namespace Square.Images.Png;

internal static class PngDecoder
{
    internal static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly (int X, int Y, int Dx, int Dy)[] Adam7 =
    [
        (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4),
        (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)
    ];

    public static Bitmap Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (!data.StartsWith(Signature)) throw new InvalidDataException("Invalid PNG signature.");
        var state = ParseChunks(data, options);
        options.ValidateDimensions(state.Width, state.Height);
        return DecodePixels(state, options);
    }

    internal static Bitmap DecodeFrame(int width, int height, int bitDepth, int colorType, int interlace,
        byte[]? palette, byte[]? transparency, byte[] compressedData, ImageDecoderOptions options)
    {
        options.ValidateDimensions(width, height);
        if (!IsValidDepth(colorType, bitDepth)) throw new InvalidDataException("Invalid PNG color type and bit depth combination.");
        var state = new PngState
        {
            Width = width,
            Height = height,
            BitDepth = bitDepth,
            ColorType = colorType,
            Interlace = interlace,
            Palette = palette,
            Transparency = transparency,
            CompressedData = compressedData,
            HasHeader = true,
            HasPalette = palette != null,
            HasTransparency = transparency != null,
            HasIdat = true,
            HasEnd = true
        };
        ValidateState(state);
        return DecodePixels(state, options);
    }

    private static PngState ParseChunks(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var state = new PngState();
        using var idat = new MemoryStream();
        var offset = Signature.Length;
        var sawNonIdatAfterIdat = false;
        while (offset < data.Length)
        {
            if (data.Length - offset < 12) throw new InvalidDataException("PNG chunk is truncated.");
            var lengthValue = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (lengthValue > int.MaxValue || lengthValue > options.MaxChunkBytes) throw new InvalidDataException("PNG chunk exceeds the configured limit.");
            var length = (int)lengthValue;
            var chunkEnd = checked((long)offset + 12 + length);
            if (chunkEnd > data.Length) throw new InvalidDataException("PNG chunk data is truncated.");
            var type = data.Slice(offset + 4, 4);
            var chunk = data.Slice(offset + 8, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + length, 4));
            ValidateChunkType(type);
            ValidateCrc(type, chunk, expectedCrc, options.PngCrcPolicy);
            var name = BinaryPrimitives.ReadUInt32BigEndian(type);

            if (!state.HasHeader && name != Chunk.IHDR) throw new InvalidDataException("IHDR must be the first PNG chunk.");
            if (state.HasIdat && name != Chunk.IDAT) sawNonIdatAfterIdat = true;
            switch (name)
            {
                case Chunk.IHDR:
                    if (state.HasHeader || length != 13) throw new InvalidDataException("PNG must contain exactly one valid IHDR chunk.");
                    ParseHeader(chunk, state);
                    break;
                case Chunk.PLTE:
                    if (!state.HasHeader || state.HasPalette || state.HasIdat) throw new InvalidDataException("Invalid PLTE chunk order.");
                    ParsePalette(chunk, state);
                    break;
                case Chunk.tRNS:
                    if (!state.HasHeader || state.HasTransparency || state.HasIdat) throw new InvalidDataException("Invalid tRNS chunk order.");
                    state.Transparency = chunk.ToArray(); state.HasTransparency = true;
                    break;
                case Chunk.IDAT:
                    if (sawNonIdatAfterIdat) throw new InvalidDataException("PNG IDAT chunks must be consecutive.");
                    idat.Write(chunk); state.HasIdat = true;
                    break;
                case Chunk.IEND:
                    if (length != 0 || state.HasEnd || !state.HasIdat) throw new InvalidDataException("Invalid PNG IEND chunk.");
                    state.HasEnd = true; offset = checked((int)chunkEnd);
                    if (offset != data.Length) throw new InvalidDataException("PNG contains trailing data after IEND.");
                    ValidateState(state);
                    state.CompressedData = idat.ToArray();
                    return state;
                default:
                    if ((type[0] & 0x20) == 0) throw new InvalidDataException("PNG contains an unsupported critical chunk.");
                    break;
            }
            offset = checked((int)chunkEnd);
        }
        throw new InvalidDataException("PNG is missing IEND.");
    }

    private static void ParseHeader(ReadOnlySpan<byte> chunk, PngState state)
    {
        var width = BinaryPrimitives.ReadUInt32BigEndian(chunk[0..4]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..8]);
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue) throw new InvalidDataException("Invalid PNG dimensions.");
        state.Width = (int)width; state.Height = (int)height;
        state.BitDepth = chunk[8]; state.ColorType = chunk[9];
        if (chunk[10] != 0 || chunk[11] != 0 || chunk[12] > 1) throw new InvalidDataException("Unsupported PNG compression, filter, or interlace method.");
        state.Interlace = chunk[12];
        if (!IsValidDepth(state.ColorType, state.BitDepth)) throw new InvalidDataException("Invalid PNG color type and bit depth combination.");
        state.HasHeader = true;
    }

    private static void ParsePalette(ReadOnlySpan<byte> chunk, PngState state)
    {
        if (state.ColorType is 0 or 4 || chunk.Length == 0 || chunk.Length % 3 != 0 || chunk.Length > 768)
            throw new InvalidDataException("Invalid PNG palette.");
        var entries = chunk.Length / 3;
        if (state.ColorType == 3 && entries > 1 << state.BitDepth) throw new InvalidDataException("PNG palette has too many entries.");
        state.Palette = chunk.ToArray(); state.HasPalette = true;
    }

    private static void ValidateState(PngState state)
    {
        if (!state.HasHeader || !state.HasIdat || !state.HasEnd) throw new InvalidDataException("PNG is missing a required chunk.");
        if (state.ColorType == 3 && !state.HasPalette) throw new InvalidDataException("Indexed PNG is missing PLTE.");
        if (!state.HasTransparency) return;
        var length = state.Transparency!.Length;
        if ((state.ColorType == 0 && length != 2) || (state.ColorType == 2 && length != 6) ||
            (state.ColorType == 3 && (state.Palette == null || length > state.Palette.Length / 3)) || state.ColorType is 4 or 6)
            throw new InvalidDataException("Invalid PNG tRNS chunk.");
    }

    private static Bitmap DecodePixels(PngState state, ImageDecoderOptions options)
    {
        var bitmap = new Bitmap(state.Width, state.Height);
        try
        {
            using var compressed = new MemoryStream(state.CompressedData!, writable: false);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            if (state.Interlace == 0)
                DecodePass(zlib, state, bitmap, 0, 0, 1, 1, state.Width, state.Height, options);
            else
                foreach (var pass in Adam7)
                {
                    var width = PassSize(state.Width, pass.X, pass.Dx);
                    var height = PassSize(state.Height, pass.Y, pass.Dy);
                    if (width > 0 && height > 0) DecodePass(zlib, state, bitmap, pass.X, pass.Y, pass.Dx, pass.Dy, width, height, options);
                }
            if (zlib.ReadByte() != -1) throw new InvalidDataException("PNG contains excess decompressed image data.");
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void DecodePass(Stream source, PngState state, Bitmap bitmap, int startX, int startY, int dx, int dy,
        int width, int height, ImageDecoderOptions options)
    {
        var channels = Channels(state.ColorType);
        var bitsPerPixel = checked(channels * state.BitDepth);
        var rowBytes = checked((int)(((long)width * bitsPerPixel + 7) / 8));
        var filterBpp = Math.Max(1, (bitsPerPixel + 7) / 8);
        if (checked((long)(rowBytes + 1) * height) > options.MaxDecodedBytes) throw new InvalidDataException("PNG pass exceeds the decoded byte limit.");
        var previous = new byte[rowBytes];
        var current = new byte[rowBytes];
        for (var row = 0; row < height; row++)
        {
            var filter = source.ReadByte();
            if (filter < 0) throw new InvalidDataException("PNG decompressed data is truncated.");
            ReadExactly(source, current);
            Unfilter(current, previous, filterBpp, filter);
            var y = startY + row * dy;
            for (var x = 0; x < width; x++) WritePixel(state, current, x, bitmap.GetPixel(startX + x * dx, y));
            (previous, current) = (current, previous);
        }
    }

    private static void WritePixel(PngState state, ReadOnlySpan<byte> row, int x, Span<byte> destination)
    {
        Span<ushort> samples = stackalloc ushort[4];
        ReadSamples(row, x, state.BitDepth, Channels(state.ColorType), samples);
        ushort red, green, blue, alpha = MaxSample(state.BitDepth);
        switch (state.ColorType)
        {
            case 0:
                red = green = blue = samples[0];
                if (state.Transparency != null && samples[0] == BinaryPrimitives.ReadUInt16BigEndian(state.Transparency)) alpha = 0;
                break;
            case 2:
                red = samples[0]; green = samples[1]; blue = samples[2];
                if (state.Transparency != null && red == BinaryPrimitives.ReadUInt16BigEndian(state.Transparency.AsSpan(0, 2)) &&
                    green == BinaryPrimitives.ReadUInt16BigEndian(state.Transparency.AsSpan(2, 2)) &&
                    blue == BinaryPrimitives.ReadUInt16BigEndian(state.Transparency.AsSpan(4, 2))) alpha = 0;
                break;
            case 3:
                var index = samples[0];
                if (state.Palette == null || index >= state.Palette.Length / 3) throw new InvalidDataException("PNG palette index is out of range.");
                red = state.Palette[index * 3]; green = state.Palette[index * 3 + 1]; blue = state.Palette[index * 3 + 2];
                alpha = state.Transparency != null && index < state.Transparency.Length ? state.Transparency[index] : (ushort)255;
                destination[0] = (byte)blue; destination[1] = (byte)green; destination[2] = (byte)red; destination[3] = (byte)alpha;
                return;
            case 4:
                red = green = blue = samples[0]; alpha = samples[1];
                break;
            case 6:
                red = samples[0]; green = samples[1]; blue = samples[2]; alpha = samples[3];
                break;
            default:
                throw new InvalidDataException("Unsupported PNG color type.");
        }
        destination[0] = Scale(blue, state.BitDepth);
        destination[1] = Scale(green, state.BitDepth);
        destination[2] = Scale(red, state.BitDepth);
        destination[3] = Scale(alpha, state.BitDepth);
    }

    private static void ReadSamples(ReadOnlySpan<byte> row, int pixel, int depth, int channels, Span<ushort> samples)
    {
        if (depth == 8)
        {
            var offset = pixel * channels;
            for (var i = 0; i < channels; i++) samples[i] = row[offset + i];
            return;
        }
        if (depth == 16)
        {
            var offset = pixel * channels * 2;
            for (var i = 0; i < channels; i++) samples[i] = BinaryPrimitives.ReadUInt16BigEndian(row.Slice(offset + i * 2, 2));
            return;
        }
        var bit = pixel * depth;
        var shift = 8 - depth - bit % 8;
        samples[0] = (ushort)((row[bit / 8] >> shift) & ((1 << depth) - 1));
    }

    private static void Unfilter(Span<byte> row, ReadOnlySpan<byte> previous, int bpp, int filter)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bpp ? row[i - bpp] : 0;
            var above = previous[i];
            var upperLeft = i >= bpp ? previous[i - bpp] : 0;
            row[i] = filter switch
            {
                0 => row[i],
                1 => unchecked((byte)(row[i] + left)),
                2 => unchecked((byte)(row[i] + above)),
                3 => unchecked((byte)(row[i] + ((left + above) >> 1))),
                4 => unchecked((byte)(row[i] + Paeth(left, above, upperLeft))),
                _ => throw new InvalidDataException("PNG contains an invalid filter type.")
            };
        }
    }

    private static byte Paeth(int left, int above, int upperLeft)
    {
        var estimate = left + above - upperLeft;
        var dl = Math.Abs(estimate - left); var da = Math.Abs(estimate - above); var du = Math.Abs(estimate - upperLeft);
        return (byte)(dl <= da && dl <= du ? left : da <= du ? above : upperLeft);
    }

    private static void ReadExactly(Stream source, Span<byte> destination)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = source.Read(destination[offset..]);
            if (read == 0) throw new InvalidDataException("PNG decompressed data is truncated.");
            offset += read;
        }
    }

    private static int Channels(int colorType) => colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
    private static ushort MaxSample(int depth) => depth == 16 ? ushort.MaxValue : (ushort)((1 << depth) - 1);
    private static byte Scale(ushort value, int depth) => depth switch
    {
        8 => (byte)value,
        16 => (byte)((value * 255L + 32767) / 65535),
        _ => (byte)((value * 255 + ((1 << depth) - 1) / 2) / ((1 << depth) - 1))
    };
    private static int PassSize(int size, int start, int step) => size <= start ? 0 : (size - start + step - 1) / step;
    private static bool IsValidDepth(int type, int depth) => type switch
    {
        0 => depth is 1 or 2 or 4 or 8 or 16,
        2 => depth is 8 or 16,
        3 => depth is 1 or 2 or 4 or 8,
        4 or 6 => depth is 8 or 16,
        _ => false
    };

    private static void ValidateChunkType(ReadOnlySpan<byte> type)
    {
        if (type.Length != 4 || (type[2] & 0x20) != 0)
            throw new InvalidDataException("Invalid PNG chunk type.");
        foreach (var value in type)
            if (value is < (byte)'A' or > (byte)'z' || value is > (byte)'Z' and < (byte)'a')
                throw new InvalidDataException("Invalid PNG chunk type.");
    }

    private static void ValidateCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data, uint expected, PngCrcPolicy policy)
    {
        if (policy == PngCrcPolicy.Ignore || policy == PngCrcPolicy.CriticalChunksOnly && (type[0] & 0x20) != 0) return;
        if (Crc32.Compute(type, data) != expected) throw new InvalidDataException("PNG chunk CRC mismatch.");
    }

    private sealed class PngState
    {
        public int Width, Height, BitDepth, ColorType, Interlace;
        public bool HasHeader, HasPalette, HasTransparency, HasIdat, HasEnd;
        public byte[]? Palette, Transparency, CompressedData;
    }

    private static class Chunk
    {
        public const uint IHDR = 0x49484452, PLTE = 0x504C5445, IDAT = 0x49444154, IEND = 0x49454E44, tRNS = 0x74524E53;
    }
}
