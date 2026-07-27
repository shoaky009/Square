using Square.Controls;
using Square.Events;
using Square.Runtime.Binding;
using Xunit;

namespace Square.UI.Tests;

public class SqvObjectBindingTests
{
    [Fact]
    public void PropertyMapAppliesUpdatesRemovesAndDisposesValues()
    {
        var button = new Button();
        var source = new ObservableValue<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?>
            {
                ["text"] = "Save",
                ["disabled"] = true
            });
        var binding = SqvObjectBinding.BindProperties(button, source);

        Assert.Equal("Save", button.TextContent);
        Assert.True(button.IsDisabled);

        source.Value = new Dictionary<string, object?>
        {
            ["text"] = "Send"
        };

        Assert.Equal("Send", button.TextContent);
        Assert.False(button.Properties.HasValue("IsDisabled"));

        binding.Dispose();

        Assert.False(button.Properties.HasValue("TextContent"));
        source.Value = new Dictionary<string, object?> { ["text"] = "Ignored" };
        Assert.Equal("", button.TextContent);
    }

    [Fact]
    public void PropertyMapTreatsNullAsPropertyRemoval()
    {
        var text = new Square.Controls.Text();
        using var binding = SqvObjectBinding.BindProperties(
            text,
            new Dictionary<string, object?> { ["text"] = null });

        Assert.False(text.Properties.HasValue("TextContent"));
    }

    [Fact]
    public void EventMapUpdatesAndDisposesListeners()
    {
        var button = new Button();
        var first = 0;
        var second = 0;
        var source = new ObservableValue<IReadOnlyDictionary<string, Action<Event>>>(
            new Dictionary<string, Action<Event>> { ["click"] = _ => first++ });
        var binding = SqvObjectBinding.BindEvents(button, source);

        button.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(1, first);

        source.Value = new Dictionary<string, Action<Event>> { ["CLICK"] = _ => second++ };
        button.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(1, first);
        Assert.Equal(1, second);

        binding.Dispose();
        button.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(1, second);
    }

    [Fact]
    public void PropertyNameMapCoversVueAndSvgAliases()
    {
        Assert.Equal("TextContent", SqvPropertyNames.Map("text"));
        Assert.Equal("StaysOpenOnClick", SqvPropertyNames.Map("stays-open-on-click"));
        Assert.Equal("StrokeWidth", SqvPropertyNames.Map("stroke-width"));
        Assert.Equal("CustomProp", SqvPropertyNames.Map("CustomProp"));
    }

    [Fact]
    public void ObjectBindingsRejectKeysThatCollideAfterNormalization()
    {
        var button = new Button();
        var properties = new Dictionary<string, object?>
        {
            ["text"] = "first",
            ["TextContent"] = "second"
        };
        var events = new Dictionary<string, Action<Event>>
        {
            ["Click"] = _ => { },
            ["click"] = _ => { }
        };

        Assert.Throws<InvalidOperationException>(() => SqvObjectBinding.BindProperties(button, properties));
        Assert.Throws<InvalidOperationException>(() => SqvObjectBinding.BindEvents(button, events));
    }

    [Fact]
    public void DynamicPropertyTracksReactiveNameAndValue()
    {
        var button = new Button();
        var name = new ObservableValue<string>("text");
        var value = new ObservableValue<string>("Save");
        var binding = SqvObjectBinding.BindProperty(button, name, value);

        Assert.Equal("Save", button.TextContent);
        value.Value = "Send";
        Assert.Equal("Send", button.TextContent);

        name.Value = "disabled";
        Assert.False(button.Properties.HasValue("TextContent"));
        Assert.True(button.Properties.HasValue("IsDisabled"));

        binding.Dispose();
        Assert.False(button.Properties.HasValue("IsDisabled"));
    }

    [Fact]
    public void DynamicEventMovesListenerWhenReactiveNameChanges()
    {
        var button = new Button();
        var name = new ObservableValue<string>("click");
        var count = 0;
        var binding = SqvObjectBinding.BindEvent(button, name, _ => count++);

        button.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(1, count);

        name.Value = "change";
        button.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(1, count);
        button.DispatchEvent(new Event("change"));
        Assert.Equal(2, count);

        binding.Dispose();
        button.DispatchEvent(new Event("change"));
        Assert.Equal(2, count);
    }
}
