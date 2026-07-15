using Square.Graphics;
using System.Numerics;

namespace Square.Rendering.Commands;

public abstract record DrawCommand;

public sealed record FillRectCommand(Rect Rect, Brush Brush) : DrawCommand;
public sealed record DrawRectCommand(Rect Rect, Pen Pen) : DrawCommand;
public sealed record FillPathCommand(PathGeometry Path, Brush Brush) : DrawCommand;
public sealed record DrawPathCommand(PathGeometry Path, Pen Pen) : DrawCommand;
public sealed record FillGeometryCommand(Geometry Geometry, Brush Brush) : DrawCommand;
public sealed record DrawGeometryCommand(Geometry Geometry, Pen Pen) : DrawCommand;
public sealed record DrawTextCommand(TextLayout Text, Point Origin, Brush Brush) : DrawCommand;
public sealed record DrawImageCommand(Image Image, Rect Dest, Rect? Source) : DrawCommand;
public sealed record PushClipCommand(Rect Rect) : DrawCommand;
public sealed record PushGeometryClipCommand(Geometry Geometry) : DrawCommand;
public sealed record PopClipCommand : DrawCommand;
public sealed record PushTransformCommand(Matrix3x2 Matrix) : DrawCommand;
public sealed record PopTransformCommand : DrawCommand;
public sealed record PushLayerCommand(Rect Bounds, float Opacity) : DrawCommand;
public sealed record PopLayerCommand : DrawCommand;
public sealed record ClearCommand(Color Color) : DrawCommand;
