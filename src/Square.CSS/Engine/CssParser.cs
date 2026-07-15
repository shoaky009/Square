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
                var parsedRules = ParseRules();
                if (parsedRules.Count > 0) rules.AddRange(parsedRules);
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

    private List<CssRule> ParseRules()
    {
        var selectors = ParseSelectors();
        if (selectors.Count == 0) return [];
        if (Peek().Type != CssTokenType.OpenBrace) return [];
        Advance();
        var decls = ParseDeclarations();
        return selectors.Select(selector => new CssRule(selector, decls)).ToList();
    }

    private List<ComplexSelector> ParseSelectors()
    {
        var result = new List<ComplexSelector>();
        var steps = new List<CompoundStep>();
        var parts = new List<SimpleSelector>();

        while (Peek().Type is not (CssTokenType.OpenBrace or CssTokenType.Eof))
        {
            var token = Peek();
            if (token.Type == CssTokenType.Whitespace)
            {
                FlushCompound(parts, steps);
                Advance();
                continue;
            }

            if (token.Type == CssTokenType.Comma)
            {
                FlushCompound(parts, steps);
                if (steps.Count > 0) result.Add(new ComplexSelector(new List<CompoundStep>(steps)));
                steps.Clear();
                Advance();
                continue;
            }

            if (token.Type == CssTokenType.OpenBracket)
            {
                Advance();
                while (Peek().Type is not (CssTokenType.CloseBracket or CssTokenType.Eof)) Advance();
                if (Peek().Type == CssTokenType.CloseBracket) Advance();
                continue;
            }

            if (token.Type == CssTokenType.Identifier)
            {
                parts.Add(new SimpleSelector(SimpleSelectorKind.Type, Advance().Text));
            }
            else if (token.Type == CssTokenType.Dot)
            {
                Advance();
                parts.Add(new SimpleSelector(SimpleSelectorKind.Class, Advance().Text));
            }
            else if (token.Type == CssTokenType.Hash)
            {
                parts.Add(new SimpleSelector(SimpleSelectorKind.Id, Advance().Text));
            }
            else if (token.Type == CssTokenType.Colon)
            {
                Advance();
                while (Peek().Type == CssTokenType.Whitespace) Advance();
                parts.Add(new SimpleSelector(SimpleSelectorKind.PseudoClass, Advance().Text));
            }
            else
            {
                Advance();
            }
        }

        FlushCompound(parts, steps);
        if (steps.Count > 0) result.Add(new ComplexSelector(steps));
        return result;
    }

    private static void FlushCompound(List<SimpleSelector> parts, List<CompoundStep> steps)
    {
        if (parts.Count == 0) return;
        steps.Add(new CompoundStep(new CompoundSelector(new List<SimpleSelector>(parts)), Combinator.Descendant));
        parts.Clear();
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
            var valueTokens = new List<CssToken>();
            while (Peek().Type is not (CssTokenType.Semicolon or CssTokenType.CloseBrace or CssTokenType.Eof))
                valueTokens.Add(Advance());
            if (Peek().Type == CssTokenType.Semicolon) Advance();
            decls.Add(new(propToken.Text, FormatValue(valueTokens)));
        }
        if (Peek().Type == CssTokenType.CloseBrace) Advance();
        return decls;
    }

    private static string FormatValue(List<CssToken> tokens)
    {
        var result = new System.Text.StringBuilder();
        CssTokenType? previous = null;
        foreach (var token in tokens)
        {
            if (token.Type == CssTokenType.Whitespace)
            {
                if (result.Length > 0 && result[result.Length - 1] != ' ') result.Append(' ');
                previous = token.Type;
                continue;
            }

            if (token.Type == CssTokenType.Hash) result.Append('#').Append(token.Text);
            else if (token.Type == CssTokenType.OpenParen) result.Append('(');
            else if (token.Type == CssTokenType.CloseParen) { while (result.Length > 0 && result[result.Length - 1] == ' ') result.Length--; result.Append(')'); }
            else if (token.Type == CssTokenType.Comma) result.Append(',');
            else if (token.Type == CssTokenType.Unit) result.Append(token.Text);
            else result.Append(token.Text);
            previous = token.Type;
        }
        return result.ToString().Trim();
    }

    private CssToken Peek() => _i < _tokens.Count ? _tokens[_i] : _tokens[^1];
    private CssToken Advance() => _i < _tokens.Count ? _tokens[_i++] : _tokens[^1];
}
