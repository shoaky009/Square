using Square.Markup.Ast;
using Square.Markup.Lexer;

namespace Square.Markup.Parser;

internal sealed class TemplateParser
{
    private readonly List<SqxToken> _tokens;
    private int _index;

    public TemplateParser(List<SqxToken> tokens) { _tokens = tokens; }

    public List<SqxNode> ParseRoots()
    {
        var roots = new List<SqxNode>();
        while (Peek().Type != SqxTokenType.Eof)
        {
            var node = ParseNode();
            if (node != null) roots.Add(node);
        }
        return roots;
    }

    private SqxNode? ParseNode()
    {
        var token = Peek();

        return token.Type switch
        {
            SqxTokenType.OpenTag => ParseElement(),
            SqxTokenType.Text => NextNode(SqxNodeKind.Text, token),
            SqxTokenType.OpenBraceExpr => NextNode(SqxNodeKind.Expression, token),
            SqxTokenType.Eof => null,
            _ => null
        };
    }

    private SqxNode NextNode(SqxNodeKind kind, SqxToken token)
    {
        Advance();
        return kind switch
        {
            SqxNodeKind.Text => new SqxText(token.Text.Trim(), token.Line, token.Column),
            SqxNodeKind.Expression => new SqxExpression(token.Text, token.Line, token.Column),
            _ => throw new SqxParseException($"Unexpected node kind {kind}", token.Line, token.Column)
        };
    }

    private SqxNode ParseElement()
    {
        var openTag = Expect(SqxTokenType.OpenTag);
        var nameToken = Expect(SqxTokenType.Identifier);
        var tagName = nameToken.Text;

        var attributes = new List<SqxAttribute>();
        while (Peek().Type is not (SqxTokenType.CloseTag or SqxTokenType.CloseSelfTag or SqxTokenType.Eof))
        {
            var a = ParseAttribute();
            if (a != null) attributes.Add(a);
        }

        var isSelfClosing = Peek().Type == SqxTokenType.CloseSelfTag;
        if (isSelfClosing)
        {
            Advance();
            var selfClosingElement = new SqxElement(tagName, attributes, [], openTag.Line, openTag.Column);
            selfClosingElement.Kind = GetElementKind(tagName);
            return selfClosingElement;
        }

        Expect(SqxTokenType.CloseTag);

        var children = new List<SqxNode>();
        while (true)
        {
            var t = Peek();
            if (t.Type == SqxTokenType.Eof) break;
            if (t.Type == SqxTokenType.EndTag) { Advance(); break; }
            var child = ParseNode();
            if (child != null) children.Add(child);
        }

        var kind = GetElementKind(tagName);
        var el = new SqxElement(tagName, attributes, children, openTag.Line, openTag.Column);
        el.Kind = kind;
        return el;
    }

    private SqxAttribute? ParseAttribute()
    {
        var nameToken = Peek();
        if (nameToken.Type != SqxTokenType.Identifier) { Advance(); return null; }
        Advance();

        if (Peek().Type != SqxTokenType.Equals)
        {
            return new SqxAttribute(nameToken.Text, null, null, nameToken.Line, nameToken.Column);
        }

        Advance();

        var valueToken = Peek();
        SqxAttributeValue? value = null;

        if (valueToken.Type == SqxTokenType.StringLiteral)
        {
            Advance();
            value = new SqxAttributeValue(false, valueToken.Text);
        }
        else if (valueToken.Type == SqxTokenType.OpenBraceExpr)
        {
            Advance();
            value = new SqxAttributeValue(true, valueToken.Text);
        }

        return new SqxAttribute(nameToken.Text, value?.Content, value, nameToken.Line, nameToken.Column);
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

    private SqxToken Peek() => _index < _tokens.Count ? _tokens[_index] : _tokens[_tokens.Count - 1];
    private SqxToken Advance()
    {
        if (_index < _tokens.Count) _index++;
        return _tokens[Math.Min(_index - 1, _tokens.Count - 1)];
    }

    private SqxToken Expect(SqxTokenType type)
    {
        var token = Peek();
        if (token.Type != type)
            throw new SqxParseException($"Expected {type} but got {token.Type}", token.Line, token.Column);
        return Advance();
    }
}
