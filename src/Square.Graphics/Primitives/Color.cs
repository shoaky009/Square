namespace Square.Graphics;

public readonly struct Color : IEquatable<Color>
{
    public readonly byte R, G, B, A;

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color White = new(255, 255, 255, 255);
    public static readonly Color Red = new(255, 0, 0, 255);
    public static readonly Color Green = new(0, 255, 0, 255);
    public static readonly Color Blue = new(0, 0, 255, 255);

    public uint ToPackedBgra() => (uint)(A << 24 | R << 16 | G << 8 | B);

    public static Color Parse(string hex)
    {
        var s = hex.TrimStart('#');
        return s.Length switch
        {
            3 => new(
                (byte)(Convert.ToByte(s[0..1], 16) * 17),
                (byte)(Convert.ToByte(s[1..2], 16) * 17),
                (byte)(Convert.ToByte(s[2..3], 16) * 17), 255),
            6 => new(
                Convert.ToByte(s[0..2], 16),
                Convert.ToByte(s[2..4], 16),
                Convert.ToByte(s[4..6], 16), 255),
            8 => new(
                Convert.ToByte(s[2..4], 16),
                Convert.ToByte(s[4..6], 16),
                Convert.ToByte(s[6..8], 16),
                Convert.ToByte(s[0..2], 16)),
            _ => throw new FormatException($"Invalid color hex: {hex}")
        };
    }

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}