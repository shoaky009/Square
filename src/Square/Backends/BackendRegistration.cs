using Square.Graphics;

namespace Square.Backends;

public static class BackendRegistration
{
    public static void RegisterDefaults()
    {
#if BACKEND_SOFTWARE
        RenderBackendRegistry.Register(new RenderBackendFactory());
        RenderBackendRegistry.SetDefault("Software");
#endif
    }
}
