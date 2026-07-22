namespace Square.UI;

/// <summary>XML document base class, aligned with the browser <c>XMLDocument</c> interface.</summary>
public abstract class XMLDocument : Document
{
    protected XMLDocument(string contentType = "application/xml") => ContentType = contentType;

    public string ContentType { get; }
    public string XmlVersion { get; set; } = "1.0";
    public string XmlEncoding { get; set; } = "UTF-8";
    public bool XmlStandalone { get; set; }
}
