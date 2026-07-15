using System.Numerics;

namespace Square.Graphics;

public sealed class RenderContextCreateInfo
{
    public required Size CanvasSize { get; set; }
    public float DpiScale { get; set; } = 1f;
    public bool VSync { get; set; } = true;
    public Action<Bitmap>? PresentFrame { get; set; }
}

public interface IRenderContext : IDisposable
{
    Size CanvasSize { get; }
    float DpiScale { get; }

    void PushTransform(Matrix3x2 matrix);
    void PopTransform();

    void PushClip(Rect rect);
    void PushClip(Geometry geometry);
    void PopClip();

    void FillRect(Rect rect, Brush brush);
    void DrawRect(Rect rect, Pen pen);
    void FillPath(PathGeometry path, Brush brush);
    void DrawPath(PathGeometry path, Pen pen);
    void FillGeometry(Geometry geometry, Brush brush);
    void DrawGeometry(Geometry geometry, Pen pen);
    void DrawText(TextLayout text, Point origin, Brush brush);
    void DrawImage(Image image, Rect dest, Rect? source = null);

    void PushLayer(Rect bounds, float opacity);
    void PopLayer();

    void Clear(Color color);
    void Flush();
    void Present();
}

public interface IResizableRenderContext
{
    void Resize(Size canvasSize);
}
