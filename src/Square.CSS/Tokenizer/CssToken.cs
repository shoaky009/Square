namespace Square.CSS.Tokenizer;

public enum CssTokenType
{
    Identifier,
    Hash,
    Dot,
    Colon,
    DoubleColon,
    OpenBrace,
    CloseBrace,
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    Semicolon,
    Comma,
    String,
    Number,
    Unit,
    Percentage,
    Whitespace,
    AtKeyword,
    Comment,
    Greater,
    Plus,
    Tilde,
    Bang,
    Asterisk,
    Equals,
    Eof
}

public readonly struct CssToken
{
    public readonly CssTokenType Type;
    public readonly string Text;
    public readonly int Line;

    public CssToken(CssTokenType type, string text, int line)
    {
        Type = type; Text = text; Line = line;
    }

    public override string ToString() => $"{Type}({Line}): {Text}";
}
