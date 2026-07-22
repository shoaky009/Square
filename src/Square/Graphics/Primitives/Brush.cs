namespace Square.Graphics;

public abstract class Brush
{
    public static SolidColorBrush FromColor(Color color) => new(color);
}

public sealed class SolidColorBrush : Brush
{
    public Color Color { get; set; }
    public SolidColorBrush(Color color) { Color = color; }
    public SolidColorBrush(byte r, byte g, byte b, byte a = 255) { Color = new Color(r, g, b, a); }
}

public enum GradientSpreadMethod { Pad, Reflect, Repeat }

public sealed class LinearGradientBrush : Brush
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public GradientStop[] Stops { get; set; } = [];
    public GradientSpreadMethod SpreadMethod { get; set; } = GradientSpreadMethod.Pad;

    public LinearGradientBrush(Point start, Point end, params GradientStop[] stops)
    {
        Start = start; End = end; Stops = stops;
    }
}

public sealed class RadialGradientBrush : Brush
{
    public Point Center { get; set; }
    public float Radius { get; set; }
    public GradientStop[] Stops { get; set; } = [];
    public GradientSpreadMethod SpreadMethod { get; set; } = GradientSpreadMethod.Pad;

    public RadialGradientBrush(Point center, float radius, params GradientStop[] stops)
    {
        Center = center; Radius = radius; Stops = stops;
    }
}

public sealed class GradientStop
{
    public float Offset { get; set; }
    public Color Color { get; set; }

    public GradientStop(float offset, Color color)
    {
        Offset = offset; Color = color;
    }
}
