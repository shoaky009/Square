namespace Square.Graphics;

public readonly struct Size : IEquatable<Size>
{
    public readonly float Width, Height;

    public Size(float width, float height) { Width = width; Height = height; }
    public static readonly Size Zero = new(0, 0);
    public static readonly Size Empty = new(float.NaN, float.NaN);

    public readonly bool IsEmpty => float.IsNaN(Width) || float.IsNaN(Height);

    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
    public static Size operator *(Size a, float s) => new(a.Width * s, a.Height * s);

    public bool Equals(Size other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Size s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public static bool operator ==(Size a, Size b) => a.Equals(b);
    public static bool operator !=(Size a, Size b) => !a.Equals(b);

    public override string ToString() => $"{Width}x{Height}";
}