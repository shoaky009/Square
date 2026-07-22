using Square.Graphics;

namespace Square.UI;

/// <summary>文档根元素 <c>UI</c>，类似 HTML <c>html</c>（documentElement）。</summary>
public sealed class UIRootElement : UIElement
{
    /// <inheritdoc />
    public override string TagName => "UI";

    /// <inheritdoc />
    public override void Paint(IRenderContext ctx)
    {
        // 壳透明；内容由 Body 子树绘制
    }
}

/// <summary>文档头 <c>Head</c>：自定义标题栏宿主。</summary>
public sealed class UIHeadElement : UIElement
{
    /// <inheritdoc />
    public override string TagName => "Head";

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Children.Count == 0) return Size.Zero;
        var height = 0f;
        foreach (var child in Children)
            height = Math.Max(height, child.Measure(availableSize).Height);
        return new Size(availableSize.Width, height);
    }

    /// <inheritdoc />
    public override void Paint(IRenderContext ctx) { }
}

/// <summary>文档体 <c>Body</c>：窗口客户区内容宿主，类似 HTML <c>body</c>。</summary>
public sealed class UIBodyElement : UIElement
{
    /// <inheritdoc />
    public override string TagName => "Body";

    /// <inheritdoc />
    public override void Paint(IRenderContext ctx)
    {
        // 子节点经 DisplayTree 绘制；Body 自身为布局容器
    }
}
