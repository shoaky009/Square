using Square.Graphics;
using Square.Runtime;

namespace Square.Platform;

public static class PlatformRegistry
{
    private static IPlatformFactory? _factory;

    public static void Register(IPlatformFactory factory) => _factory = factory;

    public static IPlatformFactory Get() =>
        _factory ?? throw new InvalidOperationException("No platform factory registered");
}