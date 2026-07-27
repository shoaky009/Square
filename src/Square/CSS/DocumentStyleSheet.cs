using Square.CSS.Ast;

namespace Square.CSS;

/// <summary>文档加载的全局 CSS 样式表及其来源信息。</summary>
public sealed class DocumentStyleSheet
{
    internal DocumentStyleSheet(string? href, string sourceText, CssStyleSheet parsedSheet,
        IReadOnlyList<DocumentStyleSheet> imports)
    {
        Href = href;
        SourceText = sourceText;
        ParsedSheet = parsedSheet;
        Imports = imports;
    }

    /// <summary>样式表的绝对文件路径；内存样式表为 null。</summary>
    public string? Href { get; }

    /// <summary>样式表源文本。</summary>
    public string SourceText { get; }

    /// <summary>解析后的 CSS AST。</summary>
    public CssStyleSheet ParsedSheet { get; }

    /// <summary>该样式表通过 <c>@import</c> 直接导入的样式表。</summary>
    public IReadOnlyList<DocumentStyleSheet> Imports { get; }
}
