using Square.Graphics;
using Square.Images.Metadata;

namespace Square.Images.Tiff;

internal static class TiffDecoder
{
    private const ushort ImageWidth = 256;
    private const ushort ImageLength = 257;
    private const ushort BitsPerSample = 258;
    private const ushort Compression = 259;
    private const ushort Photometric = 262;
    private const ushort StripOffsets = 273;
    private const ushort Orientation = 274;
    private const ushort SamplesPerPixel = 277;
    private const ushort RowsPerStrip = 278;
    private const ushort StripByteCounts = 279;
    private const ushort PlanarConfiguration = 284;
    private const ushort ColorMap = 320;
    private const ushort ExtraSamples = 338;

    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var reader = new TiffReader(data);
        var offset = reader.FirstIfdOffset;
        var visited = new HashSet<uint>();
        var items = new List<ImageItem>();
        var totalTags = 0;
        long totalDecodedBytes = 0;
        try
        {
            for (var depth = 0; offset != 0; depth++)
            {
                if (depth >= options.MaxIfdDepth) throw new InvalidDataException("TIFF IFD depth exceeds the configured limit.");
                if (items.Count >= options.MaxItemCount) throw new InvalidDataException("TIFF page count exceeds the configured limit.");
                var directory = reader.ReadDirectory(offset, options, visited, ref totalTags);
                var page = DecodePage(ref reader, directory, options, out var sourceBitDepth, out var orientation);
                var applyOrientation = options.ExifOrientationPolicy == ExifOrientationPolicy.Apply;
                if (applyOrientation && orientation != ImageOrientation.Normal)
                    page = ImageOrientationTransform.Apply(page, orientation, options);
                totalDecodedBytes = checked(totalDecodedBytes + page.Pixels.Length);
                if (totalDecodedBytes > options.MaxTotalDecodedBytes)
                {
                    page.Dispose();
                    throw new InvalidDataException("TIFF decoded pages exceed the configured total byte limit.");
                }
                var metadata = new ImageMetadata(orientation, applyOrientation && orientation != ImageOrientation.Normal);
                items.Add(new ImageItem(items.Count, page, 32, TimeSpan.Zero, sourceBitDepth: sourceBitDepth,
                    metadata: metadata));
                offset = directory.NextOffset;
            }
            if (items.Count == 0) throw new InvalidDataException("TIFF contains no image pages.");
            return new ImageDocument(ImageFormat.Tiff, ImageDocumentKind.Pages, items.ToArray(), 0,
                metadata: items[0].Metadata);
        }
        catch
        {
            foreach (var item in items) item.Dispose();
            throw;
        }
    }

    private static Bitmap DecodePage(ref TiffReader reader, TiffDirectory directory, ImageDecoderOptions options,
        out int sourceBitDepth, out ImageOrientation orientation)
    {
        var width = checked((int)RequiredSingle(ref reader, directory, ImageWidth));
        var height = checked((int)RequiredSingle(ref reader, directory, ImageLength));
        options.ValidateDimensions(width, height);
        var compression = OptionalSingle(ref reader, directory, Compression, 1);
        if (compression != 1) throw new InvalidDataException("Only uncompressed TIFF pages are supported.");
        var photometric = OptionalSingle(ref reader, directory, Photometric, 1);
        var samples = checked((int)OptionalSingle(ref reader, directory, SamplesPerPixel, 1));
        var planar = OptionalSingle(ref reader, directory, PlanarConfiguration, 1);
        if (planar != 1) throw new InvalidDataException("Only chunky TIFF planar configuration is supported.");
        var bits = Values(ref reader, directory, BitsPerSample, [1u]);
        if (bits.Length == 1 && samples > 1) bits = Enumerable.Repeat(bits[0], samples).ToArray();
        if (bits.Length != samples) throw new InvalidDataException("TIFF BitsPerSample does not match SamplesPerPixel.");
        if (bits.Any(static value => value is not (1 or 8))) throw new InvalidDataException("Only 1-bit and 8-bit TIFF samples are supported.");
        if (bits.Any(value => value != bits[0])) throw new InvalidDataException("Mixed TIFF sample depths are not supported.");
        if (bits[0] == 1 && samples != 1) throw new InvalidDataException("Only single-channel 1-bit TIFF pages are supported.");
        ValidateColorModel(photometric, samples);

        var orientationValue = OptionalSingle(ref reader, directory, Orientation, 1);
        if (orientationValue is < 1 or > 8) throw new InvalidDataException("TIFF orientation value is invalid.");
        orientation = (ImageOrientation)orientationValue;
        var rowsPerStrip = OptionalSingle(ref reader, directory, RowsPerStrip, (uint)height);
        if (rowsPerStrip == 0) throw new InvalidDataException("TIFF RowsPerStrip must be positive.");
        var offsets = RequiredValues(ref reader, directory, StripOffsets);
        var byteCounts = RequiredValues(ref reader, directory, StripByteCounts);
        if (offsets.Length == 0 || offsets.Length != byteCounts.Length)
            throw new InvalidDataException("TIFF strip offsets and byte counts do not match.");
        var expectedStrips = checked((height + (long)rowsPerStrip - 1) / rowsPerStrip);
        if (offsets.Length != expectedStrips) throw new InvalidDataException("TIFF strip count does not match RowsPerStrip.");

        var bitsPerPixel = checked((int)(bits[0] * samples));
        var rowBytes = checked((int)(((long)width * bitsPerPixel + 7) / 8));
        var palette = photometric == 3 ? ReadPalette(ref reader, directory, bits[0]) : null;
        var extraSample = ReadExtraSample(ref reader, directory, samples, photometric);
        sourceBitDepth = bitsPerPixel;
        var bitmap = new Bitmap(width, height);
        try
        {
            var row = 0;
            for (var strip = 0; strip < offsets.Length; strip++)
            {
                var rows = checked((int)Math.Min(rowsPerStrip, (uint)(height - row)));
                var requiredBytes = checked((uint)(rows * rowBytes));
                if (byteCounts[strip] < requiredBytes) throw new InvalidDataException("TIFF strip is shorter than its rows.");
                var data = reader.Slice(offsets[strip], byteCounts[strip]);
                for (var stripRow = 0; stripRow < rows; stripRow++, row++)
                    DecodeRow(data.Slice(stripRow * rowBytes, rowBytes), bitmap.GetRow(row), width,
                        bits[0], samples, photometric, palette, extraSample);
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void ValidateColorModel(uint photometric, int samples)
    {
        var valid = photometric switch
        {
            0 or 1 => samples == 1,
            2 => samples is 3 or 4,
            3 => samples == 1,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Unsupported TIFF photometric interpretation or sample count.");
    }

    private static byte[] ReadPalette(ref TiffReader reader, TiffDirectory directory, uint depth)
    {
        if (depth is not (1 or 8)) throw new InvalidDataException("Unsupported TIFF palette depth.");
        var values = RequiredValues(ref reader, directory, ColorMap);
        var entries = 1 << (int)depth;
        if (values.Length != entries * 3) throw new InvalidDataException("TIFF ColorMap has an invalid size.");
        var palette = new byte[entries * 3];
        for (var i = 0; i < entries; i++)
        {
            palette[i * 3] = Scale16(values[i]);
            palette[i * 3 + 1] = Scale16(values[i + entries]);
            palette[i * 3 + 2] = Scale16(values[i + entries * 2]);
        }
        return palette;
    }

    private static uint ReadExtraSample(ref TiffReader reader, TiffDirectory directory, int samples, uint photometric)
    {
        if (photometric != 2 || samples != 4) return 0;
        var entry = reader.Find(directory, ExtraSamples)
            ?? throw new InvalidDataException("TIFF RGBA page is missing ExtraSamples.");
        var values = reader.GetValues(entry);
        if (values.Length != 1 || values[0] is not (1 or 2))
            throw new InvalidDataException("Unsupported TIFF ExtraSamples value.");
        return values[0];
    }

    private static void DecodeRow(ReadOnlySpan<byte> source, Span<byte> destination, int width, uint depth,
        int samples, uint photometric, byte[]? palette, uint extraSample)
    {
        for (var x = 0; x < width; x++)
        {
            byte red, green, blue, alpha = 255;
            if (depth == 1)
            {
                var value = (source[x / 8] >> (7 - x % 8)) & 1;
                if (photometric == 3)
                {
                    red = palette![value * 3]; green = palette[value * 3 + 1]; blue = palette[value * 3 + 2];
                }
                else
                {
                    var gray = (byte)((photometric == 0 ? 1 - value : value) * 255);
                    red = green = blue = gray;
                }
            }
            else if (photometric == 3)
            {
                var index = source[x];
                red = palette![index * 3]; green = palette[index * 3 + 1]; blue = palette[index * 3 + 2];
            }
            else if (photometric is 0 or 1)
            {
                var gray = source[x];
                if (photometric == 0) gray = (byte)(255 - gray);
                red = green = blue = gray;
            }
            else
            {
                var offset = x * samples;
                red = source[offset]; green = source[offset + 1]; blue = source[offset + 2];
                if (samples == 4)
                {
                    alpha = source[offset + 3];
                    if (extraSample == 1 && alpha is > 0 and < 255)
                    {
                        red = Unpremultiply(red, alpha); green = Unpremultiply(green, alpha); blue = Unpremultiply(blue, alpha);
                    }
                }
            }
            var dest = x * 4;
            destination[dest] = blue; destination[dest + 1] = green;
            destination[dest + 2] = red; destination[dest + 3] = alpha;
        }
    }

    private static byte Unpremultiply(byte value, byte alpha) => (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    private static byte Scale16(uint value) => (byte)((value * 255L + 32767) / 65535);

    private static uint RequiredSingle(ref TiffReader reader, TiffDirectory directory, ushort tag)
        => reader.GetSingle(reader.Find(directory, tag) ?? throw new InvalidDataException($"TIFF is missing required tag 0x{tag:X4}."));

    private static uint OptionalSingle(ref TiffReader reader, TiffDirectory directory, ushort tag, uint fallback)
    {
        var entry = reader.Find(directory, tag);
        return entry == null ? fallback : reader.GetSingle(entry.Value);
    }

    private static uint[] RequiredValues(ref TiffReader reader, TiffDirectory directory, ushort tag)
        => reader.GetValues(reader.Find(directory, tag) ?? throw new InvalidDataException($"TIFF is missing required tag 0x{tag:X4}."));

    private static uint[] Values(ref TiffReader reader, TiffDirectory directory, ushort tag, uint[] fallback)
    {
        var entry = reader.Find(directory, tag);
        return entry == null ? fallback : reader.GetValues(entry.Value);
    }
}
