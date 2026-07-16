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
        foreach (var rule in sheet.Rules)
        {
            _rules.Add(rule);
            foreach (var declaration in rule.Declarations)
                if (declaration.Property.StartsWith("--", StringComparison.Ordinal))
                    _variables[declaration.Property] = declaration.Value;
        }
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
        ApplyInheritedProperties(visual);
        var matched = new List<(CssRule rule, int specificity, int order)>();

        for (var i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (TryMatchSelector(rule.Selector, visual, out var spec))
                matched.Add((rule, spec, i));
        }

        matched.Sort((a, b) =>
        {
            var specificity = a.specificity.CompareTo(b.specificity);
            return specificity != 0 ? specificity : a.order.CompareTo(b.order);
        });

        foreach (var (rule, specificity, _) in matched)
        {
            foreach (var decl in rule.Declarations)
            {
                if (decl.Property.StartsWith("--", StringComparison.Ordinal)) continue;
                var value = ResolveVariables(decl.Value);
                ApplyDeclaration(visual, decl.Property, value,
                    decl.Important ? int.MaxValue : specificity);
            }
        }
    }

    public void ApplyStylesToTree(Visual visual)
    {
        ApplyStyles(visual);
        foreach (var child in visual.Children)
            ApplyStylesToTree(child);
    }

    private string ResolveVariables(string value)
    {
        while (value.Contains("var(", StringComparison.Ordinal))
        {
            var start = value.IndexOf("var(", StringComparison.Ordinal);
            var end = value.IndexOf(')', start);
            if (end < 0) break;
            var inner = value[(start + 4)..end].Trim();
            var commaIdx = inner.IndexOf(',');
            var varName = commaIdx >= 0 ? inner[..commaIdx].Trim() : inner;
            var fallback = commaIdx >= 0 ? inner[(commaIdx + 1)..].Trim() : null;
            var replacement = GetVariable(varName) ?? fallback;
            if (replacement == null) break;
            value = value[..start] + replacement + value[(end + 1)..];
        }
        return value;
    }

    private string? GetVariable(string name)
    {
        if (_activeTheme != null && _themes.TryGetValue(_activeTheme, out var theme) && theme.TryGetValue(name, out var themed))
            return themed;
        return _variables.TryGetValue(name, out var value) ? value : null;
    }

    private static void ApplyInheritedProperties(Visual visual)
    {
        if (visual.Parent == null) return;
        foreach (var property in new[] { "color", "font-family", "font-size" })
        {
            if (visual.Style.Get(property) != null) continue;
            var inherited = visual.Parent.Style.Get(property);
            if (inherited != null) visual.Style.SetCascaded(property, inherited, -1);
        }
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
            var relation = selector.Steps[i + 1].Combinator;
            Visual? candidate = relation switch
            {
                Combinator.Child => current.Parent,
                Combinator.Adjacent => PreviousSibling(current),
                _ => null
            };

            if (relation is Combinator.Child or Combinator.Adjacent)
            {
                var s = 0;
                if (candidate == null || !MatchCompound(step.Selector, candidate, ref s)) return false;
                specificity += s;
                current = candidate;
                continue;
            }

            if (relation == Combinator.GeneralSibling)
            {
                var parent = current.Parent;
                if (parent == null) return false;
                var currentIndex = parent.Children.IndexOf(current);
                var matchedSibling = false;
                for (var siblingIndex = currentIndex - 1; siblingIndex >= 0; siblingIndex--)
                {
                    var s = 0;
                    var sibling = parent.Children[siblingIndex];
                    if (!MatchCompound(step.Selector, sibling, ref s)) continue;
                    specificity += s;
                    current = sibling;
                    matchedSibling = true;
                    break;
                }
                if (!matchedSibling) return false;
                continue;
            }

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

    private static Visual? PreviousSibling(Visual visual)
    {
        var parent = visual.Parent;
        if (parent == null) return null;
        var index = parent.Children.IndexOf(visual);
        return index > 0 ? parent.Children[index - 1] : null;
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

    private static bool MatchPseudoClass(Visual visual, string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.StartsWith("nth-child(", StringComparison.Ordinal) && lower.EndsWith(')'))
        {
            var argument = lower[10..^1].Trim();
            if (visual.Parent == null) return false;
            var index = visual.Parent.Children.IndexOf(visual) + 1;
            if (argument == "odd") return index % 2 == 1;
            if (argument == "even") return index % 2 == 0;
            return int.TryParse(argument, out var expected) && index == expected;
        }

        if (lower.StartsWith("not(", StringComparison.Ordinal) && lower.EndsWith(')'))
            return !MatchSimpleArgument(visual, name[4..^1].Trim());

        return lower switch
        {
            "hover" => visual.HasState(VisualState.Hover),
            "focus" => visual.HasState(VisualState.Focus),
            "active" => visual.HasState(VisualState.Active),
            "disabled" => visual.HasState(VisualState.Disabled),
            "checked" => visual.HasState(VisualState.Checked),
            "empty" => visual.Children.Count == 0,
            "first-child" => visual.Parent?.Children[0] == visual,
            "last-child" => visual.Parent?.Children[^1] == visual,
            "only-child" => visual.Parent?.Children.Count == 1,
            "root" => visual.Parent == null,
            _ => false
        };
    }

    private static bool MatchSimpleArgument(Visual visual, string selector)
    {
        if (selector.StartsWith('.')) return visual.ClassList.Contains(selector[1..]);
        if (selector.StartsWith('#')) return visual.GetProperty<string>("id") == selector[1..];
        if (selector == "*") return true;
        return string.Equals(visual.GetType().Name, selector, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyDeclaration(Visual visual, string property, string value, int specificity)
    {
        visual.Style.SetCascaded(property, value, specificity);
    }
}
