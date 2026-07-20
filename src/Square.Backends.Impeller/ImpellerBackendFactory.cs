using Square.Graphics;

namespace Square.Backends.Impeller;

public sealed class ImpellerBackendFactory : IRenderBackendFactory
{
    private readonly string? _libraryPath;

    public string Name => "Impeller";

    public ImpellerBackendFactory(string? libraryPath = null)
    {
        _libraryPath = libraryPath;
    }

    public IRenderContext CreateContext(RenderContextCreateInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.NativeTarget == null)
            throw new ImpellerException("Impeller requires a native Vulkan render target.");

        var native = ImpellerNative.Load(_libraryPath);
        try
        {
            return new ImpellerRenderContext(native, info);
        }
        catch
        {
            native.Dispose();
            throw;
        }
    }
}
