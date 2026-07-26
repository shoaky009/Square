using Square.Runtime;
using Square.Runtime.State;

namespace Square.UI;

/// <summary>Per-document UI services. Each window owns an independent dispatcher and reconciler.</summary>
public sealed class UIContext : IDisposable
{
    /// <summary>构造并可选注入 Dispatcher、Reconciler 与 StoreScope。</summary>
    public UIContext(
        Dispatcher? dispatcher = null,
        Reconciler? reconciler = null,
        StoreScope? stores = null)
    {
        Dispatcher = dispatcher ?? new Dispatcher();
        Reconciler = reconciler ?? new Reconciler();
        Stores = stores ?? new StoreScope();
    }

    /// <summary>UI 调度器。</summary>
    public Dispatcher Dispatcher { get; internal set; }

    /// <summary>协调器。</summary>
    public Reconciler Reconciler { get; }

    /// <summary>Store 作用域。</summary>
    public StoreScope Stores { get; }

    /// <summary>释放资源。</summary>
    public void Dispose() => Stores.Dispose();
}
