using Square.Graphics;

namespace Square.Platform.X11;

internal sealed unsafe class X11SoftwareRenderSurface : ISoftwareRenderSurface
{
    private readonly IntPtr _display;
    private readonly IntPtr _window;
    private readonly IntPtr _visual;
    private readonly int _depth;
    private readonly IntPtr _gc;
    private IntPtr _image;
    private IntPtr _bits;
    private IntPtr _pixmap;
    private int _width;
    private int _height;
    private int _stride;
    private bool _disposed;

    public X11SoftwareRenderSurface(
        IntPtr display, IntPtr window, IntPtr visual, int depth, IntPtr gc, int width, int height)
    {
        _display = display;
        _window = window;
        _visual = visual;
        _depth = depth;
        _gc = gc;
        Resize(width, height);
    }

    public int Width => _width;
    public int Height => _height;
    public int Stride => _stride;

    public Span<byte> GetRowSpan(int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)y >= (uint)_height) throw new ArgumentOutOfRangeException(nameof(y));
        return new Span<byte>((void*)IntPtr.Add(_bits, y * _stride), _stride);
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_width == width && _height == height) return;

        var stride = checked(width * 4);
        var bits = X11Api.Malloc(checked((nuint)(stride * height)));
        if (bits == IntPtr.Zero) throw new OutOfMemoryException("Unable to allocate XImage pixels.");
        var image = X11Api.CreateImage(
            _display, _visual, _depth, X11Api.ZPixmap, 0, bits,
            (uint)width, (uint)height, X11Api.BitmapPad, stride);
        if (image == IntPtr.Zero)
        {
            X11Api.CFree(bits);
            throw new InvalidOperationException("XCreateImage failed.");
        }
        var pixmap = X11Api.CreatePixmap(_display, _window, (uint)width, (uint)height, _depth);
        if (pixmap == IntPtr.Zero)
        {
            X11Api.DestroyImage(image);
            throw new InvalidOperationException("XCreatePixmap failed.");
        }

        ReleaseResources();
        _image = image;
        _bits = bits;
        _pixmap = pixmap;
        _width = width;
        _height = height;
        _stride = stride;
    }

    public void Present(IReadOnlyList<Rect>? dirtyRects)
    {
        if (dirtyRects is { Count: 0 } || _image == IntPtr.Zero) return;
        if (dirtyRects == null)
        {
            PutRect(0, 0, _width, _height);
        }
        else
        {
            foreach (var rect in dirtyRects)
            {
                var left = Math.Clamp((int)MathF.Floor(rect.Left), 0, _width);
                var top = Math.Clamp((int)MathF.Floor(rect.Top), 0, _height);
                var right = Math.Clamp((int)MathF.Ceiling(rect.Right), left, _width);
                var bottom = Math.Clamp((int)MathF.Ceiling(rect.Bottom), top, _height);
                if (right > left && bottom > top) PutRect(left, top, right - left, bottom - top);
            }
        }
        X11Api.Flush(_display);
    }

    internal void Repaint()
    {
        if (_image == IntPtr.Zero) return;
        X11Api.PutImage(
            _display, _window, _gc, _image,
            0, 0, 0, 0, (uint)_width, (uint)_height);
        X11Api.Flush(_display);
    }

    private void PutRect(int x, int y, int width, int height)
    {
        X11Api.PutImage(
            _display, _pixmap, _gc, _image,
            x, y, x, y, (uint)width, (uint)height);
        X11Api.PutImage(
            _display, _window, _gc, _image,
            x, y, x, y, (uint)width, (uint)height);
    }

    private void ReleaseResources()
    {
        if (_image != IntPtr.Zero)
            X11Api.DestroyImage(_image);
        else if (_bits != IntPtr.Zero)
            X11Api.CFree(_bits);
        if (_pixmap != IntPtr.Zero)
            X11Api.FreePixmap(_display, _pixmap);
        _image = IntPtr.Zero;
        _bits = IntPtr.Zero;
        _pixmap = IntPtr.Zero;
        _width = 0;
        _height = 0;
        _stride = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseResources();
    }
}
