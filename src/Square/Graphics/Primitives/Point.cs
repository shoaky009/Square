namespace Square.Graphics;

/// <summary>二维点（逻辑像素坐标）。</summary>
public readonly struct Point : IEquatable<Point>
{
    /// <summary>X 坐标。</summary>
    public readonly float X, Y;

    /// <summary>构造坐标 (<paramref name="x"/>, <paramref name="y"/>)。</summary>
    public Point(float x, float y) { X = x; Y = y; }
    /// <summary>原点 (0, 0)。</summary>
    public static readonly Point Zero = new(0, 0);

    /// <summary>向量加法。</summary>
    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    /// <summary>向量减法。</summary>
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    /// <summary>按标量缩放。</summary>
    public static Point operator *(Point a, float s) => new(a.X * s, a.Y * s);

    /// <summary>按坐标比较相等。</summary>
    public bool Equals(Point other) => X == other.X && Y == other.Y;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Point p && Equals(p);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);
    /// <summary>相等运算符。</summary>
    public static bool operator ==(Point a, Point b) => a.Equals(b);
    /// <summary>不相等运算符。</summary>
    public static bool operator !=(Point a, Point b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => $"({X}, {Y})";
}