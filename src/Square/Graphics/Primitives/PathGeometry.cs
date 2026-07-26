namespace Square.Graphics;

/// <summary>路径几何，由一系列路径命令组成。</summary>
public sealed class PathGeometry : Geometry
{
    private readonly List<PathCommand> _commands = [];

    /// <summary>路径命令列表。</summary>
    public IReadOnlyList<PathCommand> Commands => _commands;

    /// <summary>移动到指定点。</summary>
    public PathGeometry MoveTo(Point p) { _commands.Add(new MoveToCmd(p)); return this; }
    /// <summary>连线到指定点。</summary>
    public PathGeometry LineTo(Point p) { _commands.Add(new LineToCmd(p)); return this; }
    /// <summary>沿椭圆弧连接到指定点。</summary>
    public PathGeometry ArcTo(Rect oval, float startAngle, float sweepAngle)
    { _commands.Add(new ArcToCmd(oval, startAngle, sweepAngle)); return this; }
    /// <summary>闭合当前子路径。</summary>
    public PathGeometry Close() { _commands.Add(new CloseCmd()); return this; }

    /// <summary>创建空路径。</summary>
    public static PathGeometry Create() => new();
}

/// <summary>路径命令基类。</summary>
public abstract record PathCommand;
/// <summary>移动命令。</summary>
public sealed record MoveToCmd(Point Point) : PathCommand;
/// <summary>直线命令。</summary>
public sealed record LineToCmd(Point Point) : PathCommand;
/// <summary>椭圆弧命令。</summary>
public sealed record ArcToCmd(Rect Oval, float StartAngle, float SweepAngle) : PathCommand;
/// <summary>闭合命令。</summary>
public sealed record CloseCmd : PathCommand;