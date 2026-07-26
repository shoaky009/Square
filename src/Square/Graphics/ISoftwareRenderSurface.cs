namespace Square.Graphics;

/// <summary>CPU 可写的 BGRA32 软件渲染表面。尺寸和脏矩形使用物理像素。</summary>
public interface ISoftwareRenderSurface : IDisposable
{
    /// <summary>宽度（物理像素）。</summary>
    int Width { get; }
    /// <summary>高度（物理像素）。</summary>
    int Height { get; }
    /// <summary>每行字节数。</summary>
    int Stride { get; }

    /// <summary>获取指定行的可写字节跨度。</summary>
    Span<byte> GetRowSpan(int y);
    /// <summary>调整表面尺寸。</summary>
    void Resize(int width, int height);
    /// <summary>呈现帧。<paramref name="dirtyRects"/> 为 null 表示整窗。</summary>
    void Present(IReadOnlyList<Rect>? dirtyRects);
}

/// <summary>基于 <see cref="Bitmap"/> 的软件渲染表面。</summary>
public sealed class BitmapSoftwareRenderSurface : ISoftwareRenderSurface
{
    private Bitmap _bitmap;
    private readonly PresentFrameHandler? _presentFrame;

    /// <summary>用指定位图和呈现回调构造。</summary>
    public BitmapSoftwareRenderSurface(Bitmap bitmap, PresentFrameHandler? presentFrame = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        _bitmap = bitmap;
        _presentFrame = presentFrame;
    }

    /// <summary>用指定尺寸构造。</summary>
    public BitmapSoftwareRenderSurface(int width, int height, PresentFrameHandler? presentFrame = null)
        : this(new Bitmap(Math.Max(1, width), Math.Max(1, height)), presentFrame)
    {
    }

    /// <summary>底层位图。</summary>
    public Bitmap Bitmap => _bitmap;
    /// <inheritdoc/>
    public int Width => _bitmap.Width;
    /// <inheritdoc/>
    public int Height => _bitmap.Height;
    /// <inheritdoc/>
    public int Stride => _bitmap.Stride;

    /// <inheritdoc/>
    public Span<byte> GetRowSpan(int y) => _bitmap.GetRow(y);

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (Width == width && Height == height) return;

        var previous = _bitmap;
        _bitmap = new Bitmap(width, height);
        previous.Dispose();
    }

    /// <inheritdoc/>
    public void Present(IReadOnlyList<Rect>? dirtyRects)
    {
        if (dirtyRects is { Count: 0 }) return;
        _presentFrame?.Invoke(_bitmap, dirtyRects);
    }

    /// <inheritdoc/>
    public void Dispose() => _bitmap.Dispose();
}