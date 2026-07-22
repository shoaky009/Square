using System.Collections.Concurrent;
using Square.Runtime.State;
using Xunit;

namespace Square.Runtime.Tests;

public class StoreTests
{
    [Fact]
    public void UpdateIsAtomicAndVersionsChangedSnapshots()
    {
        using var store = new Store<int>(0);

        Parallel.For(0, 2_000, _ => store.Update(value => value + 1));

        Assert.Equal(2_000, store.Value);
        Assert.Equal(2_000, store.Version);
        Assert.False(store.Set(2_000));
        Assert.Equal(2_000, store.Version);
        Assert.Equal(4_000, store.Read(value => value * 2));
    }

    [Fact]
    public void SelectorSuppressesUnchangedProjectedValues()
    {
        using var store = new Store<State>(new State(1, "first"));
        using var selector = store.Select(state => state.Count);
        var values = new List<int>();
        using var subscription = selector.Subscribe(values.Add, new ReactiveSubscriptionOptions { EmitCurrent = true });

        store.Set(new State(1, "second"));
        store.Set(new State(2, "second"));
        store.Set(new State(2, "third"));

        Assert.Equal([1, 2], values);
        Assert.Equal(2, selector.Value);
    }

    [Fact]
    public void BackgroundUpdatesDispatchToTwoOwnerThreads()
    {
        using var first = new DispatcherThread();
        using var second = new DispatcherThread();
        using var store = new Store<int>(0);
        var callbacks = new ConcurrentDictionary<int, int>();
        using var firstSubscription = store.Subscribe(
            value => callbacks[first.ThreadId] = value,
            new ReactiveSubscriptionOptions { Dispatcher = first.Dispatcher });
        using var secondSubscription = store.Subscribe(
            value => callbacks[second.ThreadId] = value,
            new ReactiveSubscriptionOptions { Dispatcher = second.Dispatcher });

        RunInBackground(() => store.Set(7));
        first.Drain();
        second.Drain();

        Assert.Equal(7, callbacks[first.ThreadId]);
        Assert.Equal(7, callbacks[second.ThreadId]);
        Assert.Equal(2, callbacks.Count);
    }

    [Fact]
    public void DispatcherDeliveryCoalescesToLatestValue()
    {
        var dispatcher = new Dispatcher();
        using var store = new Store<int>(0);
        var values = new List<int>();
        using var subscription = store.Subscribe(
            values.Add,
            new ReactiveSubscriptionOptions { Dispatcher = dispatcher });

        RunInBackground(() =>
        {
            for (var value = 1; value <= 100; value++) store.Set(value);
        });
        dispatcher.Run();

        Assert.Equal([100], values);
    }

    [Fact]
    public void DisposedSubscriptionSuppressesQueuedCallback()
    {
        var dispatcher = new Dispatcher();
        using var store = new Store<int>(0);
        var callbackCount = 0;
        var subscription = store.Subscribe(
            _ => callbackCount++,
            new ReactiveSubscriptionOptions { Dispatcher = dispatcher });

        RunInBackground(() => store.Set(1));
        subscription.Dispose();
        dispatcher.Run();

        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void CallbackAndErrorHandlerExceptionsAreIsolated()
    {
        using var store = new Store<int>(0);
        var received = 0;
        var errors = 0;
        using var throwing = store.Subscribe(
            _ => throw new TestException(),
            new ReactiveSubscriptionOptions
            {
                OnError = exception =>
                {
                    Assert.IsType<TestException>(exception);
                    errors++;
                    throw new InvalidOperationException();
                }
            });
        using var healthy = store.Subscribe(value => received = value);

        Assert.True(store.Set(3));

        Assert.Equal(1, errors);
        Assert.Equal(3, received);
    }

    [Fact]
    public void CancellationStopsImmediateAndQueuedCallbacks()
    {
        using var source = new CancellationTokenSource();
        var dispatcher = new Dispatcher();
        using var store = new Store<int>(0);
        var values = new List<int>();
        using var subscription = store.Subscribe(
            values.Add,
            new ReactiveSubscriptionOptions
            {
                Dispatcher = dispatcher,
                CancellationToken = source.Token
            });

        RunInBackground(() => store.Set(1));
        source.Cancel();
        dispatcher.Run();
        store.Set(2);

        Assert.Empty(values);
    }

    [Fact]
    public void ScopeResolvesParentsAllowsOverridesAndDisposesHierarchy()
    {
        var root = new StoreScope();
        var rootStore = root.Add(new Store<int>(1));
        var child = root.CreateChild();
        var grandchild = child.CreateChild();
        var childStore = child.Add(new Store<string>("child"));

        Assert.Same(rootStore, grandchild.Get<Store<int>>());
        Assert.Same(childStore, grandchild.Get<Store<string>>());
        Assert.True(grandchild.TryGet<Store<int>>(out var found));
        Assert.Same(rootStore, found);
        Assert.False(grandchild.TryGet<Store<double>>(out _));
        Assert.Throws<InvalidOperationException>(() => root.Add(new Store<int>(2)));

        root.Dispose();

        Assert.Throws<ObjectDisposedException>(() => rootStore.Set(2));
        Assert.Throws<ObjectDisposedException>(() => childStore.Set("changed"));
        Assert.Throws<ObjectDisposedException>(() => grandchild.Get<Store<int>>());
    }

    [Fact]
    public void ScopeRegistersBusinessStoreObjects()
    {
        using var scope = new StoreScope();
        var counter = scope.Add(new CounterStore());

        Assert.Same(counter, scope.Get<CounterStore>());
    }

    private sealed record State(int Count, string Name);

    private sealed class TestException : Exception;

    private sealed class CounterStore;

    private static void RunInBackground(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }

    private sealed class DispatcherThread : IDisposable
    {
        private readonly BlockingCollection<Action> _commands = [];
        private readonly Thread _thread;

        public DispatcherThread()
        {
            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                Dispatcher = new Dispatcher();
                ThreadId = Environment.CurrentManagedThreadId;
                ready.Set();
                foreach (var command in _commands.GetConsumingEnumerable()) command();
            });
            _thread.Start();
            ready.Wait();
        }

        public Dispatcher Dispatcher { get; private set; } = null!;

        public int ThreadId { get; private set; }

        public void Drain()
        {
            using var completed = new ManualResetEventSlim();
            _commands.Add(() =>
            {
                Dispatcher.Run();
                completed.Set();
            });
            completed.Wait();
        }

        public void Dispose()
        {
            _commands.CompleteAdding();
            _thread.Join();
            _commands.Dispose();
        }
    }
}
