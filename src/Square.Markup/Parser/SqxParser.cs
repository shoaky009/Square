using Square.Markup.Ast;
using Square.Markup.Lexer;

namespace Square.Markup.Parser;

public sealed class SqxParser
{
    private string _source = "";
    private string _fileName = "";

    public SqxParser() { }

    public SqxDocument Parse(string source, string fileName = "")
    {
        _source = source;
        _fileName = fileName;

        var (templateText, scriptText, scriptLang, styleText, templateLine, scriptLine, styleLine) =
            SplitSections(source);

        var template = ParseTemplate(templateText, templateLine);
        var script = scriptText != null
            ? new SqxScript(scriptLang ?? "csharp", scriptText, scriptLine, 1)
            : null;
        var style = styleText != null
            ? new SqxStyle(styleText, styleLine, 1)
            : null;

        var name = !string.IsNullOrEmpty(fileName)
            ? Path.GetFileNameWithoutExtension(fileName)
            : "Component";

        return new SqxDocument(name, template, script, style);
    }

    private static (string template, string? script, string? scriptLang, string? style, int tLine, int sLine, int stLine)
        SplitSections(string source)
    {
        var templateStart = source.IndexOf("<template", StringComparison.OrdinalIgnoreCase);
        var scriptStart = source.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
        var styleStart = source.IndexOf("<style", StringComparison.OrdinalIgnoreCase);

        var templateEnd = FindClosingTag(source, templateStart, "template");
        var scriptEnd = scriptStart >= 0 ? FindClosingTag(source, scriptStart, "script") : -1;
        var styleEnd = styleStart >= 0 ? FindClosingTag(source, styleStart, "style") : -1;

        var templateText = "";
        string? scriptText = null;
        string? scriptLang = null;
        string? styleText = null;

        if (templateStart >= 0 && templateEnd > templateStart)
        {
            var tagEnd = source.IndexOf('>', templateStart);
            if (tagEnd >= 0)
                templateText = source[(tagEnd + 1)..templateEnd].Trim();
        }

        if (scriptStart >= 0 && scriptEnd > scriptStart)
        {
            var tagEnd = source.IndexOf('>', scriptStart);
            if (tagEnd >= 0)
            {
                var tagContent = source[scriptStart..tagEnd];
                var langMatch = System.Text.RegularExpressions.Regex.Match(tagContent, @"lang\s*=\s*[""']?([^""'\s>]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                scriptLang = langMatch.Success ? langMatch.Groups[1].Value : "csharp";
                scriptText = source[(tagEnd + 1)..scriptEnd].Trim();
            }
        }

        if (styleStart >= 0 && styleEnd > styleStart)
        {
            var tagEnd = source.IndexOf('>', styleStart);
            if (tagEnd >= 0)
                styleText = source[(tagEnd + 1)..styleEnd].Trim();
        }

        return (templateText, scriptText, scriptLang, styleText, 1, 1, 1);
    }

    private static int FindClosingTag(string source, int start, string tagName)
    {
        return source.IndexOf($"</{tagName}", start, StringComparison.OrdinalIgnoreCase);
    }

    private SqxTemplate ParseTemplate(string text, int baseLine)
    {
        var lexer = new SqxLexer(text);
        var tokens = lexer.Tokenize();
        var parser = new TemplateParser(tokens);
        var roots = parser.ParseRoots();
        return new SqxTemplate(roots, baseLine, 1);
    }
}