using Square.Events;
using Square.UI;

namespace Square.Controls;

/// <summary>A single-page viewport that navigates through its direct children.</summary>
public class Swiper : View
{
    private int _selectedIndex;

    public Swiper()
    {
        Style.SetCascaded("overflow", "hidden", int.MinValue);
        AddEventListener<KeyboardEvent>(StandardEvents.KeyDown, e =>
        {
            if (!HandleKey(e.KeyCode)) return;
            e.PreventDefault();
        });
    }

    public int SelectedIndex
    {
        get => Children.Count == 0 ? -1 : Math.Clamp(_selectedIndex, 0, Children.Count - 1);
        set => GoTo(value);
    }

    public bool Loop
    {
        get => Properties.HasValue(nameof(Loop)) && GetProperty<bool>(nameof(Loop));
        set => SetProperty(nameof(Loop), value);
    }

    public int Count => Children.Count;
    public Element? SelectedItem => SelectedIndex >= 0 ? Children[SelectedIndex] : null;
    public bool CanGoPrevious => Children.Count > 1 && (Loop || SelectedIndex > 0);
    public bool CanGoNext => Children.Count > 1 && (Loop || SelectedIndex < Children.Count - 1);

    public bool GoTo(int index)
    {
        if (Children.Count == 0)
        {
            _selectedIndex = 0;
            return false;
        }

        var next = Loop ? Mod(index, Children.Count) : Math.Clamp(index, 0, Children.Count - 1);
        var previous = SelectedIndex;
        _selectedIndex = next;
        UpdatePageVisibility();
        if (previous == next) return false;
        DispatchEvent(StandardEvents.CreateChange());
        return true;
    }

    public bool Next() => CanGoNext && GoTo(SelectedIndex + 1);
    public bool Previous() => CanGoPrevious && GoTo(SelectedIndex - 1);

    public bool HandleKey(int keyCode)
    {
        if (!IsEnabled || Children.Count == 0) return false;
        return keyCode switch
        {
            37 => Previous(),
            39 => Next(),
            36 => GoTo(0),
            35 => GoTo(Children.Count - 1),
            _ => false
        };
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(SelectedIndex)) GoTo(GetProperty<int>(nameof(SelectedIndex)));
    }

    internal override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        child.IsVisible = Children.IndexOf(child) == SelectedIndex;
    }

    internal override void OnChildRemoved(Element child)
    {
        var removedIndex = Children.IndexOf(child);
        base.OnChildRemoved(child);
        if (removedIndex >= 0 && removedIndex < _selectedIndex) _selectedIndex--;
        if (_selectedIndex >= Children.Count) _selectedIndex = Math.Max(0, Children.Count - 1);
        UpdatePageVisibility();
    }

    private void UpdatePageVisibility()
    {
        var selected = SelectedIndex;
        for (var i = 0; i < Children.Count; i++) Children[i].IsVisible = i == selected;
        InvalidateLayout();
    }

    private static int Mod(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
