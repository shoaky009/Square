namespace Square.CSS.Ast;

public enum SimpleSelectorKind { Type, Class, Id, Universal, PseudoClass, Attribute }

public sealed record SimpleSelector(SimpleSelectorKind Kind, string Name);

public sealed record CompoundSelector(List<SimpleSelector> Parts);

public sealed record ComplexSelector(List<CompoundStep> Steps);

public sealed record CompoundStep(CompoundSelector Selector, Combinator Combinator);

public enum Combinator { Descendant, Child, Adjacent, GeneralSibling }

public sealed record Declaration(string Property, string Value, bool Important = false);

public sealed record CssRule(ComplexSelector Selector, List<Declaration> Declarations);

public sealed record CssStyleSheet(List<CssRule> Rules, List<CssAtRule> AtRules)
{
    public List<KeyFramesRule> KeyFrames { get; set; } = new();
};

public sealed record CssAtRule(string Name, string Params, List<Declaration> Declarations);

public sealed record KeyFrameStop(string Selector, List<Declaration> Declarations);

public sealed record KeyFramesRule(string Name, List<KeyFrameStop> Stops);