using Square.Hosting;

namespace Square.DevTools;

public static class DevToolsApplicationExtensions
{
    public static DevToolsServer UseDevToolsServer(this AppWindow window, DevToolsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        var server = DevToolsServer.Start(window, options);
        window.Closed += server.Dispose;
        return server;
    }
}
