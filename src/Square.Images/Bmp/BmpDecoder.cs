using System.Buffers.Binary;
using Square.Graphics;

namespace Square.Images.Bmp;

internal static class BmpDecoder
{
    public static Bitmap Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (data.Length < 54) throw new InvalidDataException("BMP header is truncated.");
        var pixelOffset = BinaryPrimitives.ReadUInt32LittleEndian(data[10..14]);
        var dibSize = BinaryPrimitives.ReadUInt32LittleEndian(data[14..18]);
        if (dibSize < 40 || 14L + dibSize > data.Length) throw new InvalidDataException("Unsupported BMP DIB header.");
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[18..22]);
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(data[22..26]);
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(data[26..28]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(data[28..30]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(data[30..34]);
        if (width <= 0 || signedHeight is 0 or int.MinValue || planes != 1 || compression != 0 || bits is not (24 or 32))
            throw new InvalidDataException("Only uncompressed 24-bit and 32-bit Windows BMP images are supported.");
        var height = Math.Abs(signedHeight);
        options.ValidateDimensions(width, height);
        var bytesPerPixel = bits / 8;
        var rowBytes = checked((long)width * bytesPerPixel);
        var stride = checked((rowBytes + 3) & ~3L);
        var end = checked((long)pixelOffset + stride * height);
        if (pixelOffset < 14 + dibSize || end > data.Length) throw new InvalidDataException("BMP pixel data is truncated.");

        var bitmap = new Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            var sourceY = signedHeight < 0 ? y : height - 1 - y;
            var source = data.Slice(checked((int)(pixelOffset + sourceY * stride)), checked((int)rowBytes));
            var destination = bitmap.GetRow(y);
            for (var x = 0; x < width; x++)
            {
                var src = x * bytesPerPixel;
                var dst = x * 4;
                destination[dst] = source[src];
                destination[dst + 1] = source[src + 1];
                destination[dst + 2] = source[src + 2];
                destination[dst + 3] = bits == 32 ? source[src + 3] : (byte)255;
            }
        }
        return bitmap;
    }
}
