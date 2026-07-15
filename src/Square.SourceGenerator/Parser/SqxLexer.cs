namespace Square.SourceGenerator.Parser
{
    internal enum SqxTokenType
    {
        OpenTag, CloseTag, CloseSelfTag, EndTag,
        Equals, OpenBraceExpr, StringLiteral,
        Identifier, Text, Eof
    }

    internal struct SqxToken
    {
        public SqxTokenType Type;
        public string Text;
        public int Line;
        public int Column;
    }

    internal class SqxLexer
    {
        private readonly string _s;
        private int _pos, _line = 1, _col = 1;
        private bool _inTag;
        private int _templateExpressionDepth;

        public SqxLexer(string s) { _s = s; }

        public List<SqxToken> Tokenize()
        {
            var tokens = new List<SqxToken>();
            while (_pos < _s.Length)
            {
                var c = _s[_pos];
                if (c == '<')
                {
                    if (Peek(1) == '/')
                    {
                        _pos += 2; _col += 2;
                        _inTag = true;
                        var name = ReadIdent();
                        AdvanceWs();
                        if (_pos < _s.Length && _s[_pos] == '>') { _pos++; _col++; }
                        tokens.Add(New(SqxTokenType.EndTag, name));
                        _inTag = false;
                    }
                    else
                    {
                        _pos++; _col++;
                        _inTag = true;
                        tokens.Add(New(SqxTokenType.OpenTag, "<"));
                    }
                }
                else if (c == '/' && Peek(1) == '>')
                {
                    _pos += 2; _col += 2;
                    tokens.Add(New(SqxTokenType.CloseSelfTag, "/>"));
                    _inTag = false;
                }
                else if (c == '>')
                {
                    _pos++; _col++;
                    tokens.Add(New(SqxTokenType.CloseTag, ">"));
                    _inTag = false;
                }
                else if (c == '=')
                {
                    _pos++; _col++;
                    tokens.Add(New(SqxTokenType.Equals, "="));
                }
                else if (c == '{')
                {
                    string expr;
                    if (!_inTag && TryReadTemplateLambda(out expr))
                    {
                        tokens.Add(New(SqxTokenType.OpenBraceExpr, expr));
                        continue;
                    }
                    _pos++; _col++;
                    expr = ReadUntilBrace();
                    tokens.Add(New(SqxTokenType.OpenBraceExpr, expr));
                }
                else if (c == '}' && !_inTag && _templateExpressionDepth > 0)
                {
                    _pos++; _col++;
                    _templateExpressionDepth--;
                    tokens.Add(New(SqxTokenType.OpenBraceExpr, "}"));
                }
                else if (c == '"' || c == '\'')
                {
                    var str = ReadString(c);
                    tokens.Add(New(SqxTokenType.StringLiteral, str));
                }
                else if (char.IsWhiteSpace(c))
                {
                    AdvanceWs();
                }
                else if (_inTag && IsIdentStart(c))
                {
                    var name = ReadIdent();
                    tokens.Add(New(SqxTokenType.Identifier, name));
                }
                else
                {
                    var text = ReadText();
                    if (!string.IsNullOrWhiteSpace(text))
                        tokens.Add(New(SqxTokenType.Text, text));
                }
            }
            tokens.Add(New(SqxTokenType.Eof, ""));
            return tokens;
        }

        private SqxToken New(SqxTokenType t, string text) => new SqxToken { Type = t, Text = text, Line = _line, Column = _col };

        private char Peek(int o) => _pos + o < _s.Length ? _s[_pos + o] : '\0';

        private string ReadIdent()
        {
            var start = _pos;
            while (_pos < _s.Length && IsIdentChar(_s[_pos])) { _pos++; _col++; }
            return _s.Substring(start, _pos - start);
        }

        private string ReadString(char q)
        {
            _pos++; _col++;
            var start = _pos;
            while (_pos < _s.Length && _s[_pos] != q)
            {
                if (_s[_pos] == '\\' && _pos + 1 < _s.Length) { _pos += 2; _col += 2; }
                else Adv();
            }
            var r = _s.Substring(start, _pos - start);
            if (_pos < _s.Length) { _pos++; _col++; }
            return r;
        }

        private string ReadUntilBrace()
        {
            var start = _pos;
            var depth = 0;
            while (_pos < _s.Length)
            {
                var c = _s[_pos];
                if (c == '{') depth++;
                else if (c == '}') { if (depth == 0) { _pos++; _col++; break; } depth--; }
                Adv();
            }
            var end = _pos - 1;
            return _s.Substring(start, end - start).Trim();
        }

        private string ReadText()
        {
            var start = _pos;
            while (_pos < _s.Length && _s[_pos] != '<' && _s[_pos] != '{' &&
                   !(_s[_pos] == '}' && _templateExpressionDepth > 0)) Adv();
            return _s.Substring(start, _pos - start);
        }

        private bool TryReadTemplateLambda(out string expression)
        {
            expression = "";
            var start = _pos + 1;
            var arrow = _s.IndexOf("=>", start, StringComparison.Ordinal);
            var tag = _s.IndexOf('<', start);
            if (arrow < 0 || tag < 0 || arrow > tag) return false;

            var candidate = _s.Substring(start, arrow + 2 - start).Trim();
            if (!candidate.StartsWith("(", StringComparison.Ordinal)) return false;

            while (_pos <= arrow + 1) Adv();
            _templateExpressionDepth++;
            expression = candidate;
            return true;
        }

        private void AdvanceWs() { while (_pos < _s.Length && char.IsWhiteSpace(_s[_pos])) Adv(); }
        private void Adv() { if (_s[_pos] == '\n') { _line++; _col = 1; } else _col++; _pos++; }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '-';
        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
    }
}
