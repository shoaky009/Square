namespace Square.Markup.Lexer;

public sealed class SqxLexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _column = 1;
    private bool _inTag;
    private int _templateExpressionDepth;

    public SqxLexer(string source) { _source = source; }

    public List<SqxToken> Tokenize()
    {
        var tokens = new List<SqxToken>();
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '<')
            {
                if (Peek(1) == '/')
                {
                    _pos += 2; _column += 2;
                    _inTag = true;
                    var name = ReadIdentifier();
                    AdvanceWhitespace();
                    if (_pos < _source.Length && _source[_pos] == '>')
                    { _pos++; _column++; }
                    tokens.Add(new SqxToken(SqxTokenType.EndTag, name, _line, _column));
                    _inTag = false;
                }
                else
                {
                    _pos++; _column++;
                    _inTag = true;
                    tokens.Add(new SqxToken(SqxTokenType.OpenTag, "<", _line, _column));
                }
            }
            else if (c == '/' && Peek(1) == '>')
            {
                _pos += 2; _column += 2;
                tokens.Add(new SqxToken(SqxTokenType.CloseSelfTag, "/>", _line, _column));
                _inTag = false;
            }
            else if (c == '>')
            {
                _pos++; _column++;
                tokens.Add(new SqxToken(SqxTokenType.CloseTag, ">", _line, _column));
                _inTag = false;
            }
            else if (c == '=')
            {
                _pos++; _column++;
                tokens.Add(new SqxToken(SqxTokenType.Equals, "=", _line, _column));
            }
            else if (c == '{')
            {
                if (!_inTag && TryReadTemplateLambda(out var lambda))
                {
                    tokens.Add(new SqxToken(SqxTokenType.OpenBraceExpr, lambda, _line, _column));
                    continue;
                }
                _pos++; _column++;
                var (expr, el, ec) = ReadUntilBrace();
                _line = el; _column = ec;
                tokens.Add(new SqxToken(SqxTokenType.OpenBraceExpr, expr, _line, _column));
            }
            else if (c == '}' && !_inTag && _templateExpressionDepth > 0)
            {
                _pos++; _column++;
                _templateExpressionDepth--;
                tokens.Add(new SqxToken(SqxTokenType.OpenBraceExpr, "}", _line, _column));
            }
            else if (c == '"' || c == '\'')
            {
                var (str, el, ec) = ReadString(c);
                _line = el; _column = ec;
                tokens.Add(new SqxToken(SqxTokenType.StringLiteral, str, _line, _column));
            }
            else if (char.IsWhiteSpace(c))
            {
                AdvanceWhitespace();
            }
            else if (_inTag && IsIdentifierStart(c))
            {
                var (name, el, ec) = ReadIdentifierWithPos();
                _line = el; _column = ec;
                tokens.Add(new SqxToken(SqxTokenType.Identifier, name, _line, _column));
            }
            else
            {
                var (text, el, ec) = ReadText();
                _line = el; _column = ec;
                if (!string.IsNullOrWhiteSpace(text))
                    tokens.Add(new SqxToken(SqxTokenType.Text, text, _line, _column));
            }
        }
        tokens.Add(new SqxToken(SqxTokenType.Eof, "", _line, _column));
        return tokens;
    }

    private char Peek(int offset) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    private string ReadIdentifier()
    {
        var start = _pos;
        while (_pos < _source.Length && IsIdentifierChar(_source[_pos])) { _pos++; _column++; }
        return _source[start.._pos];
    }

    private (string, int, int) ReadIdentifierWithPos()
    {
        var (line, col) = (_line, _column);
        var name = ReadIdentifier();
        return (name, line, col);
    }

    private (string, int, int) ReadString(char quote)
    {
        var (line, col) = (_line, _column);
        _pos++; _column++;
        var start = _pos;
        while (_pos < _source.Length && _source[_pos] != quote)
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length) { _pos += 2; _column += 2; }
            else { AdvanceChar(); }
        }
        var result = _source[start.._pos];
        if (_pos < _source.Length) { _pos++; _column++; }
        return (result, line, col);
    }

    private (string, int, int) ReadUntilBrace()
    {
        var (line, col) = (_line, _column);
        var start = _pos;
        var depth = 0;
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '{') depth++;
            else if (c == '}')
            {
                if (depth == 0) { _pos++; _column++; break; }
                depth--;
            }
            AdvanceChar();
        }
        var result = _source[start..(_pos - 1)].Trim();
        return (result, line, col);
    }

    private (string, int, int) ReadText()
    {
        var (line, col) = (_line, _column);
        var start = _pos;
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '<' || c == '{' || (c == '}' && _templateExpressionDepth > 0)) break;
            AdvanceChar();
        }
        return (_source[start.._pos], line, col);
    }

    private bool TryReadTemplateLambda(out string expression)
    {
        expression = "";
        var start = _pos + 1;
        var arrow = _source.IndexOf("=>", start, StringComparison.Ordinal);
        var tag = _source.IndexOf('<', start);
        if (arrow < 0 || tag < 0 || arrow > tag) return false;

        var candidate = _source[start..(arrow + 2)].Trim();
        if (!candidate.StartsWith('(')) return false;

        while (_pos <= arrow + 1) AdvanceChar();
        _templateExpressionDepth++;
        expression = candidate;
        return true;
    }

    private void AdvanceWhitespace()
    {
        while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
            AdvanceChar();
    }

    private void AdvanceChar()
    {
        if (_source[_pos] == '\n') { _line++; _column = 1; }
        else { _column++; }
        _pos++;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_' || c == '-';
    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
}
