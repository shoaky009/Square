using Square.Events;
using Square.Graphics;
using Square.UI;

namespace Square.Controls;

public enum MenuItemRole
{
    Command,
    Check,
    Radio,
    Submenu
}

public class MenuBar : View
{
    public MenuBar()
    {
        Style.SetCascaded("display", "flex", int.MinValue);
        Style.SetCascaded("flex-direction", "row", int.MinValue);
        Style.SetCascaded("align-self", "stretch", int.MinValue);
        Style.SetCascaded("height", "32px", int.MinValue);
        Style.SetCascaded("background", "#f3f4f6", int.MinValue);
    }

    public bool IsMenuModeActive => Items.Any(item => item.Submenu?.IsOpen == true);
    public int ActiveIndex => Array.FindIndex(Items, item => item.Submenu?.IsOpen == true);
    internal MenuItem[] Items => Children.OfType<MenuItem>().ToArray();

    public void CloseMenus()
    {
        foreach (var item in Items)
            item.Submenu?.Close();
    }

    internal void Open(MenuItem item, bool toggle)
    {
        var submenu = item.Submenu;
        if (submenu == null || !item.IsEnabled) return;
        var wasOpen = submenu.IsOpen;
        foreach (var sibling in Items)
            if (!ReferenceEquals(sibling, item)) sibling.Submenu?.CloseMenuTree();
        if (toggle && wasOpen) submenu.CloseMenuTree();
        else submenu.OpenFor(item);
    }

    internal void SwitchOnHover(MenuItem item)
    {
        if (IsMenuModeActive && item.Submenu != null)
            Open(item, toggle: false);
    }

    public override void Paint(IRenderContext context)
    {
        var background = ControlDrawing.GetStyledColor(this, "background", Color.FromRgb(243, 244, 246));
        ControlDrawing.DrawStyledBackground(context, this, background);
    }
}

public class Menu : Popup
{
    private Point? _screenPosition;

    public Menu()
    {
        CloseOnEscape = true;
        DismissOnPointerDownOutside = true;
        FlipOnOverflow = true;
        ConstrainToViewport = true;
        Style.SetCascaded("display", "flex", int.MinValue);
        Style.SetCascaded("flex-direction", "column", int.MinValue);
        Style.SetCascaded("min-width", "240px", int.MinValue);
        Style.SetCascaded("background", "#ffffff", int.MinValue);
    }

    public MenuItem? OwnerItem => Parent as MenuItem;
    public MenuItem? ActiveItem { get; private set; }
    public IReadOnlyList<MenuItem> Items => Children.OfType<MenuItem>().ToArray();

    public override Size Measure(Size availableSize)
    {
        var width = 0f;
        var height = 0f;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var measured = child.Measure(availableSize);
            width = Math.Max(width, measured.Width);
            height += measured.Height;
        }
        return new Size(Math.Max(240, width), height);
    }

    public void OpenFor(MenuItem owner)
    {
        if (OwnerItem is { } previousOwner && !ReferenceEquals(previousOwner, owner))
            previousOwner.SetState(ElementState.Open, false);
        Anchor = owner;
        _screenPosition = null;
        Placement = owner.Parent is MenuBar ? PopupPlacement.Bottom : PopupPlacement.Right;
        Alignment = PopupAlignment.Start;
        HorizontalOffset = 0;
        VerticalOffset = owner.Parent is MenuBar ? 2 : 0;
        CloseSiblingMenus(owner);
        Open();
        owner.SetState(ElementState.Open, true);
    }

    public virtual void OpenAt(Point screenPosition)
    {
        Anchor = null;
        _screenPosition = screenPosition;
        Open();
    }

    public override void Close()
    {
        foreach (var item in Items)
            item.Submenu?.Close();
        ActiveItem = null;
        OwnerItem?.SetState(ElementState.Open, false);
        base.Close();
    }

    public void CloseMenuTree()
    {
        var root = GetRootMenu();
        root.Close();
    }

    public override bool ContainsPopupInteraction(Point point)
    {
        if (base.ContainsPopupInteraction(point)) return true;
        return Items.Select(item => item.Submenu)
            .Where(menu => menu?.IsOpen == true)
            .Any(menu => menu!.ContainsPopupInteraction(point));
    }

    internal void SetActiveItem(MenuItem? item)
    {
        if (ReferenceEquals(ActiveItem, item)) return;
        ActiveItem = item;
        InvalidatePaint();
    }

    internal void OpenSubmenu(MenuItem item)
    {
        if (!item.IsEnabled || item.Submenu == null) return;
        foreach (var sibling in Items)
            if (!ReferenceEquals(sibling, item)) sibling.Submenu?.Close();
        SetActiveItem(item);
        item.Submenu.OpenFor(item);
    }

    internal void ActivateRadio(MenuItem selected)
    {
        var groupName = selected.GroupName;
        if (string.IsNullOrWhiteSpace(groupName)) return;
        foreach (var item in GetMenuTreeItems())
        {
            if (!ReferenceEquals(item, selected) &&
                string.Equals(item.GroupName, groupName, StringComparison.Ordinal))
                item.SetCheckedFromGroup(false);
        }
    }

    public Menu GetDeepestOpenMenu()
    {
        var current = this;
        while (current.Items.Select(item => item.Submenu).FirstOrDefault(menu => menu?.IsOpen == true) is { } child)
            current = child;
        return current;
    }

    public override bool HandlePopupKey(int keyCode, bool shift, bool control, bool alt)
        => GetDeepestOpenMenu().HandleKey(keyCode, shift, control, alt);

    public bool HandleKey(int keyCode, bool shift = false, bool control = false, bool alt = false)
    {
        var enabled = Items.Where(item => item.IsEnabled).ToArray();
        if (enabled.Length == 0) return keyCode == 27 && CloseOnEscape;

        switch (keyCode)
        {
            case 38:
                MoveActive(enabled, -1);
                return true;
            case 40:
                MoveActive(enabled, 1);
                return true;
            case 36:
                SetActiveItem(enabled[0]);
                return true;
            case 35:
                SetActiveItem(enabled[^1]);
                return true;
            case 39:
                if (ActiveItem?.Submenu != null)
                {
                    OpenSubmenu(ActiveItem);
                    ActiveItem.Submenu.SetActiveItem(ActiveItem.Submenu.Items.FirstOrDefault(item => item.IsEnabled));
                }
                else if (GetRootMenu().OwnerItem?.Parent is MenuBar nextBar)
                    SwitchMenuBarItem(nextBar, 1);
                return true;
            case 37:
                if (OwnerItem?.Parent is Menu parent)
                {
                    Close();
                    parent.SetActiveItem(OwnerItem);
                }
                else if (OwnerItem?.Parent is MenuBar previousBar)
                    SwitchMenuBarItem(previousBar, -1);
                return true;
            case 13:
            case 32:
                ActiveItem?.DispatchEvent(StandardEvents.CreateClick());
                return true;
            case 27:
                Close();
                return true;
            case 9:
                CloseMenuTree();
                return false;
            default:
                return false;
        }
    }

    protected override Rect GetPopupBounds()
    {
        if (_screenPosition is not { } position) return base.GetPopupBounds();
        return ConstrainPopupBounds(new Rect(position.X, position.Y, Geometry.Width, Geometry.Height));
    }

    private Menu GetRootMenu()
    {
        var current = this;
        while (current.OwnerItem?.Parent is Menu parent)
            current = parent;
        return current;
    }

    private IEnumerable<MenuItem> GetMenuTreeItems()
    {
        var root = GetRootMenu();
        foreach (var item in root.QueryAll<MenuItem>())
            yield return item;
    }

    private void MoveActive(IReadOnlyList<MenuItem> enabled, int direction)
    {
        var index = ActiveItem == null ? -1 : Array.IndexOf(enabled.ToArray(), ActiveItem);
        index = direction > 0
            ? (index + 1 + enabled.Count) % enabled.Count
            : (index - 1 + enabled.Count) % enabled.Count;
        SetActiveItem(enabled[index]);
    }

    private static void SwitchMenuBarItem(MenuBar bar, int direction)
    {
        var items = bar.Items.Where(item => item.IsEnabled && item.Submenu != null).ToArray();
        if (items.Length == 0) return;
        var index = Array.FindIndex(items, item => item.Submenu?.IsOpen == true);
        index = (index + direction + items.Length) % items.Length;
        bar.Open(items[index], toggle: false);
        var submenu = items[index].Submenu!;
        submenu.SetActiveItem(submenu.Items.FirstOrDefault(item => item.IsEnabled));
    }

    private static void CloseSiblingMenus(MenuItem owner)
    {
        var siblings = owner.Parent switch
        {
            Menu menu => menu.Items,
            MenuBar bar => bar.Items,
            _ => []
        };
        foreach (var sibling in siblings)
            if (!ReferenceEquals(sibling, owner)) sibling.Submenu?.Close();
    }
}

public sealed class ContextMenu : Menu
{
}

public class MenuItem : UIElement, ITextSelectable
{
    public string TextContent
    {
        get => GetProperty<string>(nameof(TextContent)) ?? "";
        set
        {
            SetProperty(nameof(TextContent), value);
        }
    }
    public string ShortcutText { get => GetProperty<string>(nameof(ShortcutText)) ?? ""; set => SetProperty(nameof(ShortcutText), value); }
    public Square.Graphics.Image? Icon { get => GetProperty<Square.Graphics.Image>(nameof(Icon)); set => SetProperty(nameof(Icon), value); }
    public bool IsCheckable { get => GetProperty<bool>(nameof(IsCheckable)); set => SetProperty(nameof(IsCheckable), value); }
    public bool IsChecked { get => GetProperty<bool>(nameof(IsChecked)); set => SetProperty(nameof(IsChecked), value); }
    public string GroupName { get => GetProperty<string>(nameof(GroupName)) ?? ""; set => SetProperty(nameof(GroupName), value); }
    public bool StaysOpenOnClick { get => GetProperty<bool>(nameof(StaysOpenOnClick)); set => SetProperty(nameof(StaysOpenOnClick), value); }
    public Action<MenuItem>? Command { get; set; }
    public Menu? Submenu => Children.OfType<Menu>().FirstOrDefault();
    public MenuItemRole Role => Submenu != null ? MenuItemRole.Submenu :
        !string.IsNullOrWhiteSpace(GroupName) ? MenuItemRole.Radio :
        IsCheckable ? MenuItemRole.Check : MenuItemRole.Command;

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds => Geometry;

    public override Size Measure(Size availableSize)
    {
        var label = ControlDrawing.MeasureText(this, TextContent, 14);
        if (Parent is MenuBar) return new Size(Math.Max(56, label.Width + 24), 32);
        var shortcut = string.IsNullOrEmpty(ShortcutText)
            ? Size.Zero
            : ControlDrawing.MeasureText(this, ShortcutText, 12);
        var shortcutWidth = shortcut.Width > 0 ? shortcut.Width + 20 : 0;
        return new Size(Math.Max(180, 54 + label.Width + shortcutWidth), 32);
    }

    public override void Paint(IRenderContext context)
    {
        var active = HasState(ElementState.Hover) || HasState(ElementState.Open) ||
                     Parent is Menu menu && ReferenceEquals(menu.ActiveItem, this);
        var styledBackground = ControlDrawing.GetStyledColor(this, "background", Color.Transparent);
        var background = styledBackground.A > 0
            ? styledBackground
            : active && IsEnabled ? Color.FromRgb(225, 238, 252) : Color.Transparent;
        if (background.A > 0) context.FillRect(Geometry, new SolidColorBrush(background));

        var foreground = IsEnabled
            ? ControlDrawing.GetStyledColor(this, "color", Color.FromRgb(32, 36, 40))
            : Color.FromRgb(150, 154, 160);
        var isBarItem = Parent is MenuBar;
        var labelX = Geometry.X + (isBarItem ? 12 : 30);
        var labelSize = ControlDrawing.MeasureText(this, TextContent, 14);
        ControlDrawing.DrawText(context, this, TextContent,
            new Point(labelX, Geometry.Y + (Geometry.Height - labelSize.Height) / 2f), foreground, 14);

        if (isBarItem) return;
        DrawSelectionMark(context, foreground);
        if (!string.IsNullOrEmpty(ShortcutText))
        {
            var size = ControlDrawing.MeasureText(this, ShortcutText, 12);
            ControlDrawing.DrawText(context, this, ShortcutText,
                new Point(Geometry.Right - size.Width - 24, Geometry.Y + (Geometry.Height - size.Height) / 2f), foreground, 12);
        }
        if (Submenu != null)
        {
            var cy = Geometry.Y + Geometry.Height / 2f;
            var arrow = PathGeometry.Create()
                .MoveTo(new Point(Geometry.Right - 13, cy - 4))
                .LineTo(new Point(Geometry.Right - 9, cy))
                .LineTo(new Point(Geometry.Right - 13, cy + 4));
            context.DrawPath(arrow, Pen.FromColor(foreground, 1.5f));
        }
    }

    public override Element? HitTest(Point point)
        => IsVisible && Geometry.Contains(point) ? this : null;

    protected override void OnDefaultAction(Event e)
    {
        base.OnDefaultAction(e);
        if (e.Type == StandardEvents.Click) Activate();
    }

    protected override void OnStateChanged(ElementState flag, bool on)
    {
        base.OnStateChanged(flag, on);
        if (flag != ElementState.Hover || !on || !IsEnabled) return;
        if (Parent is Menu menu)
        {
            menu.SetActiveItem(this);
            if (Submenu != null) menu.OpenSubmenu(this);
        }
        else if (Parent is MenuBar bar)
        {
            bar.SwitchOnHover(this);
        }
    }

    internal void SetCheckedFromGroup(bool value)
    {
        if (IsChecked == value) return;
        IsChecked = value;
        DispatchEvent(StandardEvents.CreateChange());
    }

    private void Activate()
    {
        if (!IsEnabled) return;
        if (Submenu != null)
        {
            if (Parent is Menu menu) menu.OpenSubmenu(this);
            else if (Parent is MenuBar bar) bar.Open(this, toggle: true);
            return;
        }

        var changed = false;
        if (Role == MenuItemRole.Check)
        {
            IsChecked = !IsChecked;
            changed = true;
        }
        else if (Role == MenuItemRole.Radio && !IsChecked)
        {
            FindOwnerMenu()?.ActivateRadio(this);
            IsChecked = true;
            changed = true;
        }
        if (changed) DispatchEvent(StandardEvents.CreateChange());
        Command?.Invoke(this);
        if (!StaysOpenOnClick) FindOwnerMenu()?.CloseMenuTree();
    }

    private Menu? FindOwnerMenu()
    {
        for (var current = Parent; current != null; current = current.Parent)
            if (current is Menu menu) return menu;
        return null;
    }

    private void DrawSelectionMark(IRenderContext context, Color color)
    {
        if (!IsChecked) return;
        var center = new Point(Geometry.X + 14, Geometry.Y + Geometry.Height / 2f);
        if (Role == MenuItemRole.Radio)
        {
            context.FillGeometry(new EllipseGeometry(center, 4, 4), new SolidColorBrush(color));
            return;
        }
        var check = PathGeometry.Create()
            .MoveTo(new Point(center.X - 5, center.Y))
            .LineTo(new Point(center.X - 1, center.Y + 4))
            .LineTo(new Point(center.X + 6, center.Y - 5));
        context.DrawPath(check, Pen.FromColor(color, 1.8f));
    }
}

public sealed class MenuSeparator : UIElement
{
    public override Size Measure(Size availableSize) => new(availableSize.Width, 9);

    public override void Paint(IRenderContext context)
    {
        var y = Geometry.Y + Geometry.Height / 2f;
        context.DrawPath(PathGeometry.Create()
            .MoveTo(new Point(Geometry.X + 8, y))
            .LineTo(new Point(Geometry.Right - 8, y)), Pen.FromColor(Color.FromRgb(218, 221, 225)));
    }

    public override Element? HitTest(Point point) => null;
}
