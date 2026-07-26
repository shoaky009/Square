using Square.Graphics;
using Square.UI;

namespace Square.Hosting;

/// <summary>元素检查快照：以根节点形式承载整个文档的检查结果。</summary>
public sealed record ElementInspectionSnapshot(ElementInspectionNode Root);

/// <summary>元素检查节点：承载单个元素的可视化与调试信息。</summary>
public sealed record ElementInspectionNode(
    int Id,
    string TagName,
    string? ElementId,
    string? ComponentName,
    Rect Bounds,
    ElementInspectionState State,
    ElementInspectionSource? Source,
    string? Text,
    int ChildCount,
    IReadOnlyList<ElementInspectionNode> Children);

/// <summary>元素检查来源：承载生成该元素的源码位置信息。</summary>
public sealed record ElementInspectionSource(
    int SourceId,
    string? File,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Kind);

/// <summary>元素检查状态：承载悬停、焦点、激活、禁用等交互态。</summary>
public sealed record ElementInspectionState(
    bool Hover,
    bool Focus,
    bool Active,
    bool Disabled);
