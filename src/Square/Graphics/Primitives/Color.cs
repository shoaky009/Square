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
        if (TryParse(hex, out var color)) return color;
        throw new FormatException($"Invalid color hex: {hex}");
    }

    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var span = value.AsSpan().Trim();
        if (!span.IsEmpty && span[0] == '#') span = span[1..];
        switch (span.Length)
        {
            case 3:
                if (!TryHex(span[0], out var r) ||
                    !TryHex(span[1], out var g) ||
                    !TryHex(span[2], out var b)) return false;
                color = new Color((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
                return true;
            case 6:
                if (!TryHexByte(span, 0, out var red) ||
                    !TryHexByte(span, 2, out var green) ||
                    !TryHexByte(span, 4, out var blue)) return false;
                color = new Color(red, green, blue);
                return true;
            case 8:
                if (!TryHexByte(span, 0, out var alpha) ||
                    !TryHexByte(span, 2, out red) ||
                    !TryHexByte(span, 4, out green) ||
                    !TryHexByte(span, 6, out blue)) return false;
                color = new Color(red, green, blue, alpha);
                return true;
            default:
                return false;
        }
    }

    private static bool TryHexByte(ReadOnlySpan<char> value, int index, out byte result)
    {
        result = 0;
        if (!TryHex(value[index], out var high) || !TryHex(value[index + 1], out var low)) return false;
        result = (byte)(high * 16 + low);
        return true;
    }

    private static bool TryHex(char value, out byte result)
    {
        if (value is >= '0' and <= '9')
        {
            result = (byte)(value - '0');
            return true;
        }
        if (value is >= 'a' and <= 'f')
        {
            result = (byte)(value - 'a' + 10);
            return true;
        }
        if (value is >= 'A' and <= 'F')
        {
            result = (byte)(value - 'A' + 10);
            return true;
        }
        result = 0;
        return false;
    }

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
