using Square.Graphics;

namespace Square.Backends;

public sealed class RenderBackendFactory : IRenderBackendFactory
{
    public string Name => "Software";

    public IRenderContext CreateContext(RenderContextCreateInfo info)
    {
        var width = (int)Math.Ceiling(info.CanvasSize.Width * info.DpiScale);
        var height = (int)Math.Ceiling(info.CanvasSize.Height * info.DpiScale);
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        return new RenderContext(bitmap, info.CanvasSize, info.DpiScale, info.PresentFrame);
    }
}
