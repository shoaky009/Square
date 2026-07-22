namespace Square.Images;

public enum PngCrcPolicy
{
    AllChunks,
    CriticalChunksOnly,
    Ignore
}

public enum ExifOrientationPolicy
{
    Apply,
    Ignore
}

public sealed class ImageDecoderOptions
{
    public int MaxWidth { get; init; } = 16_384;
    public int MaxHeight { get; init; } = 16_384;
    public long MaxPixelCount { get; init; } = 100_000_000;
    public long MaxDecodedBytes { get; init; } = 400_000_000;
    public long MaxEncodedBytes { get; init; } = 256_000_000;
    public int MaxChunkBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxItemCount { get; init; } = 4_096;
    public long MaxTotalDecodedBytes { get; init; } = 400_000_000;
    public long MaxMetadataBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxExifTagCount { get; init; } = 16_384;
    public int MaxIfdDepth { get; init; } = 16;
    public PngCrcPolicy PngCrcPolicy { get; init; } = PngCrcPolicy.AllChunks;
    public ExifOrientationPolicy ExifOrientationPolicy { get; init; } = ExifOrientationPolicy.Apply;

    internal void Validate()
    {
        if (MaxWidth <= 0 || MaxHeight <= 0 || MaxPixelCount <= 0 || MaxDecodedBytes <= 0 ||
            MaxEncodedBytes <= 0 || MaxChunkBytes <= 0 || MaxItemCount <= 0 || MaxTotalDecodedBytes <= 0 ||
            MaxMetadataBytes <= 0 || MaxExifTagCount <= 0 || MaxIfdDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(ImageDecoderOptions), "Image decoder limits must be positive.");
    }

    internal void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > MaxWidth || height > MaxHeight)
            throw new InvalidDataException($"Image dimensions {width}x{height} exceed the configured limit.");
        var pixels = checked((long)width * height);
        var decodedBytes = checked(pixels * 4);
        if (pixels > MaxPixelCount || decodedBytes > MaxDecodedBytes || decodedBytes > Array.MaxLength)
            throw new InvalidDataException("Decoded image exceeds the configured pixel or byte limit.");
    }
}
