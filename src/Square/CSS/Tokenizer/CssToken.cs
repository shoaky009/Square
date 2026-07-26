namespace Square.CSS.Tokenizer;

/// <summary>CSS 令牌类型枚举。</summary>
public enum CssTokenType
{
    /// <summary>标识符。</summary>
    Identifier,
    /// <summary>井号选择器（#id）。</summary>
    Hash,
    /// <summary>点号（.）。</summary>
    Dot,
    /// <summary>冒号（:）。</summary>
    Colon,
    /// <summary>双冒号（::）。</summary>
    DoubleColon,
    /// <summary>左大括号（{）。</summary>
    OpenBrace,
    /// <summary>右大括号（}）。</summary>
    CloseBrace,
    /// <summary>左小括号（(）。</summary>
    OpenParen,
    /// <summary>右小括号（)）。</summary>
    CloseParen,
    /// <summary>左中括号（[）。</summary>
    OpenBracket,
    /// <summary>右中括号（]）。</summary>
    CloseBracket,
    /// <summary>分号（;）。</summary>
    Semicolon,
    /// <summary>逗号（,）。</summary>
    Comma,
    /// <summary>字符串字面量。</summary>
    String,
    /// <summary>数字。</summary>
    Number,
    /// <summary>单位。</summary>
    Unit,
    /// <summary>百分号（%）。</summary>
    Percentage,
    /// <summary>空白字符。</summary>
    Whitespace,
    /// <summary>At 关键字（@规则）。</summary>
    AtKeyword,
    /// <summary>注释。</summary>
    Comment,
    /// <summary>大于号（>）。</summary>
    Greater,
    /// <summary>加号（+）。</summary>
    Plus,
    /// <summary>波浪号（~）。</summary>
    Tilde,
    /// <summary>感叹号（!）。</summary>
    Bang,
    /// <summary>星号（*）。</summary>
    Asterisk,
    /// <summary>等号（=）。</summary>
    Equals,
    /// <summary>输入结束。</summary>
    Eof
}

/// <summary>表示一个 CSS 令牌。</summary>
public readonly struct CssToken
{
    /// <summary>令牌类型。</summary>
    public readonly CssTokenType Type;
    /// <summary>令牌文本内容。</summary>
    public readonly string Text;
    /// <summary>令牌所在行号。</summary>
    public readonly int Line;

    /// <summary>初始化 CssToken 结构的新实例。</summary>
    /// <param name="type">令牌类型。</param>
    /// <param name="text">令牌文本。</param>
    /// <param name="line">所在行号。</param>
    public CssToken(CssTokenType type, string text, int line)
    {
        Type = type; Text = text; Line = line;
    }

    /// <summary>返回令牌的字符串表示。</summary>
    /// <returns>格式化的令牌描述。</returns>
    public override string ToString() => $"{Type}({Line}): {Text}";
}