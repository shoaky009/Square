using System.Globalization;
using System.Numerics;
using System.Xml;

namespace Square.Graphics.Svg;

/// <summary>SVG 矢量图像：解析 SVG 文本并以矢量方式绘制到渲染上下文。</summary>
public sealed class SvgImage : VectorImage
{
    private readonly Rect _viewBox;
    private readonly SvgNode _root;

    private SvgImage(int width, int height, Rect viewBox, SvgNode root)
    {
        Width = width;
        Height = height;
        _viewBox = viewBox;
        _root = root;
    }

    /// <summary>从文件路径加载 SVG 图像。</summary>
    public static SvgImage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>从流加载 SVG 图像。</summary>
    public static SvgImage Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, leaveOpen: true);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>解析 SVG 文本为图像实例。</summary>
    public static SvgImage Parse(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(svg), settings);
        var document = new XmlDocument { XmlResolver = null };
        document.Load(reader);
        var element = document.DocumentElement;
        if (element == null || !element.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The document root must be an svg element.");

        var viewBox = ParseViewBox(element.GetAttribute("viewBox"));
        var widthValue = ParseLength(element.GetAttribute("width"), viewBox?.Width ?? 300f);
        var heightValue = ParseLength(element.GetAttribute("height"), viewBox?.Height ?? 150f);
        if (widthValue <= 0 || heightValue <= 0)
            throw new InvalidDataException("SVG width and height must be positive.");

        var width = Math.Max(1, (int)MathF.Ceiling(widthValue));
        var height = Math.Max(1, (int)MathF.Ceiling(heightValue));
        var viewport = viewBox ?? new Rect(0, 0, widthValue, heightValue);
        return new SvgImage(width, height, viewport, ParseNode(element, SvgStyle.Default));
    }

    /// <summary>将 SVG 按保持比例方式绘制到目标矩形内。</summary>
    public override void Draw(IRenderContext context, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsDisposed) throw new ObjectDisposedException(nameof(SvgImage));
        if (destination.IsEmpty || _viewBox.Width <= 0 || _viewBox.Height <= 0) return;

        var scale = MathF.Min(destination.Width / _viewBox.Width, destination.Height / _viewBox.Height);
        var offsetX = destination.X + (destination.Width - _viewBox.Width * scale) / 2f;
        var offsetY = destination.Y + (destination.Height - _viewBox.Height * scale) / 2f;
        var transform = Matrix3x2.CreateTranslation(-_viewBox.X, -_viewBox.Y) *
                        Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);

        context.PushClip(destination);
        context.PushTransform(transform);
        _root.Draw(context);
        context.PopTransform();
        context.PopClip();
    }

    protected override void DisposeCore() { }

    private static SvgNode ParseNode(XmlElement element, SvgStyle inherited)
    {
        var style = SvgStyle.FromElement(element, inherited);
        var transform = ParseTransform(element.GetAttribute("transform"));
        var children = new List<SvgNode>();
        foreach (XmlNode child in element.ChildNodes)
            if (child is XmlElement childElement && IsSupportedElement(childElement.LocalName))
                children.Add(ParseNode(childElement, style));

        Geometry? geometry = element.LocalName.ToLowerInvariant() switch
        {
            "rect" => ParseRect(element),
            "circle" => ParseCircle(element),
            "ellipse" => ParseEllipse(element),
            "line" => ParseLine(element),
            "polyline" => ParsePoints(element, close: false),
            "polygon" => ParsePoints(element, close: true),
            "path" => SvgPathParser.Parse(element.GetAttribute("d")),
            _ => null
        };
        return new SvgNode(geometry, style, transform, children.ToArray());
    }

    private static bool IsSupportedElement(string name) => name.ToLowerInvariant() is
        "svg" or "g" or "rect" or "circle" or "ellipse" or "line" or "polyline" or "polygon" or "path";

    private static Geometry? ParseRect(XmlElement element)
    {
        var x = ParseLength(element.GetAttribute("x"), 0);
        var y = ParseLength(element.GetAttribute("y"), 0);
        var width = ParseLength(element.GetAttribute("width"), 0);
        var height = ParseLength(element.GetAttribute("height"), 0);
        if (width <= 0 || height <= 0) return null;
        var rx = ParseLength(element.GetAttribute("rx"), 0);
        var ry = ParseLength(element.GetAttribute("ry"), rx);
        return rx > 0 || ry > 0
            ? new RoundedRectGeometry(new Rect(x, y, width, height), rx, ry)
            : new RectGeometry(new Rect(x, y, width, height));
    }

    private static Geometry? ParseCircle(XmlElement element)
    {
        var radius = ParseLength(element.GetAttribute("r"), 0);
        return radius > 0 ? new EllipseGeometry(new Point(ParseLength(element.GetAttribute("cx"), 0),
            ParseLength(element.GetAttribute("cy"), 0)), radius, radius) : null;
    }

    private static Geometry? ParseEllipse(XmlElement element)
    {
        var rx = ParseLength(element.GetAttribute("rx"), 0);
        var ry = ParseLength(element.GetAttribute("ry"), 0);
        return rx > 0 && ry > 0 ? new EllipseGeometry(new Point(ParseLength(element.GetAttribute("cx"), 0),
            ParseLength(element.GetAttribute("cy"), 0)), rx, ry) : null;
    }

    private static Geometry ParseLine(XmlElement element) => PathGeometry.Create()
        .MoveTo(new Point(ParseLength(element.GetAttribute("x1"), 0), ParseLength(element.GetAttribute("y1"), 0)))
        .LineTo(new Point(ParseLength(element.GetAttribute("x2"), 0), ParseLength(element.GetAttribute("y2"), 0)));

    private static Geometry? ParsePoints(XmlElement element, bool close)
    {
        var values = ParseNumberList(element.GetAttribute("points"));
        if (values.Count < 4) return null;
        var path = PathGeometry.Create().MoveTo(new Point(values[0], values[1]));
        for (var i = 2; i + 1 < values.Count; i += 2) path.LineTo(new Point(values[i], values[i + 1]));
        return close ? path.Close() : path;
    }

    private static Rect? ParseViewBox(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var values = ParseNumberList(value);
        if (values.Count != 4 || values[2] <= 0 || values[3] <= 0)
            throw new InvalidDataException("Invalid SVG viewBox.");
        return new Rect(values[0], values[1], values[2], values[3]);
    }

    internal static float ParseLength(string value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) value = value[..^2];
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    internal static List<float> ParseNumberList(string value)
    {
        var result = new List<float>();
        var index = 0;
        while (SvgPathParser.TryReadNumber(value, ref index, out var number)) result.Add(number);
        return result;
    }

    private static Matrix3x2 ParseTransform(string value)
    {
        var result = Matrix3x2.Identity;
        var index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ',')) index++;
            var start = index;
            while (index < value.Length && char.IsLetter(value[index])) index++;
            if (start == index) break;
            var name = value[start..index].ToLowerInvariant();
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
            if (index >= value.Length || value[index++] != '(') break;
            var end = value.IndexOf(')', index);
            if (end < 0) break;
            var numbers = ParseNumberList(value[index..end]);
            index = end + 1;
            var matrix = name switch
            {
                "translate" when numbers.Count >= 1 => Matrix3x2.CreateTranslation(numbers[0], numbers.Count > 1 ? numbers[1] : 0),
                "scale" when numbers.Count >= 1 => Matrix3x2.CreateScale(numbers[0], numbers.Count > 1 ? numbers[1] : numbers[0]),
                "rotate" when numbers.Count >= 1 => Matrix3x2.CreateRotation(numbers[0] * MathF.PI / 180f,
                    numbers.Count >= 3 ? new Vector2(numbers[1], numbers[2]) : Vector2.Zero),
                "matrix" when numbers.Count >= 6 => new Matrix3x2(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]),
                _ => Matrix3x2.Identity
            };
            result = matrix * result;
        }
        return result;
    }

    private sealed class SvgNode(Geometry? geometry, SvgStyle style, Matrix3x2 transform, SvgNode[] children)
    {
        public void Draw(IRenderContext context)
        {
            var transformed = !transform.IsIdentity;
            if (transformed) context.PushTransform(transform);
            if (geometry != null)
            {
                if (style.Fill is Color fill && fill.A > 0) context.FillGeometry(geometry, Brush.FromColor(fill));
                if (style.Stroke is Color stroke && stroke.A > 0 && style.StrokeWidth > 0)
                    context.DrawGeometry(geometry, Pen.FromColor(stroke, style.StrokeWidth));
            }
            foreach (var child in children) child.Draw(context);
            if (transformed) context.PopTransform();
        }
    }

    private readonly record struct SvgStyle(Color? Fill, Color? Stroke, float StrokeWidth, float Opacity)
    {
        public static SvgStyle Default => new(Color.Black, null, 1f, 1f);

        public static SvgStyle FromElement(XmlElement element, SvgStyle inherited)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlAttribute attribute in element.Attributes) values[attribute.LocalName] = attribute.Value;
            if (values.TryGetValue("style", out var declaration))
                foreach (var item in declaration.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var separator = item.IndexOf(':');
                    if (separator > 0) values[item[..separator].Trim()] = item[(separator + 1)..].Trim();
                }

            var localOpacity = values.TryGetValue("opacity", out var opacityText)
                ? Math.Clamp(ParseLength(opacityText, 1), 0, 1) : 1f;
            var opacity = inherited.Opacity * localOpacity;
            var fill = values.TryGetValue("fill", out var fillText) ? ParseColor(fillText, opacity) : ApplyOpacity(inherited.Fill, localOpacity);
            var stroke = values.TryGetValue("stroke", out var strokeText) ? ParseColor(strokeText, opacity) : ApplyOpacity(inherited.Stroke, localOpacity);
            if (values.TryGetValue("fill-opacity", out var fillOpacity)) fill = ApplyOpacity(fill, ParseLength(fillOpacity, 1));
            if (values.TryGetValue("stroke-opacity", out var strokeOpacity)) stroke = ApplyOpacity(stroke, ParseLength(strokeOpacity, 1));
            var strokeWidth = values.TryGetValue("stroke-width", out var width) ? ParseLength(width, inherited.StrokeWidth) : inherited.StrokeWidth;
            return new SvgStyle(fill, stroke, strokeWidth, opacity);
        }

        private static Color? ParseColor(string value, float opacity)
        {
            value = value.Trim();
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            var color = value.ToLowerInvariant() switch
            {
                "black" => Color.Black,
                "white" => Color.White,
                "red" => Color.Red,
                "green" => Color.Green,
                "blue" => Color.Blue,
                "transparent" => Color.Transparent,
                _ when value.StartsWith('#') => ParseHex(value),
                _ => Color.Black
            };
            return ApplyOpacity(color, opacity);
        }

        private static Color ParseHex(string value)
        {
            var text = value[1..];
            if (text.Length == 4)
                return new Color(Convert.ToByte(text[0..1], 16), Convert.ToByte(text[1..2], 16),
                    Convert.ToByte(text[2..3], 16), Convert.ToByte(text[3..4], 16));
            if (text.Length == 8)
                return new Color(Convert.ToByte(text[0..2], 16), Convert.ToByte(text[2..4], 16),
                    Convert.ToByte(text[4..6], 16), Convert.ToByte(text[6..8], 16));
            return Color.Parse(value);
        }

        private static Color? ApplyOpacity(Color? color, float opacity) => color is Color value
            ? new Color(value.R, value.G, value.B, (byte)Math.Clamp(MathF.Round(value.A * opacity), 0, 255)) : null;
    }
}
