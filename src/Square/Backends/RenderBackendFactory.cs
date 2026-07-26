using Square.Graphics;

namespace Square.Backends;

/// <summary>Software CPU 光栅后端工厂。</summary>
public sealed class RenderBackendFactory : IRenderBackendFactory
{
    /// <summary>后端名称。</summary>
    public string Name => "Software";

    /// <summary>创建软件渲染上下文。</summary>
    public IRenderContext CreateContext(RenderContextCreateInfo info)
    {
        var width = (int)Math.Ceiling(info.CanvasSize.Width * info.DpiScale);
        var height = (int)Math.Ceiling(info.CanvasSize.Height * info.DpiScale);
        var surface = info.SoftwareSurface ?? new BitmapSoftwareRenderSurface(
            Math.Max(1, width), Math.Max(1, height), info.PresentFrame);
        try
        {
            if (surface.Width != Math.Max(1, width) || surface.Height != Math.Max(1, height))
                surface.Resize(Math.Max(1, width), Math.Max(1, height));
            return new RenderContext(surface, info.CanvasSize, info.DpiScale);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }
}