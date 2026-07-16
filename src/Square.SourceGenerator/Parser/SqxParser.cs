using System.Text.RegularExpressions;

namespace Square.SourceGenerator.Parser
{
    internal static class SqxParser
    {
        public static SqxDocument Parse(string source, string fileName)
        {
            var sections = SplitSections(source);
            if (!sections.TryGetValue("template", out var templateSection))
                throw Error(source, 0, "Missing required <template> section");

            var doc = new SqxDocument
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                Template = ParseTemplate(templateSection.Content)
            };

            if (sections.TryGetValue("script", out var scriptSection))
            {
                var metadata = ParseScriptMetadata(source, scriptSection);
                doc.ScriptCode = scriptSection.Content.Trim();
                doc.ScriptLang = metadata.Language;
                doc.Namespace = metadata.Namespace;
                doc.Name = metadata.Name ?? doc.Name;
                doc.Access = metadata.Access;
            }

            if (sections.TryGetValue("style", out var styleSection))
                doc.StyleCode = styleSection.Content.Trim();

            return doc;
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

                var name = source.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
                if (name != "template" && name != "script" && name != "style")
                    throw Error(source, position, $"Unknown top-level section <{name}>");
                if (sections.ContainsKey(name))
                    throw Error(source, position, $"Duplicate <{name}> section");

                var openingEnd = FindTagEnd(source, nameEnd);
                if (openingEnd < 0) throw Error(source, position, $"Unclosed <{name}> opening tag");
                var closeStart = source.IndexOf("</" + name, openingEnd + 1, StringComparison.OrdinalIgnoreCase);
                if (closeStart < 0) throw Error(source, position, $"Unclosed <{name}> section");
                var closeEnd = FindTagEnd(source, closeStart + name.Length + 2);
                if (closeEnd < 0) throw Error(source, closeStart, $"Unclosed </{name}> tag");

                sections.Add(name, new Section(
                    position,
                    source.Substring(position, openingEnd - position + 1),
                    source.Substring(openingEnd + 1, closeStart - openingEnd - 1)));
                position = closeEnd + 1;
            }
            return sections;
        }

        private static ScriptMetadata ParseScriptMetadata(string source, Section section)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tagNameEnd = section.OpeningTag.IndexOf("script", StringComparison.OrdinalIgnoreCase) + 6;
            var attributeText = section.OpeningTag.Substring(tagNameEnd, section.OpeningTag.Length - tagNameEnd - 1);
            var matches = Regex.Matches(attributeText, @"([A-Za-z_][A-Za-z0-9_-]*)\s*=\s*(?:""([^""]*)""|'([^']*)')");
            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;
                if (name != "lang" && name != "namespace" && name != "name" && name != "access")
                    throw Error(source, section.Start + tagNameEnd + match.Index, $"Unknown script metadata '{name}'");
                if (attributes.ContainsKey(name))
                    throw Error(source, section.Start + tagNameEnd + match.Index, $"Duplicate script metadata '{name}'");
                attributes.Add(name, match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value);
            }

            var language = attributes.TryGetValue("lang", out var lang) ? lang : "csharp";
            if (!string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase))
                throw Error(source, section.Start, $"Unsupported script language '{language}'");
            var access = attributes.TryGetValue("access", out var value) ? value : "public";
            if (access != "public" && access != "internal")
                throw Error(source, section.Start, "Script access must be 'public' or 'internal'");

            attributes.TryGetValue("namespace", out var namespaceName);
            attributes.TryGetValue("name", out var componentName);
            return new ScriptMetadata("csharp", namespaceName, componentName, access);
        }

        private static void SkipTrivia(string source, ref int position)
        {
            while (position < source.Length)
            {
                if (char.IsWhiteSpace(source[position])) { position++; continue; }
                if (source.Substring(position).StartsWith("<!--", StringComparison.Ordinal))
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
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c == '\'' || c == '"') quote = c;
                else if (c == '>') return i;
            }
            return -1;
        }

        private static bool StartsWithTag(string source, int position, string name)
        {
            var text = "<" + name;
            if (position + text.Length > source.Length ||
                !source.Substring(position, text.Length).Equals(text, StringComparison.OrdinalIgnoreCase)) return false;
            var boundary = position + text.Length;
            return boundary >= source.Length || char.IsWhiteSpace(source[boundary]) || source[boundary] == '>' || source[boundary] == '/';
        }

        private static SqxParseException Error(string source, int position, string message) =>
            new SqxParseException(message, Math.Max(0, Math.Min(position, source.Length)));

        private static SqxTemplate ParseTemplate(string text)
        {
            var lexer = new SqxLexer(text);
            var parser = new TemplateParser(lexer.Tokenize());
            return new SqxTemplate { Roots = parser.ParseRoots() };
        }

        private sealed class Section
        {
            public int Start { get; }
            public string OpeningTag { get; }
            public string Content { get; }
            public Section(int start, string openingTag, string content)
            {
                Start = start;
                OpeningTag = openingTag;
                Content = content;
            }
        }

        private sealed class ScriptMetadata
        {
            public string Language { get; }
            public string Namespace { get; }
            public string Name { get; }
            public string Access { get; }
            public ScriptMetadata(string language, string namespaceName, string name, string access)
            {
                Language = language;
                Namespace = namespaceName;
                Name = name;
                Access = access;
            }
        }
    }

    internal sealed class SqxParseException : Exception
    {
        public int Position { get; }
        public SqxParseException(string message, int position) : base(message) => Position = position;
    }
}
