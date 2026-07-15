using System.Text.RegularExpressions;

namespace Square.SourceGenerator.Parser
{
    internal static class SqxParser
    {
        public static SqxDocument Parse(string source, string fileName)
        {
            var doc = new SqxDocument { Name = Path.GetFileNameWithoutExtension(fileName) };

            var parts = SplitSections(source);
            if (!string.IsNullOrEmpty(parts.Item1))
                doc.Template = ParseTemplate(parts.Item1);

            doc.ScriptCode = parts.Item2;
            doc.ScriptLang = parts.Item3 ?? "csharp";
            doc.StyleCode = parts.Item4;

            return doc;
        }

        private static Tuple<string, string, string, string> SplitSections(string source)
        {
            var tStart = source.IndexOf("<template", StringComparison.OrdinalIgnoreCase);
            var sStart = source.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
            var stStart = source.IndexOf("<style", StringComparison.OrdinalIgnoreCase);

            var template = tStart >= 0 ? ExtractContent(source, tStart, "template") : "";
            var script = sStart >= 0 ? ExtractContent(source, sStart, "script") : null;
            var style = stStart >= 0 ? ExtractContent(source, stStart, "style") : null;

            string scriptLang = "csharp";
            if (sStart >= 0)
            {
                var tagEnd = source.IndexOf('>', sStart);
                if (tagEnd >= 0)
                {
                    var tagContent = source.Substring(sStart, tagEnd - sStart);
                    var m = Regex.Match(tagContent, @"lang\s*=\s*[""']?([^""'\s>]+)", RegexOptions.IgnoreCase);
                    if (m.Success) scriptLang = m.Groups[1].Value;
                }
            }

            return Tuple.Create(template, script, scriptLang, style);
        }

        private static string ExtractContent(string source, int start, string tagName)
        {
            var tagEnd = source.IndexOf('>', start);
            if (tagEnd < 0) return "";
            var closeIdx = source.IndexOf("</" + tagName, tagEnd, StringComparison.OrdinalIgnoreCase);
            if (closeIdx < 0) return "";
            return source.Substring(tagEnd + 1, closeIdx - tagEnd - 1).Trim();
        }

        private static SqxTemplate ParseTemplate(string text)
        {
            var lexer = new SqxLexer(text);
            var tokens = lexer.Tokenize();
            var parser = new TemplateParser(tokens);
            var roots = parser.ParseRoots();
            return new SqxTemplate { Roots = roots };
        }
    }
}