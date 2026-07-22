namespace Square.Graphics;

public readonly struct Point : IEquatable<Point>
{
    public readonly float X, Y;

    public Point(float x, float y) { X = x; Y = y; }
    public static readonly Point Zero = new(0, 0);

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator *(Point a, float s) => new(a.X * s, a.Y * s);

    public bool Equals(Point other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Point a, Point b) => a.Equals(b);
    public static bool operator !=(Point a, Point b) => !a.Equals(b);

    public override string ToString() => $"({X}, {Y})";
}
