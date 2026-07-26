using System.Text.RegularExpressions;

namespace Square.UI.ElementApi;

/// <summary>
/// 轻量 CSS 选择器子集（对齐 <c>querySelector</c> / <c>querySelectorAll</c> 常用形态）。
/// 支持：标签、<c>#id</c>、<c>.class</c>、复合 <c>Tag.class</c>、后代空格、子代 <c>&gt;</c>、逗号分组。
/// </summary>
public static class CssSelector
{
    private static readonly Regex TokenRegex = new(
        @"\s*(>|\,|\.|#|[A-Za-z_][\w-]*)\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 在子树中查找第一个匹配元素。
    /// <paramref name="includeRoot"/> 为 false 时仅搜索后代（DOM Element.querySelector 行为）。
    /// </summary>
    public static Element? QuerySelector(Element root, string selector, bool includeRoot = false)
    {
        if (root == null || string.IsNullOrWhiteSpace(selector)) return null;
        foreach (var chain in SplitSelectorList(selector))
        {
            var found = QuerySelectorChain(root, chain, includeRoot);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>在子树中查找所有匹配元素（文档序）。</summary>
    public static List<Element> QuerySelectorAll(Element root, string selector, bool includeRoot = false)
    {
        var result = new List<Element>();
        if (root == null || string.IsNullOrWhiteSpace(selector)) return result;
        var seen = new HashSet<Element>();
        foreach (var chain in SplitSelectorList(selector))
        {
            foreach (var match in QuerySelectorAllChain(root, chain, includeRoot))
            {
                if (seen.Add(match))
                    result.Add(match);
            }
        }
        // 文档序：深度优先前序
        result.Sort((a, b) => CompareDocumentOrder(root, a, b));
        return result;
    }

    private static IEnumerable<string> SplitSelectorList(string selector)
    {
        var parts = selector.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    private static Element? QuerySelectorChain(Element root, string chain, bool includeRoot)
    {
        var steps = ParseChain(chain);
        if (steps.Count == 0) return null;

        IEnumerable<Element> candidates = includeRoot
            ? EnumerateSubtree(root)
            : EnumerateDescendants(root);

        foreach (var candidate in candidates)
        {
            if (MatchesChain(candidate, steps))
                return candidate;
        }
        return null;
    }

    private static List<Element> QuerySelectorAllChain(Element root, string chain, bool includeRoot)
    {
        var steps = ParseChain(chain);
        var result = new List<Element>();
        if (steps.Count == 0) return result;

        IEnumerable<Element> candidates = includeRoot
            ? EnumerateSubtree(root)
            : EnumerateDescendants(root);

        foreach (var candidate in candidates)
        {
            if (MatchesChain(candidate, steps))
                result.Add(candidate);
        }
        return result;
    }

    private static bool MatchesChain(Element element, List<SelectorStep> steps)
    {
        // 从右向左匹配：最后一步必须匹配 element，再向祖先回溯
        var index = steps.Count - 1;
        if (!MatchesSimple(element, steps[index].Simple))
            return false;

        var current = element;
        while (index > 0)
        {
            var combinator = steps[index].CombinatorFromPrevious;
            index--;
            var simple = steps[index].Simple;

            if (combinator == Combinator.Child)
            {
                current = current.Parent;
                if (current == null || !MatchesSimple(current, simple))
                    return false;
            }
            else // Descendant
            {
                var found = false;
                for (var ancestor = current.Parent; ancestor != null; ancestor = ancestor.Parent)
                {
                    if (MatchesSimple(ancestor, simple))
                    {
                        current = ancestor;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
        }
        return true;
    }

    private static bool MatchesSimple(Element element, SimpleSelector simple)
    {
        if (simple.TagName != null &&
            !string.Equals(element.TagName, simple.TagName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (simple.Id != null &&
            !string.Equals(element.Id, simple.Id, StringComparison.Ordinal))
            return false;

        foreach (var className in simple.Classes)
        {
            if (!element.ClassList.Contains(className))
                return false;
        }

        return simple.TagName != null || simple.Id != null || simple.Classes.Count > 0;
    }

    private static List<SelectorStep> ParseChain(string chain)
    {
        var steps = new List<SelectorStep>();
        var tokens = Tokenize(chain);
        if (tokens.Count == 0) return steps;

        var i = 0;
        var combinator = Combinator.None;
        while (i < tokens.Count)
        {
            if (tokens[i] == ">")
            {
                combinator = Combinator.Child;
                i++;
                continue;
            }

            var simple = new SimpleSelector();
            // 复合：Tag? (#id | .class)*
            if (IsIdent(tokens[i]) && tokens[i] is not ("." or "#"))
            {
                simple.TagName = tokens[i];
                i++;
            }

            while (i < tokens.Count)
            {
                if (tokens[i] == ".")
                {
                    i++;
                    if (i >= tokens.Count || !IsIdent(tokens[i])) break;
                    simple.Classes.Add(tokens[i]);
                    i++;
                    continue;
                }
                if (tokens[i] == "#")
                {
                    i++;
                    if (i >= tokens.Count || !IsIdent(tokens[i])) break;
                    simple.Id = tokens[i];
                    i++;
                    continue;
                }
                break;
            }

            if (simple.TagName == null && simple.Id == null && simple.Classes.Count == 0)
                break;

            steps.Add(new SelectorStep(simple, combinator == Combinator.None && steps.Count > 0
                ? Combinator.Descendant
                : combinator));
            combinator = Combinator.Descendant; // 后续默认空格为后代，直到遇到 >
        }

        // 修正：第一步不应带有来自「前一步」的组合符
        if (steps.Count > 0)
            steps[0] = new SelectorStep(steps[0].Simple, Combinator.None);

        return steps;
    }

    private static List<string> Tokenize(string input)
    {
        var list = new List<string>();
        foreach (Match match in TokenRegex.Matches(input))
        {
            var t = match.Groups[1].Value;
            if (t.Length > 0)
                list.Add(t);
        }
        return list;
    }

    private static bool IsIdent(string token) =>
        token.Length > 0 && token is not (">" or "," or "." or "#") &&
        (char.IsLetter(token[0]) || token[0] == '_');

    private static IEnumerable<Element> EnumerateSubtree(Element root)
    {
        yield return root;
        foreach (var d in EnumerateDescendants(root))
            yield return d;
    }

    private static IEnumerable<Element> EnumerateDescendants(Element root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var d in EnumerateDescendants(child))
                yield return d;
        }
    }

    private static int CompareDocumentOrder(Element root, Element a, Element b)
    {
        if (ReferenceEquals(a, b)) return 0;
        var pathA = BuildPath(root, a);
        var pathB = BuildPath(root, b);
        if (pathA == null || pathB == null) return 0;
        var n = Math.Min(pathA.Count, pathB.Count);
        for (var i = 0; i < n; i++)
        {
            if (!ReferenceEquals(pathA[i], pathB[i]))
            {
                // 比较在共同父下的兄弟顺序
                var parent = i == 0 ? null : pathA[i - 1];
                if (parent == null) return 0;
                return parent.Children.IndexOf(pathA[i]).CompareTo(parent.Children.IndexOf(pathB[i]));
            }
        }
        return pathA.Count.CompareTo(pathB.Count);
    }

    private static List<Element>? BuildPath(Element root, Element node)
    {
        var path = new List<Element>();
        for (Element? current = node; current != null; current = current.Parent)
        {
            path.Add(current);
            if (ReferenceEquals(current, root))
            {
                path.Reverse();
                return path;
            }
        }
        return null;
    }

    private enum Combinator { None, Descendant, Child }

    private sealed class SimpleSelector
    {
        /// <summary>标签名（null 表示不限定）。</summary>
        public string? TagName;
        /// <summary>id（null 表示不限定）。</summary>
        public string? Id;
        /// <summary>class 列表。</summary>
        public List<string> Classes { get; } = [];
    }

    private readonly struct SelectorStep(SimpleSelector simple, Combinator combinatorFromPrevious)
    {
        /// <summary>当前步骤的简单选择器。</summary>
        public SimpleSelector Simple { get; } = simple;
        /// <summary>与上一步的组合符。</summary>
        public Combinator CombinatorFromPrevious { get; } = combinatorFromPrevious;
    }
}
