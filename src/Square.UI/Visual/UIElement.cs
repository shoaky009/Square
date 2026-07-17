using Square.Events;
using Square.Graphics;

namespace Square.UI;

public enum HorizontalAlignment { Left, Center, Right, Stretch }
public enum VerticalAlignment { Top, Center, Bottom, Stretch }

public abstract class UIElement : Visual
{
    public SlotCollection Slots { get; } = new();

    public HorizontalAlignment HorizontalAlign { get; set; } = HorizontalAlignment.Stretch;
    public VerticalAlignment VerticalAlign { get; set; } = VerticalAlignment.Stretch;

    public float Width { get; set; } = float.NaN;
    public float Height { get; set; } = float.NaN;
    public float MinWidth { get; set; } = 0;
    public float MinHeight { get; set; } = 0;
    public float MaxWidth { get; set; } = float.PositiveInfinity;
    public float MaxHeight { get; set; } = float.PositiveInfinity;

    public float MarginLeft { get; set; }
    public float MarginTop { get; set; }
    public float MarginRight { get; set; }
    public float MarginBottom { get; set; }

    public float PaddingLeft { get; set; }
    public float PaddingTop { get; set; }
    public float PaddingRight { get; set; }
    public float PaddingBottom { get; set; }

    public bool IsDisabled
    {
        get => GetProperty<bool>(nameof(IsDisabled));
        set => SetProperty(nameof(IsDisabled), value);
    }

    public bool IsEnabled
    {
        get => !IsDisabled;
        set => IsDisabled = !value;
    }
    public bool IsFocused { get; private set; }

    public string? Tooltip { get; set; }

    protected float ConstrainWidth(float width)
    {
        if (!float.IsNaN(Width)) return Width;
        return Math.Clamp(width, MinWidth, MaxWidth);
    }

    protected float ConstrainHeight(float height)
    {
        if (!float.IsNaN(Height)) return Height;
        return Math.Clamp(height, MinHeight, MaxHeight);
    }

    public override Size Measure(Size availableSize)
    {
        var w = ConstrainWidth(availableSize.Width);
        var h = ConstrainHeight(availableSize.Height);
        return new Size(w, h);
    }

    public void Focus()
    {
        if (!IsEnabled || IsFocused) return;
        IsFocused = true;
        SetState(VisualState.Focus, true);
        RaiseEvent(StandardEvents.Focus, new RoutedEventArgs());
        for (var node = Parent; node != null; node = node.Parent)
            node.RaiseEvent(StandardEvents.FocusIn, new RoutedEventArgs());
    }

    public void Unfocus()
    {
        if (!IsFocused) return;
        IsFocused = false;
        SetState(VisualState.Focus, false);
        RaiseEvent(StandardEvents.Blur, new RoutedEventArgs());
        for (var node = Parent; node != null; node = node.Parent)
            node.RaiseEvent(StandardEvents.FocusOut, new RoutedEventArgs());
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(IsDisabled))
            SetState(VisualState.Disabled, IsDisabled);
    }
}
