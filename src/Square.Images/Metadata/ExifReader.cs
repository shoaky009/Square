using System.Buffers.Binary;

namespace Square.Images.Metadata;

internal static class ExifReader
{
    private static readonly byte[] Prefix = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];

    public static ImageOrientation ReadJpegOrientation(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return ImageOrientation.Normal;
        var offset = 2;
        while (offset < data.Length)
        {
            if (data[offset] != 0xFF) throw new InvalidDataException("JPEG marker prefix is missing while reading metadata.");
            while (offset < data.Length && data[offset] == 0xFF) offset++;
            if (offset >= data.Length) throw new InvalidDataException("JPEG metadata is truncated.");
            var marker = data[offset++];
            if (marker is 0xD9 or 0xDA) return ImageOrientation.Normal;
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7) continue;
            if (offset + 2 > data.Length) throw new InvalidDataException("JPEG metadata segment is truncated.");
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (length < 2 || offset + length > data.Length) throw new InvalidDataException("JPEG metadata segment length is invalid.");
            var payload = data.Slice(offset + 2, length - 2);
            if (marker == 0xE1 && payload.StartsWith(Prefix))
            {
                var tiff = payload[Prefix.Length..];
                if (tiff.Length > options.MaxMetadataBytes) throw new InvalidDataException("Exif metadata exceeds the configured byte limit.");
                return TiffIfdReader.ReadOrientation(tiff, options);
            }
            offset += length;
        }
        return ImageOrientation.Normal;
    }
}
