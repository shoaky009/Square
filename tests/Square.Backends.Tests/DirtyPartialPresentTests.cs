using System;
using System.Collections.Generic;
using Square.Backends;
using Square.Controls.Controls;
using Square.Graphics;
using Square.Rendering;
using Square.UI;
using System.Numerics;
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

    [Fact]
    public void DisplayTreeGeometryChangeDirtiesOldAndNewBounds()
    {
        var root = new View { Geometry = new Rect(0, 0, 200, 200) };
        var child = new View { Geometry = new Rect(10, 20, 30, 40) };
        root.Children.Add(child);
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        child.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(200, 200), 1f));

        child.Geometry = new Rect(100, 120, 30, 40);
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.Contains(dirty, r =>
            r.X <= 10 && r.Y <= 20 &&
            r.Right >= 130 && r.Bottom >= 160);
    }

    [Fact]
    public void DirtyRectsRemainAvailableAfterDisplayTreeRender()
    {
        var root = new View { Geometry = new Rect(0, 0, 100, 100) };
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(100, 100), 1f));

        root.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();
        tree.Render(new RenderContext(new Bitmap(100, 100), 1f), dirty[0]);

        Assert.Single(dirty);
    }

    [Fact]
    public void DisplayTreeDirtyRectsUseVisualBoundsWhenTextExceedsGeometry()
    {
        var root = new View { Geometry = new Rect(0, 0, 260, 80) };
        var text = new Square.Controls.Controls.Text("This text is wider than geometry")
        {
            Geometry = new Rect(10, 10, 20, 24)
        };
        root.Children.Add(text);
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        text.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(260, 80), 1f));

        text.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.Contains(dirty, r => r.Right > text.Geometry.Right + 40);
    }

    [Fact]
    public void DisplayTreeDirtyRectsUsePathVisualBoundsOutsideGeometry()
    {
        var root = new View { Geometry = new Rect(0, 0, 160, 80) };
        var element = new PathPaintElement
        {
            Geometry = new Rect(0, 0, 10, 10)
        };
        root.Children.Add(element);
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        element.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(160, 80), 1f));

        element.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.Contains(dirty, r => r.Right >= 90 && r.Bottom >= 30);
    }

    [Fact]
    public void DisplayTreeVisualBoundsRespectPushClip()
    {
        var root = new View { Geometry = new Rect(0, 0, 220, 80) };
        var element = new ClippedPaintElement
        {
            Geometry = new Rect(0, 0, 10, 10)
        };
        root.Children.Add(element);
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        element.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(220, 80), 1f));

        element.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.DoesNotContain(dirty, r => r.Right > 80);
        Assert.Contains(dirty, r => r.Right >= 40 && r.Bottom >= 40);
    }

    [Fact]
    public void DisplayTreeVisualBoundsApplyPushTransform()
    {
        var root = new View { Geometry = new Rect(0, 0, 200, 100) };
        var element = new TransformedPaintElement
        {
            Geometry = new Rect(0, 0, 10, 10)
        };
        root.Children.Add(element);
        var tree = new DisplayTree();
        tree.BuildFrom(root);
        root.ClearPaintDirty();
        element.ClearPaintDirty();
        tree.Render(new RenderContext(new Bitmap(200, 100), 1f));

        element.InvalidatePaint();
        tree.UpdateDirty();
        var dirty = tree.CollectDirtyRects();

        Assert.Contains(dirty, r => r.X <= 70 && r.Right >= 90 && r.Bottom >= 30);
    }

    [Fact]
    public void RenderContextAppliesPushTransformToFillRect()
    {
        var bmp = new Bitmap(40, 30);
        var ctx = new RenderContext(bmp, 1f);
        ctx.Clear(Color.Transparent);

        ctx.PushTransform(Matrix3x2.CreateTranslation(10, 5));
        ctx.FillRect(new Rect(0, 0, 4, 4), Brush.FromColor(Color.Red));
        ctx.PopTransform();
        ctx.FillRect(new Rect(0, 0, 2, 2), Brush.FromColor(Color.Blue));

        AssertPixel(bmp, 11, 6, Color.Red);
        AssertPixel(bmp, 1, 1, Color.Blue);
        AssertPixel(bmp, 5, 5, Color.Transparent);
    }

    private sealed class PathPaintElement : UIElement
    {
        public override void Paint(IRenderContext ctx)
        {
            ctx.DrawPath(
                PathGeometry.Create()
                    .MoveTo(new Point(50, 20))
                    .LineTo(new Point(90, 30)),
                Pen.FromColor(Color.Red, 2));
        }
    }

    private static void AssertPixel(Bitmap bmp, int x, int y, Color color)
    {
        var idx = y * bmp.Stride + x * 4;
        Assert.Equal(color.B, bmp.Pixels[idx]);
        Assert.Equal(color.G, bmp.Pixels[idx + 1]);
        Assert.Equal(color.R, bmp.Pixels[idx + 2]);
        Assert.Equal(color.A, bmp.Pixels[idx + 3]);
    }

    private sealed class ClippedPaintElement : UIElement
    {
        public override void Paint(IRenderContext ctx)
        {
            ctx.PushClip(new Rect(20, 20, 20, 20));
            ctx.FillRect(new Rect(20, 20, 160, 40), Brush.FromColor(Color.Blue));
            ctx.PopClip();
        }
    }

    private sealed class TransformedPaintElement : UIElement
    {
        public override void Paint(IRenderContext ctx)
        {
            ctx.PushTransform(Matrix3x2.CreateTranslation(70, 20));
            ctx.FillRect(new Rect(0, 0, 20, 10), Brush.FromColor(Color.Green));
            ctx.PopTransform();
        }
    }
}
