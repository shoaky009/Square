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
        ArgumentNullException.ThrowIfNull(info);
        var dpiScale = float.IsFinite(info.DpiScale) && info.DpiScale > 0 ? info.DpiScale : 1f;
        var width = (int)Math.Ceiling(info.CanvasSize.Width * dpiScale);
        var height = (int)Math.Ceiling(info.CanvasSize.Height * dpiScale);
        var surface = info.SoftwareSurface ?? new BitmapSoftwareRenderSurface(
            Math.Max(1, width), Math.Max(1, height), info.PresentFrame);
        try
        {
            if (surface.Width != Math.Max(1, width) || surface.Height != Math.Max(1, height))
                surface.Resize(Math.Max(1, width), Math.Max(1, height));
            return new RenderContext(surface, info.CanvasSize, dpiScale);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }
}
