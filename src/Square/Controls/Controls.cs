using System.Numerics;
using System.Diagnostics;
using System.Text;
using Square.Events;
using Square.Graphics;
using global::Square.Text.Glyph;
using Square.UI;
using FontManager = global::Square.Text.FontManager;

namespace Square.Controls;

public class View : UIElement
{
    public override void Paint(IRenderContext ctx)
    {
        var background = ControlDrawing.GetStyledColor(this, "background", Color.Transparent);
        ControlDrawing.DrawStyledBackground(ctx, this, background);
    }
}

/// <summary>
/// Scrollable viewport backed by the framework's CSS overflow, clipping and wheel pipeline.
/// The default mode scrolls vertically and clips horizontal overflow.
/// </summary>
public class ScrollViewer : View
{
    public ScrollViewer()
    {
        Style.SetCascaded("overflow-x", "hidden", int.MinValue);
        Style.SetCascaded("overflow-y", "auto", int.MinValue);
    }

    public float HorizontalOffset => ScrollLeft;
    public float VerticalOffset => ScrollTop;
    public float ExtentWidth => ScrollContentSize.Width;
    public float ExtentHeight => ScrollContentSize.Height;
    public float ViewportWidth => Geometry.Width;
    public float ViewportHeight => Geometry.Height;
    public float ScrollableWidth => Math.Max(0, ExtentWidth - ViewportWidth);
    public float ScrollableHeight => Math.Max(0, ExtentHeight - ViewportHeight);

    public void ScrollTo(float horizontalOffset, float verticalOffset)
    {
        ScrollLeft = horizontalOffset;
        ScrollTop = verticalOffset;
    }

    public void ScrollToTop() => ScrollTop = 0;
    public void ScrollToBottom() => ScrollTop = ScrollableHeight;
}

public class Text : UIElement, ITextSelectable
{
    private readonly DomTextContent _domText;

    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.Black; set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }

    public Text() { _domText = new DomTextContent(this); }
    public Text(string text) : this() { TextContent = text; }

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(this, TextContent, FontSize, Geometry.Position);

    public override Size Measure(Size availableSize)
    {
        if (string.IsNullOrEmpty(TextContent)) return Size.Zero;
        return ControlDrawing.MeasureText(this, TextContent, FontSize, availableSize);
    }

    public override void Paint(IRenderContext ctx)
    {
        if (string.IsNullOrEmpty(TextContent)) return;
        ControlDrawing.DrawText(ctx, this, TextContent, Geometry.Position, Color, FontSize, maxSize: Geometry.Size);
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(TextContent))
            _domText.Text = TextContent;
    }
}

/// <summary>List item, similar to HTML <c>li</c>. Optional marker and text; may also host children.</summary>
public class ListItem : UIElement, ITextSelectable
{
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.Black; set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }
    public bool IsSelected { get => GetProperty<bool>(nameof(IsSelected)); set => SetProperty(nameof(IsSelected), value); }
    /// <summary>Bullet or number prefix, e.g. "• ", "1. ". Empty draws no marker.</summary>
    public string Marker { get => GetProperty<string>(nameof(Marker)) ?? "• "; set => SetProperty(nameof(Marker), value); }

    public ListItem() { }
    public ListItem(string text) { TextContent = text; }

    public string SelectableText => Marker + TextContent;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(this, Marker + TextContent, FontSize, Geometry.Position);

    public override Size Measure(Size availableSize)
    {
        var label = Marker + TextContent;
        if (string.IsNullOrEmpty(label.Trim()))
        {
            var childWidth = 0f;
            var childHeight = 0f;
            foreach (var child in Children)
            {
                var size = child.Measure(availableSize);
                childWidth = Math.Max(childWidth, size.Width);
                childHeight += size.Height;
            }
            return new Size(childWidth, childHeight);
        }

        return ControlDrawing.MeasureText(this, label, FontSize);
    }

    public override void Paint(IRenderContext ctx)
    {
        var background = ControlDrawing.GetStyledColor(this, "background",
            IsSelected ? Color.FromRgb(0, 120, 212) : Color.Transparent);
        ControlDrawing.DrawStyledBackground(ctx, this, background);

        var label = Marker + TextContent;
        if (!string.IsNullOrEmpty(label.Trim()))
        {
            var foreground = IsSelected
                ? ControlDrawing.GetStyledColor(this, "color", Color.White)
                : ControlDrawing.GetStyledColor(this, "color", Color);
            ControlDrawing.DrawText(ctx, this, label, Geometry.Position, foreground, FontSize);
        }
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(IsSelected)) SetState(ElementState.Checked, IsSelected);
    }
}

/// <summary>Hyperlink-style control, similar to HTML <c>a</c>. Use <see cref="Href"/> for the target URL/path.</summary>
public class Link : UIElement, ITextSelectable
{
    private readonly DomTextContent _domText;

    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public string Href { get => GetProperty<string>(nameof(Href)) ?? ""; set => SetProperty(nameof(Href), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.FromRgb(0, 102, 204); set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }
    public bool Underline { get => !Properties.HasValue(nameof(Underline)) || GetProperty<bool>(nameof(Underline)); set => SetProperty(nameof(Underline), value); }

    public Link()
    {
        _domText = new DomTextContent(this);
        AddEventListener("click", Activate);
    }
    public Link(string text) : this() { TextContent = text; }
    public Link(string text, string href) : this() { TextContent = text; Href = href; }

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(this, TextContent, FontSize, Geometry.Position);

    public override Size Measure(Size availableSize)
    {
        if (string.IsNullOrEmpty(TextContent)) return Size.Zero;
        var font = ControlDrawing.ResolveFont(this, FontSize);
        var measured = ControlDrawing.MeasureText(this, TextContent, FontSize);
        return new Size(ControlDrawing.MeasureRenderedTextWidth(TextContent, font), measured.Height);
    }

    public override void Paint(IRenderContext ctx)
    {
        if (string.IsNullOrEmpty(TextContent)) return;
        var color = IsEnabled
            ? ControlDrawing.GetStyledColor(this, "color", Color)
            : Color.FromRgb(125, 130, 136);
        var origin = Geometry.Position;
        ControlDrawing.DrawText(ctx, this, TextContent, origin, color, FontSize);
        if (Underline)
        {
            var font = ControlDrawing.ResolveFont(this, FontSize);
            var underlineWidth = ControlDrawing.MeasureRenderedTextWidth(TextContent, font);
            var y = origin.Y + ControlDrawing.MeasureText(this, TextContent, FontSize).Height - 1f;
            ctx.FillRect(new Rect(origin.X, y, underlineWidth, 1f), new SolidColorBrush(color));
        }
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(TextContent))
            _domText.Text = TextContent;
    }

    protected virtual void Activate()
    {
        if (!IsEnabled || !TryGetExternalUri(Href, out var uri)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private static bool TryGetExternalUri(string href, out Uri uri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var candidate) &&
            candidate.Scheme is "http" or "https" or "mailto")
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }
}

public class Button : UIElement, ITextSelectable
{
    private readonly DomTextContent _domText;

    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Background { get => Properties.HasValue(nameof(Background)) ? GetProperty<Color>(nameof(Background)) : Color.FromRgb(0, 120, 212); set => SetProperty(nameof(Background), value); }
    public Color Foreground { get => Properties.HasValue(nameof(Foreground)) ? GetProperty<Color>(nameof(Foreground)) : Color.White; set => SetProperty(nameof(Foreground), value); }

    public Button() { _domText = new DomTextContent(this); }
    public Button(string text) : this() { TextContent = text; }

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds
    {
        get
        {
            var textSize = ControlDrawing.MeasureText(this, TextContent, 14f);
            return new Rect(
                Geometry.X + (Geometry.Width - textSize.Width) / 2f,
                Geometry.Y + (Geometry.Height - textSize.Height) / 2f,
                textSize.Width,
                textSize.Height);
        }
    }

    public override Size Measure(Size availableSize)
    {
        var textSize = ControlDrawing.MeasureText(this, TextContent, 14f);
        return new Size(textSize.Width + 32, Math.Max(36, textSize.Height + 12));
    }

    public override void Paint(IRenderContext ctx)
    {
        var background = IsEnabled
            ? ControlDrawing.GetStyledColor(this, "background", Background)
            : Color.FromRgb(170, 175, 180);
        var foreground = IsEnabled
            ? ControlDrawing.GetStyledColor(this, "color", Foreground)
            : Color.FromRgb(235, 235, 235);
        ControlDrawing.DrawStyledBackground(ctx, this, background);

        var textSize = ControlDrawing.MeasureText(this, TextContent, 14f);
        var textPosition = new Point(
            Geometry.X + (Geometry.Width - textSize.Width) / 2f,
            Geometry.Y + (Geometry.Height - textSize.Height) / 2f);
        ControlDrawing.DrawText(ctx, this, TextContent, textPosition, foreground, 14f);
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(TextContent))
            _domText.Text = TextContent;
    }
}

public class CheckBox : UIElement, ITextSelectable
{
    public bool IsChecked { get => GetProperty<bool>(nameof(IsChecked)); set => SetProperty(nameof(IsChecked), value); }
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }

    public CheckBox()
    {
        AddEventListener("click", ToggleFromInput);
    }

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(
        this,
        TextContent,
        14f,
        new Point(Geometry.X + 26, Geometry.Y + (Geometry.Height - 17) / 2f));

    public override Size Measure(Size availableSize)
    {
        var text = ControlDrawing.MeasureText(this, TextContent, 14f);
        return new Size(26 + text.Width, Math.Max(24, text.Height));
    }

    public override void Paint(IRenderContext ctx)
    {
        var box = new Rect(Geometry.X, Geometry.Y + (Geometry.Height - 18) / 2f, 18, 18);
        ctx.FillRect(box, new SolidColorBrush(IsEnabled ? Color.White : Color.FromRgb(235, 235, 235)));
        ctx.DrawRect(box, Pen.FromColor(IsFocused ? Color.FromRgb(0, 95, 184) : Color.FromRgb(95, 100, 106)));
        if (IsChecked)
        {
            ctx.FillRect(box.Inflate(-2, -2), new SolidColorBrush(Color.FromRgb(0, 120, 212)));
            ctx.DrawPath(PathGeometry.Create()
                .MoveTo(new Point(box.X + 4, box.Y + 9))
                .LineTo(new Point(box.X + 8, box.Y + 13))
                .LineTo(new Point(box.X + 15, box.Y + 5)),
                Pen.FromColor(Color.White, 2));
        }
        ControlDrawing.DrawText(ctx, this, TextContent,
            new Point(Geometry.X + 26, Geometry.Y + (Geometry.Height - 17) / 2f), Color.Black, 14f);
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(IsChecked)) SetState(ElementState.Checked, IsChecked);
    }

    private void ToggleFromInput()
    {
        if (!IsEnabled) return;
        IsChecked = !IsChecked;
        DispatchEvent(StandardEvents.CreateChange());
    }
}

public class Radio : UIElement, ITextSelectable
{
    public bool IsChecked { get => GetProperty<bool>(nameof(IsChecked)); set => SetProperty(nameof(IsChecked), value); }
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public string GroupName { get => GetProperty<string>(nameof(GroupName)) ?? ""; set => SetProperty(nameof(GroupName), value); }

    public Radio()
    {
        AddEventListener("click", SelectFromInput);
    }

    public string SelectableText => TextContent;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(
        this,
        TextContent,
        14f,
        new Point(Geometry.X + 26, Geometry.Y + (Geometry.Height - 17) / 2f));

    public override Size Measure(Size availableSize)
    {
        var text = ControlDrawing.MeasureText(this, TextContent, 14f);
        return new Size(26 + text.Width, Math.Max(24, text.Height));
    }

    public override void Paint(IRenderContext ctx)
    {
        var center = new Point(Geometry.X + 9, Geometry.Y + Geometry.Height / 2f);
        ctx.FillGeometry(new EllipseGeometry(center, 9, 9), new SolidColorBrush(IsEnabled ? Color.White : Color.FromRgb(235, 235, 235)));
        ctx.DrawGeometry(new EllipseGeometry(center, 9, 9), Pen.FromColor(Color.FromRgb(95, 100, 106)));
        if (IsChecked)
            ctx.FillGeometry(new EllipseGeometry(center, 5, 5), new SolidColorBrush(Color.FromRgb(0, 120, 212)));
        ControlDrawing.DrawText(ctx, this, TextContent,
            new Point(Geometry.X + 26, Geometry.Y + (Geometry.Height - 17) / 2f), Color.Black, 14f);
    }

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(IsChecked)) SetState(ElementState.Checked, IsChecked);
    }

    private void SelectFromInput()
    {
        if (!IsEnabled || IsChecked) return;
        if (Parent != null && !string.IsNullOrEmpty(GroupName))
        {
            foreach (var radio in Parent.QueryAll<Radio>())
                if (radio != this && radio.GroupName == GroupName) radio.IsChecked = false;
        }
        IsChecked = true;
        DispatchEvent(StandardEvents.CreateChange());
    }
}

public class Select : UIElement, IPopupElement, ITextSelectable
{
    public string Value { get => GetProperty<string>(nameof(Value)) ?? ""; set => SetProperty(nameof(Value), value); }
    public string[] Options { get => GetProperty<string[]>(nameof(Options)) ?? []; set => SetProperty(nameof(Options), value ?? []); }
    public string Placeholder { get => GetProperty<string>(nameof(Placeholder)) ?? "Select"; set => SetProperty(nameof(Placeholder), value); }
    public bool IsOpen { get; private set; }
    public bool IsPopupOpen => IsOpen && Options.Length > 0;
    public Rect PopupBounds => GetDropDownRect();
    public bool DismissOnPointerDownOutside => true;
    public bool CloseOnEscape => true;
    private int _hoveredOption = -1;

    public string SelectableText => string.IsNullOrEmpty(Value) ? Placeholder : Value;
    public Rect SelectableTextBounds => ControlDrawing.GetTextBounds(this, SelectableText, 14f, new Point(Geometry.X + 8, Geometry.Y + 8));

    public override int ZIndex
    {
        get => IsOpen ? 1000 : base.ZIndex;
        set => base.ZIndex = value;
    }

    public Select()
    {
        AddEventListener<KeyboardEvent>(StandardEvents.KeyDown, e =>
        {
            if (HandleKey(e.KeyCode, e.ShiftKey, e.ControlKey, e.AltKey))
                e.PreventDefault();
        });
    }

    public override Size Measure(Size availableSize) => new(200, 36);

    public override void Paint(IRenderContext ctx)
    {
        ControlDrawing.DrawInputFrame(ctx, this);
        var value = string.IsNullOrEmpty(Value) ? Placeholder : Value;
        var color = string.IsNullOrEmpty(Value) ? Color.FromRgb(125, 130, 136) : Color.Black;
        ControlDrawing.DrawText(ctx, this, value, new Point(Geometry.X + 8, Geometry.Y + 8), color, 14f);
        var arrowY = Geometry.Y + Geometry.Height / 2f;
        var arrow = IsOpen
            ? PathGeometry.Create().MoveTo(new Point(Geometry.Right - 20, arrowY + 3)).LineTo(new Point(Geometry.Right - 15, arrowY - 2)).LineTo(new Point(Geometry.Right - 10, arrowY + 3))
            : PathGeometry.Create().MoveTo(new Point(Geometry.Right - 20, arrowY - 2)).LineTo(new Point(Geometry.Right - 15, arrowY + 3)).LineTo(new Point(Geometry.Right - 10, arrowY - 2));
        ctx.DrawPath(arrow, Pen.FromColor(Color.FromRgb(70, 75, 80), 1.5f));

    }

    public void PaintPopup(IRenderContext ctx)
    {
        if (!IsPopupOpen) return;
        var popup = GetDropDownRect();
        ctx.FillRect(popup, new SolidColorBrush(Color.White));
        ctx.DrawRect(popup, Pen.FromColor(Color.FromRgb(145, 150, 156)));
        for (var i = 0; i < Options.Length; i++)
        {
            var row = new Rect(popup.X + 1, popup.Y + 1 + i * 32, popup.Width - 2, 32);
            if (i == _hoveredOption)
                ctx.FillRect(row, new SolidColorBrush(Color.FromRgb(230, 242, 252)));
            else if (Options[i] == Value)
                ctx.FillRect(row, new SolidColorBrush(Color.FromRgb(242, 247, 250)));
            ControlDrawing.DrawText(ctx, this, Options[i], new Point(row.X + 8, row.Y + 7), Color.Black, 14f);
        }
    }

    public override Element? HitTest(Point point)
    {
        if (!IsVisible) return null;
        return Geometry.Contains(point) ? this : null;
    }

    public Element? HitTestPopup(Point point) => IsPopupOpen && PopupBounds.Contains(point) ? this : null;
    public bool ContainsPopupInteraction(Point point) => Geometry.Contains(point) || PopupBounds.Contains(point);
    public bool HandlePopupKey(int keyCode, bool shift, bool control, bool alt)
        => HandleKey(keyCode, shift, control, alt);
    public Point MapPointToContent(Point point) => point;
    public void ClosePopup() => CloseDropDown();

    public bool HandleKey(int keyCode, bool shift = false, bool control = false, bool alt = false)
    {
        if (!IsEnabled || Options.Length == 0) return false;

        if (!IsOpen)
        {
            if (keyCode is not (13 or 32) && !(alt && keyCode == 40)) return false;
            OpenDropDown();
            return true;
        }

        switch (keyCode)
        {
            case 38:
                MoveHoveredOption(-1);
                return true;
            case 40:
                MoveHoveredOption(1);
                return true;
            case 36:
                SetHoveredOption(0);
                return true;
            case 35:
                SetHoveredOption(Options.Length - 1);
                return true;
            case 13:
            case 32:
                SelectOption(_hoveredOption >= 0 ? _hoveredOption : GetSelectedOptionIndex());
                return true;
            case 27:
                CloseDropDown();
                return true;
            case 9:
                CloseDropDown();
                return false;
            default:
                return false;
        }
    }

    public void HandlePointerDown(Point point)
    {
        if (!IsEnabled) return;
        if (Geometry.Contains(point))
        {
            ToggleDropDown();
            return;
        }

        if (!IsOpen) return;
        var popup = GetDropDownRect();
        if (popup.Contains(point))
        {
            var index = Math.Clamp((int)((point.Y - popup.Y - 1) / 32), 0, Options.Length - 1);
            SelectOption(index);
        }
    }

    public bool HandlePointerMove(Point point)
    {
        var next = IsOpen && GetDropDownRect().Contains(point)
            ? Math.Clamp((int)((point.Y - GetDropDownRect().Y - 1) / 32), 0, Options.Length - 1)
            : -1;
        if (_hoveredOption == next) return false;
        _hoveredOption = next;
        InvalidatePaint();
        return true;
    }

    public void CloseDropDown()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _hoveredOption = -1;
        Parent?.InvalidatePaint();
        InvalidatePaint();
    }

    private void ToggleDropDown()
    {
        if (!IsEnabled || Options.Length == 0) return;
        if (IsOpen) CloseDropDown();
        else OpenDropDown();
    }

    private void OpenDropDown()
    {
        IsOpen = true;
        _hoveredOption = GetSelectedOptionIndex();
        Parent?.InvalidatePaint();
        InvalidatePaint();
    }

    private int GetSelectedOptionIndex()
    {
        var index = Array.IndexOf(Options, Value);
        return index >= 0 ? index : 0;
    }

    private void MoveHoveredOption(int direction)
    {
        var index = _hoveredOption >= 0 ? _hoveredOption : GetSelectedOptionIndex();
        SetHoveredOption((index + direction + Options.Length) % Options.Length);
    }

    private void SetHoveredOption(int index)
    {
        if (_hoveredOption == index) return;
        _hoveredOption = index;
        InvalidatePaint();
    }

    private void SelectOption(int index)
    {
        if (index < 0 || index >= Options.Length) return;
        var nextValue = Options[index];
        var changed = !string.Equals(Value, nextValue, StringComparison.Ordinal);
        Value = nextValue;
        CloseDropDown();
        if (changed) DispatchEvent(StandardEvents.CreateChange());
    }

    private Rect GetDropDownRect() => new(Geometry.X, Geometry.Bottom + 2, Geometry.Width, Options.Length * 32 + 2);
}

public enum PopupPlacement { Bottom, Top, Left, Right }
public enum PopupAlignment { Start, Center, End }

/// <summary>Top-level anchored content that does not paint in its layout-tree position.</summary>
public class Popup : View, IPopupElement
{
    private bool _isOpen;

    public Popup()
    {
        Style.SetCascaded("position", "absolute", int.MinValue);
        Style.SetCascaded("box-shadow", "0 4px 8px 2px rgba(0,0,0,0.48)", int.MinValue);
    }

    public Element? Anchor { get; set; }
    public PopupPlacement Placement { get; set; } = PopupPlacement.Bottom;
    public PopupAlignment Alignment { get; set; } = PopupAlignment.Start;
    public float HorizontalOffset { get; set; }
    public float VerticalOffset { get; set; } = 4;
    public bool DismissOnPointerDownOutside { get; set; } = true;
    public bool CloseOnEscape { get; set; }
    public bool FlipOnOverflow { get; set; }
    public bool ConstrainToViewport { get; set; }
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (value) Open();
            else Close();
        }
    }

    public bool IsPopupOpen => _isOpen && !PopupBounds.IsEmpty;
    public virtual Rect PopupBounds => ContentBounds;
    protected virtual Rect ContentBounds => GetPopupBounds();

    public override int ZIndex
    {
        get => IsOpen ? 1000 : base.ZIndex;
        set => base.ZIndex = value;
    }

    public virtual void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        Parent?.InvalidatePaint();
        InvalidatePaint();
        DispatchEvent(new Event("open"));
    }

    public virtual void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        Parent?.InvalidatePaint();
        InvalidatePaint();
        DispatchEvent(new Event("close"));
    }

    public void ClosePopup() => Close();

    public virtual bool ContainsPopupInteraction(Point point)
        => ContentBounds.Contains(point) || Anchor?.Geometry.Contains(point) == true;

    public virtual bool HandlePopupKey(int keyCode, bool shift, bool control, bool alt) => false;

    public virtual Point MapPointToContent(Point point)
    {
        var bounds = ContentBounds;
        return new Point(
            point.X - bounds.X + Geometry.X,
            point.Y - bounds.Y + Geometry.Y);
    }

    public override void Paint(IRenderContext context)
    {
        // Popup content is replayed by PaintPopup in the top-level popup layer.
    }

    public virtual void PaintPopup(IRenderContext context)
    {
        if (!IsPopupOpen) return;
        var bounds = ContentBounds;
        var translation = new Vector2(bounds.X - Geometry.X, bounds.Y - Geometry.Y);
        context.PushTransform(Matrix3x2.CreateTranslation(translation));
        if (BoxShadow.TryParse(Style.GetPropertyValue("box-shadow"), out var shadow))
            BoxShadowRendering.Draw(context, Geometry, ControlDrawing.GetStyledRadius(this, Geometry), shadow);
        context.PushClip(Geometry);
        var background = ControlDrawing.GetStyledColor(this, "background", Color.White);
        ControlDrawing.DrawStyledBackground(context, this, background);
        foreach (var child in Children.OrderBy(child => child.ZIndex))
            PaintPopupSubtree(context, child);
        context.PopClip();
        context.PopTransform();
    }

    public virtual Element? HitTestPopup(Point point)
    {
        var bounds = ContentBounds;
        if (!IsPopupOpen || !bounds.Contains(point)) return null;
        var localPoint = MapPointToContent(point);
        foreach (var child in Children.OrderByDescending(child => child.ZIndex))
        {
            var hit = child.HitTest(localPoint);
            if (hit != null) return hit;
        }
        return this;
    }

    protected virtual Rect GetPopupBounds()
    {
        if (Geometry.Width <= 0 || Geometry.Height <= 0) return Rect.Empty;
        var anchor = Anchor == null ? Geometry : GetAnchorBounds(Anchor);
        var x = Placement is PopupPlacement.Bottom or PopupPlacement.Top
            ? Align(anchor.X, anchor.Width, Geometry.Width, Alignment) + HorizontalOffset
            : Placement == PopupPlacement.Left
                ? anchor.X - Geometry.Width - HorizontalOffset
                : anchor.Right + HorizontalOffset;
        var y = Placement is PopupPlacement.Left or PopupPlacement.Right
            ? Align(anchor.Y, anchor.Height, Geometry.Height, Alignment) + VerticalOffset
            : Placement == PopupPlacement.Top
                ? anchor.Y - Geometry.Height - VerticalOffset
                : anchor.Bottom + VerticalOffset;
        var bounds = new Rect(x, y, Geometry.Width, Geometry.Height);
        var viewport = GetPopupViewportBounds();
        if (FlipOnOverflow && !viewport.IsEmpty)
        {
            bounds = Placement switch
            {
                PopupPlacement.Bottom when bounds.Bottom > viewport.Bottom &&
                    anchor.Y - Geometry.Height - VerticalOffset >= viewport.Y =>
                    new Rect(bounds.X, anchor.Y - Geometry.Height - VerticalOffset, bounds.Width, bounds.Height),
                PopupPlacement.Top when bounds.Y < viewport.Y &&
                    anchor.Bottom + Geometry.Height + VerticalOffset <= viewport.Bottom =>
                    new Rect(bounds.X, anchor.Bottom + VerticalOffset, bounds.Width, bounds.Height),
                PopupPlacement.Right when bounds.Right > viewport.Right &&
                    anchor.X - Geometry.Width - HorizontalOffset >= viewport.X =>
                    new Rect(anchor.X - Geometry.Width - HorizontalOffset, bounds.Y, bounds.Width, bounds.Height),
                PopupPlacement.Left when bounds.X < viewport.X &&
                    anchor.Right + Geometry.Width + HorizontalOffset <= viewport.Right =>
                    new Rect(anchor.Right + HorizontalOffset, bounds.Y, bounds.Width, bounds.Height),
                _ => bounds
            };
        }
        return ConstrainPopupBounds(bounds);
    }

    private static Rect GetAnchorBounds(Element anchor)
    {
        var bounds = anchor.Geometry;
        for (var current = anchor.Parent; current != null; current = current.Parent)
        {
            if (current is not IPopupElement popup) continue;
            var popupBounds = popup.PopupBounds;
            var geometry = current.Geometry;
            return new Rect(
                bounds.X + popupBounds.X - geometry.X,
                bounds.Y + popupBounds.Y - geometry.Y,
                bounds.Width,
                bounds.Height);
        }
        return bounds;
    }

    protected Rect ConstrainPopupBounds(Rect bounds)
    {
        if (!ConstrainToViewport) return bounds;
        var viewport = GetPopupViewportBounds();
        if (viewport.IsEmpty) return bounds;
        var width = Math.Min(bounds.Width, viewport.Width);
        var height = Math.Min(bounds.Height, viewport.Height);
        return new Rect(
            Math.Clamp(bounds.X, viewport.X, viewport.Right - width),
            Math.Clamp(bounds.Y, viewport.Y, viewport.Bottom - height),
            width,
            height);
    }

    protected Rect GetPopupViewportBounds()
    {
        if (OwnerDocument is not UIDocument document) return Rect.Empty;
        if (!document.DocumentElement.Geometry.IsEmpty) return document.DocumentElement.Geometry;
        return !document.Body.Geometry.IsEmpty ? document.Body.Geometry : Rect.Empty;
    }

    private static float Align(float start, float anchorLength, float popupLength, PopupAlignment alignment) => alignment switch
    {
        PopupAlignment.Center => start + (anchorLength - popupLength) / 2f,
        PopupAlignment.End => start + anchorLength - popupLength,
        _ => start
    };

    private static void PaintPopupSubtree(IRenderContext context, Element element)
    {
        if (!element.IsVisible || element is IPopupElement) return;
        element.Paint(context);
        var clip = element.GetOverflowClipRect();
        if (!clip.IsEmpty) context.PushClip(clip);
        var offset = element.ScrollOffset;
        var scrolls = element.MapsScrollOffsetForChildren();
        if (scrolls) context.PushTransform(Matrix3x2.CreateTranslation(-offset.X, -offset.Y));
        foreach (var child in element.Children.OrderBy(child => child.ZIndex))
            PaintPopupSubtree(context, child);
        if (scrolls) context.PopTransform();
        if (!clip.IsEmpty) context.PopClip();
    }
}

/// <summary>Modal dialog rendered through the popup layer with a blocking backdrop.</summary>
public class Dialog : Popup
{
    private UIElement? _restoreFocus;

    public Dialog()
    {
        CloseOnEscape = true;
        DismissOnPointerDownOutside = false;
        Style.SetCascaded("background", "#ffffff", int.MinValue);
    }

    public bool IsModal { get; set; } = true;
    public bool CloseOnBackdropClick
    {
        get => DismissOnPointerDownOutside;
        set => DismissOnPointerDownOutside = value;
    }
    public Color BackdropColor { get; set; } = Color.FromRgba(0, 0, 0, 112);

    protected override Rect ContentBounds
    {
        get
        {
            var viewport = GetViewportBounds();
            if (viewport.IsEmpty) return base.ContentBounds;
            return new Rect(
                viewport.X + (viewport.Width - Geometry.Width) / 2f + HorizontalOffset,
                viewport.Y + (viewport.Height - Geometry.Height) / 2f + VerticalOffset,
                Geometry.Width,
                Geometry.Height);
        }
    }

    public override Rect PopupBounds => IsModal ? GetViewportBounds() : ContentBounds;

    public override void Open()
    {
        if (IsOpen) return;
        _restoreFocus = OwnerDocument?.DocumentElement.QueryAll<UIElement>()
            .LastOrDefault(element => element.IsFocused);
        _restoreFocus?.Unfocus();
        base.Open();
        FindInitialFocus()?.Focus();
    }

    public override void Close()
    {
        if (!IsOpen) return;
        foreach (var focused in QueryAll<UIElement>().Where(element => element.IsFocused))
            focused.Unfocus();
        base.Close();
        if (_restoreFocus is { IsAttached: true, IsEnabled: true })
            _restoreFocus.Focus();
        _restoreFocus = null;
    }

    public override bool ContainsPopupInteraction(Point point) => ContentBounds.Contains(point);

    public override void PaintPopup(IRenderContext context)
    {
        if (!IsPopupOpen) return;
        if (IsModal)
            context.FillRect(GetViewportBounds(), new SolidColorBrush(BackdropColor));
        base.PaintPopup(context);
    }

    public override Element? HitTestPopup(Point point)
    {
        if (!IsPopupOpen) return null;
        var hit = base.HitTestPopup(point);
        if (hit != null) return hit;
        return IsModal && GetViewportBounds().Contains(point) ? this : null;
    }

    private Rect GetViewportBounds()
    {
        if (OwnerDocument is UIDocument document)
        {
            if (!document.DocumentElement.Geometry.IsEmpty) return document.DocumentElement.Geometry;
            if (!document.Body.Geometry.IsEmpty) return document.Body.Geometry;
        }
        return Anchor?.Geometry ?? Geometry;
    }

    private UIElement? FindInitialFocus()
        => QueryAll<UIElement>().FirstOrDefault(element => element.IsEnabled &&
            element is Button or Input or TextArea or CheckBox or Radio or Select or Link);
}

public class Image : UIElement, ITextSelectable
{
    private IImageFrameSource? _frameSource;
    private Bitmap? _sourceSurface;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private int _frameIndex;
    private int _completedPlays;
    private TimeSpan _remainingFrameDelay;
    private long _frameDeadline;
    private bool _frameScheduled;

    public string Source { get => GetProperty<string>(nameof(Source)) ?? ""; set => SetProperty(nameof(Source), value); }
    public Square.Graphics.Image? ImageContent { get => GetProperty<Square.Graphics.Image>(nameof(ImageContent)); set => SetProperty(nameof(ImageContent), value); }
    public Exception? Error { get; private set; }

    public string SelectableText => Source;
    public Rect SelectableTextBounds => string.IsNullOrEmpty(Source)
        ? Rect.Empty
        : ControlDrawing.GetTextBounds(this, Source, 12f, new Point(Geometry.X + 8, Geometry.Y + 8));

    private Square.Graphics.Image? DisplayImage => _sourceSurface ?? ImageContent;

    public override Size Measure(Size availableSize) => DisplayImage == null
        ? new Size(160, 96)
        : new Size(DisplayImage.Width, DisplayImage.Height);

    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(Source)) BeginSourceLoad();
        else if (name == nameof(ImageContent))
        {
            if (ImageContent != null)
            {
                ++_loadVersion;
                CancelPendingLoad();
                DisposeLoadedSource();
            }
            else if (!string.IsNullOrWhiteSpace(Source)) BeginSourceLoad();
        }
    }

    protected override void OnAttachedCore()
    {
        base.OnAttachedCore();
        if (_frameSource == null && ImageContent == null && !string.IsNullOrWhiteSpace(Source)) BeginSourceLoad();
        else ResumeAnimation();
    }

    protected override void OnDetachedCore()
    {
        CancelPendingLoad();
        DisposeLoadedSource();
        base.OnDetachedCore();
    }

    protected override void OnIsVisibleChanged(bool isVisible)
    {
        base.OnIsVisibleChanged(isVisible);
        if (isVisible) ResumeAnimation();
        else PauseAnimation();
    }

    public override void Paint(IRenderContext ctx)
    {
        AdvanceAnimationIfDue();
        var image = DisplayImage;
        if (image is VectorImage vectorImage)
        {
            vectorImage.Draw(ctx, Geometry);
            return;
        }

        if (image != null)
        {
            ctx.DrawImage(image, Geometry);
            return;
        }

        const int tileSize = 12;
        for (var y = 0; y < Geometry.Height; y += tileSize)
            for (var x = 0; x < Geometry.Width; x += tileSize)
                ctx.FillRect(new Rect(Geometry.X + x, Geometry.Y + y, tileSize, tileSize),
                    new SolidColorBrush(((x + y) / tileSize) % 2 == 0 ? Color.FromRgb(230, 233, 236) : Color.White));
        ctx.DrawRect(Geometry, Pen.FromColor(Color.FromRgb(150, 155, 160)));
        if (!string.IsNullOrEmpty(Source))
            ControlDrawing.DrawText(ctx, this, Source, new Point(Geometry.X + 8, Geometry.Y + 8), Color.FromRgb(80, 85, 90), 12f);
    }

    private void BeginSourceLoad()
    {
        var version = ++_loadVersion;
        CancelPendingLoad();
        DisposeLoadedSource();
        Error = null;
        InvalidateLayout();

        var source = Source;
        if (!IsAttached || ImageContent != null || string.IsNullOrWhiteSpace(source)) return;

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _ = LoadSourceAsync(source, version, cancellation.Token);
    }

    private async Task LoadSourceAsync(string source, int version, CancellationToken cancellationToken)
    {
        IImageFrameSource? loaded = null;
        try
        {
            loaded = await ImageSourceLoaderRegistry.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            var dispatcher = Dispatcher;
            if (dispatcher == null)
            {
                loaded.Dispose();
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                if (version != _loadVersion || cancellationToken.IsCancellationRequested || !IsAttached)
                {
                    loaded.Dispose();
                    return;
                }

                ApplyLoadedSource(loaded);
                loaded = null;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            loaded?.Dispose();
        }
        catch (Exception exception)
        {
            loaded?.Dispose();
            var dispatcher = Dispatcher;
            if (dispatcher == null) return;
            await dispatcher.InvokeAsync(() =>
            {
                if (version != _loadVersion || cancellationToken.IsCancellationRequested || !IsAttached) return;
                Error = exception;
                InvalidatePaint();
                DispatchEvent(new Event("loaderror", new EventInit { Bubbles = true }));
            }).ConfigureAwait(false);
        }
    }

    private void ApplyLoadedSource(IImageFrameSource source)
    {
        DisposeLoadedSource();
        _frameSource = source;
        _sourceSurface = new Bitmap(source.Width, source.Height);
        _frameIndex = 0;
        _completedPlays = 0;
        CopyCurrentFrame();
        Error = null;
        InvalidateLayout();
        DispatchEvent(new Event("load", new EventInit { Bubbles = true }));
        ResumeAnimation();
    }

    private void CopyCurrentFrame()
    {
        if (_frameSource == null || _sourceSurface == null) return;
        _sourceSurface.CopyPixelsFrom(_frameSource.GetFrame(_frameIndex));
    }

    private void ResumeAnimation()
    {
        if (!CanAnimate() || _frameScheduled) return;
        var delay = _remainingFrameDelay > TimeSpan.Zero
            ? _remainingFrameDelay
            : NormalizeFrameDelay(_frameSource!.GetFrameDuration(_frameIndex));
        _remainingFrameDelay = TimeSpan.Zero;
        _frameDeadline = Stopwatch.GetTimestamp() + ToStopwatchTicks(delay);
        _frameScheduled = true;
        DispatchEvent(StandardEvents.CreateRequestFrame(delay));
    }

    private void PauseAnimation()
    {
        if (!_frameScheduled) return;
        var ticks = Math.Max(0, _frameDeadline - Stopwatch.GetTimestamp());
        _remainingFrameDelay = TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency);
        _frameScheduled = false;
    }

    private void AdvanceAnimationIfDue()
    {
        if (!_frameScheduled || Stopwatch.GetTimestamp() < _frameDeadline) return;
        _frameScheduled = false;
        if (!CanAnimate()) return;

        if (_frameIndex + 1 < _frameSource!.FrameCount)
        {
            _frameIndex++;
        }
        else
        {
            _completedPlays++;
            if (_frameSource.PlayCount > 0 && _completedPlays >= _frameSource.PlayCount) return;
            _frameIndex = 0;
        }

        CopyCurrentFrame();
        ResumeAnimation();
    }

    private bool CanAnimate() => IsAttached && IsVisible && _frameSource is { FrameCount: > 1 };

    private static TimeSpan NormalizeFrameDelay(TimeSpan delay) =>
        delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(10);

    private static long ToStopwatchTicks(TimeSpan delay) =>
        Math.Max(0, (long)Math.Ceiling(delay.TotalSeconds * Stopwatch.Frequency));

    private void CancelPendingLoad()
    {
        var cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        if (cancellation == null) return;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void DisposeLoadedSource()
    {
        PauseAnimation();
        _frameSource?.Dispose();
        _frameSource = null;
        _sourceSurface?.Dispose();
        _sourceSurface = null;
        _frameIndex = 0;
        _completedPlays = 0;
        _remainingFrameDelay = TimeSpan.Zero;
    }
}

public class Canvas : UIElement, ITextSelectable
{
    private Action<IRenderContext, Rect>? _animationFrameCallback;

    public Action<IRenderContext, Rect>? DrawContent { get; set; }

    public string SelectableText => GetProperty<string>(nameof(SelectableText)) ?? "Canvas";
    public Rect SelectableTextBounds => Geometry;

    /// <summary>请求后续帧；默认 30fps，避免软件全窗口 Present 时 CPU 过高。</summary>
    public void RequestFrame(double fps = 30d)
    {
        InvalidatePaint();
        DispatchEvent(StandardEvents.CreateRequestFrame(fps));
    }

    public void RequestAnimationFrame(Action<IRenderContext, Rect> callback) =>
        RequestAnimationFrame(callback, 30d);

    public void RequestAnimationFrame(Action<IRenderContext, Rect> callback, double fps)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _animationFrameCallback = callback;
        RequestFrame(fps);
    }

    public override Size Measure(Size availableSize) => new(300, 140);

    public override void Paint(IRenderContext ctx)
    {
        // 轻量背景：避免每帧绘制大量网格 Path（软件光栅很贵）
        ctx.FillRect(Geometry, new SolidColorBrush(Color.White));
        ctx.DrawRect(Geometry, Pen.FromColor(Color.FromRgb(170, 175, 180)));

        var frameCallback = _animationFrameCallback;
        _animationFrameCallback = null;
        if (frameCallback != null)
            frameCallback(ctx, Geometry);
        else if (DrawContent != null)
            DrawContent(ctx, Geometry);
        else
        {
            ctx.FillRect(new Rect(Geometry.X + 20, Geometry.Y + 20, 80, 44), new SolidColorBrush(Color.FromRgb(0, 120, 212)));
            ctx.FillGeometry(new EllipseGeometry(new Point(Geometry.X + 150, Geometry.Y + 50), 28, 28),
                new SolidColorBrush(Color.FromRgb(18, 155, 105)));
        }
    }
}

internal sealed class DomTextContent
{
    private readonly global::Square.UI.Text _node = new();

    public DomTextContent(Element owner)
    {
        owner.ChildNodes.Add(_node);
    }

    public string Text
    {
        get => _node.Data;
        set => _node.Data = value ?? "";
    }
}

internal static class ControlDrawing
{
    private static readonly SystemGlyphRasterizer GlyphRasterizer = new();

    /// <summary>从元素 CSS 字体相关属性解析 <see cref="Font"/>（font-family/size/weight/style）。</summary>
    internal static Font ResolveFont(Element element, float defaultSize)
    {
        // 优先 CSS；控件属性 FontSize 作为缺省字号
        var family = element.Style.GetPropertyValue("font-family");
        if (string.IsNullOrEmpty(family)) family = "sans-serif";

        var sizeCss = element.Style.GetPropertyValue("font-size");
        var weightCss = element.Style.GetPropertyValue("font-weight");
        var styleCss = element.Style.GetPropertyValue("font-style");

        return FontManager.Instance.FromCss(
            family,
            string.IsNullOrEmpty(sizeCss) ? null : sizeCss,
            string.IsNullOrEmpty(weightCss) ? null : weightCss,
            string.IsNullOrEmpty(styleCss) ? null : styleCss,
            defaultSize);
    }

    internal static Size MeasureText(Element element, string text, float defaultSize, Size? maxSize = null)
    {
        var font = ResolveFont(element, defaultSize);
        return new TextLayout(text, font)
        {
            MaxSize = maxSize ?? new Size(float.MaxValue, float.MaxValue)
        }.Measure();
    }

    internal static Rect GetTextBounds(Element element, string text, float defaultSize, Point origin)
    {
        if (string.IsNullOrEmpty(text)) return Rect.Empty;
        var size = MeasureText(element, text, defaultSize);
        return new Rect(origin.X, origin.Y, size.Width, size.Height);
    }

    internal static float MeasureRenderedTextWidth(string text, Font font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var lineWidth = 0f;
        var maxWidth = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                maxWidth = Math.Max(maxWidth, lineWidth);
                lineWidth = 0;
                continue;
            }
            lineWidth += MeasureRenderedRuneAdvance(rune, font);
        }
        return Math.Max(maxWidth, lineWidth);
    }

    internal static float MeasureRenderedRuneAdvance(Rune rune, Font font)
    {
        if (GlyphRasterizer.IsAvailable && rune.IsBmp)
        {
            var glyph = GlyphRasterizer.Rasterize(font, (char)rune.Value);
            if (glyph != null) return glyph.AdvanceX;
        }

        return TextLayout.MeasureRuneAdvance(rune, font);
    }

    internal static (float Left, float Right) MeasureRenderedRuneInkBounds(Rune rune, Font font)
    {
        var advance = MeasureRenderedRuneAdvance(rune, font);
        if (GlyphRasterizer.IsAvailable && rune.IsBmp)
        {
            var glyph = GlyphRasterizer.Rasterize(font, (char)rune.Value);
            if (glyph != null)
                return (Math.Min(0, glyph.OffsetX), Math.Max(advance, glyph.OffsetX + glyph.Width));
        }

        return (0, advance);
    }

    internal static void DrawText(
        IRenderContext context, Element element, string text, Point position, Color defaultColor, float defaultSize,
        float? lineHeight = null, bool useStyledColor = true, Size? maxSize = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = ResolveFont(element, defaultSize);
        var color = useStyledColor ? GetStyledColor(element, "color", defaultColor) : defaultColor;
        var layout = new TextLayout(text, font)
        {
            MaxSize = maxSize ?? new Size(float.MaxValue, float.MaxValue)
        };
        if (lineHeight.HasValue && font.Size > 0) layout.LineHeight = lineHeight.Value / font.Size;
        context.DrawText(layout, position, new SolidColorBrush(color));
    }

    internal static void DrawInputFrame(IRenderContext context, UIElement element)
    {
        var background = element.IsEnabled ? Color.White : Color.FromRgb(240, 240, 240);
        var border = element.IsFocused ? Color.FromRgb(0, 95, 184) : Color.FromRgb(165, 170, 176);
        DrawStyledBackground(context, element, background);
        DrawStyledBorder(context, element, border, element.IsFocused ? 2 : 1);
    }

    internal static void DrawStyledBackground(IRenderContext context, UIElement element, Color background)
    {
        if (background.A == 0) return;
        var radius = GetStyledRadius(element, element.Geometry);
        if (radius <= 0)
        {
            context.FillRect(element.Geometry, new SolidColorBrush(background));
            return;
        }

        context.FillGeometry(new RoundedRectGeometry(element.Geometry, radius, radius), new SolidColorBrush(background));
    }

    internal static void DrawStyledBorder(IRenderContext context, UIElement element, Color color, float width)
    {
        if (width <= 0 || color.A == 0) return;
        var radius = GetStyledRadius(element, element.Geometry);
        if (radius <= 0)
        {
            var geometry = element.Geometry;
            var horizontalWidth = Math.Min(width, geometry.Width);
            var verticalWidth = Math.Min(width, geometry.Height);
            var brush = new SolidColorBrush(color);
            context.FillRect(new Rect(geometry.X, geometry.Y, geometry.Width, verticalWidth), brush);
            context.FillRect(new Rect(geometry.X, geometry.Bottom - verticalWidth, geometry.Width, verticalWidth), brush);
            context.FillRect(new Rect(
                geometry.X,
                geometry.Y + verticalWidth,
                horizontalWidth,
                Math.Max(0, geometry.Height - verticalWidth * 2)), brush);
            context.FillRect(new Rect(
                geometry.Right - horizontalWidth,
                geometry.Y + verticalWidth,
                horizontalWidth,
                Math.Max(0, geometry.Height - verticalWidth * 2)), brush);
            return;
        }

        context.DrawGeometry(new RoundedRectGeometry(element.Geometry, radius, radius), Pen.FromColor(color, width));
    }

    internal static float GetStyledFloat(Element element, string name, float fallback)
    {
        var raw = element.Style.GetPropertyValue(name);
        if (string.IsNullOrEmpty(raw)) return fallback;
        raw = raw.Replace("px", "", StringComparison.OrdinalIgnoreCase).Trim();
        return float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    internal static float GetStyledRadius(Element element, Rect geometry)
    {
        var raw = element.Style.GetPropertyValue("border-radius");
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        var token = raw.Trim().Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return 0;

        var max = MathF.Max(0, MathF.Min(geometry.Width, geometry.Height) / 2f);
        float value;
        if (token.EndsWith("%", StringComparison.Ordinal))
        {
            var percent = token[..^1].Trim();
            if (!float.TryParse(percent, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                return 0;
            value = MathF.Min(geometry.Width, geometry.Height) * value / 100f;
        }
        else
        {
            token = token.Replace("px", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (!float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                return 0;
        }

        return Math.Clamp(value, 0, max);
    }

    internal static float GetStyledLineHeight(Element element, float fontSize)
    {
        var value = element.Style.GetPropertyValue("line-height").Trim();
        if (string.IsNullOrEmpty(value)) return Math.Max(1, MathF.Round(fontSize * TextLayout.DefaultLineHeight));

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            return Math.Max(1, GetStyledFloat(element, "line-height", fontSize * TextLayout.DefaultLineHeight));

        return float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var multiplier)
            ? Math.Max(1, fontSize * multiplier)
            : Math.Max(1, MathF.Round(fontSize * TextLayout.DefaultLineHeight));
    }

    internal static Color GetStyledColor(Element element, string name, Color fallback)
    {
        var value = element.Style.GetPropertyValue(name);
        if (name == "background")
        {
            var backgroundColor = element.Style.GetPropertyValue("background-color");
            if (!string.IsNullOrWhiteSpace(backgroundColor)) value = backgroundColor;
        }
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Color.TryParse(value, out var color) ? color : fallback;
    }
}
