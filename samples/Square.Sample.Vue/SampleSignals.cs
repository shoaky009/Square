using Square.Runtime;
using Square.Runtime.Signals;

namespace Square.Sample.Vue;

public static class SampleSignals
{
    private static Dispatcher? _uiDispatcher;

    public static Signal<string> Activity { get; } =
        SignalHub.Default.Get("square.sample.vue.activity", "Waiting for a signal");

    public static Dispatcher UiDispatcher => _uiDispatcher ??= new Dispatcher();

    public static void Initialize(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _uiDispatcher = dispatcher;
    }
}
