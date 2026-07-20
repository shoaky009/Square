namespace Square.Backends.Impeller;

internal interface IImpellerApi : IDisposable
{
    string ReadLastError();
    int CreateWin32(IntPtr window, IntPtr instance, uint width, uint height, float dpiScale, bool vsync, out IntPtr context);
    int CreateX11(IntPtr display, nuint window, int screen, uint width, uint height, float dpiScale, bool vsync, out IntPtr context);
    void DestroyContext(IntPtr context);
    int ResizeContext(IntPtr context, uint width, uint height, float dpiScale);
    int BeginFrame(IntPtr context);
    int Clear(IntPtr context, float red, float green, float blue, float alpha);
    int ClearRect(IntPtr context, float x, float y, float width, float height, float red, float green, float blue, float alpha);
    int PushTransform(IntPtr context, float m11, float m12, float m21, float m22, float m31, float m32);
    int PopTransform(IntPtr context);
    int PushClipRect(IntPtr context, float x, float y, float width, float height);
    int PushClipRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY);
    int PushClipEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY);
    int PushClipPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands);
    int PopClip(IntPtr context);
    int FillRect(IntPtr context, float x, float y, float width, float height, ImpellerBrush brush);
    int StrokeRect(IntPtr context, float x, float y, float width, float height, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style);
    int FillRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, ImpellerBrush brush);
    int StrokeRoundedRect(IntPtr context, float x, float y, float width, float height, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style);
    int FillEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, ImpellerBrush brush);
    int StrokeEllipse(IntPtr context, float centerX, float centerY, float radiusX, float radiusY, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style);
    int FillPath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, ImpellerBrush brush);
    int StrokePath(IntPtr context, IReadOnlyList<ImpellerPathCommand> commands, float strokeWidth, ImpellerBrush brush, ImpellerStrokeStyle style);
    int DrawBitmap(IntPtr context, object cacheKey, int width, int height, byte[] bgraPixels, float sourceX, float sourceY, float sourceWidth, float sourceHeight, float destinationX, float destinationY, float destinationWidth, float destinationHeight);
    int DrawText(IntPtr context, string text, string fontFamily, float fontSize, int fontWeight, bool italic, int alignment, float lineHeight, float maxWidth, float x, float y, float red, float green, float blue, float alpha);
    int PushLayer(IntPtr context, float x, float y, float width, float height, float opacity);
    int PopLayer(IntPtr context);
    int Flush(IntPtr context);
    int Present(IntPtr context);
}

internal enum ImpellerPathCommandKind { MoveTo, LineTo, ArcTo, Close }

internal enum ImpellerBrushKind { Solid, LinearGradient, RadialGradient }

internal readonly record struct ImpellerGradientStop(float Offset, float Red, float Green, float Blue, float Alpha);

internal readonly record struct ImpellerBrush(
    ImpellerBrushKind Kind,
    float X1,
    float Y1,
    float X2,
    float Y2,
    float Radius,
    int TileMode,
    IReadOnlyList<ImpellerGradientStop> Stops);

internal readonly record struct ImpellerStrokeStyle(int Cap, int Join, float MiterLimit);

internal readonly record struct ImpellerPathCommand(
    ImpellerPathCommandKind Kind,
    float X1,
    float Y1,
    float X2 = 0,
    float Y2 = 0,
    float X3 = 0,
    float Y3 = 0);
