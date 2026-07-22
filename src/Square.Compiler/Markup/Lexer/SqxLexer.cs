using Square.Compiler.ParserCore;

namespace Square.Markup.Lexer;

public sealed class SqxLexer
{
    private readonly string _source;

    public SqxLexer(string source) { _source = source; }

    public List<SqxToken> Tokenize()
    {
        var coreTokens = new SqxCoreLexer(_source).Tokenize();
        var tokens = new List<SqxToken>(coreTokens.Count);
        foreach (var token in coreTokens)
        {
            var line = token.Line;
            var column = token.Column;
            AdjustPosition(token, ref line, ref column);
            tokens.Add(new SqxToken(
                ConvertType(token.Type),
                token.Text,
                line,
                column));
        }
        return tokens;
    }

    private void AdjustPosition(CoreToken token, ref int line, ref int column)
    {
        switch (token.Type)
        {
            case CoreTokenType.OpenTag:
            case CoreTokenType.CloseTag:
            case CoreTokenType.Equals:
                column++;
                break;
            case CoreTokenType.CloseSelfTag:
                column += 2;
                break;
            case CoreTokenType.EndTag:
                var end = _source.IndexOf('>', token.Offset);
                if (end < 0) return;
                for (var i = token.Offset; i <= end; i++)
                {
                    if (_source[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                break;
        }
    }

    private static SqxTokenType ConvertType(CoreTokenType type) => type switch
    {
        CoreTokenType.OpenTag => SqxTokenType.OpenTag,
        CoreTokenType.CloseTag => SqxTokenType.CloseTag,
        CoreTokenType.CloseSelfTag => SqxTokenType.CloseSelfTag,
        CoreTokenType.EndTag => SqxTokenType.EndTag,
        CoreTokenType.Equals => SqxTokenType.Equals,
        CoreTokenType.OpenBraceExpr => SqxTokenType.OpenBraceExpr,
        CoreTokenType.StringLiteral => SqxTokenType.StringLiteral,
        CoreTokenType.Identifier => SqxTokenType.Identifier,
        CoreTokenType.Text => SqxTokenType.Text,
        _ => SqxTokenType.Eof
    };
}
