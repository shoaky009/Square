using Square.Graphics;

namespace Square.Images;

public enum ImageFormat
{
    Unknown,
    Bmp,
    Gif,
    Ico,
    Cur,
    Jpeg,
    Png,
    Tiff,
    Webp
}

public enum ImageDocumentKind
{
    Still,
    Animation,
    Pages,
    Variants
}

public enum ImageOrientation
{
    Normal = 1,
    MirrorHorizontal = 2,
    Rotate180 = 3,
    MirrorVertical = 4,
    Transpose = 5,
    Rotate90 = 6,
    Transverse = 7,
    Rotate270 = 8
}

public sealed class ImageMetadata
{
    public static ImageMetadata Empty { get; } = new(ImageOrientation.Normal, false);

    public ImageOrientation OriginalOrientation { get; }
    public bool OrientationApplied { get; }

    internal ImageMetadata(ImageOrientation originalOrientation, bool orientationApplied)
    {
        OriginalOrientation = originalOrientation;
        OrientationApplied = orientationApplied;
    }
}

public sealed class ImageAnimationInfo
{
    public bool LoopsForever { get; }
    public int PlayCount { get; }
    public TimeSpan TotalDuration { get; }

    internal ImageAnimationInfo(bool loopsForever, int playCount, TimeSpan totalDuration)
    {
        LoopsForever = loopsForever;
        PlayCount = playCount;
        TotalDuration = totalDuration;
    }
}

public sealed class ImageItem
{
    private Bitmap? _bitmap;

    public int Index { get; }
    public int Width { get; }
    public int Height { get; }
    public int BitDepth { get; }
    public int SourceBitDepth { get; }
    public TimeSpan Duration { get; }
    public Point? Hotspot { get; }
    public ImageMetadata Metadata { get; }

    internal ImageItem(int index, Bitmap bitmap, int bitDepth, TimeSpan duration, Point? hotspot = null,
        int? sourceBitDepth = null, ImageMetadata? metadata = null)
    {
        Index = index;
        Width = bitmap.Width;
        Height = bitmap.Height;
        BitDepth = bitDepth;
        SourceBitDepth = sourceBitDepth ?? bitDepth;
        Duration = duration;
        Hotspot = hotspot;
        Metadata = metadata ?? ImageMetadata.Empty;
        _bitmap = bitmap;
    }

    internal Bitmap GetBitmap() => _bitmap ?? throw new ObjectDisposedException(nameof(ImageDocument));
    internal void Dispose() => Interlocked.Exchange(ref _bitmap, null)?.Dispose();
}

public sealed class ImageDocument : IDisposable
{
    private readonly ImageItem[] _items;

    public ImageFormat Format { get; }
    public ImageDocumentKind Kind { get; }
    public IReadOnlyList<ImageItem> Items => _items;
    public int PrimaryIndex { get; }
    public ImageItem PrimaryItem => _items[PrimaryIndex];
    public Bitmap PrimaryBitmap => GetBitmap(PrimaryIndex);
    public ImageAnimationInfo? Animation { get; }
    public ImageMetadata Metadata { get; }
    public bool IsDisposed { get; private set; }

    internal ImageDocument(ImageFormat format, ImageDocumentKind kind, ImageItem[] items, int primaryIndex,
        ImageAnimationInfo? animation = null, ImageMetadata? metadata = null)
    {
        if (items.Length == 0) throw new ArgumentException("An image document must contain at least one item.", nameof(items));
        if ((uint)primaryIndex >= (uint)items.Length) throw new ArgumentOutOfRangeException(nameof(primaryIndex));
        Format = format;
        Kind = kind;
        _items = items;
        PrimaryIndex = primaryIndex;
        Animation = animation;
        Metadata = metadata ?? ImageMetadata.Empty;
    }

    public Bitmap GetBitmap(int index)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if ((uint)index >= (uint)_items.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _items[index].GetBitmap();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        foreach (var item in _items) item.Dispose();
        IsDisposed = true;
    }
}
