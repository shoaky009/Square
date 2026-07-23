using Square.UI;

namespace Square.Router;

/// <summary>
/// In-app navigation link (declarative <c>to</c>), based on the HTML-like controls Link.
/// </summary>
public sealed class Link : Square.Controls.Link
{
    public Link()
    {
    }

    public string To
    {
        get
        {
            var to = GetProperty<string>(nameof(To));
            return !string.IsNullOrEmpty(to) ? to! : Href;
        }
        set
        {
            SetProperty(nameof(To), value);
            Href = value;
        }
    }

    public bool Replace
    {
        get => GetProperty<bool>(nameof(Replace));
        set => SetProperty(nameof(Replace), value);
    }

    protected override void Activate()
    {
        var target = To;
        if (string.IsNullOrEmpty(target)) return;

        for (Element? current = Parent; current != null; current = current.Parent)
        {
            if (current is not Router router) continue;
            router.Navigate(target, Replace);
            return;
        }
    }
}
