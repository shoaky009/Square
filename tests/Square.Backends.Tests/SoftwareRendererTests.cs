using Square.Backends;
using Square.Graphics;
using Xunit;

namespace Square.Backends.Tests;

public class SoftwareRendererTests
{
    private static RenderContext CreateContext(int w, int h)
    {
        var bmp = new Bitmap(w, h);
        return new RenderContext(bmp, 1f);
    }

    [Fact]
    public void ClearFillsAllPixels()
    {
        var ctx = CreateContext(10, 10);
        ctx.Clear(Color.Red);
        var bmp = ctx.GetBitmap();
        for (int i = 0; i < bmp.Pixels.Length; i += 4)
        {
            Assert.Equal(255, bmp.Pixels[i + 3]); // A
            Assert.Equal(0, bmp.Pixels[i]);     // B
            Assert.Equal(0, bmp.Pixels[i + 1]); // G
            Assert.Equal(255, bmp.Pixels[i + 2]); // R
        }
    }

    [Fact]
    public void FillRectOpaque()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        ctx.FillRect(new Rect(5, 5, 10, 10), new SolidColorBrush(Color.White));
        var bmp = ctx.GetBitmap();
        // 中心像素应为白色
        var idx = 10 * bmp.Stride + 10 * 4;
        Assert.Equal(255, bmp.Pixels[idx + 3]);
        Assert.Equal(255, bmp.Pixels[idx]);
        Assert.Equal(255, bmp.Pixels[idx + 1]);
        Assert.Equal(255, bmp.Pixels[idx + 2]);
        // 角落像素应为黑色
        Assert.Equal(0, bmp.Pixels[0]);
        Assert.Equal(0, bmp.Pixels[1]);
        Assert.Equal(0, bmp.Pixels[2]);
    }

    [Fact]
    public void FillRectSemiTransparent()
    {
        var ctx = CreateContext(10, 10);
        ctx.Clear(Color.Black);
        ctx.FillRect(new Rect(0, 0, 10, 10), new SolidColorBrush(255, 0, 0, 128));
        var bmp = ctx.GetBitmap();
        var idx = 5 * bmp.Stride + 5 * 4;
        // Black background (A=255) + 50% red = outA=255
        Assert.Equal(255, bmp.Pixels[idx + 3]);
        // R should be blended (~128)
        Assert.True(bmp.Pixels[idx + 2] > 100 && bmp.Pixels[idx + 2] < 200);
    }

    [Fact]
    public void FillEllipse()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        ctx.FillGeometry(new EllipseGeometry(new Point(10, 10), 8, 8), new SolidColorBrush(Color.White));
        var bmp = ctx.GetBitmap();
        // 中心应为白色
        var idx = 10 * bmp.Stride + 10 * 4;
        Assert.Equal(255, bmp.Pixels[idx + 3]);
        // 角落应为黑色
        Assert.Equal(0, bmp.Pixels[0 + 2]);
    }

    [Fact]
    public void FillRoundedRect()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        ctx.FillGeometry(new RoundedRectGeometry(new Rect(2, 2, 16, 16), 4, 4), new SolidColorBrush(Color.Red));
        var bmp = ctx.GetBitmap();
        // 中心应为红色
        var idx = 10 * bmp.Stride + 10 * 4;
        Assert.Equal(255, bmp.Pixels[idx + 2]); // R
    }

    [Fact]
    public void DrawRect()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        ctx.DrawRect(new Rect(5, 5, 10, 10), Pen.FromColor(Color.White, 1));
        var bmp = ctx.GetBitmap();
        // 边框像素
        Assert.Equal(255, bmp.Pixels[5 * bmp.Stride + 5 * 4 + 3]);
        // 中心应为黑色（内部空）
        Assert.Equal(0, bmp.Pixels[10 * bmp.Stride + 10 * 4 + 2]);
    }

    [Fact]
    public void DrawLine()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        var path = PathGeometry.Create()
            .MoveTo(new Point(2, 2))
            .LineTo(new Point(18, 18));
        ctx.DrawPath(path, Pen.FromColor(Color.White, 1));
        var bmp = ctx.GetBitmap();
        // 对角线中点应有像素
        var idx = 10 * bmp.Stride + 10 * 4;
        Assert.True(bmp.Pixels[idx + 3] > 0);
    }

    [Fact]
    public void DrawText()
    {
        var ctx = CreateContext(100, 30);
        ctx.Clear(Color.Black);
        var layout = new TextLayout("HELLO", new Font("Segoe UI", 16f));
        ctx.DrawText(layout, new Point(5, 5), new SolidColorBrush(Color.White));
        var bmp = ctx.GetBitmap();
        // 应有白色像素
        var hasWhite = false;
        for (int i = 0; i < bmp.Pixels.Length; i += 4)
            if (bmp.Pixels[i + 3] > 0 && bmp.Pixels[i + 2] > 0) { hasWhite = true; break; }
        Assert.True(hasWhite);
    }

    [Fact]
    public void ClipRect()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        ctx.PushClip(new Rect(5, 5, 10, 10));
        ctx.FillRect(new Rect(0, 0, 20, 20), new SolidColorBrush(Color.White));
        ctx.PopClip();
        var bmp = ctx.GetBitmap();
        // 裁剪区内为白色
        Assert.Equal(255, bmp.Pixels[10 * bmp.Stride + 10 * 4 + 3]);
        // 裁剪区外为黑色
        Assert.Equal(0, bmp.Pixels[0 + 2]);
    }

    [Fact]
    public void DrawImage()
    {
        var ctx = CreateContext(20, 20);
        ctx.Clear(Color.Black);
        var src = new Bitmap(10, 10);
        for (int i = 0; i < src.Pixels.Length; i += 4)
        {
            src.Pixels[i] = 255;     // B
            src.Pixels[i + 1] = 0;   // G
            src.Pixels[i + 2] = 0;   // R
            src.Pixels[i + 3] = 255; // A
        }
        ctx.DrawImage(src, new Rect(0, 0, 10, 10));
        var bmp = ctx.GetBitmap();
        Assert.Equal(255, bmp.Pixels[5 * bmp.Stride + 5 * 4]);     // B
        Assert.Equal(255, bmp.Pixels[5 * bmp.Stride + 5 * 4 + 3]); // A
    }
}