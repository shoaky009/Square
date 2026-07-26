using Square.Graphics;

namespace Square.UI;

/// <summary>可被文本选择命中的元素接口（Square 扩展）。</summary>
public interface ITextSelectable
{
    /// <summary>可选中的文本内容。</summary>
    string SelectableText { get; }
    /// <summary>可选中文本的边界矩形。</summary>
    Rect SelectableTextBounds { get; }
}