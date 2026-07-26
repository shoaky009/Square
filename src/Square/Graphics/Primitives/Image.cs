namespace Square.Graphics;

/// <summary>图像基类。</summary>
public abstract class Image : IDisposable
{
    /// <summary>宽度（物理像素）。</summary>
    public int Width { get; protected set; }
    /// <summary>高度（物理像素）。</summary>
    public int Height { get; protected set; }
    /// <summary>是否已释放。</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>释放资源。</summary>
    public void Dispose()
    {
        if (!IsDisposed)
        {
            DisposeCore();
            IsDisposed = true;
        }
    }

    /// <summary>派生类实现释放逻辑。</summary>
    protected abstract void DisposeCore();
}

/// <summary>BGRA 位图，像素存储在连续字节数组中。</summary>
public sealed class Bitmap : Image
{
    /// <summary>BGRA 像素数据。</summary>
    public byte[] Pixels { get; private set; }
    /// <summary>每行字节数。</summary>
    public readonly int Stride;
    /// <summary>内容版本号，每次修改递增，用于缓存失效。</summary>
    public long ContentVersion { get; private set; }

    /// <summary>构造指定位宽高的位图。</summary>
    public Bitmap(int width, int height)
    {
        Width = width; Height = height;
        Stride = width * 4;
        Pixels = new byte[Stride * height];
    }

    /// <summary>获取指定行的可写跨度。</summary>
    public Span<byte> GetRow(int y) => Pixels.AsSpan(y * Stride, Stride);
    /// <summary>获取指定像素的 4 字节 BGRA 跨度。</summary>
    public Span<byte> GetPixel(int x, int y) => Pixels.AsSpan((y * Stride) + (x * 4), 4);

    /// <summary>整体替换像素数据并标记为脏。</summary>
    /// <exception cref="ArgumentException">长度不匹配。</exception>
    public void SetPixels(ReadOnlySpan<byte> pixels)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (pixels.Length != Pixels.Length)
            throw new ArgumentException("Pixel data must exactly match the bitmap dimensions.", nameof(pixels));
        pixels.CopyTo(Pixels);
        MarkDirty();
    }

    /// <summary>从另一同尺寸位图复制像素。</summary>
    public void CopyPixelsFrom(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        if (source.Width != Width || source.Height != Height)
            throw new ArgumentException("Source and destination bitmap dimensions must match.", nameof(source));
        SetPixels(source.Pixels);
    }

    /// <summary>标记内容已修改并递增 <see cref="ContentVersion"/>。</summary>
    public void MarkDirty()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ContentVersion++;
    }

    /// <inheritdoc/>
    protected override void DisposeCore() => Pixels = [];
}