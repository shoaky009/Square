namespace Square.UI;

/// <summary>
/// HTML 元素占位基类（对齐 DOM <c>HTMLElement</c>）。
/// 本阶段不实现语义标签与完整盒模型；供后续 HTMLDocument 扩展。
/// </summary>
public abstract class HTMLElement : Element
{
    /// <inheritdoc />
    public override string? NamespaceURI => "http://www.w3.org/1999/xhtml";
}
