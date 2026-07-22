using Square.Graphics;
namespace Square.Platform;

public static class PlatformRegistry
{
    private static IPlatformFactory? _factory;
    private static IPlatformFactory? _defaultFactory;

    public static void Register(IPlatformFactory factory) => _factory = factory;

    public static void RegisterDefault(IPlatformFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Interlocked.CompareExchange(ref _defaultFactory, factory, null);
    }

    public static bool TryGet(out IPlatformFactory? factory)
    {
        factory = _factory ?? _defaultFactory;
        return factory is not null;
    }

    public static IPlatformFactory Get() =>
        _factory ?? _defaultFactory ?? throw new InvalidOperationException(
            "No platform factory registered. Reference Square.Platform.Win32 or Square.Platform.X11.");
}
