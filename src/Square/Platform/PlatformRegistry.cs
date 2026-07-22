using Square.Graphics;
namespace Square.Platform;

public static class PlatformRegistry
{
    private static IPlatformFactory? _factory;

    public static void Register(IPlatformFactory factory) => _factory = factory;

    public static bool TryGet(out IPlatformFactory? factory)
    {
        factory = _factory;
        return factory is not null;
    }

    public static IPlatformFactory Get() =>
        _factory ?? throw new InvalidOperationException(
            "No platform factory registered. Reference Square.Platform.Win32 or Square.Platform.X11 and register its platform factory before running the application.");
}
