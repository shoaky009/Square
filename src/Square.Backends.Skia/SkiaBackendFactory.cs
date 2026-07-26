using Square.Graphics;

namespace Square.Backends.Skia;

public sealed class SkiaBackendFactory : IRenderBackendFactory
{
    public string Name => "Skia";

    public IRenderContext CreateContext(RenderContextCreateInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        TextMetrics.RegisterProvider(SkiaRegistration.TextMetricsProvider);
        return new SkiaRenderContext(info.CanvasSize, info.DpiScale, info.PresentFrame);
    }
}
