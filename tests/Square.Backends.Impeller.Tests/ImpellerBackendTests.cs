using Square.Backends.Impeller;
using Square.Graphics;
using System.Numerics;
using Xunit;

namespace Square.Backends.Impeller.Tests;

public class ImpellerBackendTests
{
    [Fact]
    public void RegistrationDoesNotLoadNativeLibrary()
    {
        ImpellerRegistration.Register("definitely-missing-square-impeller-library");

        Assert.IsType<ImpellerBackendFactory>(RenderBackendRegistry.Get("Impeller"));
    }

    [Fact]
    public void ContextCreationRequiresNativeTargetBeforeLoadingLibrary()
    {
        var factory = new ImpellerBackendFactory("definitely-missing-square-impeller-library");

        var exception = Assert.Throws<ImpellerException>(() => factory.CreateContext(new RenderContextCreateInfo
        {
            CanvasSize = new Size(100, 100)
        }));

        Assert.Contains("native Vulkan render target", exception.Message);
    }

    [Fact]
    public void MissingConfiguredLibraryProducesDetailedError()
    {
        const string path = "definitely-missing-square-impeller-library";
        var factory = new ImpellerBackendFactory(path);

        var exception = Assert.Throws<ImpellerException>(() => factory.CreateContext(new RenderContextCreateInfo
        {
            CanvasSize = new Size(100, 100),
            NativeTarget = new Win32VulkanRenderTarget(new IntPtr(1), new IntPtr(2))
        }));

        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void DefaultLibraryNameUsesOfficialImpellerSdkBinary()
    {
        var path = ImpellerNative.ResolveLibraryPath(null);

        Assert.Equal(OperatingSystem.IsWindows() ? "impeller.dll" : "libimpeller.so", path);
    }

    [Fact]
    public void OfficialSdkLibraryLoadsWhenIntegrationPathIsProvided()
    {
        var path = Environment.GetEnvironmentVariable("SQUARE_IMPELLER_TEST_LIBRARY");
        if (string.IsNullOrWhiteSpace(path)) return;

        using var native = ImpellerNative.Load(path);

        Assert.NotNull(native);
    }

    [Fact]
    public void ContextCreationUsesPhysicalSizeAndNativeTarget()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api, new Size(100, 50), 1.5f);

        Assert.Equal((uint)150, api.CreatedWidth);
        Assert.Equal((uint)75, api.CreatedHeight);
        Assert.Equal(1.5f, api.CreatedDpiScale);
        Assert.Equal(new IntPtr(1), api.CreatedWindow);
        Assert.Equal(new IntPtr(2), api.CreatedInstance);
    }

    [Fact]
    public void BasicDrawingCommandsMapToNativeApi()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);

        context.PushTransform(Matrix3x2.CreateTranslation(4, 5));
        context.PushClip(new Rect(1, 2, 30, 40));
        context.FillRect(new Rect(2, 3, 10, 20), new SolidColorBrush(Color.FromRgba(10, 20, 30, 128)));
        context.DrawRect(new Rect(4, 5, 12, 13), Pen.FromColor(Color.Red, 2));
        context.FillGeometry(new RoundedRectGeometry(new Rect(6, 7, 20, 30), 3, 4), new SolidColorBrush(Color.Blue));
        context.DrawGeometry(new EllipseGeometry(new Point(15, 16), 7, 8), Pen.FromColor(Color.Green, 3));
        context.PushLayer(new Rect(0, 0, 50, 50), 0.25f);
        context.PopLayer();
        context.PopClip();
        context.PopTransform();
        context.Flush();
        context.Present();

        Assert.Equal(1, api.BeginFrameCount);
        Assert.Contains("PushTransform:1,0,0,1,4,5", api.Calls);
        Assert.Contains("PushClipRect:1,2,30,40", api.Calls);
        Assert.Contains("FillRect:2,3,10,20,Solid,1,0", api.Calls);
        Assert.Contains("StrokeRect:4,5,12,13,2", api.Calls);
        Assert.Contains("FillRoundedRect:6,7,20,30,3,4", api.Calls);
        Assert.Contains("StrokeEllipse:15,16,7,8,3", api.Calls);
        Assert.Contains("PushLayer:0,0,50,50,0.25", api.Calls);
        Assert.Equal(1, api.FlushCount);
        Assert.Equal(1, api.PresentCount);
    }

    [Fact]
    public void EmptyDirtyRectListDoesNotStartOrPresentFrame()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);

        context.Present([]);

        Assert.Equal(0, api.BeginFrameCount);
        Assert.Equal(0, api.PresentCount);
    }

    [Fact]
    public void GeometryClipsMapToNativeApi()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);
        var path = PathGeometry.Create()
            .MoveTo(new Point(1, 2))
            .LineTo(new Point(8, 2))
            .LineTo(new Point(4, 9))
            .Close();

        context.PushClip(new RoundedRectGeometry(new Rect(2, 3, 40, 30), 6, 7));
        context.PopClip();
        context.PushClip(new EllipseGeometry(new Point(20, 30), 10, 12));
        context.PopClip();
        context.PushClip(path);
        context.PopClip();

        Assert.Contains("PushClipRoundedRect:2,3,40,30,6,7", api.Calls);
        Assert.Contains("PushClipEllipse:20,30,10,12", api.Calls);
        Assert.Contains("PushClipPath:4", api.Calls);
        Assert.Equal(3, api.Calls.Count(call => call == "PopClip"));
    }

    [Fact]
    public void GradientAndStrokeStyleMapToNativeApi()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);

        context.FillRect(
            new Rect(0, 0, 10, 10),
            new LinearGradientBrush(
                Point.Zero,
                new Point(10, 0),
                new GradientStop(0, Color.Red),
                new GradientStop(1, Color.Blue)) { SpreadMethod = GradientSpreadMethod.Reflect });
        context.FillGeometry(
            new EllipseGeometry(new Point(20, 20), 8, 8),
            new RadialGradientBrush(
                new Point(20, 20),
                8,
                new GradientStop(0, Color.White),
                new GradientStop(1, Color.Green)));
        context.DrawPath(
            PathGeometry.Create().MoveTo(Point.Zero).LineTo(new Point(20, 20)),
            new Pen(new SolidColorBrush(Color.White), 4, new StrokeStyle
            {
                Cap = LineCap.Round,
                Join = LineJoin.Bevel,
                MiterLimit = 6
            }));

        Assert.Contains("FillRect:0,0,10,10,LinearGradient,2,2", api.Calls);
        Assert.Contains("FillEllipse:20,20,8,8,RadialGradient,2", api.Calls);
        Assert.Contains("StrokePath:2,4,Solid,1,2,6", api.Calls);
    }

    [Fact]
    public void DashedStrokeFailsBeforeNativeDrawCall()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);
        var pen = new Pen(new SolidColorBrush(Color.White), 2, new StrokeStyle { DashArray = [4, 2] });

        Assert.Throws<NotSupportedException>(() => context.DrawRect(new Rect(0, 0, 10, 10), pen));

        Assert.DoesNotContain(api.Calls, call => call.StartsWith("StrokeRect:", StringComparison.Ordinal));
    }

    [Fact]
    public void ResizeAndDisposeReleaseNativeResources()
    {
        var api = new FakeImpellerApi();
        var context = CreateContext(api);

        context.Resize(new Size(200, 100), 2f);
        context.Dispose();
        context.Dispose();

        Assert.Equal((uint)400, api.ResizedWidth);
        Assert.Equal((uint)200, api.ResizedHeight);
        Assert.Equal(2f, api.ResizedDpiScale);
        Assert.Equal(1, api.DestroyCount);
        Assert.Equal(1, api.DisposeCount);
    }

    [Fact]
    public void PathAndBitmapCommandsMapToNativeApi()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);
        var path = PathGeometry.Create()
            .MoveTo(new Point(2, 3))
            .LineTo(new Point(20, 4))
            .ArcTo(new Rect(5, 6, 30, 40), 10, 80)
            .Close();
        using var bitmap = new Bitmap(4, 3);

        context.FillPath(path, new SolidColorBrush(Color.Red));
        context.DrawPath(path, Pen.FromColor(Color.Blue, 2));
        context.DrawImage(bitmap, new Rect(10, 20, 40, 30), new Rect(1, 1, 2, 2));

        Assert.Contains("FillPath:4", api.Calls);
        Assert.Contains("StrokePath:4,2,Solid,0,0,10", api.Calls);
        Assert.Contains("DrawBitmap:4,3,1,1,2,2,10,20,40,30", api.Calls);
    }

    [Fact]
    public void TextCommandMapsTypographyProperties()
    {
        var api = new FakeImpellerApi();
        using var context = CreateContext(api);
        var layout = new TextLayout("Impeller text", new Font("Segoe UI", 24, FontWeight.Bold, FontStyle.Italic))
        {
            MaxSize = new Size(280, 80),
            Alignment = TextAlignment.Center,
            LineHeight = 1.4f
        };

        context.DrawText(layout, new Point(12, 18), new SolidColorBrush(Color.White));

        Assert.Contains("DrawText:Impeller text,Segoe UI,24,700,True,1,1.4,280,12,18", api.Calls);
    }

    private static ImpellerRenderContext CreateContext(
        FakeImpellerApi api,
        Size? size = null,
        float dpiScale = 1f) => new(api, new RenderContextCreateInfo
        {
            CanvasSize = size ?? new Size(100, 100),
            DpiScale = dpiScale,
            NativeTarget = new Win32VulkanRenderTarget(new IntPtr(1), new IntPtr(2))
        });

    private sealed class FakeImpellerApi : IImpellerApi
    {
        public List<string> Calls { get; } = [];
        public uint CreatedWidth { get; private set; }
        public uint CreatedHeight { get; private set; }
        public float CreatedDpiScale { get; private set; }
        public IntPtr CreatedWindow { get; private set; }
        public IntPtr CreatedInstance { get; private set; }
        public uint ResizedWidth { get; private set; }
        public uint ResizedHeight { get; private set; }
        public float ResizedDpiScale { get; private set; }
        public int BeginFrameCount { get; private set; }
        public int FlushCount { get; private set; }
        public int PresentCount { get; private set; }
        public int DestroyCount { get; private set; }
        public int DisposeCount { get; private set; }

        public string ReadLastError() => "fake error";

        public int CreateWin32(IntPtr window, IntPtr instance, uint width, uint height, float dpiScale, bool vsync, out IntPtr context)
        {
            CreatedWindow = window;
            CreatedInstance = instance;
            CreatedWidth = width;
            CreatedHeight = height;
            CreatedDpiScale = dpiScale;
            context = new IntPtr(42);
            return 0;
        }

        public int CreateX11(IntPtr display, nuint window, int screen, uint width, uint height, float dpiScale, bool vsync, out IntPtr context)
        {
            context = new IntPtr(42);
            return 0;
        }

        public void DestroyContext(IntPtr context) => DestroyCount++;

        public int ResizeContext(IntPtr context, uint width, uint height, float dpiScale)
        {
            ResizedWidth = width;
            ResizedHeight = height;
            ResizedDpiScale = dpiScale;
            return 0;
        }

        public int BeginFrame(IntPtr context) { BeginFrameCount++; return 0; }
        public int Clear(IntPtr context, float red, float green, float blue, float alpha) { Calls.Add("Clear"); return 0; }
        public int ClearRect(IntPtr context, float x, float y, float width, float height, float red, float green, float blue, float alpha) { Calls.Add($"ClearRect:{x},{y},{width},{height}"); return 0; }
        public int PushTransform(IntPtr context, float m11, float m12, float m21, float m22, float m31, float m32) { Calls.Add($"PushTransform:{m11},{m12},{m21},{m22},{m31},{m32}"); return 0; }
        public int PopTransform(IntPtr context) { Calls.Add("PopTransform"); return 0; }
        public int PushClipRect(IntPtr context, float x, float y, float width, float height) { Calls.Add($"PushClipRect:{x},{y},{width},{height}"); return 0; }
        public int PushClipRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY) { Calls.Add($"PushClipRoundedRect:{x},{y},{width},{height},{radiusX},{radiusY}"); return 0; }
        public int PushClipEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY) { Calls.Add($"PushClipEllipse:{centerX},{centerY},{radiusX},{radiusY}"); return 0; }
        public int PushClipPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands) { Calls.Add($"PushClipPath:{commands.Count}"); return 0; }
        public int PopClip(IntPtr context) { Calls.Add("PopClip"); return 0; }
        public int FillRect(IntPtr context, float x, float y, float width, float height, ImpellerBrush brush) { Calls.Add($"FillRect:{x},{y},{width},{height},{brush.Kind},{brush.Stops.Count},{brush.TileMode}"); return 0; }
        public int StrokeRect(IntPtr context, float x, float y, float width, float height, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style) { Calls.Add($"StrokeRect:{x},{y},{width},{height},{strokeWidth}"); return 0; }
        public int FillRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, ImpellerBrush brush) { Calls.Add($"FillRoundedRect:{x},{y},{width},{height},{radiusX},{radiusY}"); return 0; }
        public int StrokeRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style) { Calls.Add($"StrokeRoundedRect:{x},{y},{width},{height},{radiusX},{radiusY},{strokeWidth}"); return 0; }
        public int FillEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, ImpellerBrush brush) { Calls.Add($"FillEllipse:{centerX},{centerY},{radiusX},{radiusY},{brush.Kind},{brush.Stops.Count}"); return 0; }
        public int StrokeEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style) { Calls.Add($"StrokeEllipse:{centerX},{centerY},{radiusX},{radiusY},{strokeWidth}"); return 0; }
        public int FillPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, ImpellerBrush brush) { Calls.Add($"FillPath:{commands.Count}"); return 0; }
        public int StrokePath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style) { Calls.Add($"StrokePath:{commands.Count},{strokeWidth},{brush.Kind},{style.Cap},{style.Join},{style.MiterLimit}"); return 0; }
        public int DrawBitmap(IntPtr context, object cacheKey, int width, int height, byte[] bgraPixels, float sourceX, float sourceY, float sourceWidth, float sourceHeight, float destinationX, float destinationY, float destinationWidth, float destinationHeight) { Calls.Add($"DrawBitmap:{width},{height},{sourceX},{sourceY},{sourceWidth},{sourceHeight},{destinationX},{destinationY},{destinationWidth},{destinationHeight}"); return 0; }
        public int DrawText(IntPtr context, string text, string fontFamily, float fontSize, int fontWeight, bool italic, int alignment, float lineHeight, float maxWidth, float x, float y, float red, float green, float blue, float alpha) { Calls.Add($"DrawText:{text},{fontFamily},{fontSize},{fontWeight},{italic},{alignment},{lineHeight},{maxWidth},{x},{y}"); return 0; }
        public int PushLayer(IntPtr context, float x, float y, float width, float height, float opacity) { Calls.Add($"PushLayer:{x},{y},{width},{height},{opacity}"); return 0; }
        public int PopLayer(IntPtr context) { Calls.Add("PopLayer"); return 0; }
        public int Flush(IntPtr context) { FlushCount++; return 0; }
        public int Present(IntPtr context) { PresentCount++; return 0; }
        public void Dispose() => DisposeCount++;
    }
}
