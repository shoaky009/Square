using System.Text;
using Square.Graphics;
using Square.Text;

namespace Square.Extensions.CodePad;

/// <summary>视口行：折叠后的文档行，可选 soft-wrap 拆成多行。</summary>
internal readonly record struct CodePadViewRow(
    int DocumentLine,
    int Start,
    int End,
    bool IsFirstOfDocumentLine);

/// <summary>将折叠后的文档行展开为绘制用视口行（支持 soft wrap）。</summary>
internal sealed class CodePadViewLayout
{
    private readonly List<CodePadViewRow> _rows = [];
    private int[] _docLineToFirstRow = [];
    private float _lastMaxWidth = -1;
    private bool _lastWordWrap;
    private int _lastTabSize = -1;
    private int _lastFoldVersion = int.MinValue;
    private int _lastLineCount = -1;
    private int _lastContentVersion = int.MinValue;

    public int RowCount => Math.Max(1, _rows.Count);
    public CodePadViewRow this[int index] => _rows[Math.Clamp(index, 0, Math.Max(0, _rows.Count - 1))];

    public void Ensure(
        ICodePadTextModel model,
        FoldingEngine folding,
        Font font,
        int tabSize,
        float maxWidth,
        bool wordWrap,
        int contentVersion)
    {
        maxWidth = Math.Max(1, maxWidth);
        if (_rows.Count > 0 &&
            _lastWordWrap == wordWrap &&
            Math.Abs(_lastMaxWidth - maxWidth) < 0.5f &&
            _lastTabSize == tabSize &&
            _lastFoldVersion == folding.Version &&
            _lastLineCount == model.LineCount &&
            _lastContentVersion == contentVersion)
            return;

        Rebuild(model, folding, font, tabSize, maxWidth, wordWrap);
        _lastWordWrap = wordWrap;
        _lastMaxWidth = maxWidth;
        _lastTabSize = tabSize;
        _lastFoldVersion = folding.Version;
        _lastLineCount = model.LineCount;
        _lastContentVersion = contentVersion;
    }

    public void Invalidate() => _lastContentVersion = int.MinValue;

    public int DocumentLineToFirstRow(int documentLine)
    {
        if (_docLineToFirstRow.Length == 0) return 0;
        documentLine = Math.Clamp(documentLine, 0, _docLineToFirstRow.Length - 1);
        var row = _docLineToFirstRow[documentLine];
        return row < 0 ? 0 : row;
    }

    public int OffsetToRow(ICodePadTextModel model, int offset)
    {
        if (_rows.Count == 0) return 0;
        var (line, col) = model.GetPositionAt(offset);
        var first = DocumentLineToFirstRow(line);
        // 隐藏行在 _docLineToFirstRow 中为 -1，DocumentLineToFirstRow 会回落为 0；
        // 应映射到折叠头（最近可见文档行）对应的视口行，而不是文档第 0 行。
        if (first == 0 && line > 0 && (_docLineToFirstRow.Length <= line || _docLineToFirstRow[line] < 0))
        {
            for (var doc = line; doc >= 0; doc--)
            {
                if (doc < _docLineToFirstRow.Length && _docLineToFirstRow[doc] >= 0)
                    return _docLineToFirstRow[doc];
            }
            return 0;
        }

        var last = first;
        for (var i = first; i < _rows.Count && _rows[i].DocumentLine == line; i++)
            last = i;

        for (var i = first; i <= last; i++)
        {
            var row = _rows[i];
            var isLast = i == last;
            if (col >= row.Start && (col < row.End || isLast && col <= row.End))
                return i;
        }
        return Math.Clamp(last, 0, _rows.Count - 1);
    }

    private void Rebuild(
        ICodePadTextModel model,
        FoldingEngine folding,
        Font font,
        int tabSize,
        float maxWidth,
        bool wordWrap)
    {
        _rows.Clear();
        _docLineToFirstRow = new int[Math.Max(1, model.LineCount)];
        Array.Fill(_docLineToFirstRow, -1);

        var visible = Math.Max(1, folding.VisibleLineCount);
        for (var v = 0; v < visible; v++)
        {
            var line = folding.VisualToDocument(v);
            if (line < 0 || line >= model.LineCount) continue;
            var content = model.GetLineContent(line);
            if (_docLineToFirstRow[line] < 0)
                _docLineToFirstRow[line] = _rows.Count;

            if (!wordWrap || content.Length == 0)
            {
                _rows.Add(new CodePadViewRow(line, 0, content.Length, true));
                continue;
            }

            var segments = WrapLine(content, font, tabSize, maxWidth);
            for (var s = 0; s < segments.Count; s++)
                _rows.Add(new CodePadViewRow(line, segments[s].Start, segments[s].End, s == 0));
        }

        if (_rows.Count == 0)
            _rows.Add(new CodePadViewRow(0, 0, 0, true));
    }

    internal static List<(int Start, int End)> WrapLine(string line, Font font, int tabSize, float maxWidth)
    {
        var result = new List<(int Start, int End)>();
        if (line.Length == 0)
        {
            result.Add((0, 0));
            return result;
        }

        var start = 0;
        var width = 0f;
        var col = 0;
        var lastBreak = -1;
        var i = 0;
        while (i < line.Length)
        {
            var rune = Decode(line, i, out var len);
            float advance;
            int colAdvance;
            if (rune.Value == '\t')
            {
                colAdvance = tabSize - col % tabSize;
                advance = TextMetrics.GetGlyphMetrics(font, new Rune(' ')).AdvanceX * colAdvance;
            }
            else
            {
                advance = TextMetrics.GetGlyphMetrics(font, rune).AdvanceX;
                colAdvance = rune.IsAscii && rune.Value < 128 ? 1 : 2;
            }

            if (width + advance > maxWidth && i > start)
            {
                var breakAt = lastBreak > start ? lastBreak : i;
                result.Add((start, breakAt));
                start = breakAt;
                width = 0f;
                col = 0;
                lastBreak = -1;
                i = start;
                continue;
            }

            width += advance;
            col += colAdvance;
            if (rune.Value is ' ' or '\t')
                lastBreak = i + len;
            i += len;
        }

        result.Add((start, line.Length));
        return result;
    }

    private static Rune Decode(string text, int index, out int length)
    {
        Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out length);
        return rune;
    }
}
