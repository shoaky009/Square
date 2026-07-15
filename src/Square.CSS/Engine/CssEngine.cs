using Square.CSS.Ast;
using Square.UI;

namespace Square.CSS.Engine;

public sealed class CssEngine
{
    private readonly List<CssRule> _rules = [];
    private readonly Dictionary<string, string> _variables = [];
    private readonly Dictionary<string, KeyFramesRule> _keyFrames = new();
    private readonly Dictionary<string, Dictionary<string, string>> _themes = new();
    private string? _activeTheme;

    public void LoadStyleSheet(CssStyleSheet sheet)
    {
        foreach (var rule in sheet.Rules) _rules.Add(rule);
        foreach (var kf in sheet.KeyFrames) _keyFrames[kf.Name] = kf;
    }

    public KeyFramesRule? GetKeyFrames(string name) =>
        _keyFrames.TryGetValue(name, out var kf) ? kf : null;

    public void SetTheme(string name)
    {
        _activeTheme = name;
    }

    public void RegisterTheme(string name, Dictionary<string, string> variables)
    {
        _themes[name] = variables;
    }

    public IReadOnlyDictionary<string, string>? GetActiveThemeVariables() =>
        _activeTheme != null && _themes.TryGetValue(_activeTheme, out var v) ? v : null;

    public void ApplyStyles(Visual visual)
    {
        var matched = new List<(CssRule rule, int specificity)>();

        foreach (var rule in _rules)
        {
            if (TryMatchSelector(rule.Selector, visual, out var spec))
                matched.Add((rule, spec));
        }

        matched.Sort((a, b) => a.specificity.CompareTo(b.specificity));

        foreach (var (rule, _) in matched)
        {
            foreach (var decl in rule.Declarations)
            {
                var value = ResolveVariables(decl.Value);
                ApplyDeclaration(visual, decl.Property, value);
            }
        }
    }

    private string ResolveVariables(string value)
    {
        if (!value.Contains("var(")) return value;
        var start = value.IndexOf("var(");
        var end = value.IndexOf(')', start);
        if (end < 0) return value;
        var inner = value[(start + 4)..end].Trim();
        var commaIdx = inner.IndexOf(',');
        var varName = commaIdx >= 0 ? inner[..commaIdx].Trim() : inner;
        var fallback = commaIdx >= 0 ? inner[(commaIdx + 1)..].Trim() : null;

        if (_variables.TryGetValue(varName, out var v)) return v;
        return fallback ?? value;
    }

    private static bool TryMatchSelector(ComplexSelector selector, Visual visual, out int specificity)
    {
        specificity = 0;
        if (selector.Steps.Count == 0) return false;

        var last = selector.Steps[^1];
        if (!MatchCompound(last.Selector, visual, ref specificity)) return false;

        var current = visual;
        for (int i = selector.Steps.Count - 2; i >= 0; i--)
        {
            var step = selector.Steps[i];
            var matched = false;
            var p = current.Parent;
            while (p != null)
            {
                var s = 0;
                if (MatchCompound(step.Selector, p, ref s))
                {
                    specificity += s;
                    current = p;
                    matched = true;
                    break;
                }
                p = p.Parent;
            }
            if (!matched) return false;
        }
        return true;
    }

    private static bool MatchCompound(CompoundSelector compound, Visual visual, ref int specificity)
    {
        foreach (var part in compound.Parts)
        {
            switch (part.Kind)
            {
                case SimpleSelectorKind.Type:
                    if (!string.Equals(visual.GetType().Name, part.Name, StringComparison.OrdinalIgnoreCase))
                        return false;
                    specificity += 1;
                    break;
                case SimpleSelectorKind.Class:
                    if (!visual.ClassList.Contains(part.Name)) return false;
                    specificity += 10;
                    break;
                case SimpleSelectorKind.Id:
                    if (visual.GetProperty<string>("id") != part.Name) return false;
                    specificity += 100;
                    break;
                case SimpleSelectorKind.Universal:
                    break;
                case SimpleSelectorKind.PseudoClass:
                    if (!MatchPseudoClass(visual, part.Name)) return false;
                    specificity += 10;
                    break;
            }
        }
        return true;
    }

    private static bool MatchPseudoClass(Visual visual, string name) => name.ToLowerInvariant() switch
    {
        "hover" => visual.HasState(VisualState.Hover),
        "focus" => visual.HasState(VisualState.Focus),
        "active" => visual.HasState(VisualState.Active),
        "disabled" => visual.HasState(VisualState.Disabled),
        "checked" => visual.HasState(VisualState.Checked),
        "empty" => visual.Children.Count == 0,
        "first-child" => visual.Parent?.Children[0] == visual,
        "last-child" => visual.Parent?.Children[^1] == visual,
        _ => false
    };

    private static void ApplyDeclaration(Visual visual, string property, string value)
    {
        visual.Style.Set(property, value);
    }
}