using Square.Controls.Controls;
using Square.UI;

namespace Square.Router;

public sealed class Link : Button
{
    public Link()
    {
        AddEventListener("click", NavigateToTarget);
    }

    public string To
    {
        get => GetProperty<string>(nameof(To)) ?? "/";
        set => SetProperty(nameof(To), value);
    }

    public bool Replace
    {
        get => GetProperty<bool>(nameof(Replace));
        set => SetProperty(nameof(Replace), value);
    }

    private void NavigateToTarget()
    {
        for (Visual? current = Parent; current != null; current = current.Parent)
        {
            if (current is not Router router) continue;
            router.Navigate(To, Replace);
            return;
        }
    }
}
