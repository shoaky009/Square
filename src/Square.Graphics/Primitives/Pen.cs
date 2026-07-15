namespace Square.Graphics;

public sealed class Pen
{
    public Brush Brush { get; set; }
    public float Width { get; set; } = 1f;
    public StrokeStyle? StrokeStyle { get; set; }

    public Pen(Brush brush, float width = 1f, StrokeStyle? style = null)
    {
        Brush = brush; Width = width; StrokeStyle = style;
    }

    public static Pen FromColor(Color color, float width = 1f) =>
        new(new SolidColorBrush(color), width);
}