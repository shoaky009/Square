using Square.Hosting;

namespace Square.Tooling;

public static class ToolingApplicationExtensions
{
    public static ToolingServer UseToolingServer(this DesktopApplication application, ToolingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        var server = ToolingServer.Start(application, options);
        application.Exited += server.Dispose;
        return server;
    }
}
