using Square.Backends.Impeller;
using Square.Graphics;
using Square.Graphics.Codecs;
using Square.Hosting;
using Square.Platform;
using Square.UI;

namespace Square.Sample.Impeller;

public static class Program
{
    public static void Main(string[] args)
    {
        var libraryPath = GetOption(args, "--library")
            ?? Environment.GetEnvironmentVariable("SQUARE_IMPELLER_LIBRARY");
        var screenshotPath = GetOption(args, "--screenshot");

        ImpellerRegistration.Register(libraryPath);

        var document = new UIDocument { Title = "Square Impeller Vulkan Smoke" };
        document.Body.Children.Add(new ImpellerSmokeCanvas());

        var app = new DesktopApplication(document, new PlatformHostCreateInfo
        {
            Title = document.Title,
            Width = 800,
            Height = 600,
            RenderBackend = "Impeller"
        })
        {
            Background = Color.FromRgb(15, 23, 42)
        };

        if (!string.IsNullOrWhiteSpace(screenshotPath))
            ScheduleScreenshot(app, screenshotPath);

        app.Run();
    }

    private static void ScheduleScreenshot(DesktopApplication app, string path)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            try
            {
                using var bitmap = await app.CaptureRendererBitmapAsync();
                BitmapPngEncoder.Save(bitmap, path);
                Console.WriteLine($"Impeller screenshot saved to {path}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Impeller screenshot failed: {exception}");
                Environment.ExitCode = 1;
            }
            finally
            {
                app.Close();
            }
        });
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

internal sealed class ImpellerSmokeCanvas : UIElement
{
    private readonly Bitmap _checkerboard = CreateCheckerboard();

    public override Size Measure(Size availableSize) => availableSize;

    public override void Paint(IRenderContext context)
    {
        context.FillRect(
            new Rect(48, 52, 300, 180),
            new LinearGradientBrush(
                new Point(48, 52),
                new Point(348, 232),
                new GradientStop(0, Color.FromRgb(37, 99, 235)),
                new GradientStop(1, Color.FromRgb(6, 182, 212))));
        context.DrawRect(new Rect(48, 52, 300, 180), Pen.FromColor(Color.FromRgb(147, 197, 253), 4));

        context.FillGeometry(
            new RoundedRectGeometry(new Rect(390, 52, 320, 180), 28, 28),
            new RadialGradientBrush(
                new Point(550, 142),
                190,
                new GradientStop(0, Color.FromRgb(192, 132, 252)),
                new GradientStop(1, Color.FromRgb(91, 33, 182))));
        context.DrawGeometry(
            new RoundedRectGeometry(new Rect(390, 52, 320, 180), 28, 28),
            Pen.FromColor(Color.FromRgb(216, 180, 254), 4));

        context.FillGeometry(
            new EllipseGeometry(new Point(205, 385), 118, 92),
            new SolidColorBrush(Color.FromRgb(5, 150, 105)));
        context.DrawGeometry(
            new EllipseGeometry(new Point(205, 385), 118, 92),
            Pen.FromColor(Color.FromRgb(110, 231, 183), 5));

        context.PushLayer(new Rect(390, 292, 320, 186), 0.72f);
        context.FillRect(new Rect(390, 292, 320, 186), new SolidColorBrush(Color.FromRgb(234, 88, 12)));
        context.FillGeometry(
            new EllipseGeometry(new Point(550, 385), 104, 70),
            new SolidColorBrush(Color.FromRgb(250, 204, 21)));
        context.PopLayer();

        var path = PathGeometry.Create()
            .MoveTo(new Point(88, 520))
            .LineTo(new Point(220, 492))
            .LineTo(new Point(342, 548))
            .LineTo(new Point(170, 566))
            .Close();
        context.FillPath(path, new SolidColorBrush(Color.FromRgb(236, 72, 153)));
        context.DrawPath(
            path,
            new Pen(new SolidColorBrush(Color.FromRgb(251, 207, 232)), 4, new StrokeStyle
            {
                Cap = LineCap.Round,
                Join = LineJoin.Bevel,
                MiterLimit = 5
            }));

        context.DrawImage(_checkerboard, new Rect(430, 500, 240, 72));

        context.PushClip(new RoundedRectGeometry(new Rect(24, 12, 110, 30), 14, 14));
        context.FillRect(new Rect(12, 4, 140, 46), new SolidColorBrush(Color.FromRgb(244, 63, 94)));
        context.PopClip();

        context.PushClip(new EllipseGeometry(new Point(184, 27), 54, 15));
        context.FillRect(new Rect(120, 4, 128, 46), new SolidColorBrush(Color.FromRgb(34, 197, 94)));
        context.PopClip();

        var clipPath = PathGeometry.Create()
            .MoveTo(new Point(266, 42))
            .LineTo(new Point(304, 8))
            .LineTo(new Point(342, 42))
            .Close();
        context.PushClip(clipPath);
        context.FillRect(new Rect(258, 4, 92, 44), new SolidColorBrush(Color.FromRgb(250, 204, 21)));
        context.PopClip();

        context.DrawText(
            new TextLayout("Impeller GPU", new Font("Segoe UI", 30, FontWeight.Bold)),
            new Point(82, 252),
            new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        context.DrawText(
            new TextLayout("Vulkan display lists, paths, textures · 中文渲染", new Font("Segoe UI", 17))
            {
                MaxSize = new Size(620, 60),
                LineHeight = 1.25f
            },
            new Point(82, 282),
            new SolidColorBrush(Color.FromRgb(148, 163, 184)));
    }

    private static Bitmap CreateCheckerboard()
    {
        var bitmap = new Bitmap(64, 32);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var bright = ((x / 8) + (y / 8)) % 2 == 0;
                var pixel = bitmap.GetPixel(x, y);
                pixel[0] = bright ? (byte)32 : (byte)180;
                pixel[1] = bright ? (byte)211 : (byte)83;
                pixel[2] = bright ? (byte)250 : (byte)14;
                pixel[3] = 255;
            }
        }
        return bitmap;
    }
}
