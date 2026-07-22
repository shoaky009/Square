using System.Buffers.Binary;
using Square.Graphics;
using Square.Images.Png;

namespace Square.Images.Icon;

internal static class IconDecoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var directory = ParseDirectory(data, options);
        var items = new ImageItem[directory.Entries.Length];
        long totalDecodedBytes = 0;
        try
        {
            for (var i = 0; i < directory.Entries.Length; i++)
            {
                var entry = directory.Entries[i];
                var bitmap = DecodeEntry(data, entry, options);
                totalDecodedBytes = checked(totalDecodedBytes + bitmap.Pixels.Length);
                if (totalDecodedBytes > options.MaxTotalDecodedBytes)
                {
                    bitmap.Dispose();
                    throw new InvalidDataException("ICO decoded variants exceed the configured total byte limit.");
                }
                items[i] = new ImageItem(i, bitmap, 32, TimeSpan.Zero, entry.Hotspot, entry.BitDepth);
            }
            return new ImageDocument(directory.Type == 1 ? ImageFormat.Ico : ImageFormat.Cur,
                ImageDocumentKind.Variants, items, directory.PrimaryIndex);
        }
        catch
        {
            foreach (var item in items) item?.Dispose();
            throw;
        }
    }

    private static IconDirectory ParseDirectory(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (data.Length < 6) throw new InvalidDataException("ICO header is truncated.");
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(data[0..2]);
        var type = BinaryPrimitives.ReadUInt16LittleEndian(data[2..4]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..6]);
        if (reserved != 0 || type is not (1 or 2)) throw new InvalidDataException("Invalid ICO file header.");
        if (count == 0) throw new InvalidDataException("ICO file contains no images.");
        if (count > 256 || count > options.MaxItemCount) throw new InvalidDataException("ICO image count exceeds the configured limit.");
        if (data.Length < 6 + count * 16L) throw new InvalidDataException("ICO directory is truncated.");

        var entries = new IconEntry[count];
        var primaryIndex = -1;
        var bestScore = long.MinValue;
        for (var i = 0; i < count; i++)
        {
            var raw = data.Slice(6 + i * 16, 16);
            var width = raw[0] == 0 ? 256 : raw[0];
            var height = raw[1] == 0 ? 256 : raw[1];
            var bytesInRes = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(8, 4));
            var imageOffset = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(12, 4));
            if (imageOffset > data.Length || bytesInRes > data.Length - imageOffset || bytesInRes == 0)
                throw new InvalidDataException("ICO image data is truncated.");
            var imageData = data.Slice((int)imageOffset, (int)bytesInRes);
            var bitDepth = type == 1 ? BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(6, 2)) : InferBitDepth(imageData);
            var hotspot = type == 2
                ? new Point(BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(4, 2)), BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(6, 2)))
                : (Point?)null;
            if (hotspot is { } point && (point.X >= width || point.Y >= height))
                throw new InvalidDataException("CUR hotspot is outside the image bounds.");
            entries[i] = new IconEntry(width, height, bitDepth, (int)imageOffset, (int)bytesInRes, hotspot);
            var score = checked((long)width * height) * 1000 + bitDepth;
            if (score > bestScore) { bestScore = score; primaryIndex = i; }
        }
        return new IconDirectory(type, entries, primaryIndex);
    }

    private static Bitmap DecodeEntry(ReadOnlySpan<byte> data, IconEntry entry, ImageDecoderOptions options)
    {
        var imageData = data.Slice(entry.Offset, entry.Length);
        Bitmap bitmap;
        if (imageData.Length >= 8 && imageData.StartsWith(PngSignature)) bitmap = PngDecoder.Decode(imageData, options);
        else bitmap = DecodeBmpEmbed(imageData, entry.Width, entry.Height, options);
        if (bitmap.Width == entry.Width && bitmap.Height == entry.Height) return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException("ICO image dimensions do not match the directory entry.");
    }

    private static int InferBitDepth(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 26 && data.StartsWith(PngSignature))
        {
            var depth = data[24];
            var channels = data[25] switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
            return depth * channels;
        }
        return data.Length >= 16 ? BinaryPrimitives.ReadUInt16LittleEndian(data[14..16]) : 0;
    }

    private static Bitmap DecodeBmpEmbed(ReadOnlySpan<byte> data, int declaredWidth, int declaredHeight, ImageDecoderOptions options)
    {
        if (data.Length < 40) throw new InvalidDataException("ICO BMP header is truncated.");
        var dibSize = BinaryPrimitives.ReadUInt32LittleEndian(data[0..4]);
        if (dibSize is < 40 or > 124) throw new InvalidDataException("Unsupported ICO BMP DIB header.");
        if (data.Length < dibSize) throw new InvalidDataException("ICO BMP DIB header is truncated.");
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[4..8]);
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(data[8..12]);
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(data[12..14]);
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(data[14..16]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(data[16..20]);
        if (width != declaredWidth || planes != 1 || compression != 0)
            throw new InvalidDataException("Unsupported ICO BMP image parameters.");
        var realHeight = signedHeight / 2;
        if (realHeight <= 0 || realHeight != declaredHeight)
            throw new InvalidDataException("ICO BMP height does not match the directory entry.");
        if (bitCount is not (1 or 4 or 8 or 24 or 32))
            throw new InvalidDataException($"Unsupported ICO bit depth {bitCount}.");
        options.ValidateDimensions(width, realHeight);

        var paletteEntries = bitCount <= 8 ? 1 << bitCount : 0;
        var paletteOffset = dibSize;
        var paletteBytes = paletteEntries * 4;
        if (data.Length < checked(paletteOffset + paletteBytes))
            throw new InvalidDataException("ICO BMP palette is truncated.");

        var bytesPerPixel = bitCount / 8;
        var rowBytes = bitCount <= 8 ? ((long)width * bitCount + 7) / 8 : checked((long)width * bytesPerPixel);
        var stride = (rowBytes + 3) & ~3L;
        var xorSize = checked(stride * realHeight);
        var andStride = ((long)width + 31) / 32 * 4;
        var andSize = checked(andStride * realHeight);
        var pixelOffset = checked(paletteOffset + paletteBytes);
        if (data.Length < checked(pixelOffset + xorSize + andSize))
            throw new InvalidDataException("ICO BMP pixel data is truncated.");

        var bitmap = new Bitmap(width, realHeight);
        try
        {
            for (var y = 0; y < realHeight; y++)
            {
                var srcY = realHeight - 1 - y;
                var xorRow = data.Slice(checked((int)(pixelOffset + srcY * stride)), checked((int)stride));
                var andRow = data.Slice(checked((int)(pixelOffset + xorSize + srcY * andStride)), checked((int)andStride));
                var dest = bitmap.GetRow(y);
                for (var x = 0; x < width; x++)
                {
                    var dst = x * 4;
                    if (bitCount <= 8)
                    {
                        var index = ReadPaletteIndex(xorRow, x, bitCount);
                        if (index >= paletteEntries) throw new InvalidDataException("ICO palette index is out of range.");
                        var pal = data.Slice(checked((int)(paletteOffset + index * 4)), 4);
                        dest[dst] = pal[0]; dest[dst + 1] = pal[1]; dest[dst + 2] = pal[2];
                    }
                    else
                    {
                        var src = x * bytesPerPixel;
                        dest[dst] = xorRow[src];
                        dest[dst + 1] = xorRow[src + 1];
                        dest[dst + 2] = xorRow[src + 2];
                    }
                    var andBit = (andRow[x / 8] >> (7 - x % 8)) & 1;
                    dest[dst + 3] = bitCount == 32 ? xorRow[x * 4 + 3] : (byte)(andBit == 0 ? 255 : 0);
                }
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static int ReadPaletteIndex(ReadOnlySpan<byte> row, int x, int bitCount)
    {
        return bitCount switch
        {
            1 => (row[x / 8] >> (7 - x % 8)) & 1,
            4 => (row[x / 2] >> (x % 2 == 0 ? 4 : 0)) & 0x0F,
            8 => row[x],
            _ => throw new InvalidOperationException()
        };
    }

    private readonly record struct IconDirectory(int Type, IconEntry[] Entries, int PrimaryIndex);
    private readonly record struct IconEntry(int Width, int Height, int BitDepth, int Offset, int Length, Point? Hotspot);
}
