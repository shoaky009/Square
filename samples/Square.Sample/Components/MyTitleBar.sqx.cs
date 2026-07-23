using Square.Events;
using Square.Platform;

namespace Square.Sample.Components;

public partial class MyTitleBar
{
    private void Minimize(Event e) => AppWindow?.Minimize();

    private void ToggleMaximize(Event e)
    {
        if (AppWindow?.State == AppWindowState.Maximized)
            AppWindow.Restore();
        else
            AppWindow?.Maximize();
    }

    private void Close(Event e) => AppWindow?.Close();
}
