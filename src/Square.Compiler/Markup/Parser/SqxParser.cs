using Square.Compiler.ParserCore;
using Square.Markup.Ast;

namespace Square.Markup.Parser;

public sealed class SqxParser
{
    public SqxDocument Parse(string source, string fileName = "")
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var core = SqxCoreParser.Parse(source, fileName, new SqxCoreParserOptions
            {
                StrictTemplate = true,
                CaseSensitiveSectionNames = true
            });
            return ConvertDocument(core);
        }
        catch (CoreParseException exception)
        {
            throw new SqxParseException(exception.Message, exception.Line, exception.Column);
        }
    }

    private static SqxDocument ConvertDocument(CoreDocument core)
    {
        var template = new SqxTemplate(
            ConvertNodes(core.Template.Roots, core.Template.Line - 1),
            core.Template.Line,
            core.Template.Column);
        SqxScript? script = core.Script == null
            ? null
            : new SqxScript(
                core.Script.Language,
                core.Script.Code,
                core.Script.Namespace,
                core.Script.ComponentName,
                core.Script.Access,
                core.Script.Line,
                core.Script.Column);
        SqxStyle? style = core.Style == null
            ? null
            : new SqxStyle(core.Style.Css, core.Style.Line, core.Style.Column);
        return new SqxDocument(core.FileName, template, script, style);
    }

    private static List<SqxNode> ConvertNodes(List<CoreNode> nodes, int lineOffset)
    {
        var result = new List<SqxNode>(nodes.Count);
        foreach (var node in nodes)
        {
            if (node is CoreText text)
            {
                result.Add(new SqxText(text.Text, text.Line + lineOffset, text.Column));
                continue;
            }
            if (node is CoreExpression expression)
            {
                result.Add(new SqxExpression(
                    expression.Expression,
                    expression.Line + lineOffset,
                    expression.Column));
                continue;
            }

            var element = (CoreElement)node;
            var converted = new SqxElement(
                element.TagName,
                ConvertAttributes(element.Attributes, lineOffset),
                ConvertNodes(element.Children, lineOffset),
                element.Line + lineOffset,
                element.Column + 1)
            {
                Kind = GetElementKind(element.TagName)
            };
            result.Add(converted);
        }
        return result;
    }

    private static List<SqxAttribute> ConvertAttributes(List<CoreAttribute> attributes, int lineOffset)
    {
        var result = new List<SqxAttribute>(attributes.Count);
        foreach (var attribute in attributes)
        {
            var value = attribute.RawValue == null
                ? null
                : new SqxAttributeValue(attribute.IsExpression, attribute.RawValue);
            result.Add(new SqxAttribute(
                attribute.Name,
                attribute.RawValue,
                value,
                attribute.Line + lineOffset,
                attribute.Column));
        }
        return result;
    }

    private static SqxNodeKind GetElementKind(string tagName) => tagName switch
    {
        "Show" => SqxNodeKind.Show,
        "For" => SqxNodeKind.For,
        "Switch" => SqxNodeKind.Switch,
        "Match" => SqxNodeKind.Match,
        "Slot" or "Outlet" => SqxNodeKind.Slot,
        "Router" => SqxNodeKind.Router,
        "Route" => SqxNodeKind.Route,
        _ => SqxNodeKind.Element
    };
}
