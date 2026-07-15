using Square.Runtime.Binding;
using Xunit;

namespace Square.Runtime.Tests;

public class ObservableValueTests
{
    [Fact]
    public void DefaultValue()
    {
        var ov = new ObservableValue<int>(42);
        Assert.Equal(42, ov.Value);
    }

    [Fact]
    public void SetValue()
    {
        var ov = new ObservableValue<string>("hello");
        ov.Value = "world";
        Assert.Equal("world", ov.Value);
    }

    [Fact]
    public void SetValueSameNoNotify()
    {
        var ov = new ObservableValue<int>(5);
        var notified = false;
        ov.Subscribe(v => notified = true);
        notified = false;
        ov.Value = 5;
        Assert.False(notified);
    }

    [Fact]
    public void SetValueDifferentNotifies()
    {
        var ov = new ObservableValue<int>(5);
        var received = 0;
        ov.Subscribe(v => received = v);
        ov.Value = 10;
        Assert.Equal(10, received);
    }

    [Fact]
    public void MultipleSubscribers()
    {
        var ov = new ObservableValue<int>(1);
        var a = 0; var b = 0;
        ov.Subscribe(v => a = v);
        ov.Subscribe(v => b = v);
        ov.Value = 99;
        Assert.Equal(99, a);
        Assert.Equal(99, b);
    }

    [Fact]
    public void Unsubscribe()
    {
        var ov = new ObservableValue<int>(1);
        var received = 0;
        var sub = ov.Subscribe(v => received = v);
        ov.Value = 10;
        Assert.Equal(10, received);
        sub.Dispose();
        ov.Value = 20;
        Assert.Equal(10, received);
    }

    [Fact]
    public void ImplicitConversion()
    {
        ObservableValue<int> ov = 42;
        Assert.Equal(42, ov.Value);
        int val = ov;
        Assert.Equal(42, val);
    }

    [Fact]
    public void NotifyManually()
    {
        var ov = new ObservableValue<int>(5);
        var received = 0;
        ov.Subscribe(v => received = v);
        ov.Notify();
        Assert.Equal(5, received);
    }
}