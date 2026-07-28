using Square.Extensions.CodePad;
using Square.Hosting;
using Square.Sample.CodePadApp.Components;
using Square.Platform;
using Square.DevTools;
using Square.UI;

namespace Square.Sample.CodePadApp;

public static class Program
{
    public static void Main(string[] args)
    {
        CodePadRegistration.RegisterDefaults();

        var window = new AppWindow("Square CodePad", 1200, 800)
        {
            RenderingMode = RenderMode.DirtyRegion
        };
        var page = new Main();
        window.Load(page);
        window.LoadCustomTitleBar(new CodePadTitleBar { Page = page });
        var app = new DesktopApplication(window);

        var devTools = window.UseDevToolsServer(new DevToolsOptions
        {
            Port = 0,
            AllowInputInjection = true,
            AllowInspector = true,
            IncludeTextContent = true
        });
        System.Console.WriteLine($"Square DevTools: {devTools.BaseAddress}/api/v1/health");
        System.Console.WriteLine($"Token header: {DevToolsServer.TokenHeader}: {devTools.AccessToken}");
        try
        {
            var infoPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "opencode",
                "codepad-devtools.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(infoPath)!);
            System.IO.File.WriteAllText(infoPath,
                $"{{\"baseAddress\":\"{devTools.BaseAddress}\",\"token\":\"{devTools.AccessToken}\",\"processId\":{System.Environment.ProcessId}}}");
        }
        catch
        {
            // ignore local debug file failures
        }
        app.Run();
    }
}
