using Square.Images.Bmp;
using Square.Images.Gif;
using Square.Images.Icon;
using Square.Images.Jpeg;
using Square.Images.Metadata;
using Square.Images.Png;
using Square.Images.Tiff;
using Square.Images.Webp;

namespace Square.Images;

public static class ImageDecoder
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static ImageDocument Decode(string path, ImageDecoderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Decode(stream, options);
    }

    public static ImageDocument Decode(Stream stream, ImageDecoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The image stream must be readable.", nameof(stream));
        options ??= new ImageDecoderOptions();
        options.Validate();

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > options.MaxEncodedBytes) throw new InvalidDataException("Encoded image exceeds the configured byte limit.");
            buffer.Write(chunk, 0, read);
        }
        return Decode(buffer.ToArray(), options);
    }

    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions? options = null)
    {
        options ??= new ImageDecoderOptions();
        options.Validate();
        if (data.Length > options.MaxEncodedBytes) throw new InvalidDataException("Encoded image exceeds the configured byte limit.");
        if (data.StartsWith(PngSignature) && ApngDecoder.IsAnimated(data)) return ApngDecoder.Decode(data, options);
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return GifDecoder.Decode(data, options);
        if (data.Length >= 6 && data[0] == 0 && data[1] == 0 && data[2] is 1 or 2 && data[3] == 0)
            return IconDecoder.Decode(data, options);
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return WebpDecoder.Decode(data, options);
        if (IsTiff(data)) return TiffDecoder.Decode(data, options);

        var format = IdentifyFormat(data);
        var bitmap = DecodeBitmap(data, format, options);
        try
        {
            var orientation = format == ImageFormat.Jpeg
                ? ExifReader.ReadJpegOrientation(data, options)
                : ImageOrientation.Normal;
            var applyOrientation = options.ExifOrientationPolicy == ExifOrientationPolicy.Apply;
            if (applyOrientation && orientation != ImageOrientation.Normal)
                bitmap = ImageOrientationTransform.Apply(bitmap, orientation, options);
            var item = new ImageItem(0, bitmap, 32, TimeSpan.Zero);
            var metadata = new ImageMetadata(orientation, applyOrientation && orientation != ImageOrientation.Normal);
            return new ImageDocument(format, ImageDocumentKind.Still, [item], 0, metadata: metadata);
        }
        catch
        {
            if (!bitmap.IsDisposed) bitmap.Dispose();
            throw;
        }
    }

    private static Square.Graphics.Bitmap DecodeBitmap(ReadOnlySpan<byte> data, ImageFormat format,
        ImageDecoderOptions options)
    {
        return format switch
        {
            ImageFormat.Png => PngDecoder.Decode(data, options),
            ImageFormat.Bmp => BmpDecoder.Decode(data, options),
            ImageFormat.Jpeg => JpegDecoder.Decode(data, options),
            _ => throw new InvalidDataException("Unsupported image format. Supported formats are PNG, JPEG, BMP, GIF, lossless WebP, ICO, CUR, and TIFF.")
        };
    }

    private static ImageFormat IdentifyFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature)) return ImageFormat.Png;
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M') return ImageFormat.Bmp;
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ImageFormat.Jpeg;
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return ImageFormat.Webp;
        if (IsTiff(data)) return ImageFormat.Tiff;
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0) return ImageFormat.Ico;
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 2 && data[3] == 0) return ImageFormat.Cur;
        return ImageFormat.Unknown;
    }

    private static bool IsTiff(ReadOnlySpan<byte> data) => data.Length >= 4 &&
        (data[..4].SequenceEqual("II*\0"u8) || data[..4].SequenceEqual("MM\0*"u8));
}
