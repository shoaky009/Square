namespace Square.UI;

/// <summary>
/// Square 应用文档：固定 <c>UI</c> / <c>Head</c> / <c>Body</c> 壳。
/// <see cref="Document.DocumentElement"/> 为只读的 <c>UI</c> 根；应用内容挂在 <see cref="Body"/> 下。
/// </summary>
public sealed class UIDocument : Document
{
    private static readonly Dictionary<string, Func<Element>> ElementFactories =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>文档根元素 <c>UI</c>（即 documentElement）。</summary>
    public UIRootElement Ui { get; }

    /// <summary>文档头（元数据 / 标题栏扩展点；本阶段高度为 0）。</summary>
    public UIHeadElement Head { get; }

    /// <summary>文档体：窗口客户区内容宿主（对齐 HTML <c>body</c>）。</summary>
    public UIBodyElement Body { get; }

    /// <summary>创建带 UI/Head/Body 壳的空文档。</summary>
    public UIDocument()
    {
        Ui = new UIRootElement();
        Head = new UIHeadElement();
        Body = new UIBodyElement();
        Ui.Children.Add(Head);
        Ui.Children.Add(Body);
        SetDocumentElement(Ui);
    }

    /// <summary>
    /// 注册标签名到工厂（AOT 友好；供 <see cref="CreateElement(string)"/> 使用）。
    /// </summary>
    public static void RegisterElement(string tagName, Func<Element> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        ArgumentNullException.ThrowIfNull(factory);
        ElementFactories[tagName] = factory;
    }

    /// <summary>按标签名创建元素（对齐 <c>document.createElement</c>；须先注册）。</summary>
    public Element CreateElement(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!ElementFactories.TryGetValue(tagName, out var factory))
            throw new InvalidOperationException($"Unknown element tag '{tagName}'. Register it with UIDocument.RegisterElement.");
        var element = factory();
        AssignOwnerDocument(element);
        return element;
    }

    /// <summary>强类型创建元素并设置 OwnerDocument。</summary>
    public T CreateElement<T>() where T : Element, new()
    {
        var element = new T();
        AssignOwnerDocument(element);
        return element;
    }

    /// <summary>构建 Body 下应用内容树（对子节点调用 <see cref="Element.BuildElementTree"/>）。</summary>
    public void Build()
    {
        AssignOwnerDocument(Ui);
        foreach (var child in Body.Children)
            child.BuildElementTree();
    }
}
