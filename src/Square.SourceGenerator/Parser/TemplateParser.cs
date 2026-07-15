namespace Square.SourceGenerator.Parser
{
    internal class TemplateParser
    {
        private readonly List<SqxToken> _t;
        private int _i;

        public TemplateParser(List<SqxToken> t) { _t = t; }

        public List<SqxNode> ParseRoots()
        {
            var roots = new List<SqxNode>();
            while (Peek().Type != SqxTokenType.Eof)
            {
                var n = ParseNode();
                if (n != null) roots.Add(n);
            }
            return roots;
        }

        private SqxNode ParseNode()
        {
            var tk = Peek();
            switch (tk.Type)
            {
                case SqxTokenType.OpenTag: return ParseElement();
                case SqxTokenType.Text: _i++; return new SqxText { Text = tk.Text.Trim(), Kind = SqxNodeKind.Text, Line = tk.Line, Column = tk.Column };
                case SqxTokenType.OpenBraceExpr: _i++; return new SqxExpression { Expression = tk.Text, Kind = SqxNodeKind.Expression, Line = tk.Line, Column = tk.Column };
                default: _i++; return null;
            }
        }

        private SqxNode ParseElement()
        {
            var open = Expect(SqxTokenType.OpenTag);
            var nameTk = Expect(SqxTokenType.Identifier);
            var tagName = nameTk.Text;

            var attrs = new List<SqxAttribute>();
            while (Peek().Type != SqxTokenType.CloseTag && Peek().Type != SqxTokenType.CloseSelfTag && Peek().Type != SqxTokenType.Eof)
            {
                var a = ParseAttr();
                if (a != null) attrs.Add(a);
            }

            var selfClose = Peek().Type == SqxTokenType.CloseSelfTag;
            if (selfClose)
            {
                _i++;
                return NewElement(tagName, attrs, open);
            }

            Expect(SqxTokenType.CloseTag);

            var children = new List<SqxNode>();
            while (true)
            {
                var t = Peek();
                if (t.Type == SqxTokenType.Eof) break;
                if (t.Type == SqxTokenType.EndTag) { _i++; break; }
                var child = ParseNode();
                if (child != null) children.Add(child);
            }

            var el = NewElement(tagName, attrs, open);
            el.Children = children;
            return el;
        }

        private SqxElement NewElement(string tagName, List<SqxAttribute> attrs, SqxToken open)
        {
            var kind = SqxNodeKind.Element;
            if (tagName == "Show") kind = SqxNodeKind.Show;
            else if (tagName == "For") kind = SqxNodeKind.For;
            else if (tagName == "Switch") kind = SqxNodeKind.Switch;
            else if (tagName == "Match") kind = SqxNodeKind.Match;
            else if (tagName == "Slot" || tagName == "Outlet") kind = SqxNodeKind.Slot;
            else if (tagName == "Router") kind = SqxNodeKind.Router;
            else if (tagName == "Route") kind = SqxNodeKind.Route;

            return new SqxElement
            {
                TagName = tagName,
                Attributes = attrs,
                Kind = kind,
                Line = open.Line,
                Column = open.Column
            };
        }

        private SqxAttribute ParseAttr()
        {
            var nameTk = Peek();
            if (nameTk.Type != SqxTokenType.Identifier) { _i++; return null; }
            _i++;

            if (Peek().Type != SqxTokenType.Equals)
                return new SqxAttribute { Name = nameTk.Text, RawValue = null, IsExpression = false, Line = nameTk.Line };

            _i++;
            var v = Peek();
            string raw = null;
            var isExpr = false;

            if (v.Type == SqxTokenType.StringLiteral) { _i++; raw = v.Text; }
            else if (v.Type == SqxTokenType.OpenBraceExpr) { _i++; raw = v.Text; isExpr = true; }

            return new SqxAttribute { Name = nameTk.Text, RawValue = raw, IsExpression = isExpr, Line = nameTk.Line };
        }

        private SqxToken Peek() { return _i < _t.Count ? _t[_i] : _t[_t.Count - 1]; }
        private SqxToken Expect(SqxTokenType type)
        {
            var tk = Peek();
            _i++;
            return tk;
        }
    }
}
