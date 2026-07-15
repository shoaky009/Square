using Square.Backends;
using Square.Controls.Controls;
using Square.Graphics;
using Square.Rendering;
using Square.Text.Glyph;
using Square.UI;
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
    public void PresentSubmitsFrameBuffer()
    {
        Bitmap? presented = null;
        var bitmap = new Bitmap(4, 4);
        var context = new RenderContext(bitmap, 1f, frame => presented = frame);

        context.Present();

        Assert.Same(bitmap, presented);
    }

    [Fact]
    public void ResizeRecreatesFrameBufferAtNewCanvasSize()
    {
        Bitmap? presented = null;
        var original = new Bitmap(10, 10);
        var context = new RenderContext(original, 1f, frame => presented = frame);

        context.Resize(new Size(320, 180));
        context.Clear(Color.Blue);
        context.Present();

        Assert.Equal(new Size(320, 180), context.CanvasSize);
        Assert.NotSame(original, context.GetBitmap());
        Assert.Same(context.GetBitmap(), presented);
        Assert.Equal(255, presented!.Pixels[0]);
    }

    [Fact]
    public void DrawTextUsesReadableSeparatedGlyphs()
    {
        var context = CreateContext(180, 40);
        context.Clear(Color.White);
        context.DrawText(
            new TextLayout("Hello 012", new Font("Segoe UI", 20)),
            new Point(2, 2), new SolidColorBrush(Color.Black));

        var bitmap = context.GetBitmap();
        var darkPixels = 0;
        for (var i = 0; i < bitmap.Pixels.Length; i += 4)
            if (bitmap.Pixels[i] < 240 || bitmap.Pixels[i + 1] < 240 || bitmap.Pixels[i + 2] < 240)
                darkPixels++;

        Assert.True(darkPixels > 80);
    }

    [Fact]
    public void SystemRasterizerDistinguishesLowercaseAndDigits()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rasterizer = new SystemGlyphRasterizer();
        var font = new Font("Segoe UI", 20);

        var upper = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, 'H'));
        var lower = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, 'e'));
        var digit = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, '0'));

        Assert.Contains(upper.Coverage, value => value > 0);
        Assert.Contains(lower.Coverage, value => value > 0);
        Assert.Contains(digit.Coverage, value => value > 0);
        Assert.False(upper.Coverage.SequenceEqual(lower.Coverage));
        Assert.False(lower.Coverage.SequenceEqual(digit.Coverage));
    }

    [Fact]
    public void SystemRasterizerSupportsChineseAndJapaneseGlyphs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rasterizer = new SystemGlyphRasterizer();
        var font = new Font("Segoe UI", 20);

        var chinese = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, '中'));
        var hiragana = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, 'あ'));
        var katakana = Assert.IsType<RasterizedGlyph>(rasterizer.Rasterize(font, 'ア'));

        Assert.Contains(chinese.Coverage, value => value > 0);
        Assert.Contains(hiragana.Coverage, value => value > 0);
        Assert.Contains(katakana.Coverage, value => value > 0);
    }

    [Fact]
    public void EveryM1ControlProducesVisibleOutput()
    {
        var preview = new Bitmap(2, 2);
        for (var i = 0; i < preview.Pixels.Length; i += 4)
        {
            preview.Pixels[i + 2] = 255;
            preview.Pixels[i + 3] = 255;
        }

        var view = new View();
        view.Style.Set("background", "#eeeeee");
        var controls = new Visual[]
        {
            view,
            new Square.Controls.Controls.Text("Text"),
            new Button("Button"),
            new Input { Placeholder = "Input" },
            new TextArea { Placeholder = "TextArea" },
            new CheckBox { TextContent = "Check", IsChecked = true },
            new Radio { TextContent = "Radio", IsChecked = true },
            new Select { Value = "Blue", Options = ["Blue", "Green"] },
            new Square.Controls.Controls.Image { ImageContent = preview },
            new Canvas()
        };

        foreach (var control in controls)
        {
            var context = CreateContext(240, 120);
            context.Clear(Color.Transparent);
            control.Geometry = new Rect(4, 4, 220, 100);
            control.Render(context);

            Assert.Contains(context.GetBitmap().Pixels.Where((_, index) => index % 4 == 3), alpha => alpha > 0);
        }
    }

    [Fact]
    public void RetainedRendererDrawsFocusedTextCarets()
    {
        var controls = new UIElement[] { new Input(), new TextArea() };

        foreach (var control in controls)
        {
            control.Geometry = new Rect(4, 4, 220, 80);
            control.Focus();
            var context = CreateContext(240, 100);
            context.Clear(Color.White);
            var tree = new RenderTree();
            tree.BuildFrom(control);

            tree.Render(context);

            Assert.True(ContainsBgra(context.GetBitmap(), 0, 0, 0, 255));
        }
    }

    [Fact]
    public void InputCaretAccountsForMixedHalfWidthAndFullWidthText()
    {
        var input = new Input { Value = "A中Ｂ", Geometry = new Rect(4, 4, 220, 36) };
        var asciiInput = new Input { Value = "ABC", Geometry = new Rect(4, 4, 220, 36) };
        input.Focus();
        asciiInput.Focus();
        var context = CreateContext(240, 50);
        context.Clear(Color.White);
        var tree = new RenderTree();
        tree.BuildFrom(input);

        tree.Render(context);

        Assert.True(input.CaretRect.X > asciiInput.CaretRect.X);
        var expectedCaretX = (int)input.CaretRect.X;
        var pixel = expectedCaretX * 4 + ((int)input.CaretRect.Y + 2) * context.GetBitmap().Stride;
        Assert.Equal(0, context.GetBitmap().Pixels[pixel]);
        Assert.Equal(0, context.GetBitmap().Pixels[pixel + 1]);
        Assert.Equal(0, context.GetBitmap().Pixels[pixel + 2]);
    }

    [Fact]
    public void FocusedTextSelectionIsRendered()
    {
        var input = new Input { Value = "Select", Geometry = new Rect(4, 4, 220, 36) };
        input.Focus();
        input.SelectAll();
        var context = CreateContext(240, 50);
        context.Clear(Color.White);
        var tree = new RenderTree();
        tree.BuildFrom(input);

        tree.Render(context);

        var bitmap = context.GetBitmap();
        var hasChromeBlueBackground = false;
        var hasWhiteForeground = false;
        var selectionTop = (int)input.Geometry.Y;
        var selectionBottom = (int)input.Geometry.Bottom;
        for (var y = selectionTop; y < selectionBottom; y++)
        {
            for (var x = 12; x < (int)input.CaretRect.X; x++)
            {
                var index = y * bitmap.Stride + x * 4;
                hasChromeBlueBackground |= bitmap.Pixels[index] == 255 && bitmap.Pixels[index + 1] == 144 && bitmap.Pixels[index + 2] == 51;
                hasWhiteForeground |= bitmap.Pixels[index] > 220 && bitmap.Pixels[index + 1] > 220 && bitmap.Pixels[index + 2] > 220;
            }
        }

        Assert.True(hasChromeBlueBackground);
        Assert.True(hasWhiteForeground);
    }

    [Fact]
    public void CompactLineHeightSelectionCoversNaturalFontHeight()
    {
        var input = new Input { Value = "Compact", Geometry = new Rect(4, 4, 220, 30) };
        input.Style.Set("font-size", "14px");
        input.Style.Set("line-height", "14px");
        input.Focus();
        input.SelectAll();
        var context = CreateContext(240, 40);
        context.Clear(Color.White);
        var tree = new RenderTree();
        tree.BuildFrom(input);

        tree.Render(context);

        var bitmap = context.GetBitmap();
        var highlightedRows = 0;
        for (var y = (int)input.Geometry.Y; y < (int)input.Geometry.Bottom; y++)
        {
            var hasSelectionPixel = false;
            for (var x = 12; x < (int)input.CaretRect.X; x++)
            {
                var index = y * bitmap.Stride + x * 4;
                hasSelectionPixel |= bitmap.Pixels[index] == 255 &&
                    bitmap.Pixels[index + 1] == 144 && bitmap.Pixels[index + 2] == 51;
            }
            if (hasSelectionPixel) highlightedRows++;
        }

        Assert.True(highlightedRows >= 17);
    }

    [Fact]
    public void FocusedCaretCanBlinkAndResetVisible()
    {
        var input = new Input { Value = "Blink", Geometry = new Rect(4, 4, 220, 36) };
        input.Focus();
        var context = CreateContext(240, 50);
        var tree = new RenderTree();

        context.Clear(Color.White);
        tree.BuildFrom(input);
        tree.Render(context);
        var caretPixel = ((int)input.CaretRect.Y + 2) * context.GetBitmap().Stride + (int)input.CaretRect.X * 4;
        Assert.Equal(0, context.GetBitmap().Pixels[caretPixel]);
        Assert.Equal(0, context.GetBitmap().Pixels[caretPixel + 1]);

        Assert.True(input.ToggleCaretBlink());
        context.Clear(Color.White);
        tree.BuildFrom(input);
        tree.Render(context);
        Assert.False(context.GetBitmap().Pixels[caretPixel] == 0 && context.GetBitmap().Pixels[caretPixel + 1] == 0);

        input.ResetCaretBlink();
        context.Clear(Color.White);
        tree.BuildFrom(input);
        tree.Render(context);
        Assert.Equal(0, context.GetBitmap().Pixels[caretPixel]);
        Assert.Equal(0, context.GetBitmap().Pixels[caretPixel + 1]);
    }

    [Fact]
    public void RetainedRendererReplaysGeometryCommands()
    {
        var radio = new Radio { IsChecked = true, Geometry = new Rect(4, 4, 100, 24) };
        var context = CreateContext(120, 40);
        context.Clear(Color.White);
        var tree = new RenderTree();
        tree.BuildFrom(radio);

        tree.Render(context);

        Assert.True(ContainsBgra(context.GetBitmap(), 212, 120, 0, 255));
    }

    [Fact]
    public void OpenSelectRendersAboveLaterSiblings()
    {
        var root = new View { Geometry = new Rect(0, 0, 240, 170) };
        var select = new Select
        {
            Geometry = new Rect(10, 10, 220, 36),
            Options = ["Blue", "Green", "Orange"],
            Value = "Blue"
        };
        var canvas = new Canvas { Geometry = new Rect(0, 48, 240, 120) };
        root.Children.Add(select);
        root.Children.Add(canvas);
        select.HandlePointerDown(new Point(20, 20));
        var context = CreateContext(240, 170);
        context.Clear(Color.White);
        var tree = new RenderTree();
        tree.BuildFrom(root);

        tree.Render(context);

        Assert.True(ContainsBgra(context.GetBitmap(), 250, 247, 242, 255));
    }

    private static bool ContainsBgra(Bitmap bitmap, byte blue, byte green, byte red, byte alpha)
    {
        for (var i = 0; i < bitmap.Pixels.Length; i += 4)
        {
            if (bitmap.Pixels[i] == blue && bitmap.Pixels[i + 1] == green &&
                bitmap.Pixels[i + 2] == red && bitmap.Pixels[i + 3] == alpha)
                return true;
        }

        return false;
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
    public void EllipseAndDiagonalLineHaveAntialiasedEdges()
    {
        var ellipseContext = CreateContext(24, 24);
        ellipseContext.Clear(Color.Transparent);
        ellipseContext.FillGeometry(
            new EllipseGeometry(new Point(12, 12), 8, 8),
            new SolidColorBrush(Color.White));

        var lineContext = CreateContext(24, 24);
        lineContext.Clear(Color.Transparent);
        lineContext.DrawPath(
            PathGeometry.Create().MoveTo(new Point(3, 5)).LineTo(new Point(20, 16)),
            Pen.FromColor(Color.White, 2));

        Assert.Contains(AlphaValues(ellipseContext.GetBitmap()), alpha => alpha is > 0 and < 255);
        Assert.Contains(AlphaValues(lineContext.GetBitmap()), alpha => alpha is > 0 and < 255);
    }

    private static IEnumerable<byte> AlphaValues(Bitmap bitmap)
    {
        for (var i = 3; i < bitmap.Pixels.Length; i += 4)
            yield return bitmap.Pixels[i];
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
