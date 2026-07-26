using System.Text;
using Square.Graphics;
using Square.Graphics.Codecs;

namespace Square.Sample;

internal static class CircleRegressionDiff
{
    private static readonly Color Background = Color.FromRgb(247, 249, 252);

    public static CircleRegressionDiffResult Save(Bitmap software, Bitmap vulkan, string outputDirectory)
        => Save(software, vulkan, outputDirectory, CreateCircleRegions(software.Width, software.Height), Background);

    public static CircleRegressionDiffResult SaveMediaSvg(Bitmap software, Bitmap vulkan, string outputDirectory)
        => Save(software, vulkan, outputDirectory, CreateMediaSvgRegions(), Color.White);

    private static CircleRegressionDiffResult Save(
        Bitmap software,
        Bitmap vulkan,
        string outputDirectory,
        DiffRegion[] regions,
        Color background)
    {
        ArgumentNullException.ThrowIfNull(software);
        ArgumentNullException.ThrowIfNull(vulkan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (software.Width != vulkan.Width || software.Height != vulkan.Height)
            throw new InvalidOperationException(
                $"Cannot diff bitmaps with different sizes: software={software.Width}x{software.Height}, vulkan={vulkan.Width}x{vulkan.Height}.");

        Directory.CreateDirectory(outputDirectory);
        var softwarePath = Path.Combine(outputDirectory, "software.png");
        var vulkanPath = Path.Combine(outputDirectory, "vulkan.png");
        var diffPath = Path.Combine(outputDirectory, "diff.png");
        var reportPath = Path.Combine(outputDirectory, "diff.txt");

        BitmapPngEncoder.Save(software, softwarePath);
        BitmapPngEncoder.Save(vulkan, vulkanPath);
        using var diff = CreateDiffBitmap(software, vulkan, regions, background, out var result);
        BitmapPngEncoder.Save(diff, diffPath);
        File.WriteAllText(reportPath, FormatReport(result), Encoding.UTF8);
        return result with
        {
            SoftwarePath = softwarePath,
            VulkanPath = vulkanPath,
            DiffPath = diffPath,
            ReportPath = reportPath
        };
    }

    private static Bitmap CreateDiffBitmap(
        Bitmap software,
        Bitmap vulkan,
        DiffRegion[] regions,
        Color background,
        out CircleRegressionDiffResult result)
    {
        var diff = new Bitmap(software.Width, software.Height);
        var stats = regions.ToDictionary(region => region.Name, _ => new DiffStatsBuilder());

        for (var y = 0; y < software.Height; y++)
        {
            for (var x = 0; x < software.Width; x++)
            {
                var soft = software.GetPixel(x, y);
                var vk = vulkan.GetPixel(x, y);
                var delta = PixelDelta(soft, vk);
                var output = diff.GetPixel(x, y);
                if (delta == 0)
                {
                    SetPixel(output, 8, 8, 8, 255);
                    continue;
                }

                var softwareColor = DistanceFromBackground(soft, background);
                var vulkanColor = DistanceFromBackground(vk, background);
                var intensity = (byte)Math.Min(255, 32 + delta * 3);
                if (softwareColor > vulkanColor)
                    SetPixel(output, 0, 0, intensity, 255);
                else if (vulkanColor > softwareColor)
                    SetPixel(output, intensity, 80, 0, 255);
                else
                    SetPixel(output, 0, intensity, intensity, 255);

                foreach (var region in regions)
                {
                    if (!region.Contains(x, y)) continue;
                    stats[region.Name].Add(delta, softwareColor, vulkanColor);
                }
            }
        }

        result = new CircleRegressionDiffResult(
            stats.ToDictionary(pair => pair.Key, pair => pair.Value.Build()),
            SoftwarePath: "",
            VulkanPath: "",
            DiffPath: "",
            ReportPath: "");
        return diff;
    }

    private static DiffRegion[] CreateCircleRegions(int width, int height)
    {
        var scaleX = width / 900f;
        var scaleY = height / 980f;
        return
        [
            ScaledRegion("filled", 40, 40, 470, 150, scaleX, scaleY),
            ScaledRegion("stroke", 40, 210, 560, 150, scaleX, scaleY),
            ScaledRegion("fractional-transform", 40, 380, 380, 130, scaleX, scaleY),
            new DiffRegion("whole", 0, 0, width, height)
        ];
    }

    private static DiffRegion[] CreateMediaSvgRegions()
        =>
        [
            new("svg", 48, 28, 1080, 557),
            new("outer-rounded-rect", 48, 28, 1080, 557),
            new("left-icon", 132, 104, 336, 336),
            new("right-circle-check", 609, 119, 252, 252),
            new("right-polygon", 708, 119, 360, 252),
            new("whole", 0, 0, int.MaxValue / 2, int.MaxValue / 2)
        ];

    private static DiffRegion ScaledRegion(string name, int x, int y, int width, int height, float scaleX, float scaleY)
        => new(
            name,
            (int)MathF.Floor(x * scaleX),
            (int)MathF.Floor(y * scaleY),
            (int)MathF.Ceiling(width * scaleX),
            (int)MathF.Ceiling(height * scaleY));

    private static string FormatReport(CircleRegressionDiffResult result)
    {
        var builder = new StringBuilder();
        foreach (var (name, stats) in result.Regions)
        {
            builder.AppendLine($"{name}:");
            builder.AppendLine($"  differingPixels={stats.DifferingPixels}");
            builder.AppendLine($"  totalDelta={stats.TotalDelta}");
            builder.AppendLine($"  maxDelta={stats.MaxDelta}");
            builder.AppendLine($"  softwareHeavier={stats.SoftwareHeavier}");
            builder.AppendLine($"  vulkanHeavier={stats.VulkanHeavier}");
            builder.AppendLine($"  shapeOnly={stats.ShapeOnly}");
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static int PixelDelta(Span<byte> a, Span<byte> b)
        => Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]) + Math.Abs(a[3] - b[3]);

    private static int DistanceFromBackground(Span<byte> pixel, Color background)
        => Math.Abs(pixel[2] - background.R) + Math.Abs(pixel[1] - background.G) + Math.Abs(pixel[0] - background.B);

    private static void SetPixel(Span<byte> pixel, byte b, byte g, byte r, byte a)
    {
        pixel[0] = b;
        pixel[1] = g;
        pixel[2] = r;
        pixel[3] = a;
    }

    private readonly record struct DiffRegion(string Name, int X, int Y, int Width, int Height)
    {
        public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    }

    private sealed class DiffStatsBuilder
    {
        private long _differingPixels;
        private long _totalDelta;
        private int _maxDelta;
        private long _softwareHeavier;
        private long _vulkanHeavier;
        private long _shapeOnly;

        public void Add(int delta, int softwareColor, int vulkanColor)
        {
            _differingPixels++;
            _totalDelta += delta;
            _maxDelta = Math.Max(_maxDelta, delta);
            if (softwareColor > 20 && vulkanColor > 20)
            {
                if (softwareColor > vulkanColor) _softwareHeavier++;
                else if (vulkanColor > softwareColor) _vulkanHeavier++;
            }
            else
            {
                _shapeOnly++;
            }
        }

        public CircleRegressionRegionStats Build()
            => new(_differingPixels, _totalDelta, _maxDelta, _softwareHeavier, _vulkanHeavier, _shapeOnly);
    }
}

internal sealed record CircleRegressionDiffResult(
    IReadOnlyDictionary<string, CircleRegressionRegionStats> Regions,
    string SoftwarePath,
    string VulkanPath,
    string DiffPath,
    string ReportPath);

internal sealed record CircleRegressionRegionStats(
    long DifferingPixels,
    long TotalDelta,
    int MaxDelta,
    long SoftwareHeavier,
    long VulkanHeavier,
    long ShapeOnly);
