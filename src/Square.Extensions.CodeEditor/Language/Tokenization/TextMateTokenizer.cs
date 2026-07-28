using TextMateSharp.Grammars;

namespace Square.Extensions.CodeEditor;

/// <summary>TextMate grammar 分词器。</summary>
public sealed class TextMateTokenizer : ITokenizer, IStatefulTokenizer
{
    private readonly IGrammar _grammar;

    internal TextMateTokenizer(IGrammar grammar) => _grammar = grammar;

    /// <inheritdoc/>
    public IReadOnlyList<TokenSpan> TokenizeLine(string line, ref string state)
    {
        object? objectState = null;
        return TokenizeLine(line, ref objectState);
    }

    IReadOnlyList<TokenSpan> IStatefulTokenizer.TokenizeLine(string line, ref object? state)
        => TokenizeLine(line, ref state);

    private IReadOnlyList<TokenSpan> TokenizeLine(string line, ref object? state)
    {
        var result = _grammar.TokenizeLine(line, state as IStateStack, TimeSpan.MaxValue);
        state = result.RuleStack;
        if (line.Length == 0 || result.Tokens.Length == 0) return [];

        var spans = new List<TokenSpan>(result.Tokens.Length);
        foreach (var token in result.Tokens)
        {
            var start = Math.Clamp(token.StartIndex, 0, line.Length);
            var end = Math.Clamp(token.EndIndex, start, line.Length);
            if (end <= start) continue;
            spans.Add(new TokenSpan(start, end - start, MapScopes(token.Scopes)));
        }
        return spans;
    }

    private static string MapScopes(IList<string> scopes)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var scope = scopes[i];
            if (scope.StartsWith("comment", StringComparison.Ordinal)) return "comment";
            if (scope.StartsWith("string", StringComparison.Ordinal)) return "string";
            if (scope.StartsWith("constant.numeric", StringComparison.Ordinal)) return "number";
            if (scope.StartsWith("constant.language", StringComparison.Ordinal)) return "constant";
            if (scope.StartsWith("keyword", StringComparison.Ordinal) ||
                scope.StartsWith("storage", StringComparison.Ordinal)) return "keyword";
            if (scope.StartsWith("entity.name.type", StringComparison.Ordinal) ||
                scope.StartsWith("support.type", StringComparison.Ordinal)) return "type";
            if (scope.StartsWith("entity.name.function", StringComparison.Ordinal) ||
                scope.StartsWith("support.function", StringComparison.Ordinal)) return "function";
            if (scope.StartsWith("variable", StringComparison.Ordinal)) return "variable";
            if (scope.StartsWith("entity.name.tag", StringComparison.Ordinal)) return "tag";
            if (scope.StartsWith("entity.other.attribute-name", StringComparison.Ordinal)) return "attribute.name";
            if (scope.StartsWith("punctuation", StringComparison.Ordinal)) return "delimiter";
            if (scope.StartsWith("invalid", StringComparison.Ordinal)) return "invalid";
        }
        return "source";
    }
}
