using Square.UI;

namespace Square.Controls.Primitives;

public sealed class ShowNode
{
    private readonly Func<bool> _condition;
    private readonly Func<Visual?> _build;
    private bool _lastValue;
    private Visual? _child;
    private Visual? _parent;

    public ShowNode(Func<bool> condition, Func<Visual?> build)
    {
        _condition = condition;
        _build = build;
        _lastValue = condition();
        if (_lastValue)
        {
            _child = _build();
        }
    }

    public void AttachTo(Visual parent)
    {
        _parent = parent;
        if (_child != null) parent.Children.Add(_child);
    }

    public void Update()
    {
        var val = _condition();
        if (val == _lastValue) return;
        _lastValue = val;

        if (val)
        {
            _child = _build();
            if (_child != null && _parent != null) _parent.Children.Add(_child);
        }
        else
        {
            if (_child != null && _parent != null) _parent.Children.Remove(_child);
            _child = null;
        }
    }
}

public sealed class ForNode<T>
{
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, Visual?> _build;
    private readonly List<(T item, Visual? node)> _nodes = new();
    private Visual? _parent;

    public ForNode(Func<IEnumerable<T>> source, Func<T, Visual?> build)
    {
        _source = source;
        _build = build;
        Rebuild();
    }

    public void AttachTo(Visual parent)
    {
        _parent = parent;
        foreach (var (_, node) in _nodes)
            if (node != null) parent.Children.Add(node);
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
            if (node != null && _parent != null) _parent.Children.Add(node);
        }
    }
}