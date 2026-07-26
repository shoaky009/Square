namespace Square.Graphics;

/// <summary>矩形（逻辑像素坐标）。</summary>
public readonly struct Rect : IEquatable<Rect>
{
    /// <summary>左上角坐标与尺寸。</summary>
    public readonly float X, Y, Width, Height;

    /// <summary>构造矩形。</summary>
    public Rect(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    /// <summary>由位置和尺寸构造矩形。</summary>
    public Rect(Point pos, Size size) : this(pos.X, pos.Y, size.Width, size.Height) { }

    /// <summary>空矩形。</summary>
    public static readonly Rect Empty = new(0, 0, 0, 0);

    /// <summary>左边界。</summary>
    public readonly float Left => X;
    /// <summary>顶边界。</summary>
    public readonly float Top => Y;
    /// <summary>右边界。</summary>
    public readonly float Right => X + Width;
    /// <summary>底边界。</summary>
    public readonly float Bottom => Y + Height;

    /// <summary>左上角位置。</summary>
    public readonly Point Position => new(X, Y);
    /// <summary>尺寸。</summary>
    public readonly Size Size => new(Width, Height);
    /// <summary>中心点。</summary>
    public readonly Point Center => new(X + Width / 2f, Y + Height / 2f);

    /// <summary>是否为空（宽或高 ≤ 0）。</summary>
    public readonly bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>是否包含指定点。</summary>
    public readonly bool Contains(Point p) =>
        p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    /// <summary>是否包含指定点。</summary>
    public readonly bool Contains(float px, float py) =>
        px >= X && px <= Right && py >= Y && py <= Bottom;

    /// <summary>是否与另一矩形相交。</summary>
    public readonly bool IntersectsWith(Rect other) =>
        other.Left < Right && other.Right > Left &&
        other.Top < Bottom && other.Bottom > Top;

    /// <summary>计算两矩形的并集。</summary>
    public static Rect Union(Rect a, Rect b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect(x, y, right - x, bottom - y);
    }

    /// <summary>计算两矩形的交集，无交集返回 <see cref="Empty"/>。</summary>
    public static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= x || bottom <= y) return Empty;
        return new Rect(x, y, right - x, bottom - y);
    }

    /// <summary>平移矩形。</summary>
    public Rect Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);
    /// <summary>向外扩展指定量。</summary>
    public Rect Inflate(float dx, float dy) => new(X - dx, Y - dy, Width + dx * 2, Height + dy * 2);

    /// <inheritdoc/>
    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rect r && Equals(r);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    /// <summary>相等运算符。</summary>
    public static bool operator ==(Rect a, Rect b) => a.Equals(b);
    /// <summary>不相等运算符。</summary>
    public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => $"[{X},{Y} {Width}x{Height}]";
}