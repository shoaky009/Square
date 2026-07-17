using Square.Platform;

namespace Square.Platform;

public static class PlatformRegistration
{
    public static void RegisterDefaults()
    {
#if PLATFORM_WIN32
        PlatformRegistry.Register(new Win32.Win32PlatformFactory());
#elif PLATFORM_X11
        PlatformRegistry.Register(new X11.X11PlatformFactory());
#endif
    }
}