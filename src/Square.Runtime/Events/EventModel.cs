namespace Square.Events;

public interface IEventTarget;

public enum RoutingStrategy
{
    Direct,
    Bubble,
    Tunnel,
    TunnelAndBubble
}

public enum EventPhase
{
    Direct,
    Tunneling,
    AtTarget,
    Bubbling
}

public abstract class EventDefinition
{
    protected EventDefinition(string name, RoutingStrategy routingStrategy, Type eventArgsType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.ToLowerInvariant();
        RoutingStrategy = routingStrategy;
        EventArgsType = eventArgsType;
    }

    public string Name { get; }
    public RoutingStrategy RoutingStrategy { get; }
    public Type EventArgsType { get; }

    public override bool Equals(object? obj) =>
        obj is EventDefinition other &&
        Name == other.Name &&
        RoutingStrategy == other.RoutingStrategy &&
        EventArgsType == other.EventArgsType;

    public override int GetHashCode() => HashCode.Combine(Name, RoutingStrategy, EventArgsType);
}

public sealed class RoutedEvent<TEventArgs> : EventDefinition
    where TEventArgs : RoutedEventArgs
{
    public RoutedEvent(string name, RoutingStrategy routingStrategy)
        : base(name, routingStrategy, typeof(TEventArgs))
    {
    }
}

public class RoutedEventArgs
{
    public EventDefinition? Event { get; set; }
    public IEventTarget? OriginalSource { get; set; }
    public IEventTarget? Source { get; set; }
    public IEventTarget? CurrentTarget { get; set; }
    public EventPhase Phase { get; set; }
    public long Timestamp { get; init; }
    public bool Handled { get; set; }
    public bool DefaultPrevented { get; private set; }

    public void PreventDefault() => DefaultPrevented = true;
}

public delegate void RoutedEventHandler<in TEventArgs>(object? sender, TEventArgs args)
    where TEventArgs : RoutedEventArgs;
