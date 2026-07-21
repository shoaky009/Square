using System.Numerics;
using Square.Graphics;
using Square.UI;

namespace Square.Sample;

internal sealed class VulkanStrokeRegressionPage : UIElement
{
    private static readonly Color Background = Color.FromRgb(246, 248, 251);
    private static readonly Color[] RequiredColors =
    [
        Color.FromRgb(26, 115, 232),
        Color.FromRgb(15, 157, 88),
        Color.FromRgb(220, 54, 69),
        Color.FromRgb(245, 128, 37),
        Color.FromRgb(107, 64, 216),
        Color.FromRgb(0, 137, 123)
    ];

    public override Size Measure(Size availableSize)
        => new(
            float.IsFinite(availableSize.Width) ? availableSize.Width : 900,
            float.IsFinite(availableSize.Height) ? availableSize.Height : 980);

    public override void Paint(IRenderContext context)
    {
        context.FillRect(Geometry, new SolidColorBrush(Background));

        var origin = new Point(Geometry.X + 48, Geometry.Y + 48);
        DrawJoinRow(context, origin);
        DrawCapRow(context, new Point(origin.X, origin.Y + 190));
        DrawDashRow(context, new Point(origin.X, origin.Y + 340));
        DrawClosedPaths(context, new Point(origin.X, origin.Y + 510));
        DrawTransformedLines(context, new Point(origin.X, origin.Y + 720));
    }

    private static void DrawJoinRow(IRenderContext context, Point origin)
    {
        DrawCorner(context, origin, LineJoin.Miter, Color.FromRgb(26, 115, 232), 10);
        DrawCorner(context, new Point(origin.X + 260, origin.Y), LineJoin.Bevel, Color.FromRgb(15, 157, 88), 10);
        DrawCorner(context, new Point(origin.X + 520, origin.Y), LineJoin.Round, Color.FromRgb(220, 54, 69), 10);

        var acute = PathGeometry.Create()
            .MoveTo(new Point(origin.X + 30, origin.Y + 125))
            .LineTo(new Point(origin.X + 120, origin.Y + 70))
            .LineTo(new Point(origin.X + 40, origin.Y + 82));
        context.DrawPath(acute, new Pen(new SolidColorBrush(Color.FromRgb(107, 64, 216)), 8,
            new StrokeStyle { Join = LineJoin.Miter, MiterLimit = 2 }));
    }

    private static void DrawCorner(IRenderContext context, Point origin, LineJoin join, Color color, float width)
    {
        var path = PathGeometry.Create()
            .MoveTo(new Point(origin.X + 15, origin.Y + 110))
            .LineTo(new Point(origin.X + 95, origin.Y + 25))
            .LineTo(new Point(origin.X + 175, origin.Y + 110));
        context.DrawPath(path, new Pen(new SolidColorBrush(color), width,
            new StrokeStyle { Join = join, Cap = LineCap.Butt, MiterLimit = 8 }));
    }

    private static void DrawCapRow(IRenderContext context, Point origin)
    {
        DrawCappedLine(context, origin, LineCap.Butt, Color.FromRgb(26, 115, 232));
        DrawCappedLine(context, new Point(origin.X + 260, origin.Y), LineCap.Square, Color.FromRgb(15, 157, 88));
        DrawCappedLine(context, new Point(origin.X + 520, origin.Y), LineCap.Round, Color.FromRgb(220, 54, 69));
    }

    private static void DrawCappedLine(IRenderContext context, Point origin, LineCap cap, Color color)
    {
        var path = PathGeometry.Create()
            .MoveTo(new Point(origin.X + 25, origin.Y + 55))
            .LineTo(new Point(origin.X + 190, origin.Y + 85));
        context.DrawPath(path, new Pen(new SolidColorBrush(color), 14,
            new StrokeStyle { Cap = cap }));
    }

    private static void DrawDashRow(IRenderContext context, Point origin)
    {
        var zigzag = PathGeometry.Create()
            .MoveTo(new Point(origin.X, origin.Y + 70))
            .LineTo(new Point(origin.X + 140, origin.Y + 10))
            .LineTo(new Point(origin.X + 280, origin.Y + 90))
            .LineTo(new Point(origin.X + 420, origin.Y + 20))
            .LineTo(new Point(origin.X + 760, origin.Y + 70));
        context.DrawPath(zigzag, new Pen(new SolidColorBrush(Color.FromRgb(245, 128, 37)), 7,
            new StrokeStyle
            {
                DashArray = [28, 11, 7, 11],
                DashOffset = 9,
                Cap = LineCap.Round,
                Join = LineJoin.Round
            }));
    }

    private static void DrawClosedPaths(IRenderContext context, Point origin)
    {
        var star = CreateStar(new Point(origin.X + 120, origin.Y + 85), 80, 34, 5);
        context.DrawPath(star, new Pen(new SolidColorBrush(Color.FromRgb(107, 64, 216)), 7,
            new StrokeStyle { Join = LineJoin.Miter, MiterLimit = 3 }));

        var dashedStar = CreateStar(new Point(origin.X + 380, origin.Y + 85), 80, 34, 7);
        context.DrawPath(dashedStar, new Pen(new SolidColorBrush(Color.FromRgb(0, 137, 123)), 6,
            new StrokeStyle
            {
                DashArray = [22, 10],
                DashOffset = -7,
                Cap = LineCap.Round,
                Join = LineJoin.Round
            }));

        var arc = PathGeometry.Create()
            .MoveTo(new Point(origin.X + 540, origin.Y + 85))
            .ArcTo(new Rect(origin.X + 540, origin.Y + 5, 170, 160), 180, 300);
        context.DrawPath(arc, new Pen(new SolidColorBrush(Color.FromRgb(220, 54, 69)), 8,
            new StrokeStyle { DashArray = [16, 8], Cap = LineCap.Round }));
    }

    private static PathGeometry CreateStar(Point center, float outerRadius, float innerRadius, int points)
    {
        var path = PathGeometry.Create();
        for (var i = 0; i < points * 2; i++)
        {
            var radius = i % 2 == 0 ? outerRadius : innerRadius;
            var angle = -MathF.PI / 2 + i * MathF.PI / points;
            var point = new Point(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
            if (i == 0) path.MoveTo(point);
            else path.LineTo(point);
        }
        return path.Close();
    }

    private static void DrawTransformedLines(IRenderContext context, Point origin)
    {
        context.PushTransform(Matrix3x2.CreateRotation(0.07f, new Vector2(origin.X + 360, origin.Y + 45)));
        for (var i = 0; i < 7; i++)
        {
            var y = origin.Y + i * 14;
            context.DrawPath(
                PathGeometry.Create()
                    .MoveTo(new Point(origin.X, y))
                    .LineTo(new Point(origin.X + 760, y + 4)),
                new Pen(new SolidColorBrush(Color.FromRgb(52, 58, 64)), 0.75f + i * 0.35f,
                    new StrokeStyle { Cap = LineCap.Round }));
        }
        context.PopTransform();
    }

    internal static void ValidateScreenshot(Bitmap bitmap)
    {
        if (bitmap.Width < 800 || bitmap.Height < 850)
            throw new InvalidOperationException($"Stroke regression capture is unexpectedly small: {bitmap.Width}x{bitmap.Height}.");

        foreach (var color in RequiredColors)
        {
            var count = CountPixelsNear(bitmap, color, tolerance: 12);
            if (count < 100)
                throw new InvalidOperationException($"Stroke regression color {color} is missing or incomplete ({count} pixels).");
        }

        var orange = Color.FromRgb(245, 128, 37);
        var dashComponents = CountColorComponents(bitmap, orange, tolerance: 35, minimumPixels: 4);
        if (dashComponents < 12)
            throw new InvalidOperationException($"Expected multiple orange dash components, found {dashComponents}.");

        var blendedEdgePixels = CountBlendedPixels(bitmap, orange, Background);
        if (blendedEdgePixels < 40)
            throw new InvalidOperationException($"Expected antialiased orange edge pixels, found {blendedEdgePixels}.");
    }

    private static int CountPixelsNear(Bitmap bitmap, Color color, int tolerance)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (IsNear(pixel, color, tolerance)) count++;
            }
        }
        return count;
    }

    private static int CountColorComponents(Bitmap bitmap, Color color, int tolerance, int minimumPixels)
    {
        var mask = new bool[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
                mask[y * bitmap.Width + x] = IsNear(bitmap.GetPixel(x, y), color, tolerance);
        }

        var visited = new bool[mask.Length];
        var queue = new Queue<int>();
        var components = 0;
        for (var index = 0; index < mask.Length; index++)
        {
            if (!mask[index] || visited[index]) continue;
            visited[index] = true;
            queue.Enqueue(index);
            var pixels = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                pixels++;
                var x = current % bitmap.Width;
                var y = current / bitmap.Width;
                Enqueue(x - 1, y);
                Enqueue(x + 1, y);
                Enqueue(x, y - 1);
                Enqueue(x, y + 1);
            }
            if (pixels >= minimumPixels) components++;
        }
        return components;

        void Enqueue(int x, int y)
        {
            if ((uint)x >= bitmap.Width || (uint)y >= bitmap.Height) return;
            var neighbor = y * bitmap.Width + x;
            if (!mask[neighbor] || visited[neighbor]) return;
            visited[neighbor] = true;
            queue.Enqueue(neighbor);
        }
    }

    private static int CountBlendedPixels(Bitmap bitmap, Color foreground, Color background)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel[3] < 250 || IsNear(pixel, foreground, 8) || IsNear(pixel, background, 8)) continue;
                if (IsBetween(pixel[2], foreground.R, background.R) &&
                    IsBetween(pixel[1], foreground.G, background.G) &&
                    IsBetween(pixel[0], foreground.B, background.B))
                    count++;
            }
        }
        return count;
    }

    private static bool IsNear(Span<byte> pixel, Color color, int tolerance)
        => Math.Abs(pixel[2] - color.R) <= tolerance &&
           Math.Abs(pixel[1] - color.G) <= tolerance &&
           Math.Abs(pixel[0] - color.B) <= tolerance &&
           pixel[3] >= 240;

    private static bool IsBetween(byte value, byte first, byte second)
        => value >= Math.Min(first, second) && value <= Math.Max(first, second);
}
