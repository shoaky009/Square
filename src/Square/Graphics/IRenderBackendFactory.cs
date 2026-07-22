namespace Square.Graphics;

public interface IRenderBackendFactory
{
    string Name { get; }
    IRenderContext CreateContext(RenderContextCreateInfo info);
}
