namespace Square.Graphics;

/// <summary>二维尺寸（逻辑像素）。</summary>
public readonly struct Size : IEquatable<Size>
{
    /// <summary>宽度和高度。</summary>
    public readonly float Width, Height;

    /// <summary>构造尺寸。</summary>
    public Size(float width, float height) { Width = width; Height = height; }
    /// <summary>零尺寸 (0, 0)。</summary>
    public static readonly Size Zero = new(0, 0);
    /// <summary>空尺寸（NaN 表示未设置）。</summary>
    public static readonly Size Empty = new(float.NaN, float.NaN);

    /// <summary>是否为空（任一维度为 NaN）。</summary>
    public readonly bool IsEmpty => float.IsNaN(Width) || float.IsNaN(Height);

    /// <summary>尺寸相加。</summary>
    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);
    /// <summary>尺寸相减。</summary>
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
    /// <summary>按标量缩放。</summary>
    public static Size operator *(Size a, float s) => new(a.Width * s, a.Height * s);

    /// <summary>按宽高比较相等。</summary>
    public bool Equals(Size other) => Width == other.Width && Height == other.Height;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Size s && Equals(s);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    /// <summary>相等运算符。</summary>
    public static bool operator ==(Size a, Size b) => a.Equals(b);
    /// <summary>不相等运算符。</summary>
    public static bool operator !=(Size a, Size b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => $"{Width}x{Height}";
}