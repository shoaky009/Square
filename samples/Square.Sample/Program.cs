using Square.Backends.Vulkan;
using Square.Graphics.Codecs;
using Square.Hosting;
using Square.Platform;
using Square.Tooling;
using Square.UI;

namespace Square.Sample;

public static class Program
{
    public static void Main(string[] args)
    {
        System.Console.WriteLine("Square Framework - M1 Window Demo");

        var document = new UIDocument
        {
            Title = "Square Framework"
        };
        document.Body.Children.Add(new Main());

        var backend = GetOption(args, "--backend") ?? Environment.GetEnvironmentVariable("SQUARE_RENDER_BACKEND") ?? "Software";
        if (string.Equals(backend, "Vulkan", StringComparison.OrdinalIgnoreCase))
            VulkanRegistration.Register();

        var app = new DesktopApplication(document, new PlatformHostCreateInfo
        {
            Title = document.Title,
            Width = 900,
            Height = 980,
            RenderBackend = backend
        });
        ConfigureRendering(app, args);
        ConfigureDebugOverlayToggle(app, document);
        SampleSignals.Initialize(app.Dispatcher);
        var screenshot = GetOption(args, "--screenshot");
        if (!string.IsNullOrWhiteSpace(screenshot)) ScheduleScreenshot(app, screenshot);

        ToolingServer? tooling = null;
        if (HasOption(args, "--tooling"))
            tooling = StartTooling(app, args);

        try
        {
            app.Run();
        }
        finally
        {
            tooling?.Dispose();
        }

        System.Console.WriteLine("Window closed. Demo complete.");
    }

    private static ToolingServer StartTooling(DesktopApplication app, string[] args)
    {
        var port = int.TryParse(GetOption(args, "--tooling-port"), out var parsedPort) ? parsedPort : 0;
        var token = GetOption(args, "--tooling-token");
        var tooling = ToolingServer.Start(app, new ToolingOptions
        {
            Port = port,
            AccessToken = token,
            AllowInputInjection = true,
            AllowInspector = true
        });
        System.Console.WriteLine($"Square Tooling: {tooling.BaseAddress}/api/v1/health");
        System.Console.WriteLine($"Token header: {ToolingServer.TokenHeader}: {tooling.AccessToken}");
        return tooling;
    }

    private static void ScheduleScreenshot(DesktopApplication app, string path)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1800);
            try
            {
                using var bitmap = await app.CaptureRendererBitmapAsync();
                BitmapPngEncoder.Save(bitmap, path);
                System.Console.WriteLine($"Screenshot saved to {path}");
            }
            catch (Exception exception)
            {
                System.Console.Error.WriteLine($"Screenshot failed: {exception}");
                Environment.ExitCode = 1;
            }
            finally
            {
                app.Close();
            }
        });
    }

    private static void ConfigureRendering(DesktopApplication app, string[] args)
    {
        var mode = GetOption(args, "--render-mode") ?? Environment.GetEnvironmentVariable("SQUARE_RENDER_MODE");
        if (Enum.TryParse<RenderMode>(mode, ignoreCase: true, out var renderMode))
            app.RenderingMode = renderMode;

        var overlay = GetOption(args, "--render-overlay") ?? Environment.GetEnvironmentVariable("SQUARE_RENDER_OVERLAY");
        if (TryParseBool(overlay, out var showOverlay))
            app.ShowRenderDiagnosticsOverlay = showOverlay;

        var dirtyOverlay = GetOption(args, "--dirty-overlay") ?? Environment.GetEnvironmentVariable("SQUARE_DIRTY_OVERLAY");
        if (TryParseBool(dirtyOverlay, out var showDirtyOverlay))
            app.ShowDirtyUnionOverlay = showDirtyOverlay;

        var maxDirtyArea = GetOption(args, "--max-dirty-area") ?? Environment.GetEnvironmentVariable("SQUARE_MAX_DIRTY_AREA");
        if (float.TryParse(maxDirtyArea, out var areaRatio))
            app.MaxDirtyAreaRatio = Math.Clamp(areaRatio, 0f, 1f);

        var maxDirtyRects = GetOption(args, "--max-dirty-rects") ?? Environment.GetEnvironmentVariable("SQUARE_MAX_DIRTY_RECTS");
        if (int.TryParse(maxDirtyRects, out var rectCount))
            app.MaxDirtyRectCount = Math.Max(1, rectCount);

        System.Console.WriteLine($"Render: mode={app.RenderingMode}, overlay={app.ShowRenderDiagnosticsOverlay}, dirtyOverlay={app.ShowDirtyUnionOverlay}, maxDirtyArea={app.MaxDirtyAreaRatio:0.##}, maxDirtyRects={app.MaxDirtyRectCount}");
    }

    private static void ConfigureDebugOverlayToggle(DesktopApplication app, UIDocument document)
    {
#if DEBUG
        const int f12 = 0x7B;
        const string baseTitle = "Square Framework";

        UpdateDebugTitle(document, app.ShowRenderDiagnosticsOverlay);
        app.GlobalKeyEvent += (keyCode, action) =>
        {
            if (action != KeyAction.Down || keyCode != f12) return;

            app.ShowRenderDiagnosticsOverlay = !app.ShowRenderDiagnosticsOverlay;
            UpdateDebugTitle(document, app.ShowRenderDiagnosticsOverlay);
            app.RequestRender();
        };

        static void UpdateDebugTitle(UIDocument document, bool overlayVisible)
        {
            document.Title = $"{baseTitle} - Overlay: {(overlayVisible ? "On" : "Off")}";
        }
#endif
    }

    private static bool HasOption(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (bool.TryParse(value, out result)) return true;
        if (value == "1" || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }
        if (value == "0" || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }
        return false;
    }

}
