namespace Square.Graphics;

public abstract class Geometry { }

public sealed class RectGeometry : Geometry
{
    public Rect Rect { get; set; }
    public RectGeometry(Rect rect) { Rect = rect; }
}

public sealed class RoundedRectGeometry : Geometry
{
    public Rect Rect { get; set; }
    public float RadiusX { get; set; }
    public float RadiusY { get; set; }

    public RoundedRectGeometry(Rect rect, float radiusX, float radiusY)
    {
        Rect = rect; RadiusX = radiusX; RadiusY = radiusY;
    }
}

public sealed class EllipseGeometry : Geometry
{
    public Point Center { get; set; }
    public float RadiusX { get; set; }
    public float RadiusY { get; set; }

    public EllipseGeometry(Point center, float radiusX, float radiusY)
    {
        Center = center; RadiusX = radiusX; RadiusY = radiusY;
    }
}
