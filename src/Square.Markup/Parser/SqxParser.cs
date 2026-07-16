using System.Text.RegularExpressions;
using Square.Markup.Ast;
using Square.Markup.Lexer;

namespace Square.Markup.Parser;

public sealed class SqxParser
{
    public SqxDocument Parse(string source, string fileName = "")
    {
        ArgumentNullException.ThrowIfNull(source);

        var sections = SplitSections(source);
        if (!sections.TryGetValue("template", out var templateSection))
            throw Error(source, 0, "Missing required <template> section");

        var template = ParseTemplate(templateSection.Content, templateSection.ContentLine);
        SqxScript? script = null;
        if (sections.TryGetValue("script", out var scriptSection))
        {
            var metadata = ParseScriptMetadata(source, scriptSection);
            script = new SqxScript(
                metadata.Language,
                scriptSection.Content.Trim(),
                metadata.Namespace,
                metadata.ComponentName,
                metadata.Access,
                scriptSection.ContentLine,
                1);
        }

        SqxStyle? style = null;
        if (sections.TryGetValue("style", out var styleSection))
            style = new SqxStyle(styleSection.Content.Trim(), styleSection.ContentLine, 1);

        var name = !string.IsNullOrEmpty(fileName)
            ? Path.GetFileNameWithoutExtension(fileName)
            : "Component";
        return new SqxDocument(name, template, script, style);
    }

    private static Dictionary<string, Section> SplitSections(string source)
    {
        var sections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        while (position < source.Length)
        {
            SkipTrivia(source, ref position);
            if (position >= source.Length) break;

            if (StartsWithTag(source, position, "sqx"))
                throw Error(source, position, "The <sqx> document root is no longer supported");
            if (source[position] != '<')
                throw Error(source, position, "Unexpected content outside a top-level section");

            var nameStart = position + 1;
            var nameEnd = nameStart;
            while (nameEnd < source.Length && char.IsLetter(source[nameEnd])) nameEnd++;
            if (nameEnd == nameStart)
                throw Error(source, position, "Invalid top-level section");

            var name = source[nameStart..nameEnd];
            if (name is not ("template" or "script" or "style"))
                throw Error(source, position, $"Unknown top-level section <{name}>");
            if (sections.ContainsKey(name))
                throw Error(source, position, $"Duplicate <{name}> section");

            var openingEnd = FindTagEnd(source, nameEnd);
            if (openingEnd < 0)
                throw Error(source, position, $"Unclosed <{name}> opening tag");

            var closeStart = source.IndexOf($"</{name}", openingEnd + 1, StringComparison.OrdinalIgnoreCase);
            if (closeStart < 0)
                throw Error(source, position, $"Unclosed <{name}> section");
            var closeEnd = FindTagEnd(source, closeStart + name.Length + 2);
            if (closeEnd < 0)
                throw Error(source, closeStart, $"Unclosed </{name}> tag");

            var openingTag = source[position..(openingEnd + 1)];
            var contentStart = openingEnd + 1;
            sections.Add(name, new Section(
                position,
                openingTag,
                source[contentStart..closeStart],
                GetLine(source, contentStart)));
            position = closeEnd + 1;
        }

        return sections;
    }

    private static void SkipTrivia(string source, ref int position)
    {
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                position++;
                continue;
            }
            if (source.AsSpan(position).StartsWith("<!--"))
            {
                var end = source.IndexOf("-->", position + 4, StringComparison.Ordinal);
                if (end < 0) throw Error(source, position, "Unclosed top-level comment");
                position = end + 3;
                continue;
            }
            break;
        }
    }

    private static int FindTagEnd(string source, int start)
    {
        var quote = '\0';
        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '\'' or '"') quote = c;
            else if (c == '>') return i;
        }
        return -1;
    }

    private static bool StartsWithTag(string source, int position, string name)
    {
        var text = $"<{name}";
        if (!source.AsSpan(position).StartsWith(text, StringComparison.OrdinalIgnoreCase)) return false;
        var boundary = position + text.Length;
        return boundary >= source.Length || char.IsWhiteSpace(source[boundary]) || source[boundary] is '>' or '/';
    }

    private static string? ReadAttribute(string openingTag, string name)
    {
        var match = Regex.Match(
            openingTag,
            $"\\b{Regex.Escape(name)}\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }

    private static ScriptMetadata ParseScriptMetadata(string source, Section section)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tagNameEnd = section.OpeningTag.IndexOf("script", StringComparison.OrdinalIgnoreCase) + 6;
        var attributeText = section.OpeningTag[tagNameEnd..^1];
        var matches = Regex.Matches(attributeText, "([A-Za-z_][A-Za-z0-9_-]*)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')");
        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (name is not ("lang" or "namespace" or "name" or "access"))
                throw Error(source, section.Start + tagNameEnd + match.Index, $"Unknown script metadata '{name}'");
            if (!attributes.TryAdd(name, match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value))
                throw Error(source, section.Start + tagNameEnd + match.Index, $"Duplicate script metadata '{name}'");
        }

        var language = attributes.GetValueOrDefault("lang", "csharp");
        if (!language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
            throw Error(source, section.Start, $"Unsupported script language '{language}'");
        var access = attributes.GetValueOrDefault("access", "public");
        if (access is not ("public" or "internal"))
            throw Error(source, section.Start, "Script access must be 'public' or 'internal'");
        return new ScriptMetadata(
            "csharp",
            attributes.GetValueOrDefault("namespace"),
            attributes.GetValueOrDefault("name"),
            access);
    }

    private static SqxParseException Error(string source, int position, string message)
    {
        var line = GetLine(source, position);
        var lastNewLine = position > 0 ? source.LastIndexOf('\n', Math.Min(position - 1, source.Length - 1)) : -1;
        return new SqxParseException(message, line, position - lastNewLine);
    }

    private static int GetLine(string source, int position)
    {
        var line = 1;
        for (var i = 0; i < position && i < source.Length; i++)
            if (source[i] == '\n') line++;
        return line;
    }

    private static SqxTemplate ParseTemplate(string text, int baseLine)
    {
        var lexer = new SqxLexer(text);
        var tokens = lexer.Tokenize();
        var parser = new TemplateParser(tokens);
        return new SqxTemplate(parser.ParseRoots(), baseLine, 1);
    }

    private sealed record Section(int Start, string OpeningTag, string Content, int ContentLine);
    private sealed record ScriptMetadata(
        string Language,
        string? Namespace,
        string? ComponentName,
        string Access);
}
