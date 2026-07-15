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
        GC.SuppressFinalize(this);
    }

    protected abstract void DisposeCore();
    ~Image() => Dispose();
}

public sealed class Bitmap : Image
{
    public byte[] Pixels { get; }
    public int Stride { get; }

    public Bitmap(int width, int height)
    {
        Width = width; Height = height;
        Stride = width * 4;
        Pixels = new byte[Stride * height];
    }

    public Span<byte> GetRow(int y) => Pixels.AsSpan(y * Stride, Stride);
    public Span<byte> GetPixel(int x, int y) => Pixels.AsSpan((y * Stride) + (x * 4), 4);

    protected override void DisposeCore() { }
}