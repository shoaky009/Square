#if SQUARE_IMAGES_BMP
using Square.Images.Bmp;
#endif
#if SQUARE_IMAGES_GIF
using Square.Images.Gif;
#endif
#if SQUARE_IMAGES_ICON
using Square.Images.Icon;
#endif
#if SQUARE_IMAGES_JPEG
using Square.Images.Jpeg;
#endif
#if SQUARE_IMAGES_JPEG
using Square.Images.Metadata;
#endif
#if SQUARE_IMAGES_PNG
using Square.Images.Png;
#endif
#if SQUARE_IMAGES_TIFF
using Square.Images.Tiff;
#endif
#if SQUARE_IMAGES_WEBP
using Square.Images.Webp;
#endif

namespace Square.Images;

public static class ImageDecoder
{
#if SQUARE_IMAGES_PNG
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
#endif

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
#if SQUARE_IMAGES_PNG
        if (data.StartsWith(PngSignature) && ApngDecoder.IsAnimated(data)) return ApngDecoder.Decode(data, options);
#endif
#if SQUARE_IMAGES_GIF
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return GifDecoder.Decode(data, options);
#endif
#if SQUARE_IMAGES_ICON
        if (data.Length >= 6 && data[0] == 0 && data[1] == 0 && data[2] is 1 or 2 && data[3] == 0)
            return IconDecoder.Decode(data, options);
#endif
#if SQUARE_IMAGES_WEBP
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return WebpDecoder.Decode(data, options);
#endif
#if SQUARE_IMAGES_TIFF
        if (IsTiff(data)) return TiffDecoder.Decode(data, options);
#endif

        var format = IdentifyFormat(data);
        var bitmap = DecodeBitmap(data, format, options);
        try
        {
#if SQUARE_IMAGES_JPEG
            var orientation = format == ImageFormat.Jpeg
                ? ExifReader.ReadJpegOrientation(data, options)
                : ImageOrientation.Normal;
            var applyOrientation = options.ExifOrientationPolicy == ExifOrientationPolicy.Apply;
            if (applyOrientation && orientation != ImageOrientation.Normal)
                bitmap = ImageOrientationTransform.Apply(bitmap, orientation, options);
#else
            var orientation = ImageOrientation.Normal;
            const bool applyOrientation = false;
#endif
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
#if SQUARE_IMAGES_PNG
            ImageFormat.Png => PngDecoder.Decode(data, options),
#endif
#if SQUARE_IMAGES_BMP
            ImageFormat.Bmp => BmpDecoder.Decode(data, options),
#endif
#if SQUARE_IMAGES_JPEG
            ImageFormat.Jpeg => JpegDecoder.Decode(data, options),
#endif
            _ => throw new InvalidDataException("Unsupported image format or the decoder was disabled at build time.")
        };
    }

    private static ImageFormat IdentifyFormat(ReadOnlySpan<byte> data)
    {
#if SQUARE_IMAGES_PNG
        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature)) return ImageFormat.Png;
#endif
#if SQUARE_IMAGES_BMP
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M') return ImageFormat.Bmp;
#endif
#if SQUARE_IMAGES_JPEG
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ImageFormat.Jpeg;
#endif
#if SQUARE_IMAGES_WEBP
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return ImageFormat.Webp;
#endif
#if SQUARE_IMAGES_TIFF
        if (IsTiff(data)) return ImageFormat.Tiff;
#endif
#if SQUARE_IMAGES_ICON
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0) return ImageFormat.Ico;
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 2 && data[3] == 0) return ImageFormat.Cur;
#endif
        return ImageFormat.Unknown;
    }

#if SQUARE_IMAGES_TIFF
    private static bool IsTiff(ReadOnlySpan<byte> data) => data.Length >= 4 &&
        (data[..4].SequenceEqual("II*\0"u8) || data[..4].SequenceEqual("MM\0*"u8));
#endif
}
