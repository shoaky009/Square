using Square.Graphics;
using Square.UI;

namespace Square.Hosting;

public sealed record ElementInspectionSnapshot(ElementInspectionNode Root);

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

public sealed record ElementInspectionSource(
    int SourceId,
    string? File,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Kind);

public sealed record ElementInspectionState(
    bool Hover,
    bool Focus,
    bool Active,
    bool Disabled);
