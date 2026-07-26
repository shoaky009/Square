using Square.Graphics;

namespace Square.Backends;

/// <summary>内置后端注册入口。</summary>
public static class BackendRegistration
{
    /// <summary>注册默认后端（Software，受 BACKEND_SOFTWARE 编译符号控制）。</summary>
    public static void RegisterDefaults()
    {
#if BACKEND_SOFTWARE
        RenderBackendRegistry.Register(new RenderBackendFactory());
        RenderBackendRegistry.SetDefault("Software");
#endif
    }
}