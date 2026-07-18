using Square.Backends;
using Square.Controls.Controls;
using Square.Graphics;
using Square.Rendering;
using Square.UI;
using Xunit;

namespace Square.Backends.Tests;

public class DirtyPartialPresentTests
{
    [Fact]
    public void ClearRectOnlyTouchesPixelsInsideRect()
    {
        var bmp = new Bitmap(10, 10);
        var ctx = new RenderContext(bmp, 1f);
        ctx.Clear(Color.Black);
        ctx.Clear(Color.White, new Rect(2, 2, 3, 3));

        // Outside remains black (BGRA: B=0,G=0,R=0,A=255)
        Assert.Equal(0, bmp.Pixels[0]);
        Assert.Equal(255, bmp.Pixels[3]);

        // Inside (2,2) is white premultiplied
        var idx = 2 * bmp.Stride + 2 * 4;
        Assert.Equal(255, bmp.Pixels[idx]);     // B
        Assert.Equal(255, bmp.Pixels[idx + 1]); // G
        Assert.Equal(255, bmp.Pixels[idx + 2]); // R
        Assert.Equal(255, bmp.Pixels[idx + 3]); // A
    }

    [Fact]
    public void PresentWithDirtyRectsForwardsListToHandler()
    {
        IReadOnlyList<Rect>? received = null;
        Bitmap? frame = null;
        var bmp = new Bitmap(8, 8);
        var ctx = new RenderContext(bmp, 1f, (bitmap, dirty) =>
        {
            frame = bitmap;
            received = dirty;
        });

        var rects = new List<Rect> { new(1, 2, 3, 4) };
        ctx.Present(rects);

        Assert.Same(bmp, frame);
        Assert.NotNull(received);
        Assert.Single(received!);
        Assert.Equal(new Rect(1, 2, 3, 4), received![0]);
    }

    [Fact]
    public void PresentEmptyDirtyListIsNoOp()
    {
        var calls = 0;
        var bmp = new Bitmap(4, 4);
        var ctx = new RenderContext(bmp, 1f, (_, _) => calls++);
        ctx.Present(Array.Empty<Rect>());
        Assert.Equal(0, calls);
    }

    [Fact]
    public void PresentNullDirtyMeansFullWindow()
    {
        var calls = 0;
        IReadOnlyList<Rect>? received = new List<Rect>(); // sentinel non-null
        var bmp = new Bitmap(4, 4);
        var ctx = new RenderContext(bmp, 1f, (_, dirty) =>
        {
            calls++;
            received = dirty;
        });
        ctx.Present();
        Assert.Equal(1, calls);
        Assert.Null(received);
    }

    [Fact]
    public void DisplayTreeCollectDirtyRectsIncludesNeedsPaintGeometry()
    {
        var root = new View { Geometry = new Rect(0, 0, 200, 200) };
        var staticChild = new View { Geometry = new Rect(0, 0, 50, 50) };
        var canvas = new Canvas { Geometry = new Rect(40, 60, 80, 100) };
        root.Children.Add(staticChild);
        root.Children.Add(canvas);

        var tree = new DisplayTree();
        tree.BuildFrom(root);
        // Clear paint dirty from build path
        staticChild.ClearPaintDirty();
        canvas.ClearPaintDirty();
        tree.UpdateDirty();

        canvas.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.NotEmpty(dirty);
        // Canvas geometry should be covered by some dirty rect (with pad)
        Assert.Contains(dirty, r =>
            r.X <= canvas.Geometry.X &&
            r.Y <= canvas.Geometry.Y &&
            r.Right >= canvas.Geometry.Right &&
            r.Bottom >= canvas.Geometry.Bottom);
    }

    [Fact]
    public void DisplayTreeMergeUnionsOverlappingRects()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);
        var merged = DisplayTree.MergeDirtyRects([a, b]);
        Assert.Single(merged);
        var u = merged[0];
        Assert.Equal(0, u.X);
        Assert.Equal(0, u.Y);
        Assert.Equal(15, u.Width);
        Assert.Equal(15, u.Height);
    }
}
