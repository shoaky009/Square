using Square.Runtime;
using Square.Runtime.State;

namespace Square.UI;

/// <summary>Per-document UI services. Each window owns an independent dispatcher and reconciler.</summary>
public sealed class UIContext : IDisposable
{
    public UIContext(
        Dispatcher? dispatcher = null,
        Reconciler? reconciler = null,
        StoreScope? stores = null)
    {
        Dispatcher = dispatcher ?? new Dispatcher();
        Reconciler = reconciler ?? new Reconciler();
        Stores = stores ?? new StoreScope();
    }

    public Dispatcher Dispatcher { get; internal set; }

    public Reconciler Reconciler { get; }

    public StoreScope Stores { get; }

    public void Dispose() => Stores.Dispose();
}
