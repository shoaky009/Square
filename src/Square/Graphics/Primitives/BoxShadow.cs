using System.Globalization;

namespace Square.Graphics;

public readonly record struct BoxShadow(float OffsetX, float OffsetY, float BlurRadius, float SpreadRadius, Color Color)
{
    public static bool TryParse(string? value, out BoxShadow shadow)
    {
        shadow = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("inset", StringComparison.OrdinalIgnoreCase) ||
            HasTopLevelComma(text)) return false;

        var tokens = Tokenize(text);
        var lengths = new List<float>(4);
        var color = Color.FromRgba(0, 0, 0, 64);
        foreach (var token in tokens)
        {
            if (TryParseLength(token, out var length))
            {
                lengths.Add(length);
                continue;
            }
            if (!TryParseColor(token, out color)) return false;
        }

        if (lengths.Count is < 2 or > 4) return false;
        shadow = new BoxShadow(
            lengths[0], lengths[1],
            Math.Max(0, lengths.Count > 2 ? lengths[2] : 0),
            lengths.Count > 3 ? lengths[3] : 0,
            color);
        return true;
    }

    private static bool HasTopLevelComma(string text)
    {
        var depth = 0;
        foreach (var character in text)
        {
            if (character == '(') depth++;
            else if (character == ')') depth--;
            else if (character == ',' && depth == 0) return true;
        }
        return false;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') depth--;
                if (!char.IsWhiteSpace(text[i]) || depth > 0) continue;
            }
            if (i > start) tokens.Add(text[start..i]);
            start = i + 1;
        }
        return tokens;
    }

    private static bool TryParseLength(string token, out float value)
    {
        var text = token.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text[..^2];
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseColor(string token, out Color color)
    {
        var text = token.Trim();
        try
        {
            if (text.StartsWith('#'))
            {
                color = Color.Parse(text);
                return true;
            }
            if (text.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
            {
                var parts = text[5..^1].Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != 4 || !byte.TryParse(parts[0], out var r) || !byte.TryParse(parts[1], out var g) ||
                    !byte.TryParse(parts[2], out var b) ||
                    !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha))
                {
                    color = default;
                    return false;
                }
                color = Color.FromRgba(r, g, b, (byte)Math.Clamp(MathF.Round(alpha * 255), 0, 255));
                return true;
            }
            if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
            {
                var parts = text[4..^1].Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length == 3 && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b))
                {
                    color = Color.FromRgb(r, g, b);
                    return true;
                }
            }
        }
        catch (FormatException) { }
        color = default;
        return false;
    }
}

public static class BoxShadowRendering
{
    public static Rect GetVisualBounds(Rect box, BoxShadow shadow)
    {
        if (box.IsEmpty || shadow.Color.A == 0) return box;
        var shadowBounds = box.Offset(shadow.OffsetX, shadow.OffsetY)
            .Inflate(shadow.SpreadRadius + Math.Max(0, shadow.BlurRadius), shadow.SpreadRadius + Math.Max(0, shadow.BlurRadius));
        return shadowBounds.IsEmpty ? box : Rect.Union(box, shadowBounds);
    }

    public static void Draw(IRenderContext context, Rect box, float cornerRadius, BoxShadow shadow)
    {
        if (box.IsEmpty || shadow.Color.A == 0) return;
        var baseRect = box.Offset(shadow.OffsetX, shadow.OffsetY).Inflate(shadow.SpreadRadius, shadow.SpreadRadius);
        if (baseRect.IsEmpty) return;
        var blur = Math.Max(0, shadow.BlurRadius);
        var steps = blur <= 0 ? 1 : Math.Clamp((int)MathF.Ceiling(blur), 2, 24);
        for (var i = steps; i >= 1; i--)
        {
            var t = steps == 1 ? 0 : i / (float)steps;
            var expansion = blur * t;
            var alphaScale = steps == 1 ? 1f : MathF.Pow(1f - t, 1.6f) * 0.42f;
            var alpha = (byte)Math.Clamp(MathF.Round(shadow.Color.A * alphaScale), 0, 255);
            if (alpha == 0) continue;
            var rect = baseRect.Inflate(expansion, expansion);
            var radius = Math.Max(0, cornerRadius + shadow.SpreadRadius + expansion);
            var color = Color.FromRgba(shadow.Color.R, shadow.Color.G, shadow.Color.B, alpha);
            if (radius <= 0) context.FillRect(rect, new SolidColorBrush(color));
            else context.FillGeometry(new RoundedRectGeometry(rect, radius, radius), new SolidColorBrush(color));
        }
    }
}
