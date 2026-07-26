namespace Square.Graphics;

/// <summary>渲染后端工厂接口。</summary>
public interface IRenderBackendFactory
{
    /// <summary>后端名称（不区分大小写）。</summary>
    string Name { get; }
    /// <summary>创建渲染上下文。</summary>
    IRenderContext CreateContext(RenderContextCreateInfo info);
}