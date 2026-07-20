using System.Runtime.InteropServices;

namespace Square.Backends.Impeller;

internal sealed class ImpellerNative : IImpellerApi
{
    internal const string LibraryEnvironmentVariable = "SQUARE_IMPELLER_LIBRARY";

    private readonly IntPtr _library;
    private readonly IntPtr _vulkanLibrary;
    private readonly VulkanProcAddressCallback _vulkanProcAddressCallback;
    private readonly IntPtr _vulkanProcAddressCallbackPointer;
    private static readonly MappingReleaseDelegate MappingReleaseCallback = ReleaseMapping;
    private static readonly IntPtr MappingReleaseCallbackPointer = Marshal.GetFunctionPointerForDelegate(MappingReleaseCallback);
    private readonly Dictionary<IntPtr, ContextState> _states = [];
    private long _nextStateId;
    private string _lastError = "No native diagnostic was provided.";
    private bool _disposed;

    private GetVersionDelegate GetVersion { get; }
    private ContextCreateVulkanDelegate ContextCreateVulkan { get; }
    private ContextGetVulkanInfoDelegate ContextGetVulkanInfo { get; }
    private HandleOperationDelegate ContextRelease { get; }
    private SwapchainCreateDelegate SwapchainCreate { get; }
    private HandleOperationDelegate SwapchainRelease { get; }
    private AcquireSurfaceDelegate AcquireSurface { get; }
    private SurfaceDrawDisplayListDelegate SurfaceDrawDisplayList { get; }
    private SurfacePresentDelegate SurfacePresent { get; }
    private HandleOperationDelegate SurfaceRelease { get; }
    private BuilderCreateDelegate BuilderCreate { get; }
    private HandleOperationDelegate BuilderRelease { get; }
    private BuilderCreateDisplayListDelegate BuilderCreateDisplayList { get; }
    private HandleOperationDelegate DisplayListRelease { get; }
    private BuilderOperationDelegate BuilderSave { get; }
    private BuilderOperationDelegate BuilderRestore { get; }
    private BuilderSaveLayerDelegate BuilderSaveLayer { get; }
    private BuilderTransformDelegate BuilderTransform { get; }
    private BuilderClipRectDelegate BuilderClipRect { get; }
    private BuilderClipRectDelegate BuilderClipOval { get; }
    private BuilderClipRoundedRectDelegate BuilderClipRoundedRect { get; }
    private BuilderClipPathDelegate BuilderClipPath { get; }
    private BuilderDrawShapeDelegate BuilderDrawRect { get; }
    private BuilderDrawShapeDelegate BuilderDrawOval { get; }
    private BuilderDrawRoundedRectDelegate BuilderDrawRoundedRect { get; }
    private BuilderDrawPathDelegate BuilderDrawPath { get; }
    private BuilderDrawTextureRectDelegate BuilderDrawTextureRect { get; }
    private BuilderDrawParagraphDelegate BuilderDrawParagraph { get; }
    private BuilderDrawPaintDelegate BuilderDrawPaint { get; }
    private PaintCreateDelegate PaintCreate { get; }
    private HandleOperationDelegate PaintRelease { get; }
    private PaintSetColorDelegate PaintSetColor { get; }
    private PaintSetIntDelegate PaintSetBlendMode { get; }
    private PaintSetIntDelegate PaintSetDrawStyle { get; }
    private PaintSetFloatDelegate PaintSetStrokeWidth { get; }
    private PaintSetIntDelegate PaintSetStrokeCap { get; }
    private PaintSetIntDelegate PaintSetStrokeJoin { get; }
    private PaintSetFloatDelegate PaintSetStrokeMiter { get; }
    private PaintSetHandleDelegate PaintSetColorSource { get; }
    private ColorSourceCreateLinearGradientDelegate ColorSourceCreateLinearGradient { get; }
    private ColorSourceCreateRadialGradientDelegate ColorSourceCreateRadialGradient { get; }
    private HandleOperationDelegate ColorSourceRelease { get; }
    private PathBuilderCreateDelegate PathBuilderCreate { get; }
    private HandleOperationDelegate PathBuilderRelease { get; }
    private PathBuilderPointDelegate PathBuilderMoveTo { get; }
    private PathBuilderPointDelegate PathBuilderLineTo { get; }
    private PathBuilderArcDelegate PathBuilderAddArc { get; }
    private HandleOperationDelegate PathBuilderClose { get; }
    private PathBuilderTakePathDelegate PathBuilderTakePath { get; }
    private HandleOperationDelegate PathRelease { get; }
    private TextureCreateDelegate TextureCreate { get; }
    private HandleOperationDelegate TextureRelease { get; }
    private TypographyCreateDelegate TypographyCreate { get; }
    private HandleOperationDelegate TypographyRelease { get; }
    private TypographyRegisterFontDelegate TypographyRegisterFont { get; }
    private ParagraphStyleCreateDelegate ParagraphStyleCreate { get; }
    private HandleOperationDelegate ParagraphStyleRelease { get; }
    private ParagraphStyleSetHandleDelegate ParagraphStyleSetForeground { get; }
    private ParagraphStyleSetIntDelegate ParagraphStyleSetFontWeight { get; }
    private ParagraphStyleSetIntDelegate ParagraphStyleSetFontStyle { get; }
    private ParagraphStyleSetStringDelegate ParagraphStyleSetFontFamily { get; }
    private ParagraphStyleSetFloatDelegate ParagraphStyleSetFontSize { get; }
    private ParagraphStyleSetFloatDelegate ParagraphStyleSetHeight { get; }
    private ParagraphStyleSetIntDelegate ParagraphStyleSetTextAlignment { get; }
    private ParagraphStyleSetIntDelegate ParagraphStyleSetTextDirection { get; }
    private ParagraphBuilderCreateDelegate ParagraphBuilderCreate { get; }
    private HandleOperationDelegate ParagraphBuilderRelease { get; }
    private ParagraphBuilderPushStyleDelegate ParagraphBuilderPushStyle { get; }
    private ParagraphBuilderAddTextDelegate ParagraphBuilderAddText { get; }
    private ParagraphBuilderBuildDelegate ParagraphBuilderBuild { get; }
    private HandleOperationDelegate ParagraphRelease { get; }

    private ImpellerNative(IntPtr library, IntPtr vulkanLibrary)
    {
        _library = library;
        _vulkanLibrary = vulkanLibrary;
        _vulkanProcAddressCallback = ResolveVulkanProcAddress;
        _vulkanProcAddressCallbackPointer = Marshal.GetFunctionPointerForDelegate(_vulkanProcAddressCallback);

        GetVersion = GetExport<GetVersionDelegate>("ImpellerGetVersion");
        ContextCreateVulkan = GetExport<ContextCreateVulkanDelegate>("ImpellerContextCreateVulkanNew");
        ContextGetVulkanInfo = GetExport<ContextGetVulkanInfoDelegate>("ImpellerContextGetVulkanInfo");
        ContextRelease = GetExport<HandleOperationDelegate>("ImpellerContextRelease");
        SwapchainCreate = GetExport<SwapchainCreateDelegate>("ImpellerVulkanSwapchainCreateNew");
        SwapchainRelease = GetExport<HandleOperationDelegate>("ImpellerVulkanSwapchainRelease");
        AcquireSurface = GetExport<AcquireSurfaceDelegate>("ImpellerVulkanSwapchainAcquireNextSurfaceNew");
        SurfaceDrawDisplayList = GetExport<SurfaceDrawDisplayListDelegate>("ImpellerSurfaceDrawDisplayList");
        SurfacePresent = GetExport<SurfacePresentDelegate>("ImpellerSurfacePresent");
        SurfaceRelease = GetExport<HandleOperationDelegate>("ImpellerSurfaceRelease");
        BuilderCreate = GetExport<BuilderCreateDelegate>("ImpellerDisplayListBuilderNew");
        BuilderRelease = GetExport<HandleOperationDelegate>("ImpellerDisplayListBuilderRelease");
        BuilderCreateDisplayList = GetExport<BuilderCreateDisplayListDelegate>("ImpellerDisplayListBuilderCreateDisplayListNew");
        DisplayListRelease = GetExport<HandleOperationDelegate>("ImpellerDisplayListRelease");
        BuilderSave = GetExport<BuilderOperationDelegate>("ImpellerDisplayListBuilderSave");
        BuilderRestore = GetExport<BuilderOperationDelegate>("ImpellerDisplayListBuilderRestore");
        BuilderSaveLayer = GetExport<BuilderSaveLayerDelegate>("ImpellerDisplayListBuilderSaveLayer");
        BuilderTransform = GetExport<BuilderTransformDelegate>("ImpellerDisplayListBuilderTransform");
        BuilderClipRect = GetExport<BuilderClipRectDelegate>("ImpellerDisplayListBuilderClipRect");
        BuilderClipOval = GetExport<BuilderClipRectDelegate>("ImpellerDisplayListBuilderClipOval");
        BuilderClipRoundedRect = GetExport<BuilderClipRoundedRectDelegate>("ImpellerDisplayListBuilderClipRoundedRect");
        BuilderClipPath = GetExport<BuilderClipPathDelegate>("ImpellerDisplayListBuilderClipPath");
        BuilderDrawRect = GetExport<BuilderDrawShapeDelegate>("ImpellerDisplayListBuilderDrawRect");
        BuilderDrawOval = GetExport<BuilderDrawShapeDelegate>("ImpellerDisplayListBuilderDrawOval");
        BuilderDrawRoundedRect = GetExport<BuilderDrawRoundedRectDelegate>("ImpellerDisplayListBuilderDrawRoundedRect");
        BuilderDrawPath = GetExport<BuilderDrawPathDelegate>("ImpellerDisplayListBuilderDrawPath");
        BuilderDrawTextureRect = GetExport<BuilderDrawTextureRectDelegate>("ImpellerDisplayListBuilderDrawTextureRect");
        BuilderDrawParagraph = GetExport<BuilderDrawParagraphDelegate>("ImpellerDisplayListBuilderDrawParagraph");
        BuilderDrawPaint = GetExport<BuilderDrawPaintDelegate>("ImpellerDisplayListBuilderDrawPaint");
        PaintCreate = GetExport<PaintCreateDelegate>("ImpellerPaintNew");
        PaintRelease = GetExport<HandleOperationDelegate>("ImpellerPaintRelease");
        PaintSetColor = GetExport<PaintSetColorDelegate>("ImpellerPaintSetColor");
        PaintSetBlendMode = GetExport<PaintSetIntDelegate>("ImpellerPaintSetBlendMode");
        PaintSetDrawStyle = GetExport<PaintSetIntDelegate>("ImpellerPaintSetDrawStyle");
        PaintSetStrokeWidth = GetExport<PaintSetFloatDelegate>("ImpellerPaintSetStrokeWidth");
        PaintSetStrokeCap = GetExport<PaintSetIntDelegate>("ImpellerPaintSetStrokeCap");
        PaintSetStrokeJoin = GetExport<PaintSetIntDelegate>("ImpellerPaintSetStrokeJoin");
        PaintSetStrokeMiter = GetExport<PaintSetFloatDelegate>("ImpellerPaintSetStrokeMiter");
        PaintSetColorSource = GetExport<PaintSetHandleDelegate>("ImpellerPaintSetColorSource");
        ColorSourceCreateLinearGradient = GetExport<ColorSourceCreateLinearGradientDelegate>("ImpellerColorSourceCreateLinearGradientNew");
        ColorSourceCreateRadialGradient = GetExport<ColorSourceCreateRadialGradientDelegate>("ImpellerColorSourceCreateRadialGradientNew");
        ColorSourceRelease = GetExport<HandleOperationDelegate>("ImpellerColorSourceRelease");
        PathBuilderCreate = GetExport<PathBuilderCreateDelegate>("ImpellerPathBuilderNew");
        PathBuilderRelease = GetExport<HandleOperationDelegate>("ImpellerPathBuilderRelease");
        PathBuilderMoveTo = GetExport<PathBuilderPointDelegate>("ImpellerPathBuilderMoveTo");
        PathBuilderLineTo = GetExport<PathBuilderPointDelegate>("ImpellerPathBuilderLineTo");
        PathBuilderAddArc = GetExport<PathBuilderArcDelegate>("ImpellerPathBuilderAddArc");
        PathBuilderClose = GetExport<HandleOperationDelegate>("ImpellerPathBuilderClose");
        PathBuilderTakePath = GetExport<PathBuilderTakePathDelegate>("ImpellerPathBuilderTakePathNew");
        PathRelease = GetExport<HandleOperationDelegate>("ImpellerPathRelease");
        TextureCreate = GetExport<TextureCreateDelegate>("ImpellerTextureCreateWithContentsNew");
        TextureRelease = GetExport<HandleOperationDelegate>("ImpellerTextureRelease");
        TypographyCreate = GetExport<TypographyCreateDelegate>("ImpellerTypographyContextNew");
        TypographyRelease = GetExport<HandleOperationDelegate>("ImpellerTypographyContextRelease");
        TypographyRegisterFont = GetExport<TypographyRegisterFontDelegate>("ImpellerTypographyContextRegisterFont");
        ParagraphStyleCreate = GetExport<ParagraphStyleCreateDelegate>("ImpellerParagraphStyleNew");
        ParagraphStyleRelease = GetExport<HandleOperationDelegate>("ImpellerParagraphStyleRelease");
        ParagraphStyleSetForeground = GetExport<ParagraphStyleSetHandleDelegate>("ImpellerParagraphStyleSetForeground");
        ParagraphStyleSetFontWeight = GetExport<ParagraphStyleSetIntDelegate>("ImpellerParagraphStyleSetFontWeight");
        ParagraphStyleSetFontStyle = GetExport<ParagraphStyleSetIntDelegate>("ImpellerParagraphStyleSetFontStyle");
        ParagraphStyleSetFontFamily = GetExport<ParagraphStyleSetStringDelegate>("ImpellerParagraphStyleSetFontFamily");
        ParagraphStyleSetFontSize = GetExport<ParagraphStyleSetFloatDelegate>("ImpellerParagraphStyleSetFontSize");
        ParagraphStyleSetHeight = GetExport<ParagraphStyleSetFloatDelegate>("ImpellerParagraphStyleSetHeight");
        ParagraphStyleSetTextAlignment = GetExport<ParagraphStyleSetIntDelegate>("ImpellerParagraphStyleSetTextAlignment");
        ParagraphStyleSetTextDirection = GetExport<ParagraphStyleSetIntDelegate>("ImpellerParagraphStyleSetTextDirection");
        ParagraphBuilderCreate = GetExport<ParagraphBuilderCreateDelegate>("ImpellerParagraphBuilderNew");
        ParagraphBuilderRelease = GetExport<HandleOperationDelegate>("ImpellerParagraphBuilderRelease");
        ParagraphBuilderPushStyle = GetExport<ParagraphBuilderPushStyleDelegate>("ImpellerParagraphBuilderPushStyle");
        ParagraphBuilderAddText = GetExport<ParagraphBuilderAddTextDelegate>("ImpellerParagraphBuilderAddText");
        ParagraphBuilderBuild = GetExport<ParagraphBuilderBuildDelegate>("ImpellerParagraphBuilderBuildParagraphNew");
        ParagraphRelease = GetExport<HandleOperationDelegate>("ImpellerParagraphRelease");
    }

    internal static ImpellerNative Load(string? configuredPath)
    {
        var path = ResolveLibraryPath(configuredPath);
        IntPtr library = IntPtr.Zero;
        IntPtr vulkanLibrary = IntPtr.Zero;
        try
        {
            library = NativeLibrary.Load(path);
            vulkanLibrary = LoadVulkanLibrary();
            return new ImpellerNative(library, vulkanLibrary);
        }
        catch (ImpellerException)
        {
            if (vulkanLibrary != IntPtr.Zero) NativeLibrary.Free(vulkanLibrary);
            if (library != IntPtr.Zero) NativeLibrary.Free(library);
            throw;
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            if (vulkanLibrary != IntPtr.Zero) NativeLibrary.Free(vulkanLibrary);
            if (library != IntPtr.Zero) NativeLibrary.Free(library);
            throw new ImpellerException($"Unable to load Impeller native library '{path}': {exception.Message}", exception);
        }
    }

    internal static string ResolveLibraryPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;
        var environmentPath = Environment.GetEnvironmentVariable(LibraryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath)) return environmentPath;
        if (OperatingSystem.IsWindows()) return "impeller.dll";
        if (OperatingSystem.IsLinux()) return "libimpeller.so";
        throw new PlatformNotSupportedException("The Impeller backend currently supports Windows and Linux only.");
    }

    public string ReadLastError() => _lastError;

    public int CreateWin32(IntPtr window, IntPtr instance, uint width, uint height, float dpiScale, bool vsync, out IntPtr context)
        => CreateContext(new VulkanTarget(window, instance, IntPtr.Zero, 0, false), width, height, dpiScale, out context);

    public int CreateX11(IntPtr display, nuint window, int screen, uint width, uint height, float dpiScale, bool vsync, out IntPtr context)
        => CreateContext(new VulkanTarget((IntPtr)window, IntPtr.Zero, display, screen, true), width, height, dpiScale, out context);

    public void DestroyContext(IntPtr context)
    {
        if (!_states.Remove(context, out var state)) return;
        if (state.Builder != IntPtr.Zero) BuilderRelease(state.Builder);
        foreach (var texture in state.Textures.Values) TextureRelease(texture);
        if (state.Typography != IntPtr.Zero) TypographyRelease(state.Typography);
        if (state.Swapchain != IntPtr.Zero) SwapchainRelease(state.Swapchain);
        if (state.Context != IntPtr.Zero) ContextRelease(state.Context);
    }

    public int ResizeContext(IntPtr context, uint width, uint height, float dpiScale)
    {
        if (!TryGetState(context, out var state)) return Fail("Invalid Impeller context.");
        state.Width = width;
        state.Height = height;
        state.DpiScale = dpiScale;
        return 0;
    }

    public int BeginFrame(IntPtr context)
    {
        if (!TryGetState(context, out var state)) return Fail("Invalid Impeller context.");
        if (state.Builder != IntPtr.Zero) BuilderRelease(state.Builder);
        state.Builder = BuilderCreate(IntPtr.Zero);
        if (state.Builder == IntPtr.Zero) return Fail("ImpellerDisplayListBuilderNew returned null.");
        if (state.DpiScale != 1f)
        {
            var scale = ImpellerMatrix.CreateScale(state.DpiScale);
            BuilderTransform(state.Builder, ref scale);
        }
        return 0;
    }

    public int Clear(IntPtr context, float red, float green, float blue, float alpha)
        => DrawPaint(context, red, green, blue, alpha, blendMode: 1);

    public int ClearRect(IntPtr context, float x, float y, float width, float height, float red, float green, float blue, float alpha)
        => DrawRect(
            context,
            new ImpellerRect(x, y, width, height),
            new ImpellerBrush(ImpellerBrushKind.Solid, 0, 0, 0, 0, 0, 0, [new ImpellerGradientStop(0, red, green, blue, alpha)]),
            0,
            default,
            blendMode: 1);

    public int PushTransform(IntPtr context, float m11, float m12, float m21, float m22, float m31, float m32)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        BuilderSave(builder);
        var matrix = ImpellerMatrix.From2D(m11, m12, m21, m22, m31, m32);
        BuilderTransform(builder, ref matrix);
        return 0;
    }

    public int PopTransform(IntPtr context) => Restore(context);

    public int PushClipRect(IntPtr context, float x, float y, float width, float height)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        BuilderSave(builder);
        var rect = new ImpellerRect(x, y, width, height);
        BuilderClipRect(builder, ref rect, 1);
        return 0;
    }

    public int PopClip(IntPtr context) => Restore(context);

    public int PushClipRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        BuilderSave(builder);
        var rect = new ImpellerRect(x, y, width, height);
        var radii = ImpellerRoundingRadii.Uniform(radiusX, radiusY);
        BuilderClipRoundedRect(builder, ref rect, ref radii, 1);
        return 0;
    }

    public int PushClipEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        BuilderSave(builder);
        var bounds = new ImpellerRect(centerX - radiusX, centerY - radiusY, radiusX * 2, radiusY * 2);
        BuilderClipOval(builder, ref bounds, 1);
        return 0;
    }

    public int PushClipPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var path = CreatePath(commands);
        if (path == IntPtr.Zero) return 1;
        try
        {
            BuilderSave(builder);
            BuilderClipPath(builder, path, 1);
            return 0;
        }
        finally { PathRelease(path); }
    }

    public int FillRect(IntPtr context, float x, float y, float width, float height, ImpellerBrush brush)
        => DrawRect(context, new ImpellerRect(x, y, width, height), brush, 0, default);

    public int StrokeRect(IntPtr context, float x, float y, float width, float height, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style)
        => DrawRect(context, new ImpellerRect(x, y, width, height), brush, strokeWidth, style);

    public int FillRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, ImpellerBrush brush)
        => DrawRoundedRect(context, new ImpellerRect(x, y, width, height), radiusX, radiusY, brush, 0, default);

    public int StrokeRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style)
        => DrawRoundedRect(context, new ImpellerRect(x, y, width, height), radiusX, radiusY, brush, strokeWidth, style);

    public int FillEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, ImpellerBrush brush)
        => DrawOval(context, new ImpellerRect(centerX - radiusX, centerY - radiusY, radiusX * 2, radiusY * 2), brush, 0, default);

    public int StrokeEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style)
        => DrawOval(context, new ImpellerRect(centerX - radiusX, centerY - radiusY, radiusX * 2, radiusY * 2), brush, strokeWidth, style);

    public int FillPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, ImpellerBrush brush)
        => DrawPath(context, commands, 0, brush, default);

    public int StrokePath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style)
        => DrawPath(context, commands, strokeWidth, brush, style);

    public int DrawBitmap(IntPtr context, object cacheKey, int width, int height, byte[] bgraPixels, float sourceX, float sourceY, float sourceWidth, float sourceHeight, float destinationX, float destinationY, float destinationWidth, float destinationHeight)
    {
        if (!TryGetState(context, out var state) || !TryGetBuilder(context, out var builder)) return 1;
        if (!state.Textures.TryGetValue(cacheKey, out var texture))
        {
            texture = CreateTexture(state.Context, width, height, bgraPixels);
            if (texture == IntPtr.Zero) return 1;
            state.Textures.Add(cacheKey, texture);
        }

        var source = new ImpellerRect(sourceX, sourceY, sourceWidth, sourceHeight);
        var destination = new ImpellerRect(destinationX, destinationY, destinationWidth, destinationHeight);
        BuilderDrawTextureRect(builder, texture, ref source, ref destination, 1, IntPtr.Zero);
        return 0;
    }

    public int DrawText(IntPtr context, string text, string fontFamily, float fontSize, int fontWeight, bool italic, int alignment, float lineHeight, float maxWidth, float x, float y, float red, float green, float blue, float alpha)
    {
        if (!TryGetState(context, out var state) || !TryGetBuilder(context, out var displayListBuilder)) return 1;
        var resolvedFamily = ResolveFontFamily(fontFamily);
        if (!EnsureFont(state, resolvedFamily, fontWeight, italic)) return 1;
        EnsureFallbackFonts(state);

        var paint = CreatePaint(red, green, blue, alpha, 0);
        var style = ParagraphStyleCreate();
        var paragraphBuilder = IntPtr.Zero;
        var paragraph = IntPtr.Zero;
        if (paint == IntPtr.Zero || style == IntPtr.Zero)
        {
            if (paint != IntPtr.Zero) PaintRelease(paint);
            if (style != IntPtr.Zero) ParagraphStyleRelease(style);
            return Fail("Unable to create Impeller text paint or paragraph style.");
        }

        var familyPointer = Marshal.StringToCoTaskMemUTF8(resolvedFamily);
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        var textHandle = GCHandle.Alloc(textBytes, GCHandleType.Pinned);
        try
        {
            ParagraphStyleSetForeground(style, paint);
            ParagraphStyleSetFontWeight(style, WeightToImpeller(fontWeight));
            ParagraphStyleSetFontStyle(style, italic ? 1 : 0);
            ParagraphStyleSetFontFamily(style, familyPointer);
            ParagraphStyleSetFontSize(style, fontSize);
            ParagraphStyleSetHeight(style, lineHeight);
            ParagraphStyleSetTextAlignment(style, AlignmentToImpeller(alignment));
            ParagraphStyleSetTextDirection(style, 1);

            paragraphBuilder = ParagraphBuilderCreate(state.Typography);
            if (paragraphBuilder == IntPtr.Zero) return Fail("ImpellerParagraphBuilderNew returned null.");
            ParagraphBuilderPushStyle(paragraphBuilder, style);
            ParagraphBuilderAddText(paragraphBuilder, textHandle.AddrOfPinnedObject(), (uint)textBytes.Length);
            paragraph = ParagraphBuilderBuild(paragraphBuilder, maxWidth);
            if (paragraph == IntPtr.Zero) return Fail("ImpellerParagraphBuilderBuildParagraphNew returned null.");

            var origin = new ImpellerPoint(x, y);
            BuilderDrawParagraph(displayListBuilder, paragraph, ref origin);
            return 0;
        }
        finally
        {
            if (paragraph != IntPtr.Zero) ParagraphRelease(paragraph);
            if (paragraphBuilder != IntPtr.Zero) ParagraphBuilderRelease(paragraphBuilder);
            textHandle.Free();
            Marshal.FreeCoTaskMem(familyPointer);
            ParagraphStyleRelease(style);
            PaintRelease(paint);
        }
    }

    public int PushLayer(IntPtr context, float x, float y, float width, float height, float opacity)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var paint = CreatePaint(1, 1, 1, opacity, 0);
        if (paint == IntPtr.Zero) return Fail("ImpellerPaintNew returned null.");
        try
        {
            var bounds = new ImpellerRect(x, y, width, height);
            BuilderSaveLayer(builder, ref bounds, paint, IntPtr.Zero);
            return 0;
        }
        finally
        {
            PaintRelease(paint);
        }
    }

    public int PopLayer(IntPtr context) => Restore(context);
    public int Flush(IntPtr context) => TryGetBuilder(context, out _) ? 0 : 1;

    public int Present(IntPtr context)
    {
        if (!TryGetState(context, out var state) || state.Builder == IntPtr.Zero) return Fail("No active Impeller frame.");
        var displayList = BuilderCreateDisplayList(state.Builder);
        BuilderRelease(state.Builder);
        state.Builder = IntPtr.Zero;
        if (displayList == IntPtr.Zero) return Fail("Impeller display list creation failed.");
        try
        {
            var surface = AcquireSurface(state.Swapchain);
            if (surface == IntPtr.Zero) return Fail("Impeller swapchain did not provide a surface.");
            try
            {
                if (!SurfaceDrawDisplayList(surface, displayList)) return Fail("ImpellerSurfaceDrawDisplayList failed.");
                if (!SurfacePresent(surface)) return Fail("ImpellerSurfacePresent failed.");
                return 0;
            }
            finally
            {
                SurfaceRelease(surface);
            }
        }
        finally
        {
            DisplayListRelease(displayList);
        }
    }

    private int CreateContext(VulkanTarget target, uint width, uint height, float dpiScale, out IntPtr managedContext)
    {
        managedContext = IntPtr.Zero;
        var settings = new ImpellerContextVulkanSettings
        {
            UserData = IntPtr.Zero,
            ProcAddressCallback = _vulkanProcAddressCallbackPointer,
            EnableValidation = false
        };
        var nativeContext = ContextCreateVulkan(GetVersion(), ref settings);
        if (nativeContext == IntPtr.Zero) return Fail("ImpellerContextCreateVulkanNew returned null.");

        IntPtr surface = IntPtr.Zero;
        IntPtr swapchain = IntPtr.Zero;
        try
        {
            if (!ContextGetVulkanInfo(nativeContext, out var info)) return Fail("ImpellerContextGetVulkanInfo failed.");
            surface = target.IsX11
                ? CreateX11Surface(info.Instance, target.Display, (nuint)target.Window)
                : CreateWin32Surface(info.Instance, target.Instance, target.Window);
            if (surface == IntPtr.Zero) return 1;
            swapchain = SwapchainCreate(nativeContext, surface);
            surface = IntPtr.Zero; // Ownership transfers to Impeller.
            if (swapchain == IntPtr.Zero) return Fail("ImpellerVulkanSwapchainCreateNew returned null.");

            var typography = TypographyCreate();
            if (typography == IntPtr.Zero) return Fail("ImpellerTypographyContextNew returned null.");

            managedContext = new IntPtr(Interlocked.Increment(ref _nextStateId));
            _states.Add(managedContext, new ContextState(nativeContext, swapchain, typography, width, height, dpiScale));
            nativeContext = IntPtr.Zero;
            swapchain = IntPtr.Zero;
            return 0;
        }
        finally
        {
            if (surface != IntPtr.Zero) DestroyVulkanSurface(nativeContext, surface);
            if (swapchain != IntPtr.Zero) SwapchainRelease(swapchain);
            if (nativeContext != IntPtr.Zero) ContextRelease(nativeContext);
        }
    }

    private int DrawPaint(IntPtr context, float red, float green, float blue, float alpha, int blendMode)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var paint = CreatePaint(red, green, blue, alpha, 0, blendMode);
        if (paint == IntPtr.Zero) return Fail("ImpellerPaintNew returned null.");
        try { BuilderDrawPaint(builder, paint); return 0; }
        finally { PaintRelease(paint); }
    }

    private int DrawRect(IntPtr context, ImpellerRect rect, ImpellerBrush brush, float strokeWidth, ImpellerStrokeStyle style, int blendMode = 3)
        => DrawShape(context, rect, brush, strokeWidth, style, blendMode, BuilderDrawRect);

    private int DrawOval(IntPtr context, ImpellerRect rect, ImpellerBrush brush, float strokeWidth, ImpellerStrokeStyle style)
        => DrawShape(context, rect, brush, strokeWidth, style, 3, BuilderDrawOval);

    private int DrawShape(IntPtr context, ImpellerRect rect, ImpellerBrush brush, float strokeWidth, ImpellerStrokeStyle style, int blendMode, BuilderDrawShapeDelegate draw)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var paint = CreatePaint(brush, strokeWidth, style, blendMode);
        if (paint == IntPtr.Zero) return Fail("ImpellerPaintNew returned null.");
        try { draw(builder, ref rect, paint); return 0; }
        finally { PaintRelease(paint); }
    }

    private int DrawRoundedRect(IntPtr context, ImpellerRect rect, float radiusX, float radiusY, ImpellerBrush brush, float strokeWidth, ImpellerStrokeStyle style)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var paint = CreatePaint(brush, strokeWidth, style);
        if (paint == IntPtr.Zero) return Fail("ImpellerPaintNew returned null.");
        try
        {
            var radii = ImpellerRoundingRadii.Uniform(radiusX, radiusY);
            BuilderDrawRoundedRect(builder, ref rect, ref radii, paint);
            return 0;
        }
        finally { PaintRelease(paint); }
    }

    private int DrawPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        var path = CreatePath(commands);
        if (path == IntPtr.Zero) return 1;
        try
        {
            var paint = CreatePaint(brush, strokeWidth, style);
            if (paint == IntPtr.Zero) return Fail("ImpellerPaintNew returned null.");
            try { BuilderDrawPath(builder, path, paint); return 0; }
            finally { PaintRelease(paint); }
        }
        finally { PathRelease(path); }
    }

    private IntPtr CreatePath(IReadOnlyList<ImpellerPathCommand> commands)
    {
        var pathBuilder = PathBuilderCreate();
        if (pathBuilder == IntPtr.Zero) { Fail("ImpellerPathBuilderNew returned null."); return IntPtr.Zero; }
        try
        {
            foreach (var command in commands)
            {
                switch (command.Kind)
                {
                    case ImpellerPathCommandKind.MoveTo:
                    case ImpellerPathCommandKind.LineTo:
                        var point = new ImpellerPoint(command.X1, command.Y1);
                        if (command.Kind == ImpellerPathCommandKind.MoveTo) PathBuilderMoveTo(pathBuilder, ref point);
                        else PathBuilderLineTo(pathBuilder, ref point);
                        break;
                    case ImpellerPathCommandKind.ArcTo:
                        var oval = new ImpellerRect(command.X1, command.Y1, command.X2, command.Y2);
                        PathBuilderAddArc(pathBuilder, ref oval, command.X3, command.Y3);
                        break;
                    case ImpellerPathCommandKind.Close:
                        PathBuilderClose(pathBuilder);
                        break;
                }
            }
            var path = PathBuilderTakePath(pathBuilder, 0);
            if (path == IntPtr.Zero) Fail("ImpellerPathBuilderTakePathNew returned null.");
            return path;
        }
        finally { PathBuilderRelease(pathBuilder); }
    }

    private IntPtr CreateTexture(IntPtr context, int width, int height, byte[] bgraPixels)
    {
        var rgba = new byte[checked(width * height * 4)];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = bgraPixels[i + 2];
            rgba[i + 1] = bgraPixels[i + 1];
            rgba[i + 2] = bgraPixels[i];
            rgba[i + 3] = bgraPixels[i + 3];
        }

        var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            var descriptor = new ImpellerTextureDescriptor { PixelFormat = 0, Size = new ImpellerISize(width, height), MipCount = 1 };
            var mapping = new ImpellerMapping { Data = pinned.AddrOfPinnedObject(), Length = (ulong)rgba.Length, OnRelease = IntPtr.Zero };
            var texture = TextureCreate(context, ref descriptor, ref mapping, IntPtr.Zero);
            if (texture == IntPtr.Zero) Fail("ImpellerTextureCreateWithContentsNew returned null.");
            return texture;
        }
        finally { pinned.Free(); }
    }

    private bool EnsureFont(ContextState state, string family, int fontWeight, bool italic)
    {
        var key = $"{family}|{fontWeight}|{italic}";
        if (state.Fonts.Contains(key)) return true;
        var path = FindFontPath(family, fontWeight, italic);
        if (path == null) return Fail($"Unable to locate a system font for '{family}'.") == 0;

        var registration = FontRegistration.Create(File.ReadAllBytes(path));
        var alias = Marshal.StringToCoTaskMemUTF8(family);
        try
        {
            var mapping = new ImpellerMapping
            {
                Data = registration.Pointer,
                Length = (ulong)registration.Length,
                OnRelease = MappingReleaseCallbackPointer
            };
            if (!TypographyRegisterFont(state.Typography, ref mapping, registration.Pointer, alias))
            {
                registration.Dispose();
                Fail($"Impeller failed to register font '{path}'.");
                return false;
            }
            registration.TransferOwnership();
            state.Fonts.Add(key);
            return true;
        }
        finally { Marshal.FreeCoTaskMem(alias); }
    }

    private void EnsureFallbackFonts(ContextState state)
    {
        if (OperatingSystem.IsWindows())
        {
            EnsureFont(state, "Microsoft YaHei", 400, false);
            EnsureFont(state, "Segoe UI Emoji", 400, false);
        }
        else
        {
            EnsureFont(state, "Noto Sans CJK", 400, false);
        }
    }

    private static string ResolveFontFamily(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return OperatingSystem.IsWindows() ? "Segoe UI" : "DejaVu Sans";
        return family.ToLowerInvariant() switch
        {
            "sans-serif" or "system-ui" or "ui-sans-serif" => OperatingSystem.IsWindows() ? "Segoe UI" : "DejaVu Sans",
            "serif" or "ui-serif" => OperatingSystem.IsWindows() ? "Times New Roman" : "DejaVu Serif",
            "monospace" or "ui-monospace" => OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono",
            _ => family
        };
    }

    private static string? FindFontPath(string family, int weight, bool italic)
    {
        IEnumerable<string> candidates;
        if (OperatingSystem.IsWindows())
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            candidates = family.ToLowerInvariant() switch
            {
                "segoe ui" when weight >= 700 && italic => ["segoeuiz.ttf", "segoeuib.ttf", "segoeui.ttf"],
                "segoe ui" when weight >= 700 => ["segoeuib.ttf", "segoeui.ttf"],
                "segoe ui" when italic => ["segoeuii.ttf", "segoeui.ttf"],
                "segoe ui" => ["segoeui.ttf"],
                "consolas" when weight >= 700 => ["consolab.ttf", "consola.ttf"],
                "consolas" => ["consola.ttf"],
                "times new roman" when weight >= 700 => ["timesbd.ttf", "times.ttf"],
                "times new roman" => ["times.ttf"],
                "microsoft yahei" => ["msyh.ttc", "msyh.ttf"],
                "segoe ui emoji" => ["seguiemj.ttf"],
                _ => [$"{family}.ttf", "segoeui.ttf"]
            };
            return candidates.Select(name => Path.Combine(fonts, name)).FirstOrDefault(File.Exists);
        }

        candidates = family.ToLowerInvariant() switch
        {
            "dejavu serif" => ["/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf"],
            "dejavu sans mono" => ["/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"],
            "noto sans cjk" => ["/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc"],
            _ => ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"]
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static int WeightToImpeller(int weight) => Math.Clamp((weight + 50) / 100 - 1, 0, 8);

    private static int AlignmentToImpeller(int alignment) => alignment switch
    {
        1 => 2, // Center
        2 => 1, // Right
        3 => 3, // Justify
        _ => 0  // Left
    };

    private static void ReleaseMapping(IntPtr userData)
    {
        if (userData != IntPtr.Zero) Marshal.FreeHGlobal(userData);
    }

    private IntPtr CreatePaint(float red, float green, float blue, float alpha, float strokeWidth, int blendMode = 3)
    {
        var paint = PaintCreate();
        if (paint == IntPtr.Zero) return IntPtr.Zero;
        var color = new ImpellerColor(red, green, blue, alpha, 0);
        PaintSetColor(paint, ref color);
        PaintSetBlendMode(paint, blendMode);
        PaintSetDrawStyle(paint, strokeWidth > 0 ? 1 : 0);
        if (strokeWidth > 0) PaintSetStrokeWidth(paint, strokeWidth);
        return paint;
    }

    private IntPtr CreatePaint(ImpellerBrush brush, float strokeWidth, ImpellerStrokeStyle style, int blendMode = 3)
    {
        if (brush.Stops.Count == 0) return IntPtr.Zero;
        var first = brush.Stops[0];
        var paint = CreatePaint(first.Red, first.Green, first.Blue, first.Alpha, strokeWidth, blendMode);
        if (paint == IntPtr.Zero) return IntPtr.Zero;

        if (strokeWidth > 0)
        {
            PaintSetStrokeCap(paint, style.Cap);
            PaintSetStrokeJoin(paint, style.Join);
            PaintSetStrokeMiter(paint, style.MiterLimit);
        }

        if (brush.Kind == ImpellerBrushKind.Solid) return paint;
        var colors = brush.Stops.Select(stop => new ImpellerColor(stop.Red, stop.Green, stop.Blue, stop.Alpha, 0)).ToArray();
        var stops = brush.Stops.Select(stop => stop.Offset).ToArray();
        var colorsHandle = GCHandle.Alloc(colors, GCHandleType.Pinned);
        var stopsHandle = GCHandle.Alloc(stops, GCHandleType.Pinned);
        IntPtr colorSource = IntPtr.Zero;
        try
        {
            var point = new ImpellerPoint(brush.X1, brush.Y1);
            if (brush.Kind == ImpellerBrushKind.LinearGradient)
            {
                var end = new ImpellerPoint(brush.X2, brush.Y2);
                colorSource = ColorSourceCreateLinearGradient(
                    ref point, ref end, (uint)colors.Length,
                    colorsHandle.AddrOfPinnedObject(), stopsHandle.AddrOfPinnedObject(), brush.TileMode, IntPtr.Zero);
            }
            else
            {
                colorSource = ColorSourceCreateRadialGradient(
                    ref point, brush.Radius, (uint)colors.Length,
                    colorsHandle.AddrOfPinnedObject(), stopsHandle.AddrOfPinnedObject(), brush.TileMode, IntPtr.Zero);
            }

            if (colorSource == IntPtr.Zero)
            {
                PaintRelease(paint);
                Fail("Impeller gradient color source creation returned null.");
                return IntPtr.Zero;
            }
            PaintSetColorSource(paint, colorSource);
            return paint;
        }
        finally
        {
            if (colorSource != IntPtr.Zero) ColorSourceRelease(colorSource);
            stopsHandle.Free();
            colorsHandle.Free();
        }
    }

    private int Restore(IntPtr context)
    {
        if (!TryGetBuilder(context, out var builder)) return 1;
        BuilderRestore(builder);
        return 0;
    }

    private bool TryGetBuilder(IntPtr context, out IntPtr builder)
    {
        if (TryGetState(context, out var state) && state.Builder != IntPtr.Zero)
        {
            builder = state.Builder;
            return true;
        }
        builder = IntPtr.Zero;
        Fail("No active Impeller frame.");
        return false;
    }

    private bool TryGetState(IntPtr context, out ContextState state) => _states.TryGetValue(context, out state!);

    private int Fail(string message)
    {
        _lastError = message;
        return 1;
    }

    private IntPtr ResolveVulkanProcAddress(IntPtr instance, IntPtr procName, IntPtr userData)
    {
        var getInstanceProcAddr = NativeLibrary.GetExport(_vulkanLibrary, "vkGetInstanceProcAddr");
        var callback = Marshal.GetDelegateForFunctionPointer<VkGetInstanceProcAddrDelegate>(getInstanceProcAddr);
        return callback(instance, procName);
    }

    private static IntPtr LoadVulkanLibrary()
    {
        var name = OperatingSystem.IsWindows() ? "vulkan-1.dll" : "libvulkan.so.1";
        try { return NativeLibrary.Load(name); }
        catch (Exception exception) { throw new ImpellerException($"Unable to load Vulkan loader '{name}': {exception.Message}", exception); }
    }

    private IntPtr CreateWin32Surface(IntPtr vkInstance, IntPtr module, IntPtr window)
    {
        var function = GetVulkanFunction<VkCreateWin32SurfaceDelegate>(vkInstance, "vkCreateWin32SurfaceKHR");
        var info = new VkWin32SurfaceCreateInfo { SType = 1000009000, HInstance = module, HWnd = window };
        var result = function(vkInstance, ref info, IntPtr.Zero, out var surface);
        if (result != 0) { Fail($"vkCreateWin32SurfaceKHR failed with VkResult {result}."); return IntPtr.Zero; }
        return surface;
    }

    private IntPtr CreateX11Surface(IntPtr vkInstance, IntPtr display, nuint window)
    {
        var function = GetVulkanFunction<VkCreateXlibSurfaceDelegate>(vkInstance, "vkCreateXlibSurfaceKHR");
        var info = new VkXlibSurfaceCreateInfo { SType = 1000004000, Display = display, Window = window };
        var result = function(vkInstance, ref info, IntPtr.Zero, out var surface);
        if (result != 0) { Fail($"vkCreateXlibSurfaceKHR failed with VkResult {result}."); return IntPtr.Zero; }
        return surface;
    }

    private void DestroyVulkanSurface(IntPtr vkInstance, IntPtr surface)
    {
        var function = GetVulkanFunction<VkDestroySurfaceDelegate>(vkInstance, "vkDestroySurfaceKHR");
        function(vkInstance, surface, IntPtr.Zero);
    }

    private T GetVulkanFunction<T>(IntPtr instance, string name) where T : Delegate
    {
        var namePointer = Marshal.StringToCoTaskMemUTF8(name);
        try
        {
            var pointer = ResolveVulkanProcAddress(instance, namePointer, IntPtr.Zero);
            if (pointer == IntPtr.Zero) throw new ImpellerException($"Vulkan function '{name}' is unavailable.");
            return Marshal.GetDelegateForFunctionPointer<T>(pointer);
        }
        finally { Marshal.FreeCoTaskMem(namePointer); }
    }

    private T GetExport<T>(string name) where T : Delegate
    {
        try { return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name)); }
        catch (Exception exception) when (exception is EntryPointNotFoundException or ArgumentException)
        { throw new ImpellerException($"Impeller native library is missing required export '{name}'.", exception); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var context in _states.Keys.ToArray()) DestroyContext(context);
        NativeLibrary.Free(_library);
        NativeLibrary.Free(_vulkanLibrary);
    }

    private sealed class ContextState(IntPtr context, IntPtr swapchain, IntPtr typography, uint width, uint height, float dpiScale)
    {
        public IntPtr Context { get; } = context;
        public IntPtr Swapchain { get; } = swapchain;
        public IntPtr Typography { get; } = typography;
        public IntPtr Builder { get; set; }
        public uint Width { get; set; } = width;
        public uint Height { get; set; } = height;
        public float DpiScale { get; set; } = dpiScale;
        public Dictionary<object, IntPtr> Textures { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FontRegistration : IDisposable
    {
        private IntPtr _pointer;
        private bool _transferred;

        private FontRegistration(byte[] bytes)
        {
            Length = bytes.Length;
            _pointer = Marshal.AllocHGlobal(Length);
            Marshal.Copy(bytes, 0, _pointer, Length);
        }

        public IntPtr Pointer => _pointer;
        public int Length { get; }
        public static FontRegistration Create(byte[] bytes) => new(bytes);
        public void TransferOwnership() => _transferred = true;
        public void Dispose()
        {
            if (!_transferred && _pointer != IntPtr.Zero) Marshal.FreeHGlobal(_pointer);
            _pointer = IntPtr.Zero;
        }
    }

    private readonly record struct VulkanTarget(IntPtr Window, IntPtr Instance, IntPtr Display, int Screen, bool IsX11);

    [StructLayout(LayoutKind.Sequential)] private struct ImpellerContextVulkanSettings { public IntPtr UserData; public IntPtr ProcAddressCallback; [MarshalAs(UnmanagedType.I1)] public bool EnableValidation; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerContextVulkanInfo { public IntPtr Instance; public IntPtr PhysicalDevice; public IntPtr LogicalDevice; public uint QueueFamily; public uint QueueIndex; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerRect(float x, float y, float width, float height) { public float X = x; public float Y = y; public float Width = width; public float Height = height; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerPoint(float x, float y) { public float X = x; public float Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerColor(float red, float green, float blue, float alpha, int colorSpace) { public float Red = red; public float Green = green; public float Blue = blue; public float Alpha = alpha; public int ColorSpace = colorSpace; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerISize(long width, long height) { public long Width = width; public long Height = height; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerTextureDescriptor { public int PixelFormat; public ImpellerISize Size; public uint MipCount; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerMapping { public IntPtr Data; public ulong Length; public IntPtr OnRelease; }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerRoundingRadii { public ImpellerPoint TopLeft; public ImpellerPoint BottomLeft; public ImpellerPoint TopRight; public ImpellerPoint BottomRight; public static ImpellerRoundingRadii Uniform(float x, float y) { var p = new ImpellerPoint(x, y); return new() { TopLeft = p, BottomLeft = p, TopRight = p, BottomRight = p }; } }
    [StructLayout(LayoutKind.Sequential)] private struct ImpellerMatrix { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public float[] Values; public static ImpellerMatrix CreateScale(float scale) => From2D(scale, 0, 0, scale, 0, 0); public static ImpellerMatrix From2D(float m11, float m12, float m21, float m22, float m31, float m32) => new() { Values = [m11, m12, 0, 0, m21, m22, 0, 0, 0, 0, 1, 0, m31, m32, 0, 1] }; }
    [StructLayout(LayoutKind.Sequential)] private struct VkWin32SurfaceCreateInfo { public int SType; public IntPtr PNext; public uint Flags; public IntPtr HInstance; public IntPtr HWnd; }
    [StructLayout(LayoutKind.Sequential)] private struct VkXlibSurfaceCreateInfo { public int SType; public IntPtr PNext; public uint Flags; public IntPtr Display; public nuint Window; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr VulkanProcAddressCallback(IntPtr instance, IntPtr procName, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr VkGetInstanceProcAddrDelegate(IntPtr instance, IntPtr procName);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int VkCreateWin32SurfaceDelegate(IntPtr instance, ref VkWin32SurfaceCreateInfo createInfo, IntPtr allocator, out IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int VkCreateXlibSurfaceDelegate(IntPtr instance, ref VkXlibSurfaceCreateInfo createInfo, IntPtr allocator, out IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VkDestroySurfaceDelegate(IntPtr instance, IntPtr surface, IntPtr allocator);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint GetVersionDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ContextCreateVulkanDelegate(uint version, ref ImpellerContextVulkanSettings settings);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool ContextGetVulkanInfoDelegate(IntPtr context, out ImpellerContextVulkanInfo info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void HandleOperationDelegate(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr SwapchainCreateDelegate(IntPtr context, IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AcquireSurfaceDelegate(IntPtr swapchain);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool SurfaceDrawDisplayListDelegate(IntPtr surface, IntPtr displayList);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool SurfacePresentDelegate(IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr BuilderCreateDelegate(IntPtr cullRect);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr BuilderCreateDisplayListDelegate(IntPtr builder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderOperationDelegate(IntPtr builder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderSaveLayerDelegate(IntPtr builder, ref ImpellerRect bounds, IntPtr paint, IntPtr backdrop);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderTransformDelegate(IntPtr builder, ref ImpellerMatrix matrix);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderClipRectDelegate(IntPtr builder, ref ImpellerRect rect, int operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderClipRoundedRectDelegate(IntPtr builder, ref ImpellerRect rect, ref ImpellerRoundingRadii radii, int operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderClipPathDelegate(IntPtr builder, IntPtr path, int operation);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawShapeDelegate(IntPtr builder, ref ImpellerRect rect, IntPtr paint);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawRoundedRectDelegate(IntPtr builder, ref ImpellerRect rect, ref ImpellerRoundingRadii radii, IntPtr paint);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawPathDelegate(IntPtr builder, IntPtr path, IntPtr paint);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawTextureRectDelegate(IntPtr builder, IntPtr texture, ref ImpellerRect source, ref ImpellerRect destination, int sampling, IntPtr paint);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawParagraphDelegate(IntPtr builder, IntPtr paragraph, ref ImpellerPoint point);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuilderDrawPaintDelegate(IntPtr builder, IntPtr paint);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PaintCreateDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PaintSetColorDelegate(IntPtr paint, ref ImpellerColor color);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PaintSetIntDelegate(IntPtr paint, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PaintSetFloatDelegate(IntPtr paint, float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PaintSetHandleDelegate(IntPtr paint, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ColorSourceCreateLinearGradientDelegate(ref ImpellerPoint start, ref ImpellerPoint end, uint stopCount, IntPtr colors, IntPtr stops, int tileMode, IntPtr transform);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ColorSourceCreateRadialGradientDelegate(ref ImpellerPoint center, float radius, uint stopCount, IntPtr colors, IntPtr stops, int tileMode, IntPtr transform);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PathBuilderCreateDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PathBuilderPointDelegate(IntPtr builder, ref ImpellerPoint point);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PathBuilderArcDelegate(IntPtr builder, ref ImpellerRect oval, float startAngle, float endAngle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PathBuilderTakePathDelegate(IntPtr builder, int fillType);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr TextureCreateDelegate(IntPtr context, ref ImpellerTextureDescriptor descriptor, ref ImpellerMapping mapping, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void MappingReleaseDelegate(IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr TypographyCreateDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool TypographyRegisterFontDelegate(IntPtr context, ref ImpellerMapping mapping, IntPtr userData, IntPtr familyAlias);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ParagraphStyleCreateDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphStyleSetHandleDelegate(IntPtr style, IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphStyleSetIntDelegate(IntPtr style, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphStyleSetFloatDelegate(IntPtr style, float value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphStyleSetStringDelegate(IntPtr style, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ParagraphBuilderCreateDelegate(IntPtr typography);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphBuilderPushStyleDelegate(IntPtr builder, IntPtr style);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParagraphBuilderAddTextDelegate(IntPtr builder, IntPtr data, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ParagraphBuilderBuildDelegate(IntPtr builder, float width);
}
