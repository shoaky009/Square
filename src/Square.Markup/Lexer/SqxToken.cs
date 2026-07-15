namespace Square.Markup.Lexer;

public enum SqxTokenType
{
    OpenTag,        // <
    CloseTag,       // >
    CloseSelfTag,   // />
    OpenEndTag,     // </
    EndTag,         // </name>
    Equals,         // =
    OpenBrace,      // {
    CloseBrace,     // }
    StringLiteral,  // "..." or '...'
    Identifier,     // name
    Text,           // text content
    OpenBraceExpr,  // {expr}
    Eof
}

public readonly struct SqxToken
{
    public readonly SqxTokenType Type;
    public readonly string Text;
    public readonly int Line;
    public readonly int Column;

    public SqxToken(SqxTokenType type, string text, int line, int column)
    {
        Type = type; Text = text; Line = line; Column = column;
    }

    public override string ToString() => $"{Type}({Line}:{Column}): {Text}";
}