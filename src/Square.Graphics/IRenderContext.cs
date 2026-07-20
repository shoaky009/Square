using System.Numerics;

namespace Square.Graphics;

/// <summary>
/// Present 回调：<paramref name="dirtyRects"/> 为 null 时表示整窗；
/// 非 null 时仅上传列表中的矩形（逻辑像素，与 Bitmap 同坐标系）。
/// </summary>
public delegate void PresentFrameHandler(Bitmap bitmap, IReadOnlyList<Rect>? dirtyRects);

public sealed class RenderContextCreateInfo
{
    public required Size CanvasSize { get; set; }
    public float DpiScale { get; set; } = 1f;
    public bool VSync { get; set; } = true;
    public PresentFrameHandler? PresentFrame { get; set; }
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

    /// <summary>清除整个帧缓冲。</summary>
    void Clear(Color color);

    /// <summary>仅清除指定矩形（受当前 clip 约束）。</summary>
    void Clear(Color color, Rect rect);

    void Flush();

    /// <summary>整窗 Present。</summary>
    void Present();

    /// <summary>
    /// 局部 Present。空列表视为 no-op；null 视为整窗。
    /// </summary>
    void Present(IReadOnlyList<Rect>? dirtyRects);
}

public interface IResizableRenderContext
{
    void Resize(Size canvasSize);
}

public interface IRenderBitmapSource
{
    Bitmap CaptureBitmap();
}
