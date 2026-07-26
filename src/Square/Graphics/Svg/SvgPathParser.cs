using System.Globalization;

namespace Square.Graphics.Svg;

internal static class SvgPathParser
{
    /// <summary>解析 SVG path 数据字符串并返回 <see cref="PathGeometry"/>；空或空白返回 null。</summary>
    public static PathGeometry? Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        var path = PathGeometry.Create();
        var index = 0;
        var command = '\0';
        var current = new Point(0, 0);
        var start = current;
        var lastControl = current;
        while (index < data.Length)
        {
            SkipSeparators(data, ref index);
            if (index >= data.Length) break;
            if (char.IsLetter(data[index])) command = data[index++];
            if (command == '\0') throw new InvalidDataException("SVG path data must start with a command.");
            var relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    if (!ReadPoint(data, ref index, relative, current, out var move)) throw InvalidPath();
                    current = move; start = move; path.MoveTo(move); command = relative ? 'l' : 'L';
                    break;
                case 'L':
                    if (!ReadPoint(data, ref index, relative, current, out var line)) throw InvalidPath();
                    current = line; path.LineTo(line);
                    break;
                case 'H':
                    if (!TryReadNumber(data, ref index, out var x)) throw InvalidPath();
                    current = new Point(relative ? current.X + x : x, current.Y); path.LineTo(current);
                    break;
                case 'V':
                    if (!TryReadNumber(data, ref index, out var y)) throw InvalidPath();
                    current = new Point(current.X, relative ? current.Y + y : y); path.LineTo(current);
                    break;
                case 'C':
                    if (!ReadPoint(data, ref index, relative, current, out var c1) ||
                        !ReadPoint(data, ref index, relative, current, out var c2) ||
                        !ReadPoint(data, ref index, relative, current, out var end)) throw InvalidPath();
                    FlattenCubic(path, current, c1, c2, end); current = end; lastControl = c2;
                    break;
                case 'S':
                    if (!ReadPoint(data, ref index, relative, current, out var smoothControl) ||
                        !ReadPoint(data, ref index, relative, current, out var smoothEnd)) throw InvalidPath();
                    var reflected = new Point(current.X * 2 - lastControl.X, current.Y * 2 - lastControl.Y);
                    FlattenCubic(path, current, reflected, smoothControl, smoothEnd);
                    current = smoothEnd; lastControl = smoothControl;
                    break;
                case 'Q':
                    if (!ReadPoint(data, ref index, relative, current, out var control) ||
                        !ReadPoint(data, ref index, relative, current, out var quadraticEnd)) throw InvalidPath();
                    FlattenQuadratic(path, current, control, quadraticEnd); current = quadraticEnd; lastControl = control;
                    break;
                case 'T':
                    if (!ReadPoint(data, ref index, relative, current, out var quadraticSmoothEnd)) throw InvalidPath();
                    var quadraticControl = new Point(current.X * 2 - lastControl.X, current.Y * 2 - lastControl.Y);
                    FlattenQuadratic(path, current, quadraticControl, quadraticSmoothEnd);
                    current = quadraticSmoothEnd; lastControl = quadraticControl;
                    break;
                case 'Z':
                    path.Close(); current = start; command = '\0';
                    break;
                default:
                    throw new InvalidDataException($"Unsupported SVG path command '{command}'.");
            }
        }
        return path;
    }

    internal static bool TryReadNumber(string text, ref int index, out float value)
    {
        SkipSeparators(text, ref index);
        var start = index;
        if (index < text.Length && text[index] is '+' or '-') index++;
        var digits = false;
        while (index < text.Length && char.IsDigit(text[index])) { index++; digits = true; }
        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsDigit(text[index])) { index++; digits = true; }
        }
        if (!digits) { value = 0; index = start; return false; }
        if (index < text.Length && text[index] is 'e' or 'E')
        {
            var exponent = index++;
            if (index < text.Length && text[index] is '+' or '-') index++;
            var exponentStart = index;
            while (index < text.Length && char.IsDigit(text[index])) index++;
            if (exponentStart == index) index = exponent;
        }
        return float.TryParse(text.AsSpan(start, index - start), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
    }

    private static bool ReadPoint(string data, ref int index, bool relative, Point origin, out Point point)
    {
        point = default;
        if (!TryReadNumber(data, ref index, out var x) || !TryReadNumber(data, ref index, out var y)) return false;
        point = relative ? new Point(origin.X + x, origin.Y + y) : new Point(x, y);
        return true;
    }

    private static void FlattenCubic(PathGeometry path, Point p0, Point p1, Point p2, Point p3)
    {
        const int segments = 16;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1 - t;
            path.LineTo(new Point(u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
                u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y));
        }
    }

    private static void FlattenQuadratic(PathGeometry path, Point p0, Point p1, Point p2)
    {
        const int segments = 12;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1 - t;
            path.LineTo(new Point(u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X,
                u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y));
        }
    }

    private static void SkipSeparators(string text, ref int index)
    {
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ',')) index++;
    }

    private static InvalidDataException InvalidPath() => new("Invalid SVG path data.");
}
