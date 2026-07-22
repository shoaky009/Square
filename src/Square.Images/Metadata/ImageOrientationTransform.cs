using Square.Graphics;

namespace Square.Images.Metadata;

internal static class ImageOrientationTransform
{
    public static Bitmap Apply(Bitmap source, ImageOrientation orientation, ImageDecoderOptions options)
    {
        if (orientation == ImageOrientation.Normal) return source;
        var swapsAxes = orientation is ImageOrientation.Transpose or ImageOrientation.Rotate90 or
            ImageOrientation.Transverse or ImageOrientation.Rotate270;
        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;
        options.ValidateDimensions(width, height);
        Bitmap? result = null;
        try
        {
            result = new Bitmap(width, height);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var (sourceX, sourceY) = SourceCoordinate(x, y, source.Width, source.Height, orientation);
                    source.GetPixel(sourceX, sourceY).CopyTo(result.GetPixel(x, y));
                }
            source.Dispose();
            return result;
        }
        catch
        {
            result?.Dispose();
            source.Dispose();
            throw;
        }
    }

    private static (int X, int Y) SourceCoordinate(int x, int y, int width, int height, ImageOrientation orientation)
    {
        return orientation switch
        {
            ImageOrientation.MirrorHorizontal => (width - 1 - x, y),
            ImageOrientation.Rotate180 => (width - 1 - x, height - 1 - y),
            ImageOrientation.MirrorVertical => (x, height - 1 - y),
            ImageOrientation.Transpose => (y, x),
            ImageOrientation.Rotate90 => (y, height - 1 - x),
            ImageOrientation.Transverse => (width - 1 - y, height - 1 - x),
            ImageOrientation.Rotate270 => (width - 1 - y, x),
            _ => (x, y)
        };
    }
}
