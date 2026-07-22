namespace Square.UI;

[Flags]
public enum ElementInvalidation
{
    None = 0,
    Paint = 1 << 0,
    Layout = 1 << 1,
    Style = 1 << 2,
    DisplayTree = 1 << 3,
    HitTest = 1 << 4
}
