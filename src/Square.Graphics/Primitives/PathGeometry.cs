namespace Square.Graphics;

public sealed class PathGeometry : Geometry
{
    private readonly List<PathCommand> _commands = [];

    public IReadOnlyList<PathCommand> Commands => _commands;

    public PathGeometry MoveTo(Point p) { _commands.Add(new MoveToCmd(p)); return this; }
    public PathGeometry LineTo(Point p) { _commands.Add(new LineToCmd(p)); return this; }
    public PathGeometry ArcTo(Rect oval, float startAngle, float sweepAngle)
    { _commands.Add(new ArcToCmd(oval, startAngle, sweepAngle)); return this; }
    public PathGeometry Close() { _commands.Add(new CloseCmd()); return this; }

    public static PathGeometry Create() => new();
}

public abstract record PathCommand;
public sealed record MoveToCmd(Point Point) : PathCommand;
public sealed record LineToCmd(Point Point) : PathCommand;
public sealed record ArcToCmd(Rect Oval, float StartAngle, float SweepAngle) : PathCommand;
public sealed record CloseCmd : PathCommand;