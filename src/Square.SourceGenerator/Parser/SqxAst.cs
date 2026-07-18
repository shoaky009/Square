namespace Square.SourceGenerator.Parser
{
    /// <summary>
    /// AST 节点种类。结构原语统一为 <see cref="Directive"/>（由 DirectiveCatalog 识别）。
    /// </summary>
    internal enum SqxNodeKind
    {
        Element,
        Text,
        Expression,
        /// <summary>结构指令（Show/For/Switch/Match/Slot/Router/Route 等）。</summary>
        Directive
    }

    internal abstract class SqxNode
    {
        public SqxNodeKind Kind;
        public int Line;
        public int Column;
    }

    internal class SqxElement : SqxNode
    {
        public string TagName = "";
        /// <summary>Catalog 归一化后的主标签（如 Outlet → Slot）；非指令时为 null。</summary>
        public string DirectiveId;
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
        public string Namespace;
        public string Access = "public";
    }
}
