using Square.Graphics;
using Square.Platform;
using Square.UI;

namespace Square.Controls;

/// <summary>
/// Semantic custom title bar with <c>icon</c>, default title, and <c>control</c> slots.
/// The control slot falls back to standard window buttons.
/// </summary>
public class TitleBar : View
{
    private bool _visualTreeBuilt;

    public override string TagName => "TitleBar";

    public float PreferredHeight
    {
        get => GetProperty<float>(nameof(PreferredHeight)) is > 0 and var value ? value : 36f;
        set => SetProperty(nameof(PreferredHeight), Math.Max(1f, value));
    }

    public override Size Measure(Size availableSize) =>
        new(availableSize.Width, ResolveHeight());

    public override void BuildElementTree()
    {
        if (_visualTreeBuilt) return;
        _visualTreeBuilt = true;

        Style.Set("display", "flex");
        Style.Set("flex-direction", "row");
        Style.Set("align-items", "center");
        Style.Set("justify-content", "space-between");

        var iconHost = CreateHost("title-bar-icon");
        var titleHost = CreateHost("title-bar-title");
        var controlHost = CreateHost("title-bar-control");
        titleHost.Style.Set("flex-grow", "1");
        controlHost.Style.Set("flex-direction", "row");

        Children.Add(iconHost);
        Children.Add(titleHost);
        Children.Add(controlHost);

        Slots.Render("icon", iconHost);
        if (!Slots.Render("", titleHost))
            titleHost.Children.Add(new Text(AppWindow?.Title ?? ""));
        if (!Slots.Render("control", controlHost))
            BuildDefaultControls(controlHost);
    }

    private void BuildDefaultControls(View host)
    {
        var minimize = CreateWindowButton("-", "title-bar-minimize");
        var maximize = CreateWindowButton("[]", "title-bar-maximize");
        var close = CreateWindowButton("X", "title-bar-close");

        minimize.AddEventListener("click", _ => AppWindow?.Minimize());
        maximize.AddEventListener("click", _ =>
        {
            if (AppWindow?.State == AppWindowState.Maximized)
                AppWindow.Restore();
            else
                AppWindow?.Maximize();
        });
        close.AddEventListener("click", _ => AppWindow?.Close());

        host.Children.Add(minimize);
        host.Children.Add(maximize);
        host.Children.Add(close);
    }

    private static View CreateHost(string className)
    {
        var host = new View();
        host.ClassList.Add(className);
        host.Style.Set("display", "flex");
        host.Style.Set("align-items", "center");
        return host;
    }

    private static Button CreateWindowButton(string text, string className)
    {
        var button = new Button(text);
        button.ClassList.Add("title-bar-button");
        button.ClassList.Add(className);
        button.Style.Set("width", "46px");
        button.Style.Set("height", "36px");
        return button;
    }

    private float ResolveHeight()
    {
        var value = Style.Get("height")?.Trim();
        if (!string.IsNullOrEmpty(value))
        {
            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                value = value[..^2];
            if (float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var height) && height > 0)
                return height;
        }
        return PreferredHeight;
    }
}
