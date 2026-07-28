using Square.Graphics;

namespace Square.Extensions.CodeEditor;

/// <summary>
/// 行级装饰（VS Code gutter decoration 子集）：断点图标、git diff 色条、行背景等。
/// </summary>
public sealed class CodeEditorLineDecoration
{
    /// <summary>稳定标识；同 Id 再次写入会替换。</summary>
    public required string Id { get; init; }

    /// <summary>0-based 文档行号。</summary>
    public int Line { get; init; }

    /// <summary>glyph margin 中绘制的符号，如 "●" / "◆"。</summary>
    public string? Glyph { get; init; }

    /// <summary>glyph 颜色；未指定时用主题行号色。</summary>
    public Color? GlyphColor { get; init; }

    /// <summary>glyph margin 左侧细色条（git added/modified/deleted 风格）。</summary>
    public Color? GutterColor { get; init; }

    /// <summary>文本区整行背景（可选）。</summary>
    public Color? LineBackground { get; init; }

    /// <summary>overview ruler 上的标记色；未指定时回退 GutterColor / GlyphColor。</summary>
    public Color? OverviewRulerColor { get; init; }
}

/// <summary>gutter 点击信息。</summary>
public sealed class CodeEditorGutterClickEventArgs : EventArgs
{
    /// <summary>初始化。</summary>
    public CodeEditorGutterClickEventArgs(int line, Point point, CodeEditorGutterLane lane)
    {
        Line = line;
        Point = point;
        Lane = lane;
    }

    /// <summary>0-based 文档行。</summary>
    public int Line { get; }

    /// <summary>点击坐标（控件局部）。</summary>
    public Point Point { get; }

    /// <summary>命中的 gutter 列。</summary>
    public CodeEditorGutterLane Lane { get; }
}

/// <summary>左侧 gutter 列。</summary>
public enum CodeEditorGutterLane
{
    /// <summary>断点/书签/自定义图标列。</summary>
    Glyph,
    /// <summary>行号列。</summary>
    LineNumbers,
    /// <summary>折叠列。</summary>
    Folding,
}
