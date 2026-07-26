namespace Square.Graphics;

/// <summary>画刷基类，描述填充或描边的来源。</summary>
public abstract class Brush
{
    /// <summary>由颜色构造纯色画刷。</summary>
    public static SolidColorBrush FromColor(Color color) => new(color);
}

/// <summary>纯色画刷。</summary>
public sealed class SolidColorBrush : Brush
{
    /// <summary>颜色。</summary>
    public Color Color { get; set; }
    /// <summary>用颜色构造。</summary>
    public SolidColorBrush(Color color) { Color = color; }
    /// <summary>用 RGBA 分量构造。</summary>
    public SolidColorBrush(byte r, byte g, byte b, byte a = 255) { Color = new Color(r, g, b, a); }
}

/// <summary>渐变超出 [0,1] 范围时的铺展方式。</summary>
public enum GradientSpreadMethod { Pad, Reflect, Repeat }

/// <summary>线性渐变画刷。</summary>
public sealed class LinearGradientBrush : Brush
{
    /// <summary>渐变起点。</summary>
    public Point Start { get; set; }
    /// <summary>渐变终点。</summary>
    public Point End { get; set; }
    /// <summary>渐变停靠点。</summary>
    public GradientStop[] Stops { get; set; } = [];
    /// <summary>超出范围的铺展方式。</summary>
    public GradientSpreadMethod SpreadMethod { get; set; } = GradientSpreadMethod.Pad;

    /// <summary>构造线性渐变。</summary>
    public LinearGradientBrush(Point start, Point end, params GradientStop[] stops)
    {
        Start = start; End = end; Stops = stops;
    }
}

/// <summary>径向渐变画刷。</summary>
public sealed class RadialGradientBrush : Brush
{
    /// <summary>圆心。</summary>
    public Point Center { get; set; }
    /// <summary>半径。</summary>
    public float Radius { get; set; }
    /// <summary>渐变停靠点。</summary>
    public GradientStop[] Stops { get; set; } = [];
    /// <summary>超出范围的铺展方式。</summary>
    public GradientSpreadMethod SpreadMethod { get; set; } = GradientSpreadMethod.Pad;

    /// <summary>构造径向渐变。</summary>
    public RadialGradientBrush(Point center, float radius, params GradientStop[] stops)
    {
        Center = center; Radius = radius; Stops = stops;
    }
}

/// <summary>渐变停靠点。</summary>
public sealed class GradientStop
{
    /// <summary>归一化偏移（0 表示起点，1 表示终点）。</summary>
    public float Offset { get; set; }
    /// <summary>该偏移处的颜色。</summary>
    public Color Color { get; set; }

    /// <summary>构造停靠点。</summary>
    public GradientStop(float offset, Color color)
    {
        Offset = offset; Color = color;
    }
}