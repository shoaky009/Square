namespace Square.UI;

[Flags]
public enum VisualState : byte
{
    None = 0,
    Hover = 1,
    Focus = 2,
    Active = 4,
    Disabled = 8,
    Checked = 16,
    Empty = 32
}

public static class VisualStateExtensions
{
    public static bool Has(this VisualState state, VisualState flag) => (state & flag) != 0;
}