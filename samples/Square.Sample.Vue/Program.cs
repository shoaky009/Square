using Square.Hosting;
using Square.Images;
using Square.Platform;
using Square.Sample.Vue.Components;
using Square.UI;
using Square.Backends.Vulkan;
namespace Square.Sample.Vue;

public static class Program
{
    public static void Main(string[] args)
    {
        System.Console.WriteLine("Square Vue Template Sample");
        ImageSourceRegistration.RegisterDefaults();
        var window = new AppWindow("Square Vue Template Sample", 900, 980);
        //window.UseVulkanBackend();
        window.RenderingMode = RenderMode.DirtyRegion;
        window.Load(new Main());
        var app = new DesktopApplication(window);
        ConfigureRendering(window, args);
        SampleSignals.Initialize(app.Dispatcher);
        app.Run();
    }

    private static void ConfigureRendering(AppWindow window, string[] args)
    {
        var mode = GetOption(args, "--render-mode") ?? Environment.GetEnvironmentVariable("SQUARE_RENDER_MODE");
        if (Enum.TryParse<RenderMode>(mode, ignoreCase: true, out var renderMode))
            window.RenderingMode = renderMode;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

}
