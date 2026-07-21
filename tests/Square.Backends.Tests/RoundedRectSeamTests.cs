using Square.Backends;
using Square.Graphics;
using Xunit;

namespace Square.Backends.Tests;

public class RoundedRectSeamTests
{
    [Fact]
    public void TranslucentRoundedRectDoesNotDoubleBlendAtCornerJoins()
    {
        var bitmap = new Bitmap(48, 40);
        using var context = new RenderContext(bitmap, 1f);
        context.Clear(Color.Transparent);
        context.FillGeometry(
            new RoundedRectGeometry(new Rect(8, 8, 32, 24), 8, 8),
            new SolidColorBrush(Color.FromRgba(0, 0, 0, 128)));

        var bodyAlpha = AlphaAt(bitmap, 24, 12);
        Assert.Equal(128, bodyAlpha);
        Assert.Equal(bodyAlpha, AlphaAt(bitmap, 16, 10)); // top-left to horizontal edge
        Assert.Equal(bodyAlpha, AlphaAt(bitmap, 10, 16)); // top-left to vertical edge
        Assert.Equal(bodyAlpha, AlphaAt(bitmap, 37, 16)); // top-right to vertical edge
        Assert.Equal(bodyAlpha, AlphaAt(bitmap, 16, 29)); // bottom-left to horizontal edge

        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            Assert.True(AlphaAt(bitmap, x, y) <= bodyAlpha,
                $"Pixel ({x},{y}) was blended more than once: {AlphaAt(bitmap, x, y)} > {bodyAlpha}.");
    }

    [Fact]
    public void FractionalRoundedRectHasNoGapWhereVerticalEdgesMeetBottomCorners()
    {
        var bitmap = new Bitmap(48, 40);
        using var context = new RenderContext(bitmap, 1f);
        context.Clear(Color.Transparent);
        context.FillGeometry(
            new RoundedRectGeometry(new Rect(8.25f, 7.75f, 31.5f, 24.5f), 7.5f, 7.5f),
            new SolidColorBrush(Color.FromRgba(0, 0, 0, 128)));

        Assert.True(AlphaAt(bitmap, 9, 24) > 0);
        Assert.True(AlphaAt(bitmap, 38, 24) > 0);
        Assert.True(AlphaAt(bitmap, 10, 25) > 0);
        Assert.True(AlphaAt(bitmap, 37, 25) > 0);
    }

    private static byte AlphaAt(Bitmap bitmap, int x, int y) => bitmap.Pixels[y * bitmap.Stride + x * 4 + 3];
}
