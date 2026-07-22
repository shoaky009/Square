namespace Square.UI.Svg;

/// <summary>An SVG document owned by an embedded <see cref="SVGSVGElement"/>.</summary>
public sealed class SVGDocument : XMLDocument
{
    internal SVGDocument(SVGSVGElement root) : base("image/svg+xml") => SetDocumentElement(root);

    public SVGElement CreateElement(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        SVGElement element = tagName.ToLowerInvariant() switch
        {
            "svg" => new SVGSVGElement(),
            "g" => new SVGGElement(),
            "path" => new SVGPathElement(),
            "rect" => new SVGRectElement(),
            "circle" => new SVGCircleElement(),
            "ellipse" => new SVGEllipseElement(),
            "line" => new SVGLineElement(),
            "polyline" => new SVGPolylineElement(),
            "polygon" => new SVGPolygonElement(),
            _ => throw new InvalidOperationException($"Unsupported SVG element '{tagName}'.")
        };
        AssignOwnerDocument(element);
        return element;
    }
}
