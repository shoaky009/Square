namespace Square.Graphics;

/// <summary>矢量图像基类，可按目标矩形绘制到渲染上下文。</summary>
public abstract class VectorImage : Image
{
    /// <summary>将矢量内容绘制到指定目标矩形。</summary>
    public abstract void Draw(IRenderContext context, Rect destination);
}