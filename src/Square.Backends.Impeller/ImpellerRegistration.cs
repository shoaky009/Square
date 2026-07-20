using Square.Graphics;

namespace Square.Backends.Impeller;

public static class ImpellerRegistration
{
    public static void Register(string? libraryPath = null)
        => RenderBackendRegistry.Register(new ImpellerBackendFactory(libraryPath));
}
