using System.Numerics;
using Square.Graphics;
using Square.UI;

namespace Square.Sample;

internal sealed class CircleRegressionPage : UIElement
{
    private static readonly Color Background = Color.FromRgb(247, 249, 252);
    private static readonly Color Blue = Color.FromRgb(26, 115, 232);
    private static readonly Color Red = Color.FromRgb(220, 54, 69);
    private static readonly Color Green = Color.FromRgb(15, 157, 88);
    private static readonly Color Purple = Color.FromRgb(107, 64, 216);

    public override Size Measure(Size availableSize)
        => new(
            float.IsFinite(availableSize.Width) ? availableSize.Width : 760,
            float.IsFinite(availableSize.Height) ? availableSize.Height : 560);

    public override void Paint(IRenderContext context)
    {
        context.FillRect(Geometry, new SolidColorBrush(Background));
        var origin = new Point(Geometry.X + 60, Geometry.Y + 55);

        DrawFilledCircles(context, origin);
        DrawStrokedCircles(context, new Point(origin.X, origin.Y + 170));
        DrawFractionalAndTransformedCircles(context, new Point(origin.X, origin.Y + 340));
    }

    internal static void ValidateScreenshot(Bitmap bitmap)
    {
        if (bitmap.Width < 640 || bitmap.Height < 480)
            throw new InvalidOperationException($"Circle regression capture is unexpectedly small: {bitmap.Width}x{bitmap.Height}.");

        AssertEnoughPixels(bitmap, Blue, 500, "blue filled circles");
        AssertEnoughPixels(bitmap, Red, 300, "red strokes");
        AssertEnoughPixels(bitmap, Green, 250, "green fractional circles");
        AssertEnoughPixels(bitmap, Purple, 150, "purple transformed ellipse");

        var blendedBlue = CountBlendedPixels(bitmap, Blue, Background, tolerance: 18);
        if (blendedBlue < 70)
            throw new InvalidOperationException($"Expected antialiased blue circle edge pixels, found {blendedBlue}.");

        var hardTopPixels = CountNearInRect(bitmap, Blue, tolerance: 10, x0: 134, y0: 75, width: 34, height: 5);
        var blendedTopPixels = CountBlendedInRect(bitmap, Blue, Background, tolerance: 18, x0: 134, y0: 75, width: 34, height: 5);
        if (hardTopPixels > blendedTopPixels)
            throw new InvalidOperationException($"Circle top edge looks too hard: hard={hardTopPixels}, blended={blendedTopPixels}.");
    }

    private static void DrawFilledCircles(IRenderContext context, Point origin)
    {
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 34, origin.Y + 48), 8, 8), new SolidColorBrush(Blue));
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 110, origin.Y + 52), 28, 28), new SolidColorBrush(Blue));
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 220.35f, origin.Y + 52.65f), 36, 24), new SolidColorBrush(Blue));
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 340, origin.Y + 54), 44, 44), new SolidColorBrush(Color.FromRgba(26, 115, 232, 150)));
    }

    private static void DrawStrokedCircles(IRenderContext context, Point origin)
    {
        context.DrawGeometry(new EllipseGeometry(new Point(origin.X + 40, origin.Y + 48), 22, 22), Pen.FromColor(Red, 1));
        context.DrawGeometry(new EllipseGeometry(new Point(origin.X + 130, origin.Y + 48), 34, 24), Pen.FromColor(Red, 3));
        context.DrawGeometry(new EllipseGeometry(new Point(origin.X + 250, origin.Y + 48), 42, 42), Pen.FromColor(Red, 8));
        context.DrawGeometry(new EllipseGeometry(new Point(origin.X + 390.5f, origin.Y + 48.25f), 48, 20), Pen.FromColor(Color.FromRgba(220, 54, 69, 180), 5));
    }

    private static void DrawFractionalAndTransformedCircles(IRenderContext context, Point origin)
    {
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 42.4f, origin.Y + 45.7f), 17.5f, 17.5f), new SolidColorBrush(Green));
        context.DrawGeometry(new EllipseGeometry(new Point(origin.X + 120.5f, origin.Y + 47.25f), 26.5f, 26.5f), Pen.FromColor(Green, 2.5f));

        var pivot = new Vector2(origin.X + 235, origin.Y + 48);
        context.PushTransform(Matrix3x2.CreateRotation(0.23f, pivot));
        context.FillGeometry(new EllipseGeometry(new Point(origin.X + 235, origin.Y + 48), 54, 22), new SolidColorBrush(Purple));
        context.PopTransform();
    }

    private static void AssertEnoughPixels(Bitmap bitmap, Color color, int minimum, string label)
    {
        var count = CountNear(bitmap, color, tolerance: 20);
        if (count < minimum)
            throw new InvalidOperationException($"Expected {label}, found {count} pixels near {color}.");
    }

    private static int CountNear(Bitmap bitmap, Color color, int tolerance)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (IsNear(bitmap.GetPixel(x, y), color, tolerance)) count++;
        }
        return count;
    }

    private static int CountNearInRect(Bitmap bitmap, Color color, int tolerance, int x0, int y0, int width, int height)
    {
        var count = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(bitmap.Height, y0 + height); y++)
        for (var x = Math.Max(0, x0); x < Math.Min(bitmap.Width, x0 + width); x++)
        {
            if (IsNear(bitmap.GetPixel(x, y), color, tolerance)) count++;
        }
        return count;
    }

    private static int CountBlendedPixels(Bitmap bitmap, Color foreground, Color background, int tolerance)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (IsBlend(bitmap.GetPixel(x, y), foreground, background, tolerance)) count++;
        }
        return count;
    }

    private static int CountBlendedInRect(Bitmap bitmap, Color foreground, Color background, int tolerance, int x0, int y0, int width, int height)
    {
        var count = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(bitmap.Height, y0 + height); y++)
        for (var x = Math.Max(0, x0); x < Math.Min(bitmap.Width, x0 + width); x++)
        {
            if (IsBlend(bitmap.GetPixel(x, y), foreground, background, tolerance)) count++;
        }
        return count;
    }

    private static bool IsBlend(ReadOnlySpan<byte> pixel, Color foreground, Color background, int tolerance)
    {
        if (IsNear(pixel, foreground, tolerance) || IsNear(pixel, background, tolerance)) return false;
        return Between(pixel[2], foreground.R, background.R) &&
               Between(pixel[1], foreground.G, background.G) &&
               Between(pixel[0], foreground.B, background.B);
    }

    private static bool IsNear(ReadOnlySpan<byte> pixel, Color color, int tolerance)
        => Math.Abs(pixel[2] - color.R) <= tolerance &&
           Math.Abs(pixel[1] - color.G) <= tolerance &&
           Math.Abs(pixel[0] - color.B) <= tolerance &&
           Math.Abs(pixel[3] - color.A) <= tolerance;

    private static bool Between(byte value, byte a, byte b)
        => value >= Math.Min(a, b) && value <= Math.Max(a, b);
}
