namespace Square.Graphics;

/// <summary>画笔，描述描边的画刷、宽度与样式。</summary>
public sealed class Pen
{
    /// <summary>描边画刷。</summary>
    public Brush Brush { get; set; }
    /// <summary>描边宽度（逻辑像素）。</summary>
    public float Width { get; set; } = 1f;
    /// <summary>描边样式；为 null 时使用默认值。</summary>
    public StrokeStyle? StrokeStyle { get; set; }

    /// <summary>构造画笔。</summary>
    public Pen(Brush brush, float width = 1f, StrokeStyle? style = null)
    {
        Brush = brush; Width = width; StrokeStyle = style;
    }

    /// <summary>由颜色构造纯色描边画笔。</summary>
    public static Pen FromColor(Color color, float width = 1f) =>
        new(new SolidColorBrush(color), width);
}