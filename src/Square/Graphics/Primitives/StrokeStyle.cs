using System.Numerics;

namespace Square.Graphics;

/// <summary>线帽样式。</summary>
public enum LineCap { Butt, Round, Square }
/// <summary>线段连接样式。</summary>
public enum LineJoin { Miter, Round, Bevel }

/// <summary>描边样式。</summary>
public sealed class StrokeStyle
{
    /// <summary>虚线段长度数组（按描边宽度倍数表示）。</summary>
    public float[]? DashArray { get; set; }
    /// <summary>虚线起始偏移。</summary>
    public float DashOffset { get; set; }
    /// <summary>线帽样式。</summary>
    public LineCap Cap { get; set; } = LineCap.Butt;
    /// <summary>线段连接样式。</summary>
    public LineJoin Join { get; set; } = LineJoin.Miter;
    /// <summary>斜接连接的截断比例。</summary>
    public float MiterLimit { get; set; } = 10f;
}