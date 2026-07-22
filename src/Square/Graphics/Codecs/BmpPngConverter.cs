using System.Buffers.Binary;

namespace Square.Graphics.Codecs;

public static class BmpPngConverter
{
    public static void Convert(string bmpPath, string pngPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bmpPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);

        using var bitmap = LoadBmp(bmpPath);
        BitmapPngEncoder.Save(bitmap, pngPath);
    }

    public static Bitmap LoadBmp(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            throw new InvalidDataException("Unsupported BMP file.");

        var pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(10, 4));
        var dibHeaderSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14, 4));
        if (dibHeaderSize < 40 || bytes.Length < 14 + dibHeaderSize)
            throw new InvalidDataException("Unsupported BMP DIB header.");

        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4));
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22, 4));
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(30, 4));

        if (width <= 0 || signedHeight == 0 || planes != 1 || compression != 0 || bitsPerPixel is not (24 or 32))
            throw new InvalidDataException("Only uncompressed 24-bit or 32-bit BMP files are supported.");

        var height = Math.Abs(signedHeight);
        var bytesPerPixel = bitsPerPixel / 8;
        var sourceStride = ((width * bytesPerPixel) + 3) / 4 * 4;
        var requiredBytes = pixelOffset + sourceStride * height;
        if (pixelOffset < 0 || requiredBytes > bytes.Length)
            throw new InvalidDataException("BMP pixel data is truncated.");

        var topDown = signedHeight < 0;
        var bitmap = new Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            var sourceY = topDown ? y : height - 1 - y;
            var source = bytes.AsSpan(pixelOffset + sourceY * sourceStride, width * bytesPerPixel);
            var destination = bitmap.GetRow(y);

            for (var x = 0; x < width; x++)
            {
                var src = x * bytesPerPixel;
                var dst = x * 4;
                destination[dst] = source[src];
                destination[dst + 1] = source[src + 1];
                destination[dst + 2] = source[src + 2];
                destination[dst + 3] = bitsPerPixel == 32 ? source[src + 3] : (byte)255;
            }
        }

        return bitmap;
    }
}
