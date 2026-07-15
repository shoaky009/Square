namespace Square.SourceGenerator.Parser
{
    internal enum SqxNodeKind { Element, Text, Expression, Show, For, Switch, Match }

    internal abstract class SqxNode
    {
        public SqxNodeKind Kind;
        public int Line;
        public int Column;
    }

    internal class SqxElement : SqxNode
    {
        public string TagName = "";
        public List<SqxAttribute> Attributes = new List<SqxAttribute>();
        public List<SqxNode> Children = new List<SqxNode>();
    }

    internal class SqxText : SqxNode
    {
        public string Text = "";
    }

    internal class SqxExpression : SqxNode
    {
        public string Expression = "";
    }

    internal class SqxAttribute
    {
        public string Name = "";
        public string RawValue;
        public bool IsExpression;
        public int Line;
    }

    internal class SqxTemplate
    {
        public List<SqxNode> Roots = new List<SqxNode>();
    }

    internal class SqxDocument
    {
        public string Name = "";
        public SqxTemplate Template = new SqxTemplate();
        public string ScriptCode;
        public string ScriptLang;
        public string StyleCode;
    }
}