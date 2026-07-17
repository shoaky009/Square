using Square.Hosting;
using Square.Platform;

namespace Square.Sample;

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine("Square Framework - M1 Window Demo");

        var app = new DesktopApplication(new Main(), new PlatformHostCreateInfo
        {
            Title = "Square Framework",
            Width = 900,
            Height = 980
        });
        SampleSignals.Initialize(app.Dispatcher);
        app.Run();

        System.Console.WriteLine("Window closed. Demo complete.");
    }
}
