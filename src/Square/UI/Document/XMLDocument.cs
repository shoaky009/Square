namespace Square.UI;

/// <summary>XML document base class, aligned with the browser <c>XMLDocument</c> interface.</summary>
public abstract class XMLDocument : Document
{
    /// <summary>构造并指定内容类型。</summary>
    protected XMLDocument(string contentType = "application/xml") => ContentType = contentType;

    /// <summary>文档内容类型（对齐 <c>contentType</c>）。</summary>
    public string ContentType { get; }
    /// <summary>XML 版本（对齐 <c>xmlVersion</c>）。</summary>
    public string XmlVersion { get; set; } = "1.0";
    /// <summary>XML 编码（对齐 <c>xmlEncoding</c>）。</summary>
    public string XmlEncoding { get; set; } = "UTF-8";
    /// <summary>是否独立声明（对齐 <c>xmlStandalone</c>）。</summary>
    public bool XmlStandalone { get; set; }
}
