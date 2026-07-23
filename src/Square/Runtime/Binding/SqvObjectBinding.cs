using Square.Events;
using Square.Runtime.State;
using Square.UI;

namespace Square.Runtime.Binding;

/// <summary>AOT-safe runtime protocol for Vue object-form property and event bindings.</summary>
public static class SqvObjectBinding
{
    public static IDisposable BindProperties(
        Element target,
        IReadOnlyDictionary<string, object?> values) =>
        new PropertyMapBinding(target, values);

    public static IDisposable BindProperties<TMap>(Element target, ObservableValue<TMap> source)
        where TMap : IReadOnlyDictionary<string, object?>
    {
        var binding = new PropertyMapBinding(target, source.Value);
        binding.SetSubscription(source.Subscribe(value => binding.Apply(value)));
        return binding;
    }

    public static IDisposable BindProperties<TMap>(Element target, IReactiveValue<TMap> source)
        where TMap : IReadOnlyDictionary<string, object?>
    {
        var binding = new PropertyMapBinding(target, source.Value);
        binding.SetSubscription(source.Subscribe(
            value => binding.Apply(value),
            new ReactiveSubscriptionOptions { Dispatcher = target.Dispatcher }));
        return binding;
    }

    public static IDisposable BindEvents(
        EventTarget target,
        IReadOnlyDictionary<string, Action<Event>> listeners) =>
        new EventMapBinding(target, listeners);

    public static IDisposable BindEvents<TMap>(EventTarget target, ObservableValue<TMap> source)
        where TMap : IReadOnlyDictionary<string, Action<Event>>
    {
        var binding = new EventMapBinding(target, source.Value);
        binding.SetSubscription(source.Subscribe(value => binding.Apply(value)));
        return binding;
    }

    public static IDisposable BindEvents<TMap>(EventTarget target, IReactiveValue<TMap> source)
        where TMap : IReadOnlyDictionary<string, Action<Event>>
    {
        var binding = new EventMapBinding(target, source.Value);
        var dispatcher = target is Element element ? element.Dispatcher : null;
        binding.SetSubscription(source.Subscribe(
            value => binding.Apply(value),
            new ReactiveSubscriptionOptions { Dispatcher = dispatcher }));
        return binding;
    }

    private sealed class PropertyMapBinding : IDisposable
    {
        private readonly Element _target;
        private readonly HashSet<string> _owned = new(StringComparer.OrdinalIgnoreCase);
        private IDisposable? _subscription;
        private bool _disposed;

        public PropertyMapBinding(Element target, IReadOnlyDictionary<string, object?> values)
        {
            _target = target;
            Apply(values);
        }

        public void SetSubscription(IDisposable subscription) => _subscription = subscription;

        public void Apply(IReadOnlyDictionary<string, object?> values)
        {
            if (_disposed) return;
            ArgumentNullException.ThrowIfNull(values);

            var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in values)
            {
                var name = SqvPropertyNames.Map(pair.Key);
                if (!normalized.TryAdd(name, pair.Value))
                    throw new InvalidOperationException("Duplicate mapped property '" + name + "' in SQV object binding.");
            }

            foreach (var pair in normalized)
            {
                if (pair.Value == null)
                    _target.RemoveProperty(pair.Key);
                else
                    _target.SetProperty<object?>(pair.Key, pair.Value);
            }

            foreach (var name in _owned)
                if (!normalized.ContainsKey(name)) _target.RemoveProperty(name);
            _owned.Clear();
            _owned.UnionWith(normalized.Keys);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
            foreach (var name in _owned) _target.RemoveProperty(name);
            _owned.Clear();
        }
    }

    private sealed class EventMapBinding : IDisposable
    {
        private readonly EventTarget _target;
        private readonly List<IDisposable> _listeners = [];
        private IDisposable? _subscription;
        private bool _disposed;

        public EventMapBinding(EventTarget target, IReadOnlyDictionary<string, Action<Event>> listeners)
        {
            _target = target;
            Apply(listeners);
        }

        public void SetSubscription(IDisposable subscription) => _subscription = subscription;

        public void Apply(IReadOnlyDictionary<string, Action<Event>> listeners)
        {
            if (_disposed) return;
            ArgumentNullException.ThrowIfNull(listeners);

            var normalized = new Dictionary<string, Action<Event>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in listeners)
            {
                if (pair.Value == null) continue;
                var name = pair.Key.Trim().ToLowerInvariant();
                if (name.Length == 0)
                    throw new InvalidOperationException("SQV event binding names cannot be empty.");
                if (!normalized.TryAdd(name, pair.Value))
                    throw new InvalidOperationException("Duplicate event '" + name + "' in SQV object binding.");
            }

            ClearListeners();
            foreach (var pair in normalized)
                _listeners.Add(_target.Listen(pair.Key, pair.Value));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
            ClearListeners();
        }

        private void ClearListeners()
        {
            foreach (var listener in _listeners) listener.Dispose();
            _listeners.Clear();
        }
    }
}

/// <summary>Maps Vue/SQV attribute names to Square property names without reflection.</summary>
public static class SqvPropertyNames
{
    public static string Map(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        return name.ToLowerInvariant() switch
        {
            "text" => "TextContent",
            "value" => "Value",
            "checked" => "IsChecked",
            "disabled" => "IsDisabled",
            "placeholder" => "Placeholder",
            "source" => "Source",
            "image" => "ImageContent",
            "group" => "GroupName",
            "shortcut" => "ShortcutText",
            "checkable" => "IsCheckable",
            "stays-open-on-click" => "StaysOpenOnClick",
            "options" => "Options",
            "items" => "Items",
            "selected-index" => "SelectedIndex",
            "expanded" => "IsExpanded",
            "loop" => "Loop",
            "to" => "To",
            "href" => "Href",
            "marker" => "Marker",
            "replace" => "Replace",
            "color" => "Color",
            "background" => "Background",
            "underline" => "Underline",
            "type" => "Type",
            "viewbox" => "ViewBox",
            "x" => "X",
            "y" => "Y",
            "width" => "Width",
            "height" => "Height",
            "rx" => "RadiusX",
            "ry" => "RadiusY",
            "cx" => "CenterX",
            "cy" => "CenterY",
            "r" => "Radius",
            "x1" => "X1",
            "y1" => "Y1",
            "x2" => "X2",
            "y2" => "Y2",
            "points" => "Points",
            "d" => "Data",
            "transform" => "Transform",
            "fill" => "Fill",
            "stroke" => "Stroke",
            "stroke-width" => "StrokeWidth",
            "opacity" => "Opacity",
            "fill-opacity" => "FillOpacity",
            "stroke-opacity" => "StrokeOpacity",
            _ => name
        };
    }
}
