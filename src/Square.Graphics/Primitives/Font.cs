namespace Square.Graphics;

public enum FontWeight : ushort
{
    Thin = 100,
    ExtraLight = 200,
    Light = 300,
    Normal = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    Black = 900
}

public enum FontStyle : byte
{
    Normal,
    Italic,
    Oblique
}

public enum TextAlignment : byte
{
    Left,
    Center,
    Right,
    Justify
}

public sealed class Font
{
    public string Family { get; set; } = "Segoe UI";
    public float Size { get; set; } = 16f;
    public FontWeight Weight { get; set; } = FontWeight.Normal;
    public FontStyle Style { get; set; } = FontStyle.Normal;

    public Font() { }
    public Font(string family, float size) { Family = family; Size = size; }
    public Font(string family, float size, FontWeight weight, FontStyle style = FontStyle.Normal)
    { Family = family; Size = size; Weight = weight; Style = style; }

    public Font WithSize(float size) => new(Family, size, Weight, Style);
    public Font WithWeight(FontWeight weight) => new(Family, Size, weight, Style);
}