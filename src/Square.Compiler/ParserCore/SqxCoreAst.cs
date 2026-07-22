namespace Square.Compiler.ParserCore
{
    internal abstract class CoreNode
    {
        public int Line;
        public int Column;
    }

    internal sealed class CoreElement : CoreNode
    {
        public string TagName = "";
        public List<CoreAttribute> Attributes = new List<CoreAttribute>();
        public List<CoreNode> Children = new List<CoreNode>();
    }

    internal sealed class CoreText : CoreNode
    {
        public string Text = "";
    }

    internal sealed class CoreExpression : CoreNode
    {
        public string Expression = "";
    }

    internal sealed class CoreAttribute
    {
        public string Name = "";
        public string RawValue;
        public bool IsExpression;
        public int Line;
        public int Column;
    }

    internal sealed class CoreTemplate
    {
        public List<CoreNode> Roots = new List<CoreNode>();
        public int Line;
        public int Column;
    }

    internal sealed class CoreScript
    {
        public string Language = "csharp";
        public string Code = "";
        public string Namespace;
        public string ComponentName;
        public string Access = "public";
        public int Line;
        public int Column;
    }

    internal sealed class CoreStyle
    {
        public string Css = "";
        public int Line;
        public int Column;
    }

    internal sealed class CoreDocument
    {
        public string FileName = "";
        public CoreTemplate Template = new CoreTemplate();
        public CoreScript Script;
        public CoreStyle Style;
    }
}
