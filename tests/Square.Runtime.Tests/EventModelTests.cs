using Square.Events;
using Xunit;

namespace Square.Runtime.Tests;

public class EventModelTests
{
    [Fact]
    public void RoutedEventCarriesNameStrategyAndArgumentType()
    {
        var routedEvent = new RoutedEvent<RoutedEventArgs>("save", RoutingStrategy.Bubble);

        Assert.Equal("save", routedEvent.Name);
        Assert.Equal(RoutingStrategy.Bubble, routedEvent.RoutingStrategy);
        Assert.Equal(typeof(RoutedEventArgs), routedEvent.EventArgsType);
    }

    [Fact]
    public void RoutedEventNamesAreNormalizedForCaseInsensitiveLookup()
    {
        var routedEvent = new RoutedEvent<RoutedEventArgs>("PointerDown", RoutingStrategy.TunnelAndBubble);

        Assert.Equal("pointerdown", routedEvent.Name);
    }

    [Fact]
    public void EventArgumentsTrackRoutingStateAndDefaultPrevention()
    {
        var target = new TestTarget();
        var args = new RoutedEventArgs
        {
            OriginalSource = target,
            Source = target,
            CurrentTarget = target,
            Phase = EventPhase.AtTarget
        };

        args.Handled = true;
        args.PreventDefault();

        Assert.True(args.Handled);
        Assert.True(args.DefaultPrevented);
        Assert.Same(target, args.OriginalSource);
        Assert.Equal(EventPhase.AtTarget, args.Phase);
    }

    [Fact]
    public void StandardEventsResolveNamesCaseInsensitively()
    {
        Assert.Same(StandardEvents.Click, StandardEvents.Resolve("CLICK"));
        Assert.Same(StandardEvents.PointerDown, StandardEvents.Resolve("pointerDown"));
        Assert.Null(StandardEvents.Resolve("missing"));
    }

    [Fact]
    public void CustomEventDefinitionsUseValueIdentityWithoutGlobalRegistration()
    {
        var first = StandardEvents.ResolveOrCreate("saved");
        var second = StandardEvents.ResolveOrCreate("SAVED");

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(RoutingStrategy.Bubble, first.RoutingStrategy);
    }

    private sealed class TestTarget : IEventTarget;
}
