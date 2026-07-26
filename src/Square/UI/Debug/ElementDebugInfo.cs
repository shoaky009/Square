namespace Square.UI;

/// <summary>元素调试来源信息（Square 生成器专用）。</summary>
public sealed class ElementDebugInfo
{
    /// <summary>源片段标识。</summary>
    public int SourceId { get; init; }
    /// <summary>源文件路径。</summary>
    public string? SourcePath { get; init; }
    /// <summary>起始行号。</summary>
    public int StartLine { get; init; }
    /// <summary>起始列号。</summary>
    public int StartColumn { get; init; }
    /// <summary>结束行号。</summary>
    public int EndLine { get; init; }
    /// <summary>结束列号。</summary>
    public int EndColumn { get; init; }
    /// <summary>标签名。</summary>
    public string? TagName { get; init; }
    /// <summary>组件名。</summary>
    public string? ComponentName { get; init; }
    /// <summary>生成类型。</summary>
    public ElementGeneratedKind Kind { get; init; }

    /// <summary>创建 <see cref="ElementDebugInfo"/> 实例。</summary>
    public static ElementDebugInfo Create(
        int sourceId,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        string? tagName = null,
        string? componentName = null,
        ElementGeneratedKind kind = ElementGeneratedKind.TemplateNode,
        string? sourcePath = null) => new()
    {
        SourceId = sourceId,
        SourcePath = sourcePath,
        StartLine = startLine,
        StartColumn = startColumn,
        EndLine = endLine,
        EndColumn = endColumn,
        TagName = tagName,
        ComponentName = componentName,
        Kind = kind
    };
}

/// <summary>元素生成类型。</summary>
public enum ElementGeneratedKind
{
    /// <summary>模板节点。</summary>
    TemplateNode,
    /// <summary>组件根节点。</summary>
    ComponentRoot,
    /// <summary>插槽内容。</summary>
    SlotContent,
    /// <summary>循环项。</summary>
    ForItem,
    /// <summary>条件分支。</summary>
    ConditionalBranch,
    /// <summary>生成器包装节点。</summary>
    GeneratedWrapper
}