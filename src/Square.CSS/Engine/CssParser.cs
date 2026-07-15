using Square.CSS.Ast;
using Square.CSS.Tokenizer;

namespace Square.CSS.Engine;

public sealed class CssParser
{
    private readonly List<CssToken> _tokens;
    private int _i;

    public CssParser(List<CssToken> tokens) { _tokens = tokens; }

    public CssStyleSheet Parse()
    {
        var rules = new List<CssRule>();
        var atRules = new List<CssAtRule>();
        var keyFrames = new List<KeyFramesRule>();
        while (Peek().Type != CssTokenType.Eof)
        {
            if (Peek().Type == CssTokenType.AtKeyword)
            {
                var atName = Peek().Text;
                if (string.Equals(atName, "keyframes", StringComparison.OrdinalIgnoreCase))
                {
                    var kf = ParseKeyFrames();
                    if (kf != null) keyFrames.Add(kf);
                }
                else
                {
                    var atRule = ParseAtRule();
                    if (atRule != null) atRules.Add(atRule);
                }
            }
            else
            {
                var rule = ParseRule();
                if (rule != null) rules.Add(rule);
            }
        }
        return new CssStyleSheet(rules, atRules) { KeyFrames = keyFrames };
    }

    private KeyFramesRule? ParseKeyFrames()
    {
        Advance(); // @keyframes
        var name = "";
        while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.Eof))
        {
            if (Peek().Type == CssTokenType.Identifier)
                name = Advance().Text;
            else
                Advance();
        }
        if (Peek().Type != CssTokenType.OpenBrace) return null;
        Advance();

        var stops = new List<KeyFrameStop>();
        while (Peek().Type is not (CssTokenType.CloseBrace or CssTokenType.Eof))
        {
            var selector = new System.Text.StringBuilder();
            while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.CloseBrace or CssTokenType.Eof))
            {
                var t = Advance();
                if (t.Type != CssTokenType.Whitespace)
                    selector.Append(t.Text).Append(' ');
            }
            if (Peek().Type == CssTokenType.OpenBrace)
            {
                Advance();
                var decls = ParseDeclarations();
                stops.Add(new KeyFrameStop(selector.ToString().Trim(), decls));
            }
        }
        if (Peek().Type == CssTokenType.CloseBrace) Advance();
        return new KeyFramesRule(name, stops);
    }

    private CssAtRule? ParseAtRule()
    {
        var name = Advance().Text;
        var sb = new System.Text.StringBuilder();
        while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.Eof))
            sb.Append(Advance().Text).Append(' ');
        if (Peek().Type != CssTokenType.OpenBrace) return null;
        Advance();
        var decls = ParseDeclarations();
        return new CssAtRule(name, sb.ToString().Trim(), decls);
    }

    private CssRule? ParseRule()
    {
        var selectors = ParseSelectors();
        if (selectors.Count == 0) return null;
        if (Peek().Type != CssTokenType.OpenBrace) return null;
        Advance();
        var decls = ParseDeclarations();
        return new CssRule(selectors[0], decls);
    }

    private List<ComplexSelector> ParseSelectors()
    {
        var result = new List<ComplexSelector>();
        while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.Eof))
        {
            var steps = new List<CompoundStep>();
            var parts = new List<SimpleSelector>();
            Combinator comb = Combinator.Descendant;

            while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.Comma or CssTokenType.Eof))
            {
                var t = Peek();
                if (t.Type == CssTokenType.Whitespace) { Advance(); comb = Combinator.Descendant; continue; }
                if (t.Type == CssTokenType.OpenBracket) { Advance(); while (Peek().Type != CssTokenType.CloseBracket && Peek().Type != CssTokenType.Eof) Advance(); if (Peek().Type == CssTokenType.CloseBracket) Advance(); continue; }

                if (t.Type == CssTokenType.Identifier) { Advance(); parts.Add(new SimpleSelector(SimpleSelectorKind.Type, t.Text)); }
                else if (t.Type == CssTokenType.Dot) { Advance(); var n = Advance().Text; parts.Add(new SimpleSelector(SimpleSelectorKind.Class, n)); }
                else if (t.Type == CssTokenType.Hash) { Advance(); parts.Add(new SimpleSelector(SimpleSelectorKind.Id, t.Text)); }
                else if (t.Type == CssTokenType.Colon) { Advance(); var pseudoName = Advance().Text; parts.Add(new SimpleSelector(SimpleSelectorKind.PseudoClass, pseudoName)); }
                else { Advance(); }
            }
            if (parts.Count > 0) steps.Add(new CompoundStep(new CompoundSelector(parts), comb));
            result.Add(new(steps));
            if (Peek().Type == CssTokenType.Comma) Advance();
        }
        return result;
    }

    private List<Declaration> ParseDeclarations()
    {
        var decls = new List<Declaration>();
        while (Peek().Type is not (CssTokenType.CloseBrace or CssTokenType.Eof))
        {
            var propToken = Peek();
            if (propToken.Type != CssTokenType.Identifier) { Advance(); continue; }
            Advance();
            if (Peek().Type != CssTokenType.Colon) { while (Peek().Type is not (CssTokenType.Semicolon or CssTokenType.CloseBrace or CssTokenType.Eof)) Advance(); if (Peek().Type == CssTokenType.Semicolon) Advance(); continue; }
            Advance();
            var sb = new System.Text.StringBuilder();
            while (Peek().Type is not (CssTokenType.Semicolon or CssTokenType.CloseBrace or CssTokenType.Eof))
            {
                var t = Advance();
                if (t.Type != CssTokenType.Whitespace) sb.Append(t.Text).Append(' ');
            }
            if (Peek().Type == CssTokenType.Semicolon) Advance();
            decls.Add(new(propToken.Text, sb.ToString().Trim()));
        }
        if (Peek().Type == CssTokenType.CloseBrace) Advance();
        return decls;
    }

    private CssToken Peek() => _i < _tokens.Count ? _tokens[_i] : _tokens[^1];
    private CssToken Advance() => _i < _tokens.Count ? _tokens[_i++] : _tokens[^1];
}