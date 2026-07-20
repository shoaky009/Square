using System.Numerics;
using Square.Graphics;

namespace Square.Backends.Impeller;

internal sealed class ImpellerRenderContext : IRenderContext, IDpiResizableRenderContext
{
    private readonly IImpellerApi _native;
    private IntPtr _context;
    private bool _frameStarted;
    private bool _disposed;

    public Size CanvasSize { get; private set; }
    public float DpiScale { get; private set; }

    internal ImpellerRenderContext(IImpellerApi native, RenderContextCreateInfo info)
    {
        _native = native;
        CanvasSize = info.CanvasSize;
        DpiScale = NormalizeDpiScale(info.DpiScale);
        var physicalWidth = ToPhysicalPixels(CanvasSize.Width, DpiScale);
        var physicalHeight = ToPhysicalPixels(CanvasSize.Height, DpiScale);

        var result = info.NativeTarget switch
        {
            Win32VulkanRenderTarget win32 => native.CreateWin32(
                win32.WindowHandle, win32.InstanceHandle, physicalWidth, physicalHeight, DpiScale, info.VSync, out _context),
            X11VulkanRenderTarget x11 => native.CreateX11(
                x11.DisplayHandle, (nuint)x11.WindowHandle, x11.Screen, physicalWidth, physicalHeight, DpiScale, info.VSync, out _context),
            _ => throw new ImpellerException($"Unsupported native render target '{info.NativeTarget?.Kind}'.")
        };

        ThrowIfFailed(result, "create context");
        if (_context == IntPtr.Zero)
            throw new ImpellerException("Impeller reported success but returned a null context.");
    }

    public void Resize(Size canvasSize) => Resize(canvasSize, DpiScale);

    public void Resize(Size canvasSize, float dpiScale)
    {
        ThrowIfDisposed();
        dpiScale = NormalizeDpiScale(dpiScale);
        var result = _native.ResizeContext(
            _context,
            ToPhysicalPixels(canvasSize.Width, dpiScale),
            ToPhysicalPixels(canvasSize.Height, dpiScale),
            dpiScale);
        ThrowIfFailed(result, "resize context");
        CanvasSize = canvasSize;
        DpiScale = dpiScale;
        _frameStarted = false;
    }

    public void Clear(Color color)
    {
        EnsureFrame();
        ThrowIfFailed(_native.Clear(
            _context,
            color.R / 255f,
            color.G / 255f,
            color.B / 255f,
            color.A / 255f), "clear frame");
    }

    public void Flush()
    {
        EnsureFrame();
        ThrowIfFailed(_native.Flush(_context), "flush frame");
    }

    public void Present() => Present(null);

    public void Present(IReadOnlyList<Rect>? dirtyRects)
    {
        if (dirtyRects is { Count: 0 }) return;
        EnsureFrame();
        ThrowIfFailed(_native.Present(_context), "present frame");
        _frameStarted = false;
    }

    public void PushTransform(Matrix3x2 matrix)
    {
        EnsureFrame();
        ThrowIfFailed(_native.PushTransform(
            _context, matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32), "push transform");
    }

    public void PopTransform()
    {
        EnsureFrame();
        ThrowIfFailed(_native.PopTransform(_context), "pop transform");
    }

    public void PushClip(Rect rect)
    {
        EnsureFrame();
        ThrowIfFailed(_native.PushClipRect(_context, rect.X, rect.Y, rect.Width, rect.Height), "push rectangle clip");
    }

    public void PushClip(Geometry geometry)
    {
        EnsureFrame();
        var result = geometry switch
        {
            RectGeometry rect => _native.PushClipRect(
                _context, rect.Rect.X, rect.Rect.Y, rect.Rect.Width, rect.Rect.Height),
            RoundedRectGeometry rounded => _native.PushClipRoundedRect(
                _context, rounded.Rect.X, rounded.Rect.Y, rounded.Rect.Width, rounded.Rect.Height,
                rounded.RadiusX, rounded.RadiusY),
            EllipseGeometry ellipse => _native.PushClipEllipse(
                _context, ellipse.Center.X, ellipse.Center.Y, ellipse.RadiusX, ellipse.RadiusY),
            PathGeometry path => _native.PushClipPath(_context, ConvertPath(path)),
            _ => throw new NotSupportedException($"Impeller does not support geometry clip type '{geometry.GetType().Name}'.")
        };
        ThrowIfFailed(result, "push geometry clip");
    }

    public void PopClip()
    {
        EnsureFrame();
        ThrowIfFailed(_native.PopClip(_context), "pop clip");
    }

    public void FillRect(Rect rect, Brush brush)
    {
        var nativeBrush = ConvertBrush(brush);
        EnsureFrame();
        ThrowIfFailed(_native.FillRect(
            _context, rect.X, rect.Y, rect.Width, rect.Height, nativeBrush), "fill rectangle");
    }

    public void DrawRect(Rect rect, Pen pen)
    {
        var nativeBrush = ConvertBrush(pen.Brush);
        var style = ConvertStrokeStyle(pen);
        EnsureFrame();
        ThrowIfFailed(_native.StrokeRect(
            _context, rect.X, rect.Y, rect.Width, rect.Height, pen.Width, nativeBrush, style), "stroke rectangle");
    }
    public void FillPath(PathGeometry path, Brush brush)
    {
        var nativeBrush = ConvertBrush(brush);
        EnsureFrame();
        ThrowIfFailed(_native.FillPath(
            _context, ConvertPath(path), nativeBrush), "fill path");
    }

    public void DrawPath(PathGeometry path, Pen pen)
    {
        var nativeBrush = ConvertBrush(pen.Brush);
        var style = ConvertStrokeStyle(pen);
        EnsureFrame();
        ThrowIfFailed(_native.StrokePath(
            _context, ConvertPath(path), pen.Width, nativeBrush, style), "stroke path");
    }
    public void FillGeometry(Geometry geometry, Brush brush)
    {
        var nativeBrush = ConvertBrush(brush);
        EnsureFrame();
        var result = geometry switch
        {
            RectGeometry rect => _native.FillRect(
                _context, rect.Rect.X, rect.Rect.Y, rect.Rect.Width, rect.Rect.Height, nativeBrush),
            RoundedRectGeometry rounded => _native.FillRoundedRect(
                _context, rounded.Rect.X, rounded.Rect.Y, rounded.Rect.Width, rounded.Rect.Height,
                rounded.RadiusX, rounded.RadiusY, nativeBrush),
            EllipseGeometry ellipse => _native.FillEllipse(
                _context, ellipse.Center.X, ellipse.Center.Y, ellipse.RadiusX, ellipse.RadiusY, nativeBrush),
            _ => throw new NotSupportedException($"Impeller does not support geometry type '{geometry.GetType().Name}'.")
        };
        ThrowIfFailed(result, "fill geometry");
    }

    public void DrawGeometry(Geometry geometry, Pen pen)
    {
        var nativeBrush = ConvertBrush(pen.Brush);
        var style = ConvertStrokeStyle(pen);
        EnsureFrame();
        var result = geometry switch
        {
            RectGeometry rect => _native.StrokeRect(
                _context, rect.Rect.X, rect.Rect.Y, rect.Rect.Width, rect.Rect.Height, pen.Width, nativeBrush, style),
            RoundedRectGeometry rounded => _native.StrokeRoundedRect(
                _context, rounded.Rect.X, rounded.Rect.Y, rounded.Rect.Width, rounded.Rect.Height,
                rounded.RadiusX, rounded.RadiusY, pen.Width, nativeBrush, style),
            EllipseGeometry ellipse => _native.StrokeEllipse(
                _context, ellipse.Center.X, ellipse.Center.Y, ellipse.RadiusX, ellipse.RadiusY, pen.Width, nativeBrush, style),
            _ => throw new NotSupportedException($"Impeller does not support geometry type '{geometry.GetType().Name}'.")
        };
        ThrowIfFailed(result, "stroke geometry");
    }
    public void DrawText(TextLayout text, Point origin, Brush brush)
    {
        var color = brush is SolidColorBrush solid
            ? solid.Color
            : throw new NotSupportedException("Impeller text currently supports only solid-color brushes.");
        if (string.IsNullOrEmpty(text.Text)) return;
        EnsureFrame();
        var maxWidth = float.IsFinite(text.MaxSize.Width) && text.MaxSize.Width > 0
            ? text.MaxSize.Width
            : Math.Max(CanvasSize.Width - origin.X, 1f);
        ThrowIfFailed(_native.DrawText(
            _context,
            text.Text,
            text.Font.Family,
            text.Font.Size,
            (int)text.Font.Weight,
            text.Font.Style != FontStyle.Normal,
            (int)text.Alignment,
            text.LineHeight,
            maxWidth,
            origin.X,
            origin.Y,
            ToFloat(color.R), ToFloat(color.G), ToFloat(color.B), ToFloat(color.A)), "draw text");
    }
    public void DrawImage(Image image, Rect dest, Rect? source = null)
    {
        if (image is not Bitmap bitmap)
            throw new NotSupportedException($"Impeller does not support image type '{image.GetType().Name}'.");
        if (bitmap.IsDisposed) throw new ObjectDisposedException(nameof(image));
        var src = source ?? new Rect(0, 0, bitmap.Width, bitmap.Height);
        EnsureFrame();
        ThrowIfFailed(_native.DrawBitmap(
            _context, bitmap, bitmap.Width, bitmap.Height, bitmap.Pixels,
            src.X, src.Y, src.Width, src.Height,
            dest.X, dest.Y, dest.Width, dest.Height), "draw bitmap");
    }
    public void PushLayer(Rect bounds, float opacity)
    {
        EnsureFrame();
        ThrowIfFailed(_native.PushLayer(
            _context, bounds.X, bounds.Y, bounds.Width, bounds.Height, Math.Clamp(opacity, 0, 1)), "push opacity layer");
    }

    public void PopLayer()
    {
        EnsureFrame();
        ThrowIfFailed(_native.PopLayer(_context), "pop opacity layer");
    }

    public void Clear(Color color, Rect rect)
    {
        EnsureFrame();
        ThrowIfFailed(_native.ClearRect(
            _context, rect.X, rect.Y, rect.Width, rect.Height,
            ToFloat(color.R), ToFloat(color.G), ToFloat(color.B), ToFloat(color.A)), "clear rectangle");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_context != IntPtr.Zero)
        {
            _native.DestroyContext(_context);
            _context = IntPtr.Zero;
        }
        _native.Dispose();
    }

    private void EnsureFrame()
    {
        ThrowIfDisposed();
        if (_frameStarted) return;
        ThrowIfFailed(_native.BeginFrame(_context), "begin frame");
        _frameStarted = true;
    }

    private void ThrowIfFailed(int result, string operation)
    {
        if (result == 0) return;
        throw new ImpellerException($"Impeller failed to {operation}: {_native.ReadLastError()} (result {result}).");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static ImpellerBrush ConvertBrush(Brush brush)
        => brush switch
        {
            SolidColorBrush solid => new(
                ImpellerBrushKind.Solid, 0, 0, 0, 0, 0, 0,
                [ConvertStop(new GradientStop(0, solid.Color))]),
            LinearGradientBrush linear => new(
                ImpellerBrushKind.LinearGradient,
                linear.Start.X, linear.Start.Y, linear.End.X, linear.End.Y, 0,
                ConvertSpreadMethod(linear.SpreadMethod), ConvertStops(linear.Stops)),
            RadialGradientBrush radial when radial.Radius > 0 => new(
                ImpellerBrushKind.RadialGradient,
                radial.Center.X, radial.Center.Y, 0, 0, radial.Radius,
                ConvertSpreadMethod(radial.SpreadMethod), ConvertStops(radial.Stops)),
            RadialGradientBrush => throw new ArgumentOutOfRangeException(nameof(brush), "Gradient radius must be positive."),
            _ => throw new NotSupportedException($"Impeller does not support brush type '{brush.GetType().Name}'.")
        };

    private static IReadOnlyList<ImpellerGradientStop> ConvertStops(GradientStop[] stops)
    {
        if (stops.Length == 0) throw new ArgumentException("A gradient must contain at least one stop.", nameof(stops));
        return stops.Select(ConvertStop).ToArray();
    }

    private static ImpellerGradientStop ConvertStop(GradientStop stop)
        => new(Math.Clamp(stop.Offset, 0, 1), ToFloat(stop.Color.R), ToFloat(stop.Color.G), ToFloat(stop.Color.B), ToFloat(stop.Color.A));

    private static int ConvertSpreadMethod(GradientSpreadMethod method) => method switch
    {
        GradientSpreadMethod.Repeat => 1,
        GradientSpreadMethod.Reflect => 2,
        _ => 0
    };

    private static ImpellerStrokeStyle ConvertStrokeStyle(Pen pen)
    {
        if (pen.Width <= 0) throw new ArgumentOutOfRangeException(nameof(pen), "Stroke width must be positive.");
        var style = pen.StrokeStyle;
        if (style?.DashArray is { Length: > 0 })
            ThrowDrawingNotImplemented("dashed paths because the Impeller C API exposes only a two-point dashed-line primitive");
        if (style != null && style.MiterLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(pen), "Stroke miter limit must be positive.");
        return new(
            style?.Cap switch { LineCap.Round => 1, LineCap.Square => 2, _ => 0 },
            style?.Join switch { LineJoin.Round => 1, LineJoin.Bevel => 2, _ => 0 },
            style?.MiterLimit ?? 10f);
    }

    private static void ThrowDrawingNotImplemented()
        => ThrowDrawingNotImplemented("this drawing command");

    private static void ThrowDrawingNotImplemented(string feature)
        => throw new NotSupportedException($"Impeller does not yet implement {feature}. See docs/Impeller-Backend-Plan.md.");

    private static float ToFloat(byte component) => component / 255f;

    private static IReadOnlyList<ImpellerPathCommand> ConvertPath(PathGeometry path)
    {
        var commands = new ImpellerPathCommand[path.Commands.Count];
        for (var i = 0; i < path.Commands.Count; i++)
        {
            commands[i] = path.Commands[i] switch
            {
                MoveToCmd move => new(ImpellerPathCommandKind.MoveTo, move.Point.X, move.Point.Y),
                LineToCmd line => new(ImpellerPathCommandKind.LineTo, line.Point.X, line.Point.Y),
                ArcToCmd arc => new(
                    ImpellerPathCommandKind.ArcTo,
                    arc.Oval.X, arc.Oval.Y, arc.Oval.Width, arc.Oval.Height,
                    arc.StartAngle, arc.StartAngle + arc.SweepAngle),
                CloseCmd => new(ImpellerPathCommandKind.Close, 0, 0),
                _ => throw new NotSupportedException($"Impeller does not support path command '{path.Commands[i].GetType().Name}'.")
            };
        }
        return commands;
    }

    private static uint ToPhysicalPixels(float logicalPixels, float dpiScale)
        => (uint)Math.Max(1, MathF.Ceiling(logicalPixels * dpiScale));

    private static float NormalizeDpiScale(float dpiScale)
        => float.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1f;
}
