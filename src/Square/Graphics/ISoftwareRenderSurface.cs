namespace Square.Graphics;

/// <summary>CPU-writable BGRA32 render target. Dimensions and dirty rectangles use physical pixels.</summary>
public interface ISoftwareRenderSurface : IDisposable
{
    int Width { get; }
    int Height { get; }
    int Stride { get; }

    Span<byte> GetRowSpan(int y);
    void Resize(int width, int height);
    void Present(IReadOnlyList<Rect>? dirtyRects);
}

public sealed class BitmapSoftwareRenderSurface : ISoftwareRenderSurface
{
    private Bitmap _bitmap;
    private readonly PresentFrameHandler? _presentFrame;

    public BitmapSoftwareRenderSurface(Bitmap bitmap, PresentFrameHandler? presentFrame = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _bitmap = bitmap;
        _presentFrame = presentFrame;
    }

    public BitmapSoftwareRenderSurface(int width, int height, PresentFrameHandler? presentFrame = null)
        : this(new Bitmap(Math.Max(1, width), Math.Max(1, height)), presentFrame)
    {
    }

    public Bitmap Bitmap => _bitmap;
    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public int Stride => _bitmap.Stride;

    public Span<byte> GetRowSpan(int y) => _bitmap.GetRow(y);

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (Width == width && Height == height) return;

        var previous = _bitmap;
        _bitmap = new Bitmap(width, height);
        previous.Dispose();
    }

    public void Present(IReadOnlyList<Rect>? dirtyRects)
    {
        if (dirtyRects is { Count: 0 }) return;
        _presentFrame?.Invoke(_bitmap, dirtyRects);
    }

    public void Dispose() => _bitmap.Dispose();
}
