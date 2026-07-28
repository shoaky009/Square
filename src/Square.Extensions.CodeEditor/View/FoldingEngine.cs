using System.Text.RegularExpressions;

namespace Square.Extensions.CodeEditor;

/// <summary>可折叠区间（文档行号，含首尾；折叠后隐藏 StartLine+1..EndLine）。</summary>
public readonly record struct FoldRegion(int StartLine, int EndLine)
{
    /// <summary>被隐藏的行数。</summary>
    public int HiddenLineCount => Math.Max(0, EndLine - StartLine);

    /// <summary>折叠占位符替换头行内容的起始列。</summary>
    public int StartColumn { get; init; } = -1;

    /// <summary>折叠后的显示文本。</summary>
    public string Placeholder { get; init; } = "...";
}

/// <summary>基于括号对与 HTML/XML 标签的折叠区间计算与折叠状态。</summary>
internal sealed class FoldingEngine
{
    private static readonly Regex AnyTag = new(
        @"</?(?<name>[\w:-]+)(?<attrs>\s[^>]*?)?(?<self>/)?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private List<FoldRegion> _regions = [];
    private readonly HashSet<int> _collapsedStarts = [];
    private bool[]? _hidden;
    private int[]? _docToVisual;
    private int[]? _visualToDoc;
    private int _visibleCount = 1;
    private int _version;

    public IReadOnlyList<FoldRegion> Regions => _regions;
    public int VisibleLineCount => Math.Max(1, _visibleCount);
    public int Version => _version;

    public bool IsCollapsed(int startLine) => _collapsedStarts.Contains(startLine);

    public bool CanFoldAt(int line) =>
        _regions.Any(r => r.StartLine == line && r.HiddenLineCount > 0);

    public FoldRegion? GetRegionStartingAt(int line)
    {
        foreach (var r in _regions)
        {
            if (r.StartLine == line && r.HiddenLineCount > 0)
                return r;
        }
        return null;
    }

    public bool IsLineHidden(int documentLine)
    {
        EnsureMaps();
        return documentLine >= 0 && documentLine < _hidden!.Length && _hidden[documentLine];
    }

    public int DocumentToVisual(int documentLine)
    {
        EnsureMaps();
        if (documentLine < 0) return 0;
        if (documentLine >= _docToVisual!.Length)
            return Math.Max(0, _visibleCount - 1);
        if (_hidden![documentLine])
        {
            // map to nearest previous visible
            for (var i = documentLine; i >= 0; i--)
            {
                if (!_hidden[i]) return _docToVisual[i];
            }
            return 0;
        }
        return _docToVisual[documentLine];
    }

    public int VisualToDocument(int visualLine)
    {
        EnsureMaps();
        if (_visualToDoc == null || _visualToDoc.Length == 0) return 0;
        visualLine = Math.Clamp(visualLine, 0, _visualToDoc.Length - 1);
        return _visualToDoc[visualLine];
    }

    public bool ToggleAt(int startLine)
    {
        if (!CanFoldAt(startLine)) return false;
        if (!_collapsedStarts.Add(startLine))
            _collapsedStarts.Remove(startLine);
        RebuildMaps();
        _version++;
        return true;
    }

    public void ExpandAll()
    {
        if (_collapsedStarts.Count == 0) return;
        _collapsedStarts.Clear();
        RebuildMaps();
        _version++;
    }

    public void CollapseAll()
    {
        _collapsedStarts.Clear();
        foreach (var r in _regions)
        {
            if (r.HiddenLineCount > 0)
                _collapsedStarts.Add(r.StartLine);
        }
        RebuildMaps();
        _version++;
    }

    public void Recompute(ICodeEditorTextModel model, LanguageConfiguration config, string languageId)
    {
        var kept = new HashSet<int>(_collapsedStarts);
        _regions = ComputeRegions(model, config, languageId);
        // drop collapsed starts that no longer exist
        _collapsedStarts.Clear();
        foreach (var r in _regions)
        {
            if (r.HiddenLineCount > 0 && kept.Contains(r.StartLine))
                _collapsedStarts.Add(r.StartLine);
        }
        RebuildMaps(model.LineCount);
        _version++;
    }

    public void InvalidateMapsOnly(int lineCount)
    {
        RebuildMaps(lineCount);
    }

    private void EnsureMaps()
    {
        if (_hidden == null || _docToVisual == null || _visualToDoc == null)
            RebuildMaps();
    }

    private void RebuildMaps(int? lineCount = null)
    {
        var count = lineCount ?? (_hidden?.Length ?? 1);
        count = Math.Max(1, count);
        _hidden = new bool[count];
        foreach (var start in _collapsedStarts)
        {
            var region = GetRegionStartingAt(start);
            if (region == null) continue;
            for (var line = region.Value.StartLine + 1; line <= region.Value.EndLine && line < count; line++)
                _hidden[line] = true;
        }

        _docToVisual = new int[count];
        var visualList = new List<int>(count);
        var visual = 0;
        for (var line = 0; line < count; line++)
        {
            if (_hidden[line])
            {
                _docToVisual[line] = Math.Max(0, visual - 1);
                continue;
            }
            _docToVisual[line] = visual;
            visualList.Add(line);
            visual++;
        }
        _visualToDoc = visualList.Count == 0 ? [0] : visualList.ToArray();
        _visibleCount = _visualToDoc.Length;
    }

    private static List<FoldRegion> ComputeRegions(
        ICodeEditorTextModel model,
        LanguageConfiguration config,
        string languageId)
    {
        var regions = new List<FoldRegion>();
        var isMarkup = languageId.Equals("html", StringComparison.OrdinalIgnoreCase) ||
                       languageId.Equals("xml", StringComparison.OrdinalIgnoreCase);

        if (isMarkup)
            regions.AddRange(ComputeTagRegions(model));
        else
        {
            regions.AddRange(ComputeBracketRegions(model, config));
            if (languageId.Equals("python", StringComparison.OrdinalIgnoreCase))
                regions.AddRange(ComputeIndentRegions(model));
        }

        // Prefer outer/longer ranges first for nesting display; keep all multi-line.
        regions = regions
            .Where(r => r.EndLine > r.StartLine)
            .GroupBy(r => r.StartLine)
            .Select(g => g.OrderByDescending(r => r.EndLine).First())
            .OrderBy(r => r.StartLine)
            .ThenByDescending(r => r.EndLine)
            .ToList();
        return regions;
    }

    private static List<FoldRegion> ComputeBracketRegions(
        ICodeEditorTextModel model,
        LanguageConfiguration config)
    {
        var pairs = config.Brackets ?? [("{", "}"), ("[", "]"), ("(", ")")];
        // Only use single-char structural brackets for folding.
        var openToClose = new Dictionary<char, char>();
        foreach (var (open, close) in pairs)
        {
            if (open.Length == 1 && close.Length == 1)
                openToClose[open[0]] = close[0];
        }
        if (openToClose.Count == 0) return [];

        var closeSet = new HashSet<char>(openToClose.Values);
        var stack = new Stack<(char Open, int Line, int Column)>();
        var regions = new List<FoldRegion>();

        for (var line = 0; line < model.LineCount; line++)
        {
            var content = model.GetLineContent(line);
            for (var i = 0; i < content.Length; i++)
            {
                var ch = content[i];
                // skip strings roughly: ignore chars inside " ' `
                if (ch is '"' or '\'' or '`')
                {
                    var quote = ch;
                    i++;
                    while (i < content.Length)
                    {
                        if (content[i] == '\\' && i + 1 < content.Length) { i += 2; continue; }
                        if (content[i] == quote) break;
                        i++;
                    }
                    continue;
                }

                if (openToClose.ContainsKey(ch))
                {
                    stack.Push((ch, line, i));
                }
                else if (closeSet.Contains(ch) && stack.Count > 0)
                {
                    var (open, startLine, startColumn) = stack.Pop();
                    if (openToClose.TryGetValue(open, out var expected) && expected == ch && line > startLine)
                    {
                        regions.Add(new FoldRegion(startLine, line)
                        {
                            StartColumn = startColumn,
                            Placeholder = open switch
                            {
                                '{' => "{...}",
                                '[' => "[...]",
                                _ => "...",
                            },
                        });
                    }
                }
            }
        }

        return regions;
    }

    private static List<FoldRegion> ComputeIndentRegions(ICodeEditorTextModel model)
    {
        var regions = new List<FoldRegion>();
        var stack = new Stack<(int Line, int Indent)>();
        var previousContentLine = -1;
        var previousIndent = 0;
        var previousEndsBlock = false;

        for (var line = 0; line < model.LineCount; line++)
        {
            var content = model.GetLineContent(line);
            if (string.IsNullOrWhiteSpace(content)) continue;
            var indent = GetIndentWidth(content);

            while (stack.Count > 0 && indent <= stack.Peek().Indent)
            {
                var block = stack.Pop();
                if (line - 1 > block.Line)
                    regions.Add(new FoldRegion(block.Line, line - 1));
            }

            if (previousContentLine >= 0 && previousEndsBlock && indent > previousIndent)
                stack.Push((previousContentLine, previousIndent));

            previousContentLine = line;
            previousIndent = indent;
            previousEndsBlock = content.TrimEnd().EndsWith(':');
        }

        var endLine = model.LineCount - 1;
        while (stack.Count > 0)
        {
            var block = stack.Pop();
            if (endLine > block.Line)
                regions.Add(new FoldRegion(block.Line, endLine));
        }
        return regions;
    }

    private static int GetIndentWidth(string content)
    {
        var width = 0;
        foreach (var ch in content)
        {
            if (ch == ' ') width++;
            else if (ch == '\t') width += 4;
            else break;
        }
        return width;
    }

    private static List<FoldRegion> ComputeTagRegions(ICodeEditorTextModel model)
    {
        var stack = new Stack<(string Name, int Line, int Column, string Placeholder)>();
        var regions = new List<FoldRegion>();
        var voidTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "area","base","br","col","embed","hr","img","input","link","meta","param","source","track","wbr"
        };

        foreach (Match m in AnyTag.Matches(model.GetValue()))
        {
            var name = m.Groups["name"].Value;
            var isClose = m.Value.StartsWith("</", StringComparison.Ordinal);
            var isSelf = m.Groups["self"].Success;
            var (startLine, _) = model.GetPositionAt(m.Index);
            var (tagEndLine, tagEndColumn) = model.GetPositionAt(m.Index + m.Length - 1);
            if (isClose)
            {
                while (stack.Count > 0)
                {
                    var top = stack.Pop();
                    if (string.Equals(top.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (startLine > top.Line)
                        {
                            regions.Add(new FoldRegion(top.Line, startLine)
                            {
                                StartColumn = top.Column,
                                Placeholder = top.Placeholder,
                            });
                        }
                        break;
                    }
                }
                continue;
            }

            var isMultiLine = tagEndLine > startLine;
            var startColumn = isMultiLine
                ? model.GetLineContent(startLine).Length
                : tagEndColumn;
            var placeholder = isSelf ? " .../>" : " ...>";

            // 多行属性本身即可折叠；若标签还有正文，后续会生成更长的同起始行区域并优先采用。
            if (isMultiLine)
            {
                regions.Add(new FoldRegion(startLine, tagEndLine)
                {
                    StartColumn = startColumn,
                    Placeholder = placeholder,
                });
            }

            if (isSelf || voidTags.Contains(name)) continue;
            stack.Push((name, startLine, startColumn, placeholder));
        }

        return regions;
    }
}
