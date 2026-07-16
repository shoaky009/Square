namespace Square.Markup.Ast;

public sealed record SqxTemplate(List<SqxNode> Roots, int Line, int Column);

public sealed record SqxScript(
    string Language,
    string Code,
    string? Namespace,
    string? ComponentName,
    string Access,
    int Line,
    int Column
);

public sealed record SqxStyle(
    string Css,
    int Line,
    int Column
);

public sealed record SqxDocument(
    string Name,
    SqxTemplate Template,
    SqxScript? Script,
    SqxStyle? Style
);