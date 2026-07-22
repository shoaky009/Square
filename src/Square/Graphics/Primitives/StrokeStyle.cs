using System.Numerics;

namespace Square.Graphics;

public enum LineCap { Butt, Round, Square }
public enum LineJoin { Miter, Round, Bevel }

public sealed class StrokeStyle
{
    public float[]? DashArray { get; set; }
    public float DashOffset { get; set; }
    public LineCap Cap { get; set; } = LineCap.Butt;
    public LineJoin Join { get; set; } = LineJoin.Miter;
    public float MiterLimit { get; set; } = 10f;
}
