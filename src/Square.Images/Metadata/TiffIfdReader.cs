namespace Square.Images.Metadata;

internal static class TiffIfdReader
{
    private const ushort OrientationTag = 0x0112;

    public static ImageOrientation ReadOrientation(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var reader = new TiffReader(data);
        var offset = reader.FirstIfdOffset;
        var visited = new HashSet<uint>();
        var totalTags = 0;
        for (var depth = 0; offset != 0; depth++)
        {
            if (depth >= options.MaxIfdDepth) throw new InvalidDataException("Exif IFD depth exceeds the configured limit.");
            var directory = reader.ReadDirectory(offset, options, visited, ref totalTags);
            var orientation = reader.Find(directory, OrientationTag);
            if (orientation != null)
            {
                if (orientation.Value.Type != 3 || orientation.Value.Count != 1)
                    throw new InvalidDataException("Exif orientation has an invalid field type.");
                var value = reader.GetSingle(orientation.Value);
                if (value is < 1 or > 8) throw new InvalidDataException("Exif orientation value is invalid.");
                return (ImageOrientation)value;
            }
            offset = directory.NextOffset;
        }
        return ImageOrientation.Normal;
    }
}
