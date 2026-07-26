using Square.Graphics;
using System.Numerics;

namespace Square.Rendering.Commands;

/// <summary>绘制命令基类。</summary>
public abstract record DrawCommand;

/// <summary>填充矩形命令。</summary>
public sealed record FillRectCommand(Rect Rect, Brush Brush) : DrawCommand;
/// <summary>描边矩形命令。</summary>
public sealed record DrawRectCommand(Rect Rect, Pen Pen) : DrawCommand;
/// <summary>填充路径命令。</summary>
public sealed record FillPathCommand(PathGeometry Path, Brush Brush) : DrawCommand;
/// <summary>描边路径命令。</summary>
public sealed record DrawPathCommand(PathGeometry Path, Pen Pen) : DrawCommand;
/// <summary>填充几何命令。</summary>
public sealed record FillGeometryCommand(Geometry Geometry, Brush Brush) : DrawCommand;
/// <summary>描边几何命令。</summary>
public sealed record DrawGeometryCommand(Geometry Geometry, Pen Pen) : DrawCommand;
/// <summary>绘制文本命令。</summary>
public sealed record DrawTextCommand(TextLayout Text, Point Origin, Brush Brush) : DrawCommand;
/// <summary>绘制图像命令。</summary>
public sealed record DrawImageCommand(Image Image, Rect Dest, Rect? Source) : DrawCommand;
/// <summary>压入矩形裁剪命令。</summary>
public sealed record PushClipCommand(Rect Rect) : DrawCommand;
/// <summary>压入几何裁剪命令。</summary>
public sealed record PushGeometryClipCommand(Geometry Geometry) : DrawCommand;
/// <summary>弹出裁剪命令。</summary>
public sealed record PopClipCommand : DrawCommand;
/// <summary>压入变换命令。</summary>
public sealed record PushTransformCommand(Matrix3x2 Matrix) : DrawCommand;
/// <summary>弹出变换命令。</summary>
public sealed record PopTransformCommand : DrawCommand;
/// <summary>压入透明度图层命令。</summary>
public sealed record PushLayerCommand(Rect Bounds, float Opacity) : DrawCommand;
/// <summary>弹出透明度图层命令。</summary>
public sealed record PopLayerCommand : DrawCommand;
/// <summary>清除命令。</summary>
public sealed record ClearCommand(Color Color) : DrawCommand;