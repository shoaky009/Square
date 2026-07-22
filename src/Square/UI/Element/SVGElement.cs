namespace Square.UI;

/// <summary>
/// SVG 元素占位基类（对齐 DOM <c>SVGElement</c>）。
/// 本阶段不实现矢量几何与 viewBox；绘制仍使用 UI 控件或后续 SVG 管线。
/// </summary>
public abstract class SVGElement : Element
{
    /// <inheritdoc />
    public override string? NamespaceURI => "http://www.w3.org/2000/svg";
}
