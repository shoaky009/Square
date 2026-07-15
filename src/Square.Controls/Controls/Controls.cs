using Square.Graphics;
using Square.Runtime;
using Square.Runtime.Binding;
using Square.UI;

namespace Square.Controls.Controls;

public class View : UIElement
{
    public View() { }
}

public class Text : UIElement
{
    public string TextContent { get; set; } = "";
    public Color Color { get; set; } = Color.Black;
    public float FontSize { get; set; } = 16f;

    public Text() { }
    public Text(string text) { TextContent = text; }

    public override Size Measure(Size availableSize)
    {
        if (string.IsNullOrEmpty(TextContent)) return Size.Zero;
        var layout = new TextLayout(TextContent, new Font("Segoe UI", FontSize));
        return layout.Measure();
    }

    public override void Render(IRenderContext ctx)
    {
        if (string.IsNullOrEmpty(TextContent)) return;
        var layout = new TextLayout(TextContent, new Font("Segoe UI", FontSize));
        ctx.DrawText(layout, Geometry.Position, new SolidColorBrush(Color));
    }
}

public class Button : UIElement
{
    public string TextContent { get; set; } = "";
    public Color Background { get; set; } = Color.FromRgb(0, 120, 212);
    public Color Foreground { get; set; } = Color.White;

    public Button() { }
    public Button(string text) { TextContent = text; }

    public override Size Measure(Size availableSize)
    {
        var layout = new TextLayout(TextContent, new Font("Segoe UI", 14f));
        var textSize = layout.Measure();
        return new Size(textSize.Width + 32, Math.Max(32, textSize.Height + 16));
    }

    public override void Arrange(Rect finalRect) { Geometry = finalRect; }

    public override void Render(IRenderContext ctx)
    {
        ctx.FillRect(Geometry, new SolidColorBrush(Background));
        var layout = new TextLayout(TextContent, new Font("Segoe UI", 14f));
        var textSize = layout.Measure();
        var textPos = new Point(
            Geometry.X + (Geometry.Width - textSize.Width) / 2f,
            Geometry.Y + (Geometry.Height - textSize.Height) / 2f);
        ctx.DrawText(layout, textPos, new SolidColorBrush(Foreground));
    }
}

public class Input : UIElement
{
    public string Value { get; set; } = "";
    public string Placeholder { get; set; } = "";

    public Input() { }

    public override Size Measure(Size availableSize) => new(200, 32);
    public override void Render(IRenderContext ctx)
    {
        ctx.FillRect(Geometry, new SolidColorBrush(Color.White));
        ctx.DrawRect(Geometry, Pen.FromColor(Color.FromRgb(180, 180, 180)));
        if (!string.IsNullOrEmpty(Value))
        {
            var layout = new TextLayout(Value, new Font("Segoe UI", 14f));
            ctx.DrawText(layout, new Point(Geometry.X + 8, Geometry.Y + 8), new SolidColorBrush(Color.Black));
        }
    }
}

public class TextArea : UIElement
{
    public string Value { get; set; } = "";
    public TextArea() { }
    public override Size Measure(Size availableSize) => new(300, 100);
    public override void Render(IRenderContext ctx) => ctx.FillRect(Geometry, new SolidColorBrush(Color.White));
}

public class CheckBox : UIElement
{
    public bool IsChecked { get; set; }
    public CheckBox() { }
    public override Size Measure(Size availableSize) => new(20, 20);
    public override void Render(IRenderContext ctx)
    {
        ctx.FillRect(Geometry, new SolidColorBrush(IsChecked ? Color.FromRgb(0, 120, 212) : Color.White));
        ctx.DrawRect(Geometry, Pen.FromColor(Color.FromRgb(100, 100, 100)));
    }
}

public class Radio : UIElement
{
    public bool IsChecked { get; set; }
    public Radio() { }
    public override Size Measure(Size availableSize) => new(20, 20);
    public override void Render(IRenderContext ctx) => ctx.FillRect(Geometry, new SolidColorBrush(IsChecked ? Color.FromRgb(0, 120, 212) : Color.White));
}

public class Select : UIElement
{
    public string Value { get; set; } = "";
    public Select() { }
    public override Size Measure(Size availableSize) => new(200, 32);
    public override void Render(IRenderContext ctx) => ctx.FillRect(Geometry, new SolidColorBrush(Color.White));
}

public class Image : UIElement
{
    public string Source { get; set; } = "";
    public Image() { }
    public override Size Measure(Size availableSize) => new(100, 100);
    public override void Render(IRenderContext ctx) { }
}

public class Canvas : UIElement
{
    public Canvas() { }
    public override Size Measure(Size availableSize) => new(300, 200);
    public override void Render(IRenderContext ctx) => ctx.FillRect(Geometry, new SolidColorBrush(Color.White));
}