using Square.Extensions.Registration;
using Square.Hosting;
using Square.Platform;
using Square.Tooling;
using Square.UI;

namespace Square.Sample.RichText;

public static class Program
{
    public static void Main(string[] args)
    {
        ExtensionRegistration.RegisterDefaults();

        var document = new UIDocument
        {
            Title = "Square RichText Editor"
        };
        document.Body.Children.Add(new Main());

        var app = new DesktopApplication(document, new PlatformHostCreateInfo
        {
            Title = document.Title,
            Width = 1100,
            Height = 760
        })
        {
            RenderingMode = RenderMode.DirtyRegion
        };

        var tooling = app.UseToolingServer(new ToolingOptions
        {
            Port = 0,
            AccessToken = "square-richtext-demo",
            AllowInputInjection = true
        });
        System.Console.WriteLine($"Square Tooling: {tooling.BaseAddress}/api/v1/health");
        System.Console.WriteLine($"Token header: {ToolingServer.TokenHeader}: {tooling.AccessToken}");
        app.Run();
    }
}
