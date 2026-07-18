using System.Collections.Specialized;
using Square.Runtime.Binding;
using Square.UI;

namespace Square.Controls.Primitives;

public sealed class ShowNode : IDisposable
{
    private readonly ObservableValue<bool>? _source;
    private readonly Func<bool> _condition;
    private readonly Func<Element?> _build;
    private IDisposable? _subscription;
    private bool _lastValue;
    private Element? _child;
    private Element? _parent;
    private int _index;
    private bool _disposed;

    public ShowNode(ObservableValue<bool> source, Func<Element?> build)
        : this(() => source.Value, build)
    {
        _source = source;
        _subscription = source.Subscribe(_ => ScheduleUpdate());
    }

    public ShowNode(Func<bool> condition, Func<Element?> build)
    {
        _condition = condition;
        _build = build;
        _lastValue = condition();
        if (_lastValue) _child = _build();
    }

    public void AttachTo(Element parent)
    {
        _parent = parent;
        _index = parent.Children.Count;
        if (_child != null) parent.Children.Insert(_index, _child);
    }

    /// <summary>通过 Reconciler 批处理，而非即时修改树。</summary>
    private void ScheduleUpdate()
    {
        if (_disposed) return;
        Reconciler.Current.ScheduleUpdate(Update);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subscription?.Dispose();
        _subscription = null;
        if (_child != null && _parent != null) _parent.Children.Remove(_child);
        _child = null;
        _parent = null;
    }
}

public interface IForNode : IDisposable
{
    void AttachTo(Element parent);
    void Update();
}

public static class ForNode
{
    public static IForNode Create<T>(ObservableCollection<T> source, Func<T, Element?> build) =>
        new ForNode<T>(() => source, build, source);

    public static IForNode Create<T>(IEnumerable<T> source, Func<T, Element?> build) =>
        new ForNode<T>(() => source, build, source as INotifyCollectionChanged);
}

public sealed class ForNode<T> : IForNode
{
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, Element?> _build;
    private readonly List<(T item, Element? node)> _nodes = new();
    private readonly INotifyCollectionChanged? _observableSource;
    private Element? _parent;
    private int _index;

    public ForNode(Func<IEnumerable<T>> source, Func<T, Element?> build)
        : this(source, build, source() as INotifyCollectionChanged)
    {
    }

    internal ForNode(Func<IEnumerable<T>> source, Func<T, Element?> build, INotifyCollectionChanged? observableSource)
    {
        _source = source;
        _build = build;
        _observableSource = observableSource;
        Rebuild();
        if (_observableSource != null) _observableSource.CollectionChanged += OnCollectionChanged;
    }

    public void AttachTo(Element parent)
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
        // 通过 Reconciler 批处理集合变更，而非即时操作树
        Square.UI.Reconciler.Current.ScheduleUpdate(() => ApplyCollectionChange(e));
    }

    private void ApplyCollectionChange(NotifyCollectionChangedEventArgs e)
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

    public void Dispose()
    {
        if (_observableSource != null)
            _observableSource.CollectionChanged -= OnCollectionChanged;
        if (_parent != null)
        {
            foreach (var (_, node) in _nodes)
                if (node != null) _parent.Children.Remove(node);
        }
        _nodes.Clear();
        _parent = null;
    }
}

public sealed class SwitchNode : IDisposable
{
    private readonly Func<int> _selector;
    private readonly List<MatchBranch> _branches = [];
    private Element? _parent;
    private int _index;
    private int _activeBranch = -1;
    private bool _disposed;

    public SwitchNode(Func<int> selector)
    {
        _selector = selector;
    }

    public void AddBranch(Func<bool> condition, Func<Element?> build)
    {
        _branches.Add(new MatchBranch(condition, build));
    }

    public void AddDefault(Func<Element?> build)
    {
        _branches.Add(new MatchBranch(null, build));
    }

    public void AttachTo(Element parent)
    {
        _parent = parent;
        _index = parent.Children.Count;
        Update();
    }

    public void Update()
    {
        if (_disposed || _parent == null) return;
        // 通过 Reconciler 批处理分支切换
        Square.UI.Reconciler.Current.ScheduleUpdate(UpdateCore);
    }

    private void UpdateCore()
    {
        if (_disposed || _parent == null) return;
        var match = FindMatch();
        if (match == _activeBranch) return;

        if (_activeBranch >= 0 && _activeBranch < _branches.Count)
        {
            var child = _branches[_activeBranch].Child;
            if (child != null) _parent.Children.Remove(child);
        }

        _activeBranch = match;
        if (match >= 0 && match < _branches.Count)
        {
            var branch = _branches[match];
            branch.Child ??= branch.Build();
            if (branch.Child != null)
                _parent.Children.Insert(Math.Min(_index, _parent.Children.Count), branch.Child);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_parent != null)
        {
            foreach (var branch in _branches)
                if (branch.Child != null) _parent.Children.Remove(branch.Child);
        }
        _branches.Clear();
        _parent = null;
    }

    private int FindMatch()
    {
        for (var i = 0; i < _branches.Count; i++)
        {
            var branch = _branches[i];
            if (branch.Condition == null || branch.Condition())
                return i;
        }
        return -1;
    }

    private sealed class MatchBranch(Func<bool>? condition, Func<Element?> build)
    {
        public Func<bool>? Condition { get; } = condition;
        public Func<Element?> Build { get; } = build;
        public Element? Child { get; set; }
    }
}
