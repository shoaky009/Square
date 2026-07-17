namespace Square.Markup.Ast;

public enum SqxNodeKind
{
    Element,
    Text,
    Expression,
    Show,
    For,
    Switch,
    Match,
    Slot,
    Router,
    Route
}

public abstract record SqxNode(SqxNodeKind Kind, int Line, int Column);

public sealed record SqxElement(
    string TagName,
    List<SqxAttribute> Attributes,
    List<SqxNode> Children,
    int Line,
    int Column
) : SqxNode(SqxNodeKind.Element, Line, Column)
{
    public new SqxNodeKind Kind { get; set; } = SqxNodeKind.Element;
};

public sealed record SqxText(
    string Text,
    int Line,
    int Column
) : SqxNode(SqxNodeKind.Text, Line, Column);

public sealed record SqxExpression(
    string Expression,
    int Line,
    int Column
) : SqxNode(SqxNodeKind.Expression, Line, Column);

public sealed record SqxAttribute(
    string Name,
    string? RawValue,
    SqxAttributeValue? Value,
    int Line,
    int Column
);

public sealed record SqxAttributeValue(
    bool IsExpression,
    string Content
);
