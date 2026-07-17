namespace Square.Events;

public static class StandardEvents
{
    private static readonly Dictionary<string, EventDefinition> Events = new(StringComparer.OrdinalIgnoreCase);

    public static readonly RoutedEvent<RoutedEventArgs> PointerDown = Register<RoutedEventArgs>("pointerdown", RoutingStrategy.TunnelAndBubble);
    public static readonly RoutedEvent<RoutedEventArgs> PointerUp = Register<RoutedEventArgs>("pointerup", RoutingStrategy.TunnelAndBubble);
    public static readonly RoutedEvent<RoutedEventArgs> PointerMove = Register<RoutedEventArgs>("pointermove", RoutingStrategy.TunnelAndBubble);
    public static readonly RoutedEvent<RoutedEventArgs> Wheel = Register<RoutedEventArgs>("wheel", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> KeyDown = Register<RoutedEventArgs>("keydown", RoutingStrategy.TunnelAndBubble);
    public static readonly RoutedEvent<RoutedEventArgs> KeyUp = Register<RoutedEventArgs>("keyup", RoutingStrategy.TunnelAndBubble);
    public static readonly RoutedEvent<RoutedEventArgs> TextInput = Register<RoutedEventArgs>("textinput", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> FocusIn = Register<RoutedEventArgs>("focusin", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> FocusOut = Register<RoutedEventArgs>("focusout", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> Focus = Register<RoutedEventArgs>("focus", RoutingStrategy.Direct);
    public static readonly RoutedEvent<RoutedEventArgs> Blur = Register<RoutedEventArgs>("blur", RoutingStrategy.Direct);
    public static readonly RoutedEvent<RoutedEventArgs> Click = Register<RoutedEventArgs>("click", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> Change = Register<RoutedEventArgs>("change", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> Input = Register<RoutedEventArgs>("input", RoutingStrategy.Bubble);
    public static readonly RoutedEvent<FrameRequestEventArgs> RequestFrame = Register<FrameRequestEventArgs>("requestframe", RoutingStrategy.Bubble);

    public static EventDefinition? Resolve(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return null;
        return Events.GetValueOrDefault(eventName);
    }

    public static RoutedEvent<RoutedEventArgs> ResolveOrCreate(
        string eventName,
        RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (Resolve(eventName) is RoutedEvent<RoutedEventArgs> routedEvent) return routedEvent;
        return new RoutedEvent<RoutedEventArgs>(eventName, routingStrategy);
    }

    private static RoutedEvent<TEventArgs> Register<TEventArgs>(string name, RoutingStrategy strategy)
        where TEventArgs : RoutedEventArgs
    {
        var routedEvent = new RoutedEvent<TEventArgs>(name, strategy);
        Events.Add(routedEvent.Name, routedEvent);
        return routedEvent;
    }
}
