namespace Square.UI;

public sealed class ElementDebugInfo
{
    public int SourceId { get; init; }
    public string? SourcePath { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
    public string? TagName { get; init; }
    public string? ComponentName { get; init; }
    public ElementGeneratedKind Kind { get; init; }

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

public enum ElementGeneratedKind
{
    TemplateNode,
    ComponentRoot,
    SlotContent,
    ForItem,
    ConditionalBranch,
    GeneratedWrapper
}
