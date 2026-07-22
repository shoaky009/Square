using Square.Hosting;
using Square.Platform;
#if PLATFORM_WIN32
using Square.Platform.Win32;
#elif PLATFORM_X11
using Square.Platform.X11;
#endif
using Square.UI;

namespace Square.Sample.Vue;

public static class Program
{
    public static void Main(string[] args)
    {
        System.Console.WriteLine("Square Vue Template Sample");
#if PLATFORM_WIN32
        PlatformRegistry.Register(new Win32PlatformFactory());
#elif PLATFORM_X11
        PlatformRegistry.Register(new X11PlatformFactory());
#else
        throw new PlatformNotSupportedException("No Square platform package is configured for this build.");
#endif

        var document = new UIDocument
        {
            Title = "Square Vue Template Sample"
        };
        document.Body.Children.Add(new Main());

        var app = new DesktopApplication(document, new PlatformHostCreateInfo
        {
            Title = document.Title,
            Width = 900,
            Height = 980
        });
        ConfigureRendering(app, args);
        SampleSignals.Initialize(app.Dispatcher);
        app.Run();
    }

    private static void ConfigureRendering(DesktopApplication app, string[] args)
    {
        var mode = GetOption(args, "--render-mode") ?? Environment.GetEnvironmentVariable("SQUARE_RENDER_MODE");
        if (Enum.TryParse<RenderMode>(mode, ignoreCase: true, out var renderMode))
            app.RenderingMode = renderMode;
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
