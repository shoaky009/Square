using System.Buffers.Binary;
using System.IO.Compression;

namespace Square.Graphics.Codecs;

/// <summary>将 <see cref="Bitmap"/> 编码为 PNG 文件的静态工具。</summary>
public static class BitmapPngEncoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>将位图保存为 PNG 文件。</summary>
    public static void Save(Bitmap bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        Save(bitmap, stream);
    }

    /// <summary>将位图以 PNG 格式写入流。</summary>
    public static void Save(Bitmap bitmap, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(stream);
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            throw new ArgumentException("Bitmap dimensions must be greater than zero.", nameof(bitmap));
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        stream.Write(PngSignature);
        WriteHeader(stream, bitmap.Width, bitmap.Height);
        WriteImageData(stream, bitmap);
        WriteChunk(stream, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteHeader(Stream stream, int width, int height)
    {
        Span<byte> data = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(data[4..8], height);
        data[8] = 8;
        data[9] = 6;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        WriteChunk(stream, "IHDR"u8, data);
    }

    private static void WriteImageData(Stream stream, Bitmap bitmap)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[bitmap.Width * 4 + 1];
            for (var y = 0; y < bitmap.Height; y++)
            {
                row[0] = 0;
                var source = bitmap.GetRow(y);
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var src = x * 4;
                    var dst = 1 + src;
                    row[dst] = source[src + 2];
                    row[dst + 1] = source[src + 1];
                    row[dst + 2] = source[src];
                    row[dst + 3] = source[src + 3];
                }

                zlib.Write(row);
            }
        }

        WriteChunk(stream, "IDAT"u8, compressed.ToArray());
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        var crc = Crc32.Compute(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }
}
