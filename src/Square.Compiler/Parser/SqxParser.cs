using Square.Compiler.Directives;
using Square.Compiler.ParserCore;

namespace Square.Compiler.Parser
{
    internal static class SqxParser
    {
        public static SqxDocument Parse(string source, string fileName)
        {
            try
            {
                var core = SqxCoreParser.Parse(source, fileName, new SqxCoreParserOptions
                {
                    StrictTemplate = false,
                    CaseSensitiveSectionNames = false
                });
                return ConvertDocument(core);
            }
            catch (CoreParseException exception)
            {
                throw new SqxParseException(exception.Message, exception.Position);
            }
        }

        private static SqxDocument ConvertDocument(CoreDocument core)
        {
            var document = new SqxDocument
            {
                Name = core.Script != null && core.Script.ComponentName != null
                    ? core.Script.ComponentName
                    : core.FileName,
                Template = new SqxTemplate { Roots = ConvertNodes(core.Template.Roots) }
            };
            if (core.Script != null)
            {
                document.ScriptCode = core.Script.Code;
                document.ScriptLang = core.Script.Language;
                document.Namespace = core.Script.Namespace;
                document.Access = core.Script.Access;
            }
            if (core.Style != null) document.StyleCode = core.Style.Css;
            return document;
        }

        private static List<SqxNode> ConvertNodes(List<CoreNode> nodes)
        {
            var result = new List<SqxNode>(nodes.Count);
            foreach (var node in nodes)
            {
                var text = node as CoreText;
                if (text != null)
                {
                    result.Add(new SqxText
                    {
                        Text = text.Text,
                        Kind = SqxNodeKind.Text,
                        Line = text.Line,
                        Column = text.Column
                    });
                    continue;
                }

                var expression = node as CoreExpression;
                if (expression != null)
                {
                    result.Add(new SqxExpression
                    {
                        Expression = expression.Expression,
                        Kind = SqxNodeKind.Expression,
                        Line = expression.Line,
                        Column = expression.Column
                    });
                    continue;
                }

                var element = (CoreElement)node;
                string directiveId = null;
                var kind = SqxNodeKind.Element;
                DirectiveDescriptor descriptor;
                if (DirectiveCatalog.BuiltIn.TryGet(element.TagName, out descriptor))
                {
                    kind = SqxNodeKind.Directive;
                    directiveId = descriptor.TagName;
                }
                result.Add(new SqxElement
                {
                    TagName = element.TagName,
                    DirectiveId = directiveId,
                    Attributes = ConvertAttributes(element.Attributes),
                    Children = ConvertNodes(element.Children),
                    Kind = kind,
                    Line = element.Line,
                    Column = element.Column + 1
                });
            }
            return result;
        }

        private static List<SqxAttribute> ConvertAttributes(List<CoreAttribute> attributes)
        {
            var result = new List<SqxAttribute>(attributes.Count);
            foreach (var attribute in attributes)
            {
                result.Add(new SqxAttribute
                {
                    Name = attribute.Name,
                    RawValue = attribute.RawValue,
                    IsExpression = attribute.IsExpression,
                    Line = attribute.Line
                });
            }
            return result;
        }
    }

    internal sealed class SqxParseException : Exception
    {
        public int Position { get; }
        public SqxParseException(string message, int position) : base(message) => Position = position;
    }
}
