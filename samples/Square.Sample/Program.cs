using Square.Hosting;
using Square.Platform;
using Square.UI;

namespace Square.Sample;

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine("Square Framework - M1 Window Demo");

        var document = new UIDocument
        {
            Title = "Square Framework"
        };
        document.Body.Children.Add(new Main());

        var app = new DesktopApplication(document, new PlatformHostCreateInfo
        {
            Title = document.Title,
            Width = 900,
            Height = 980
        });
        SampleSignals.Initialize(app.Dispatcher);
        app.Run();

        System.Console.WriteLine("Window closed. Demo complete.");
    }
}
