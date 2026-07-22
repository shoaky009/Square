using System.Text;
using System.Text.RegularExpressions;

namespace Square.Compiler.Parser;

internal static class SqvParser
{
    public static SqxDocument Parse(string source, string fileName)
    {
        var normalized = Normalize(source);
        return SqxParser.Parse(normalized, fileName);
    }

    private static string Normalize(string source)
    {
        var template = FindTemplateSection(source);
        if (template == null) return source;

        var content = NormalizeTemplate(template.Value.Content);
        return source.Substring(0, template.Value.ContentStart) +
               content +
               source.Substring(template.Value.ContentStart + template.Value.Content.Length);
    }

    private static TemplateSection? FindTemplateSection(string source)
    {
        var open = Regex.Match(
            source,
            "<template(?<attrs>[^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!open.Success) return null;

        var closeStart = source.LastIndexOf("</template>", StringComparison.OrdinalIgnoreCase);
        if (closeStart < open.Index + open.Length) return null;

        var contentStart = open.Index + open.Length;
        return new TemplateSection(contentStart, source.Substring(contentStart, closeStart - contentStart));
    }

    private static string NormalizeTemplate(string template)
    {
        var withInterpolations = Regex.Replace(
            template,
            "\\{\\{(?<expr>.*?)\\}\\}",
            match => "{" + match.Groups["expr"].Value.Trim() + "}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        return NormalizeTags(withInterpolations);
    }

    private static string NormalizeTags(string template)
    {
        var result = new StringBuilder();
        var stack = new Stack<TagFrame>();
        var index = 0;
        var tagPattern = new Regex(
            "<(?<close>/)?(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(?<attrs>(?:[^\"'<>]|\"[^\"]*\"|'[^']*')*)(?<self>/?)>",
            RegexOptions.CultureInvariant);

        foreach (Match match in tagPattern.Matches(template))
        {
            result.Append(template, index, match.Index - index);
            index = match.Index + match.Length;

            var isClosing = match.Groups["close"].Success && match.Groups["close"].Value == "/";
            var name = match.Groups["name"].Value;

            if (isClosing)
            {
                if (stack.Count > 0 && stack.Peek().SourceName == name)
                {
                    var frame = stack.Pop();
                    result.Append("</").Append(frame.OutputName).Append('>');
                    if (frame.WrapShow) result.Append("</Show>");
                }
                else
                {
                    result.Append(match.Value);
                }
                continue;
            }

            var rawAttributes = match.Groups["attrs"].Value;
            var selfClosing = match.Groups["self"].Success && match.Groups["self"].Value == "/";
            if (!selfClosing && rawAttributes.TrimEnd().EndsWith("/", StringComparison.Ordinal))
            {
                selfClosing = true;
                rawAttributes = rawAttributes.Substring(0, rawAttributes.LastIndexOf('/'));
            }

            var outputName = string.Equals(name, "template", StringComparison.OrdinalIgnoreCase)
                ? "Fragment"
                : name;

            var attrs = NormalizeAttributes(rawAttributes, outputName, out var showCondition);

            if (showCondition != null)
                result.Append("<Show when={").Append(showCondition).Append("}>");

            result.Append('<').Append(outputName).Append(attrs);
            if (selfClosing)
            {
                result.Append(" />");
                if (showCondition != null) result.Append("</Show>");
            }
            else
            {
                result.Append('>');
                stack.Push(new TagFrame(name, outputName, showCondition != null));
            }
        }

        result.Append(template, index, template.Length - index);
        return result.ToString();
    }

    private static string NormalizeAttributes(string attributes, string tagName, out string showCondition)
    {
        showCondition = null;
        var result = new StringBuilder();
        var pattern = new Regex(
            "(?<name>[:@#.]?[A-Za-z_][A-Za-z0-9_.:-]*)(?:\\s*=\\s*(?:\"(?<dq>[^\"]*)\"|'(?<sq>[^']*)'|(?<bare>[^\\s\"'>/]+)))?",
            RegexOptions.CultureInvariant);

        foreach (Match match in pattern.Matches(attributes))
        {
            var name = match.Groups["name"].Value;
            var value = ReadValue(match);

            if (name == "v-if")
            {
                showCondition = value ?? "false";
                continue;
            }

            if (name == "v-else" || name == "v-else-if")
                continue;

            if (name == "v-text")
            {
                AppendExpressionAttribute(result, "text", value);
                continue;
            }

            if (name == "v-model" || name.StartsWith("v-model.", StringComparison.Ordinal))
            {
                AppendModelAttributes(result, tagName, name, value);
                continue;
            }

            if (name.StartsWith(":", StringComparison.Ordinal))
            {
                AppendExpressionAttribute(result, StripModifiers(name.Substring(1)), value);
                continue;
            }

            if (name.StartsWith("v-bind:", StringComparison.Ordinal))
            {
                AppendExpressionAttribute(result, StripModifiers(name.Substring("v-bind:".Length)), value);
                continue;
            }

            if (name.StartsWith("@", StringComparison.Ordinal))
            {
                AppendExpressionAttribute(result, ToEventAttribute(name.Substring(1)), value);
                continue;
            }

            if (name.StartsWith("v-on:", StringComparison.Ordinal))
            {
                AppendExpressionAttribute(result, ToEventAttribute(name.Substring("v-on:".Length)), value);
                continue;
            }

            if (name.StartsWith("#", StringComparison.Ordinal))
            {
                AppendStaticAttribute(result, "slot", NormalizeSlotName(name.Substring(1)));
                continue;
            }

            if (name.StartsWith("v-slot", StringComparison.Ordinal))
            {
                AppendStaticAttribute(result, "slot", NormalizeSlotName(name.Substring("v-slot".Length)));
                continue;
            }

            AppendStaticAttribute(result, name, value);
        }

        return result.ToString();
    }

    private static string ReadValue(Match match)
    {
        if (match.Groups["dq"].Success) return match.Groups["dq"].Value;
        if (match.Groups["sq"].Success) return match.Groups["sq"].Value;
        return match.Groups["bare"].Success ? match.Groups["bare"].Value : null;
    }

    private static void AppendExpressionAttribute(StringBuilder result, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        result.Append(' ').Append(name).Append("={").Append(value ?? "null").Append('}');
    }

    private static void AppendModelAttributes(StringBuilder result, string tagName, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var modifiers = GetModifiers(name);
        var property = GetModelProperty(tagName);
        if (property == null) return;

        var eventName = GetModelEvent(tagName, modifiers.Contains("lazy"));
        var targetValue = GetModelTargetValue(tagName);
        var writeValue = ApplyModelModifiers(targetValue, modifiers);

        AppendExpressionAttribute(result, property.Value.AttributeName, value);
        AppendExpressionAttribute(result, ToEventAttribute(eventName),
            "e => " + value + ".Value = " + writeValue);
    }

    private static ModelProperty? GetModelProperty(string tagName)
    {
        if (string.Equals(tagName, "CheckBox", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "Radio", StringComparison.OrdinalIgnoreCase))
            return new ModelProperty("checked");

        if (string.Equals(tagName, "Input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "TextArea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "Select", StringComparison.OrdinalIgnoreCase))
            return new ModelProperty("value");

        return null;
    }

    private static string GetModelEvent(string tagName, bool lazy)
    {
        if (string.Equals(tagName, "Input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "TextArea", StringComparison.OrdinalIgnoreCase))
            return lazy ? "change" : "input";

        return "change";
    }

    private static string GetModelTargetValue(string tagName)
    {
        if (string.Equals(tagName, "CheckBox", StringComparison.OrdinalIgnoreCase))
            return "((Square.Controls.CheckBox)e.Target!).IsChecked";
        if (string.Equals(tagName, "Radio", StringComparison.OrdinalIgnoreCase))
            return "((Square.Controls.Radio)e.Target!).IsChecked";
        if (string.Equals(tagName, "TextArea", StringComparison.OrdinalIgnoreCase))
            return "((Square.Controls.TextArea)e.Target!).Value";
        if (string.Equals(tagName, "Select", StringComparison.OrdinalIgnoreCase))
            return "((Square.Controls.Select)e.Target!).Value";

        return "((Square.Controls.Input)e.Target!).Value";
    }

    private static string ApplyModelModifiers(string valueExpression, HashSet<string> modifiers)
    {
        if (modifiers.Contains("trim"))
            valueExpression += ".Trim()";

        if (modifiers.Contains("number"))
            valueExpression = "double.Parse(" + valueExpression + ", System.Globalization.CultureInfo.InvariantCulture)";

        return valueExpression;
    }

    private static HashSet<string> GetModifiers(string name)
    {
        var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var firstDot = name.IndexOf('.');
        if (firstDot < 0) return modifiers;

        foreach (var modifier in name.Substring(firstDot + 1).Split('.'))
            if (!string.IsNullOrWhiteSpace(modifier)) modifiers.Add(modifier);
        return modifiers;
    }

    private static void AppendStaticAttribute(StringBuilder result, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        result.Append(' ').Append(name);
        if (value != null)
            result.Append("=\"").Append(EscapeAttribute(value)).Append('"');
    }

    private static string ToEventAttribute(string eventName)
    {
        eventName = StripModifiers(eventName);
        if (eventName.Length == 0) return "on";
        return "on" + char.ToUpperInvariant(eventName[0]) + eventName.Substring(1);
    }

    private static string StripModifiers(string value)
    {
        var dot = value.IndexOf('.');
        return dot >= 0 ? value.Substring(0, dot) : value;
    }

    private static string EscapeAttribute(string value) =>
        value.Replace("&", "&amp;").Replace("\"", "&quot;");

    private static string NormalizeSlotName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (value.StartsWith(":", StringComparison.Ordinal)) value = value.Substring(1);
        return value == "default" ? "" : value;
    }

    private readonly struct TemplateSection
    {
        public int ContentStart { get; }
        public string Content { get; }

        public TemplateSection(int contentStart, string content)
        {
            ContentStart = contentStart;
            Content = content;
        }
    }

    private readonly struct TagFrame
    {
        public string SourceName { get; }
        public string OutputName { get; }
        public bool WrapShow { get; }

        public TagFrame(string sourceName, string outputName, bool wrapShow)
        {
            SourceName = sourceName;
            OutputName = outputName;
            WrapShow = wrapShow;
        }
    }

    private readonly struct ModelProperty
    {
        public string AttributeName { get; }

        public ModelProperty(string attributeName)
        {
            AttributeName = attributeName;
        }
    }
}
