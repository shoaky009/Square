using Square.Graphics;

namespace Square.Backends.Skia;

public static class SkiaRegistration
{
    internal static SkiaTextMetricsProvider TextMetricsProvider { get; } = new();

    public static void Register()
    {
        TextMetrics.RegisterProvider(TextMetricsProvider);
        RenderBackendRegistry.Register(new SkiaBackendFactory());
    }
}
