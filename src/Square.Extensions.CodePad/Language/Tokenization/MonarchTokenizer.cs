using System.Text.Json;
using System.Text.RegularExpressions;

namespace Square.Extensions.CodePad;

/// <summary>Monaco Monarch 兼容子集运行时。</summary>
public sealed class MonarchTokenizer : ITokenizer
{
    private readonly Dictionary<string, List<MonarchRule>> _states;
    private readonly HashSet<string> _keywords;
    private readonly string _defaultToken;
    private readonly string _tokenPostfix;

    private MonarchTokenizer(
        Dictionary<string, List<MonarchRule>> states,
        HashSet<string> keywords,
        string defaultToken,
        string tokenPostfix)
    {
        _states = states;
        _keywords = keywords;
        _defaultToken = defaultToken;
        _tokenPostfix = tokenPostfix;
    }

    /// <summary>从 Monarch JSON 定义创建。</summary>
    public static MonarchTokenizer FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var defaultToken = root.TryGetProperty("defaultToken", out var dt) ? dt.GetString() ?? "source" : "source";
        var tokenPostfix = root.TryGetProperty("tokenPostfix", out var tp) ? tp.GetString() ?? "" : "";
        var keywords = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in kw.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) keywords.Add(s);
            }
        }

        var states = new Dictionary<string, List<MonarchRule>>(StringComparer.Ordinal);
        if (root.TryGetProperty("tokenizer", out var tokenizer) && tokenizer.ValueKind == JsonValueKind.Object)
        {
            foreach (var state in tokenizer.EnumerateObject())
            {
                var rules = new List<MonarchRule>();
                if (state.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ruleEl in state.Value.EnumerateArray())
                        rules.Add(ParseRule(ruleEl));
                }
                states[state.Name] = rules;
            }
        }

        if (!states.ContainsKey("root"))
            states["root"] = [];

        return new MonarchTokenizer(states, keywords, defaultToken, tokenPostfix);
    }

    /// <inheritdoc/>
    public IReadOnlyList<TokenSpan> TokenizeLine(string line, ref string state)
    {
        // state encoding: "name" or "name|parent|..." for @pop stack
        var stack = DecodeState(state);
        var tokens = new List<TokenSpan>();
        var i = 0;
        var guard = 0;
        while (i < line.Length && guard++ < line.Length * 8 + 8)
        {
            var current = stack.Count > 0 ? stack[^1] : "root";
            if (!_states.TryGetValue(current, out var rules) || rules.Count == 0)
            {
                tokens.Add(new TokenSpan(i, line.Length - i, Qualify(_defaultToken)));
                break;
            }

            var matched = false;
            foreach (var rule in rules)
            {
                if (rule.Include is { } include)
                {
                    var name = include.Trim().TrimStart('@', '#');
                    if (_states.TryGetValue(name, out var included))
                    {
                        foreach (var inner in included)
                        {
                            if (TryMatch(line, i, inner, stack, tokens, out var next))
                            {
                                i = next;
                                matched = true;
                                break;
                            }
                        }
                        if (matched) break;
                    }
                    continue;
                }

                if (TryMatch(line, i, rule, stack, tokens, out var advanced))
                {
                    i = advanced;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                tokens.Add(new TokenSpan(i, 1, Qualify(_defaultToken)));
                i++;
            }
        }

        state = EncodeState(stack);
        return Merge(tokens);
    }

    private bool TryMatch(string line, int index, MonarchRule rule, List<string> stack, List<TokenSpan> tokens, out int nextIndex)
    {
        nextIndex = index;
        if (rule.Regex == null) return false;
        // Use suffix + ^ so matching is always anchored at the current column.
        var m = rule.Regex.Match(line[index..]);
        if (!m.Success || m.Index != 0 || m.Length == 0) return false;

        var token = rule.Token ?? _defaultToken;
        if (rule.CasesKeyword && _keywords.Contains(m.Value))
            token = rule.KeywordToken ?? "keyword";

        tokens.Add(new TokenSpan(index, m.Length, Qualify(token)));
        if (!string.IsNullOrEmpty(rule.Next))
        {
            var next = rule.Next!;
            if (string.Equals(next, "@pop", StringComparison.Ordinal) || string.Equals(next, "pop", StringComparison.Ordinal))
            {
                if (stack.Count > 1) stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                if (next.Length > 0 && next[0] == '@')
                    next = next[1..];
                if (stack.Count == 0) stack.Add("root");
                stack.Add(next);
            }
        }
        nextIndex = index + m.Length;
        return true;
    }

    private static List<string> DecodeState(string state)
    {
        if (string.IsNullOrEmpty(state)) return ["root"];
        if (!state.Contains('|', StringComparison.Ordinal))
            return [state];
        return state.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string EncodeState(List<string> stack)
    {
        if (stack.Count == 0) return "root";
        if (stack.Count == 1) return stack[0];
        return string.Join('|', stack);
    }

    private string Qualify(string token)
    {
        if (string.IsNullOrEmpty(_tokenPostfix)) return token;
        if (token.Contains('.', StringComparison.Ordinal)) return token;
        return token + _tokenPostfix;
    }

    private static List<TokenSpan> Merge(List<TokenSpan> tokens)
    {
        if (tokens.Count <= 1) return tokens;
        var result = new List<TokenSpan>(tokens.Count);
        var current = tokens[0];
        for (var i = 1; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Type == current.Type && t.Start == current.Start + current.Length)
                current = new TokenSpan(current.Start, current.Length + t.Length, current.Type);
            else
            {
                result.Add(current);
                current = t;
            }
        }
        result.Add(current);
        return result;
    }

    private static MonarchRule ParseRule(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            // { include: '@whitespace' } style sometimes appears as object
            if (el.TryGetProperty("include", out var inc))
                return new MonarchRule { Include = inc.GetString() };
        }

        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() == 0)
            return new MonarchRule();

        var arr = el.EnumerateArray().ToArray();
        var first = arr[0].GetString() ?? "";
        if (first.StartsWith("@", StringComparison.Ordinal) && arr.Length == 1)
            return new MonarchRule { Include = first };

        var pattern = first;
        // monarch uses /pattern/ or raw pattern; strip / /
        if (pattern.Length >= 2 && pattern[0] == '/' && pattern.LastIndexOf('/') > 0)
        {
            var last = pattern.LastIndexOf('/');
            pattern = pattern[1..last];
        }

        string? token = null;
        string? next = null;
        var casesKeyword = false;
        string? keywordToken = null;

        if (arr.Length >= 2)
        {
            if (arr[1].ValueKind == JsonValueKind.String)
                token = arr[1].GetString();
            else if (arr[1].ValueKind == JsonValueKind.Object)
            {
                var obj = arr[1];
                if (obj.TryGetProperty("token", out var t)) token = t.GetString();
                if (obj.TryGetProperty("next", out var n)) next = n.GetString();
                if (obj.TryGetProperty("cases", out var cases) && cases.ValueKind == JsonValueKind.Object)
                {
                    casesKeyword = true;
                    if (cases.TryGetProperty("@keywords", out var k))
                        keywordToken = k.GetString() ?? "keyword";
                    else if (cases.TryGetProperty("@default", out var d))
                        token = d.GetString() ?? token;
                }
            }
        }

        if (arr.Length >= 3 && arr[2].ValueKind == JsonValueKind.String)
            next = arr[2].GetString();

        Regex? regex = null;
        try
        {
            // Match against a suffix of the line with ^ so startat/\G issues are avoided.
            regex = new Regex("^" + pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }
        catch
        {
            regex = null;
        }

        return new MonarchRule
        {
            Regex = regex,
            Token = token,
            Next = next,
            CasesKeyword = casesKeyword,
            KeywordToken = keywordToken,
        };
    }

    private sealed class MonarchRule
    {
        public string? Include { get; init; }
        public Regex? Regex { get; init; }
        public string? Token { get; init; }
        public string? Next { get; init; }
        public bool CasesKeyword { get; init; }
        public string? KeywordToken { get; init; }
    }
}

/// <summary>按文档缓存行 token 与状态。</summary>
internal sealed class TokenizationCache
{
    private readonly ITokenizer _tokenizer;
    private readonly List<string> _states = ["root"]; // state at start of line i
    private readonly List<IReadOnlyList<TokenSpan>?> _lines = [];
    private int _validUntil;

    public TokenizationCache(ITokenizer tokenizer) => _tokenizer = tokenizer;

    public void InvalidateFromLine(int line)
    {
        _validUntil = Math.Clamp(line, 0, _validUntil);
    }

    public void Reset()
    {
        _states.Clear();
        _states.Add("root");
        _lines.Clear();
        _validUntil = 0;
    }

    public IReadOnlyList<TokenSpan> GetLineTokens(ICodePadTextModel model, int line)
    {
        EnsureLine(model, line);
        return _lines[line] ?? [];
    }

    private void EnsureLine(ICodePadTextModel model, int line)
    {
        while (_validUntil <= line && _validUntil < model.LineCount)
        {
            var state = _validUntil < _states.Count ? _states[_validUntil] : "root";
            var content = model.GetLineContent(_validUntil);
            var tokens = _tokenizer.TokenizeLine(content, ref state);
            while (_lines.Count <= _validUntil) _lines.Add(null);
            _lines[_validUntil] = tokens;
            while (_states.Count <= _validUntil + 1) _states.Add("root");
            _states[_validUntil + 1] = state;
            _validUntil++;
        }
    }
}
