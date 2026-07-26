namespace Square.Graphics;

/// <summary>几何图形基类。</summary>
public abstract class Geometry { }

/// <summary>矩形几何。</summary>
public sealed class RectGeometry : Geometry
{
    /// <summary>矩形。</summary>
    public Rect Rect { get; set; }
    /// <summary>构造矩形几何。</summary>
    public RectGeometry(Rect rect) { Rect = rect; }
}

/// <summary>圆角矩形几何。</summary>
public sealed class RoundedRectGeometry : Geometry
{
    /// <summary>外接矩形。</summary>
    public Rect Rect { get; set; }
    /// <summary>X 方向圆角半径。</summary>
    public float RadiusX { get; set; }
    /// <summary>Y 方向圆角半径。</summary>
    public float RadiusY { get; set; }

    /// <summary>构造圆角矩形几何。</summary>
    public RoundedRectGeometry(Rect rect, float radiusX, float radiusY)
    {
        Rect = rect; RadiusX = radiusX; RadiusY = radiusY;
    }
}

/// <summary>椭圆几何。</summary>
public sealed class EllipseGeometry : Geometry
{
    /// <summary>圆心。</summary>
    public Point Center { get; set; }
    /// <summary>X 方向半径。</summary>
    public float RadiusX { get; set; }
    /// <summary>Y 方向半径。</summary>
    public float RadiusY { get; set; }

    /// <summary>构造椭圆几何。</summary>
    public EllipseGeometry(Point center, float radiusX, float radiusY)
    {
        Center = center; RadiusX = radiusX; RadiusY = radiusY;
    }
}