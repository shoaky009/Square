using Square.Controls;
using Xunit;

namespace Square.UI.Tests;

public class SlotsTests
{
    [Fact]
    public void ScopedSlotReceivesTypedProperties()
    {
        var slots = new SlotCollection();
        var parent = new View();
        var props = new SlotProps();
        props.Set("label", "Projected");
        props.Set("index", 3);
        slots.Set("row", (host, values) =>
        {
            host.Children.Add(new Square.Controls.Text(values.Get<string>("label")));
            Assert.Equal(3, values.Get<int>("index"));
        });

        Assert.True(slots.Render("row", parent, props));
        Assert.Equal("Projected", Assert.IsType<Square.Controls.Text>(Assert.Single(parent.Children)).TextContent);
    }

    [Fact]
    public void LegacySlotApiRemainsCompatible()
    {
        var slots = new SlotCollection();
        var parent = new View();
        slots.Set("", host => host.Children.Add(new Square.Controls.Text("Legacy")));

        Assert.True(slots.Render(null, parent));
        Assert.Equal("Legacy", Assert.IsType<Square.Controls.Text>(Assert.Single(parent.Children)).TextContent);
    }

    [Fact]
    public void SlotPropsRejectMissingAndWrongTypes()
    {
        var props = new SlotProps();
        props.Set("count", 2);

        Assert.Throws<KeyNotFoundException>(() => props.Get<int>("missing"));
        Assert.Throws<InvalidCastException>(() => props.Get<string>("count"));
    }

    [Fact]
    public void DiscardingSlotHostReleasesProjectedContentResources()
    {
        var slots = new SlotCollection();
        var host = new View();
        var resource = new TrackingDisposable();
        slots.Set("", parent =>
        {
            var child = new Square.Controls.Text("Projected");
            child.RegisterGeneratedResource(resource);
            parent.Children.Add(child);
        });
        slots.Render("", host);

        host.DiscardGeneratedSubtree();

        Assert.Equal(1, resource.DisposeCount);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
