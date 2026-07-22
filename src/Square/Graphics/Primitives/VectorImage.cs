namespace Square.Graphics;

public abstract class VectorImage : Image
{
    public abstract void Draw(IRenderContext context, Rect destination);
}
