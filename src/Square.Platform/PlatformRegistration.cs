using Square.Platform;

namespace Square.Platform;

public static class PlatformRegistration
{
    public static void RegisterDefaults()
    {
#if PLATFORM_WIN32
        PlatformRegistry.Register(new Win32.Win32PlatformFactory());
#endif
    }
}