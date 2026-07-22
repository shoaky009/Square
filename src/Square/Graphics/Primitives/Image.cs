namespace Square.Graphics;

public abstract class Image : IDisposable
{
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            DisposeCore();
            IsDisposed = true;
        }
    }

    protected abstract void DisposeCore();
}

public sealed class Bitmap : Image
{
    public byte[] Pixels { get; private set; }
    public readonly int Stride;
    public long ContentVersion { get; private set; }

    public Bitmap(int width, int height)
    {
        Width = width; Height = height;
        Stride = width * 4;
        Pixels = new byte[Stride * height];
    }

    public Span<byte> GetRow(int y) => Pixels.AsSpan(y * Stride, Stride);
    public Span<byte> GetPixel(int x, int y) => Pixels.AsSpan((y * Stride) + (x * 4), 4);

    public void SetPixels(ReadOnlySpan<byte> pixels)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (pixels.Length != Pixels.Length)
            throw new ArgumentException("Pixel data must exactly match the bitmap dimensions.", nameof(pixels));
        pixels.CopyTo(Pixels);
        MarkDirty();
    }

    public void CopyPixelsFrom(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        if (source.Width != Width || source.Height != Height)
            throw new ArgumentException("Source and destination bitmap dimensions must match.", nameof(source));
        SetPixels(source.Pixels);
    }

    public void MarkDirty()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ContentVersion++;
    }

    protected override void DisposeCore() => Pixels = [];
}
