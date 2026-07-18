using Square.Events;
using Square.Graphics;
using Square.Text.FontManager;
using Square.UI;

namespace Square.Controls.Controls;

public class View : UIElement
{
    public override void Paint(IRenderContext ctx)
    {
        var background = ControlDrawing.GetStyledColor(this, "background", Color.Transparent);
        if (background.A > 0) ctx.FillRect(Geometry, new SolidColorBrush(background));
    }
}

public class Text : UIElement
{
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.Black; set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }

    public Text() { }
    public Text(string text) { TextContent = text; }

    public override Size Measure(Size availableSize)
    {
        if (string.IsNullOrEmpty(TextContent)) return Size.Zero;
        return ControlDrawing.MeasureText(this, TextContent, FontSize);
    }

    public override void Paint(IRenderContext ctx)
    {
        if (string.IsNullOrEmpty(TextContent)) return;
        ControlDrawing.DrawText(ctx, this, TextContent, Geometry.Position, Color, FontSize);
    }
}

/// <summary>List item, similar to HTML <c>li</c>. Optional marker and text; may also host children.</summary>
public class ListItem : UIElement
{
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.Black; set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }
    /// <summary>Bullet or number prefix, e.g. "• ", "1. ". Empty draws no marker.</summary>
    public string Marker { get => GetProperty<string>(nameof(Marker)) ?? "• "; set => SetProperty(nameof(Marker), value); }

    public ListItem() { }
    public ListItem(string text) { TextContent = text; }

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
        var background = ControlDrawing.GetStyledColor(this, "background", Color.Transparent);
        if (background.A > 0) ctx.FillRect(Geometry, new SolidColorBrush(background));

        var label = Marker + TextContent;
        if (!string.IsNullOrEmpty(label.Trim()))
            ControlDrawing.DrawText(ctx, this, label, Geometry.Position, Color, FontSize);
    }
}

/// <summary>Hyperlink-style control, similar to HTML <c>a</c>. Use <see cref="Href"/> for the target URL/path.</summary>
public class Link : UIElement
{
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public string Href { get => GetProperty<string>(nameof(Href)) ?? ""; set => SetProperty(nameof(Href), value); }
    public Color Color { get => Properties.HasValue(nameof(Color)) ? GetProperty<Color>(nameof(Color)) : Color.FromRgb(0, 102, 204); set => SetProperty(nameof(Color), value); }
    public float FontSize { get => Properties.HasValue(nameof(FontSize)) ? GetProperty<float>(nameof(FontSize)) : 16f; set => SetProperty(nameof(FontSize), value); }
    public bool Underline { get => !Properties.HasValue(nameof(Underline)) || GetProperty<bool>(nameof(Underline)); set => SetProperty(nameof(Underline), value); }

    public Link() { }
    public Link(string text) { TextContent = text; }
    public Link(string text, string href) { TextContent = text; Href = href; }

    public override Size Measure(Size availableSize)
    {
        if (string.IsNullOrEmpty(TextContent)) return Size.Zero;
        return ControlDrawing.MeasureText(this, TextContent, FontSize);
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
            var textSize = ControlDrawing.MeasureText(this, TextContent, FontSize);
            var y = origin.Y + textSize.Height - 1f;
            ctx.DrawRect(new Rect(origin.X, y, textSize.Width, 1f), Pen.FromColor(color, 1f));
        }
    }
}

public class Button : UIElement
{
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public Color Background { get => Properties.HasValue(nameof(Background)) ? GetProperty<Color>(nameof(Background)) : Color.FromRgb(0, 120, 212); set => SetProperty(nameof(Background), value); }
    public Color Foreground { get => Properties.HasValue(nameof(Foreground)) ? GetProperty<Color>(nameof(Foreground)) : Color.White; set => SetProperty(nameof(Foreground), value); }

    public Button() { }
    public Button(string text) { TextContent = text; }

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
        ctx.FillRect(Geometry, new SolidColorBrush(background));

        var textSize = ControlDrawing.MeasureText(this, TextContent, 14f);
        var textPosition = new Point(
            Geometry.X + (Geometry.Width - textSize.Width) / 2f,
            Geometry.Y + (Geometry.Height - textSize.Height) / 2f);
        ControlDrawing.DrawText(ctx, this, TextContent, textPosition, foreground, 14f);
    }
}

public class CheckBox : UIElement
{
    public bool IsChecked { get => GetProperty<bool>(nameof(IsChecked)); set => SetProperty(nameof(IsChecked), value); }
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }

    public CheckBox()
    {
        AddEventListener("click", ToggleFromInput);
    }

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

public class Radio : UIElement
{
    public bool IsChecked { get => GetProperty<bool>(nameof(IsChecked)); set => SetProperty(nameof(IsChecked), value); }
    public string TextContent { get => GetProperty<string>(nameof(TextContent)) ?? ""; set => SetProperty(nameof(TextContent), value); }
    public string GroupName { get => GetProperty<string>(nameof(GroupName)) ?? ""; set => SetProperty(nameof(GroupName), value); }

    public Radio()
    {
        AddEventListener("click", SelectFromInput);
    }

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

public class Select : UIElement
{
    public string Value { get => GetProperty<string>(nameof(Value)) ?? ""; set => SetProperty(nameof(Value), value); }
    public string[] Options { get => GetProperty<string[]>(nameof(Options)) ?? []; set => SetProperty(nameof(Options), value ?? []); }
    public string Placeholder { get => GetProperty<string>(nameof(Placeholder)) ?? "Select"; set => SetProperty(nameof(Placeholder), value); }
    public bool IsOpen { get; private set; }
    private int _hoveredOption = -1;

    public override int ZIndex
    {
        get => IsOpen ? 1000 : base.ZIndex;
        set => base.ZIndex = value;
    }

    public Select() { }

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

        if (!IsOpen || Options.Length == 0) return;

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
        return Geometry.Contains(point) || IsOpen && GetDropDownRect().Contains(point) ? this : null;
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
            Value = Options[index];
            CloseDropDown();
            DispatchEvent(StandardEvents.CreateChange());
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
        IsOpen = !IsOpen;
        _hoveredOption = -1;
        Parent?.InvalidatePaint();
        InvalidatePaint();
    }

    private Rect GetDropDownRect() => new(Geometry.X, Geometry.Bottom + 2, Geometry.Width, Options.Length * 32 + 2);
}

public class Image : UIElement
{
    public string Source { get => GetProperty<string>(nameof(Source)) ?? ""; set => SetProperty(nameof(Source), value); }
    public Square.Graphics.Image? ImageContent { get => GetProperty<Square.Graphics.Image>(nameof(ImageContent)); set => SetProperty(nameof(ImageContent), value); }

    public override Size Measure(Size availableSize) => ImageContent == null
        ? new Size(160, 96)
        : new Size(ImageContent.Width, ImageContent.Height);

    public override void Paint(IRenderContext ctx)
    {
        if (ImageContent != null)
        {
            ctx.DrawImage(ImageContent, Geometry);
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
}

public class Canvas : UIElement
{
    private Action<IRenderContext, Rect>? _animationFrameCallback;

    public Action<IRenderContext, Rect>? DrawContent { get; set; }

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

internal static class ControlDrawing
{
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

    internal static Size MeasureText(Element element, string text, float defaultSize)
    {
        var font = ResolveFont(element, defaultSize);
        return new TextLayout(text, font).Measure();
    }

    internal static void DrawText(
        IRenderContext context, Element element, string text, Point position, Color defaultColor, float defaultSize,
        float? lineHeight = null, bool useStyledColor = true)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = ResolveFont(element, defaultSize);
        var color = useStyledColor ? GetStyledColor(element, "color", defaultColor) : defaultColor;
        var layout = new TextLayout(text, font);
        if (lineHeight.HasValue && font.Size > 0) layout.LineHeight = lineHeight.Value / font.Size;
        context.DrawText(layout, position, new SolidColorBrush(color));
    }

    internal static void DrawInputFrame(IRenderContext context, UIElement element)
    {
        var background = element.IsEnabled ? Color.White : Color.FromRgb(240, 240, 240);
        var border = element.IsFocused ? Color.FromRgb(0, 95, 184) : Color.FromRgb(165, 170, 176);
        context.FillRect(element.Geometry, new SolidColorBrush(background));
        context.DrawRect(element.Geometry, Pen.FromColor(border, element.IsFocused ? 2 : 1));
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
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try { return Color.Parse(value.Replace(" ", "")); }
        catch (FormatException) { return fallback; }
    }
}
