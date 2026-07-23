namespace Square.Images.Webp;

internal static class AlphaDecoder
{
    internal static byte[] Decode(ReadOnlySpan<byte> data, int width, int height, ImageDecoderOptions options)
    {
        ValidateDimensions(width, height, options, out var pixelCount);
        if (data.Length == 0) throw Bad("header is truncated");
        if (data.Length > options.MaxChunkBytes) throw Bad("payload exceeds the configured chunk limit");

        var header = data[0];
        var method = header & 3;
        var filter = header >> 2 & 3;
        var preprocessing = header >> 4 & 3;
        if ((header & 0xC0) != 0) throw Bad("header contains reserved bits");
        if (method > 1) throw Bad("compression method");
        if (preprocessing > 1) throw Bad("preprocessing method");

        byte[] alpha;
        if (method == 0)
        {
            if (data.Length - 1 != pixelCount) throw Bad("raw payload size");
            alpha = data[1..].ToArray();
        }
        else
        {
            if (width > 16_384 || height > 16_384) throw Bad("lossless dimensions");
            var lossless = new byte[checked(data.Length + 4)];
            var widthMinusOne = width - 1;
            var heightMinusOne = height - 1;
            lossless[0] = 0x2f;
            lossless[1] = (byte)widthMinusOne;
            lossless[2] = (byte)(widthMinusOne >> 8 | heightMinusOne << 6);
            lossless[3] = (byte)(heightMinusOne >> 2);
            lossless[4] = (byte)(heightMinusOne >> 10);
            data[1..].CopyTo(lossless.AsSpan(5));

            var decoded = Vp8LDecoder.DecodePixels(lossless, options, requireExact: true);
            if (decoded.Width != width || decoded.Height != height || decoded.Pixels.Length != pixelCount)
                throw Bad("lossless output size");
            alpha = new byte[pixelCount];
            for (var i = 0; i < alpha.Length; i++) alpha[i] = (byte)(decoded.Pixels[i] >> 8);
        }

        Unfilter(alpha, width, filter);
        return alpha;
    }

    private static void ValidateDimensions(int width, int height, ImageDecoderOptions options, out int pixelCount)
    {
        if (width <= 0 || height <= 0 || width > options.MaxWidth || height > options.MaxHeight)
            throw Bad("dimensions");
        var pixels = checked((long)width * height);
        if (pixels > options.MaxPixelCount || pixels > options.MaxDecodedBytes || pixels > Array.MaxLength)
            throw Bad("output exceeds the configured limit");
        pixelCount = (int)pixels;
    }

    private static void Unfilter(byte[] alpha, int width, int filter)
    {
        if (filter == 0) return;

        for (var x = 1; x < width; x++) alpha[x] = unchecked((byte)(alpha[x] + alpha[x - 1]));
        for (var row = width; row < alpha.Length; row += width)
        {
            alpha[row] = unchecked((byte)(alpha[row] + alpha[row - width]));
            for (var x = 1; x < width; x++)
            {
                var index = row + x;
                var prediction = filter switch
                {
                    1 => alpha[index - 1],
                    2 => alpha[index - width],
                    3 => Math.Clamp(alpha[index - 1] + alpha[index - width] - alpha[index - width - 1], 0, 255),
                    _ => throw Bad("filter")
                };
                alpha[index] = unchecked((byte)(alpha[index] + prediction));
            }
        }
    }

    private static InvalidDataException Bad(string part) => new($"Invalid WebP ALPH {part}.");
}
