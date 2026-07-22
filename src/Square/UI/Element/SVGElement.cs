namespace Square.UI.Svg;

/// <summary>
/// SVG DOM 元素基类（对齐浏览器 <c>SVGElement</c>）。
/// </summary>
public abstract class SVGElement : Element
{
    /// <inheritdoc />
    public override string? NamespaceURI => "http://www.w3.org/2000/svg";

    public override void InvalidatePaint()
    {
        base.InvalidatePaint();
        var root = OwnerSVGElement;
        if (root != null && !ReferenceEquals(root, this)) root.InvalidatePaint();
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        OwnerSVGElement?.InvalidatePaint();
    }

    internal SVGSVGElement? OwnerSVGElement
    {
        get
        {
            for (Element? current = this; current != null; current = current.Parent)
                if (current is SVGSVGElement svg) return svg;
            return OwnerDocument?.DocumentElement as SVGSVGElement;
        }
    }
}
