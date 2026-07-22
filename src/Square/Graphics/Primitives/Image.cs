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

    public Bitmap(int width, int height)
    {
        Width = width; Height = height;
        Stride = width * 4;
        Pixels = new byte[Stride * height];
    }

    public Span<byte> GetRow(int y) => Pixels.AsSpan(y * Stride, Stride);
    public Span<byte> GetPixel(int x, int y) => Pixels.AsSpan((y * Stride) + (x * 4), 4);

    protected override void DisposeCore() => Pixels = [];
}
