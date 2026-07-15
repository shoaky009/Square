using System.Collections.Specialized;
using Square.Runtime.Binding;
using Square.UI;

namespace Square.Controls.Primitives;

public sealed class ShowNode
{
    private readonly ObservableValue<bool>? _source;
    private readonly Func<bool> _condition;
    private readonly Func<Visual?> _build;
    private readonly IDisposable? _subscription;
    private bool _lastValue;
    private Visual? _child;
    private Visual? _parent;
    private int _index;

    public ShowNode(ObservableValue<bool> source, Func<Visual?> build)
        : this(() => source.Value, build)
    {
        _source = source;
        _subscription = source.Subscribe(_ => Update());
    }

    public ShowNode(Func<bool> condition, Func<Visual?> build)
    {
        _condition = condition;
        _build = build;
        _lastValue = condition();
        if (_lastValue) _child = _build();
    }

    public void AttachTo(Visual parent)
    {
        _parent = parent;
        _index = parent.Children.Count;
        if (_child != null) parent.Children.Insert(_index, _child);
    }

    public void Update()
    {
        var val = _condition();
        if (val == _lastValue) return;
        _lastValue = val;

        if (val)
        {
            _child ??= _build();
            if (_child != null && _parent != null)
                _parent.Children.Insert(Math.Min(_index, _parent.Children.Count), _child);
        }
        else
        {
            if (_child != null && _parent != null) _parent.Children.Remove(_child);
        }
    }
}

public interface IForNode
{
    void AttachTo(Visual parent);
    void Update();
}

public static class ForNode
{
    public static IForNode Create<T>(ObservableCollection<T> source, Func<T, Visual?> build) =>
        new ForNode<T>(() => source, build, source);

    public static IForNode Create<T>(IEnumerable<T> source, Func<T, Visual?> build) =>
        new ForNode<T>(() => source, build, source as INotifyCollectionChanged);
}

public sealed class ForNode<T> : IForNode
{
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, Visual?> _build;
    private readonly List<(T item, Visual? node)> _nodes = new();
    private readonly INotifyCollectionChanged? _observableSource;
    private Visual? _parent;
    private int _index;

    public ForNode(Func<IEnumerable<T>> source, Func<T, Visual?> build)
        : this(source, build, source() as INotifyCollectionChanged)
    {
    }

    internal ForNode(Func<IEnumerable<T>> source, Func<T, Visual?> build, INotifyCollectionChanged? observableSource)
    {
        _source = source;
        _build = build;
        _observableSource = observableSource;
        Rebuild();
        if (_observableSource != null) _observableSource.CollectionChanged += OnCollectionChanged;
    }

    public void AttachTo(Visual parent)
    {
        _parent = parent;
        _index = parent.Children.Count;
        for (var i = 0; i < _nodes.Count; i++)
            InsertNode(i);
    }

    public void Update()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        if (_parent != null)
        {
            foreach (var (_, node) in _nodes)
                if (node != null) _parent.Children.Remove(node);
        }
        _nodes.Clear();

        foreach (var item in _source())
        {
            var node = _build(item);
            _nodes.Add((item, node));
            if (node != null && _parent != null)
                _parent.Children.Insert(Math.Min(_index + _nodes.Count - 1, _parent.Children.Count), node);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                var addIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : _nodes.Count;
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    var item = (T)e.NewItems[i]!;
                    _nodes.Insert(addIndex + i, (item, _build(item)));
                    InsertNode(addIndex + i);
                }
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                var removeIndex = e.OldStartingIndex;
                for (var i = 0; i < e.OldItems.Count; i++) RemoveNode(removeIndex);
                break;
            case NotifyCollectionChangedAction.Move:
                MoveNode(e.OldStartingIndex, e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Replace when e.NewItems != null:
                var replaceIndex = e.NewStartingIndex;
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    RemoveNode(replaceIndex);
                    var item = (T)e.NewItems[i]!;
                    _nodes.Insert(replaceIndex, (item, _build(item)));
                    InsertNode(replaceIndex);
                }
                break;
            default:
                Rebuild();
                break;
        }
    }

    private void InsertNode(int nodeIndex)
    {
        var node = _nodes[nodeIndex].node;
        if (node != null && _parent != null)
            _parent.Children.Insert(GetInsertionIndex(nodeIndex), node);
    }

    private int GetInsertionIndex(int nodeIndex)
    {
        if (_parent == null) return 0;
        for (var i = nodeIndex - 1; i >= 0; i--)
        {
            var previous = _nodes[i].node;
            if (previous?.Parent == _parent) return _parent.Children.IndexOf(previous) + 1;
        }
        for (var i = nodeIndex + 1; i < _nodes.Count; i++)
        {
            var next = _nodes[i].node;
            if (next?.Parent == _parent) return _parent.Children.IndexOf(next);
        }
        return Math.Min(_index, _parent.Children.Count);
    }

    private void RemoveNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _nodes.Count) return;
        var node = _nodes[nodeIndex].node;
        if (node != null && _parent != null) _parent.Children.Remove(node);
        _nodes.RemoveAt(nodeIndex);
    }

    private void MoveNode(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _nodes.Count || newIndex < 0 || newIndex >= _nodes.Count) return;
        var entry = _nodes[oldIndex];
        if (entry.node != null && _parent != null) _parent.Children.Remove(entry.node);
        _nodes.RemoveAt(oldIndex);
        _nodes.Insert(newIndex, entry);
        InsertNode(newIndex);
    }
}
