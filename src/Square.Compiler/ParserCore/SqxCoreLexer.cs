namespace Square.Compiler.ParserCore
{
    internal enum CoreTokenType
    {
        OpenTag,
        CloseTag,
        CloseSelfTag,
        EndTag,
        Equals,
        OpenBraceExpr,
        StringLiteral,
        Identifier,
        Text,
        Eof
    }

    internal struct CoreToken
    {
        public CoreTokenType Type;
        public string Text;
        public int Line;
        public int Column;
        public int Offset;
    }

    internal sealed class SqxCoreLexer
    {
        private readonly string _source;
        private int _position;
        private int _line = 1;
        private int _column = 1;
        private bool _inTag;
        private int _templateExpressionDepth;

        public SqxCoreLexer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public List<CoreToken> Tokenize()
        {
            var tokens = new List<CoreToken>();
            while (_position < _source.Length)
            {
                var c = _source[_position];
                if (c == '<')
                {
                    var line = _line;
                    var column = _column;
                    var offset = _position;
                    if (Peek(1) == '/')
                    {
                        AdvanceChar();
                        AdvanceChar();
                        _inTag = true;
                        var name = ReadIdentifier();
                        AdvanceWhitespace();
                        if (_position < _source.Length && _source[_position] == '>') AdvanceChar();
                        tokens.Add(New(CoreTokenType.EndTag, name, line, column, offset));
                        _inTag = false;
                    }
                    else
                    {
                        AdvanceChar();
                        _inTag = true;
                        tokens.Add(New(CoreTokenType.OpenTag, "<", line, column, offset));
                    }
                }
                else if (c == '/' && Peek(1) == '>')
                {
                    var token = New(CoreTokenType.CloseSelfTag, "/>", _line, _column, _position);
                    AdvanceChar();
                    AdvanceChar();
                    tokens.Add(token);
                    _inTag = false;
                }
                else if (c == '>')
                {
                    var token = New(CoreTokenType.CloseTag, ">", _line, _column, _position);
                    AdvanceChar();
                    tokens.Add(token);
                    _inTag = false;
                }
                else if (c == '=')
                {
                    var token = New(CoreTokenType.Equals, "=", _line, _column, _position);
                    AdvanceChar();
                    tokens.Add(token);
                }
                else if (c == '{')
                {
                    var line = _line;
                    var column = _column;
                    var offset = _position;
                    string expression;
                    if (!_inTag && TryReadTemplateLambda(out expression))
                    {
                        tokens.Add(New(CoreTokenType.OpenBraceExpr, expression, line, column, offset));
                        continue;
                    }

                    AdvanceChar();
                    expression = ReadUntilBrace();
                    tokens.Add(New(CoreTokenType.OpenBraceExpr, expression, line, column, offset));
                }
                else if (c == '}' && !_inTag && _templateExpressionDepth > 0)
                {
                    var token = New(CoreTokenType.OpenBraceExpr, "}", _line, _column, _position);
                    AdvanceChar();
                    _templateExpressionDepth--;
                    tokens.Add(token);
                }
                else if (c == '"' || c == '\'')
                {
                    var line = _line;
                    var column = _column;
                    var offset = _position;
                    tokens.Add(New(CoreTokenType.StringLiteral, ReadString(c), line, column, offset));
                }
                else if (char.IsWhiteSpace(c))
                {
                    AdvanceWhitespace();
                }
                else if (_inTag && IsIdentifierStart(c))
                {
                    var line = _line;
                    var column = _column;
                    var offset = _position;
                    tokens.Add(New(CoreTokenType.Identifier, ReadIdentifier(), line, column, offset));
                }
                else
                {
                    var line = _line;
                    var column = _column;
                    var offset = _position;
                    var text = ReadText();
                    if (!string.IsNullOrWhiteSpace(text))
                        tokens.Add(New(CoreTokenType.Text, text, line, column, offset));
                }
            }

            tokens.Add(New(CoreTokenType.Eof, "", _line, _column, _position));
            return tokens;
        }

        private static CoreToken New(CoreTokenType type, string text, int line, int column, int offset)
        {
            return new CoreToken { Type = type, Text = text, Line = line, Column = column, Offset = offset };
        }

        private char Peek(int offset)
        {
            return _position + offset < _source.Length ? _source[_position + offset] : '\0';
        }

        private string ReadIdentifier()
        {
            var start = _position;
            while (_position < _source.Length && IsIdentifierChar(_source[_position])) AdvanceChar();
            return _source.Substring(start, _position - start);
        }

        private string ReadString(char quote)
        {
            AdvanceChar();
            var start = _position;
            while (_position < _source.Length && _source[_position] != quote)
            {
                if (_source[_position] == '\\' && _position + 1 < _source.Length)
                {
                    AdvanceChar();
                    AdvanceChar();
                }
                else
                {
                    AdvanceChar();
                }
            }

            var result = _source.Substring(start, _position - start);
            if (_position < _source.Length) AdvanceChar();
            return result;
        }

        private string ReadUntilBrace()
        {
            var start = _position;
            var depth = 0;
            while (_position < _source.Length)
            {
                var c = _source[_position];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    if (depth == 0)
                    {
                        var result = _source.Substring(start, _position - start).Trim();
                        AdvanceChar();
                        return result;
                    }
                    depth--;
                }
                AdvanceChar();
            }
            return _source.Substring(start).Trim();
        }

        private string ReadText()
        {
            var start = _position;
            while (_position < _source.Length)
            {
                var c = _source[_position];
                if (c == '<' || c == '{' || (c == '}' && _templateExpressionDepth > 0)) break;
                AdvanceChar();
            }
            return _source.Substring(start, _position - start);
        }

        private bool TryReadTemplateLambda(out string expression)
        {
            expression = "";
            var start = _position + 1;
            var arrow = _source.IndexOf("=>", start, StringComparison.Ordinal);
            var tag = _source.IndexOf('<', start);
            if (arrow < 0 || tag < 0 || arrow > tag) return false;

            var candidate = _source.Substring(start, arrow + 2 - start).Trim();
            if (!candidate.StartsWith("(", StringComparison.Ordinal)) return false;

            while (_position <= arrow + 1) AdvanceChar();
            _templateExpressionDepth++;
            expression = candidate;
            return true;
        }

        private void AdvanceWhitespace()
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position])) AdvanceChar();
        }

        private void AdvanceChar()
        {
            if (_source[_position] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_' || c == '-';
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
        }
    }
}
