using Square.Hosting;

namespace Square.DevTools;

public static class DevToolsApplicationExtensions
{
    public static DevToolsServer UseDevToolsServer(this DesktopApplication application, DevToolsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        var server = DevToolsServer.Start(application, options);
        application.Exited += server.Dispose;
        return server;
    }
}
