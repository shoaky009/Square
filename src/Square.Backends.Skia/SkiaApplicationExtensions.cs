using Square.Graphics;

namespace Square.Backends.Skia;

public static class SkiaApplicationExtensions
{
    public static T UseSkiaBackend<T>(this T window)
        where T : IRenderBackendApplication
    {
        ArgumentNullException.ThrowIfNull(window);
        SkiaRegistration.Register();
        window.RenderBackend = "Skia";
        return window;
    }
}
