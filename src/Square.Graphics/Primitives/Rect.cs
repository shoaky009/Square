namespace Square.Graphics;

public readonly struct Rect : IEquatable<Rect>
{
    public readonly float X, Y, Width, Height;

    public Rect(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public Rect(Point pos, Size size) : this(pos.X, pos.Y, size.Width, size.Height) { }

    public static readonly Rect Empty = new(0, 0, 0, 0);

    public readonly float Left => X;
    public readonly float Top => Y;
    public readonly float Right => X + Width;
    public readonly float Bottom => Y + Height;

    public readonly Point Position => new(X, Y);
    public readonly Size Size => new(Width, Height);
    public readonly Point Center => new(X + Width / 2f, Y + Height / 2f);

    public readonly bool IsEmpty => Width <= 0 || Height <= 0;

    public readonly bool Contains(Point p) =>
        p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public readonly bool Contains(float px, float py) =>
        px >= X && px <= Right && py >= Y && py <= Bottom;

    public readonly bool IntersectsWith(Rect other) =>
        other.Left < Right && other.Right > Left &&
        other.Top < Bottom && other.Bottom > Top;

    public static Rect Union(Rect a, Rect b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect(x, y, right - x, bottom - y);
    }

    public static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= x || bottom <= y) return Empty;
        return new Rect(x, y, right - x, bottom - y);
    }

    public Rect Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);
    public Rect Inflate(float dx, float dy) => new(X - dx, Y - dy, Width + dx * 2, Height + dy * 2);

    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(Rect a, Rect b) => a.Equals(b);
    public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

    public override string ToString() => $"[{X},{Y} {Width}x{Height}]";
}