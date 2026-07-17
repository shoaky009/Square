namespace Square.CSS.Tokenizer;

public sealed class CssTokenizer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;

    public CssTokenizer(string source) { _source = source; }

    public List<CssToken> Tokenize()
    {
        var tokens = new List<CssToken>();
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '/' && Peek(1) == '*') { SkipComment(); continue; }
            if (char.IsWhiteSpace(c))
            {
                var line = _line;
                SkipWhitespace();
                tokens.Add(new CssToken(CssTokenType.Whitespace, " ", line));
                continue;
            }
            if (c == '{') { tokens.Add(new CssToken(CssTokenType.OpenBrace, "{", _line)); _pos++; continue; }
            if (c == '}') { tokens.Add(new CssToken(CssTokenType.CloseBrace, "}", _line)); _pos++; continue; }
            if (c == '(') { tokens.Add(new CssToken(CssTokenType.OpenParen, "(", _line)); _pos++; continue; }
            if (c == ')') { tokens.Add(new CssToken(CssTokenType.CloseParen, ")", _line)); _pos++; continue; }
            if (c == '[') { tokens.Add(new CssToken(CssTokenType.OpenBracket, "[", _line)); _pos++; continue; }
            if (c == ']') { tokens.Add(new CssToken(CssTokenType.CloseBracket, "]", _line)); _pos++; continue; }
            if (c == ';') { tokens.Add(new CssToken(CssTokenType.Semicolon, ";", _line)); _pos++; continue; }
            if (c == ',') { tokens.Add(new CssToken(CssTokenType.Comma, ",", _line)); _pos++; continue; }
            if (c == '>') { tokens.Add(new CssToken(CssTokenType.Greater, ">", _line)); _pos++; continue; }
            if (c == '+') { tokens.Add(new CssToken(CssTokenType.Plus, "+", _line)); _pos++; continue; }
            if (c == '~') { tokens.Add(new CssToken(CssTokenType.Tilde, "~", _line)); _pos++; continue; }
            if (c == '!') { tokens.Add(new CssToken(CssTokenType.Bang, "!", _line)); _pos++; continue; }
            if (c == '*') { tokens.Add(new CssToken(CssTokenType.Asterisk, "*", _line)); _pos++; continue; }
            if (c == '=') { tokens.Add(new CssToken(CssTokenType.Equals, "=", _line)); _pos++; continue; }
            if (c == ':') { tokens.Add(Peek(1) == ':' ? new CssToken(CssTokenType.DoubleColon, "::", _line) : new CssToken(CssTokenType.Colon, ":", _line)); _pos += Peek(1) == ':' ? 2 : 1; continue; }
            if (c == '.') { tokens.Add(new CssToken(CssTokenType.Dot, ".", _line)); _pos++; continue; }
            if (c == '#') { _pos++; var name = ReadIdent(); tokens.Add(new CssToken(CssTokenType.Hash, name, _line)); continue; }
            if (c == '@') { _pos++; var name = ReadIdent(); tokens.Add(new CssToken(CssTokenType.AtKeyword, name, _line)); continue; }
            if (c == '"' || c == '\'') { var s = ReadString(c); tokens.Add(new CssToken(CssTokenType.String, s, _line)); continue; }
            if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek(1)))) { var (num, unit) = ReadNumber(); tokens.Add(new CssToken(CssTokenType.Number, num, _line)); if (unit != null) tokens.Add(new CssToken(CssTokenType.Unit, unit, _line)); continue; }
            if (IsIdentStart(c)) { var name = ReadIdent(); tokens.Add(new CssToken(CssTokenType.Identifier, name, _line)); continue; }
            _pos++;
        }
        tokens.Add(new CssToken(CssTokenType.Eof, "", _line));
        return tokens;
    }

    private char Peek(int o) => _pos + o < _source.Length ? _source[_pos + o] : '\0';
    private void SkipWhitespace() { while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos])) { if (_source[_pos] == '\n') _line++; _pos++; } }
    private void SkipComment() { _pos += 2; while (_pos < _source.Length && !(_source[_pos] == '*' && Peek(1) == '/')) { if (_source[_pos] == '\n') _line++; _pos++; } _pos += 2; }
    private string ReadIdent() { var s = _pos; while (_pos < _source.Length && IsIdentChar(_source[_pos])) _pos++; return _source[s.._pos]; }
    private string ReadString(char q) { _pos++; var s = _pos; while (_pos < _source.Length && _source[_pos] != q) _pos++; var r = _source[s.._pos]; if (_pos < _source.Length) _pos++; return r; }
    private (string, string?) ReadNumber() { var s = _pos; if (_pos < _source.Length && _source[_pos] == '-') _pos++; while (_pos < _source.Length && (char.IsDigit(_source[_pos]) || _source[_pos] == '.')) _pos++; var num = _source[s.._pos]; string? unit = null; if (_pos < _source.Length && IsIdentStart(_source[_pos])) { var us = _pos; while (_pos < _source.Length && IsIdentChar(_source[_pos])) _pos++; unit = _source[us.._pos]; } return (num, unit); }
    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '-';
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';
}
