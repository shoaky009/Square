using Square.Backends;
using Square.Controls.Controls;
using Square.Graphics;
using Square.Platform;
using Square.Runtime;

namespace Square.Sample;

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine("Square Framework - M1 Window Demo");

        BackendRegistration.RegisterDefaults();
        PlatformRegistration.RegisterDefaults();

        // Build generated component from .sqx
        var main = new Main();
        main.BuildVisualTree();
        ((IComponentLifecycle)main).OnAttached();

        // Create Win32 window
        var platform = PlatformRegistry.Get();
        var host = platform.CreateHost(new PlatformHostCreateInfo
        {
            Title = "Square Framework",
            Width = 800,
            Height = 600
        });

        host.Show();
        var ctx = host.CreateRenderContext();

        host.SizeChanged += size =>
        {
            main.Arrange(new Rect(0, 0, size.Width, size.Height));
            ctx.Clear(Color.White);
            main.Render(ctx);
            ctx.Flush();
            ctx.Present();
        };

        host.MouseEvent += (pt, action) =>
        {
            if (action == MouseAction.Down)
            {
                main.RaiseEvent("click");
            }
        };

        // Initial render
        main.Arrange(new Rect(0, 0, host.ClientSize.Width, host.ClientSize.Height));
        ctx.Clear(Color.White);
        main.Render(ctx);
        ctx.Flush();
        ctx.Present();

        // Message loop
        host.PumpEvents();

        System.Console.WriteLine("Window closed. Demo complete.");
    }
}