namespace Square.UI.ElementApi;

using Square.Runtime;

public sealed class ChildrenCollection : IList<Visual>
{
    private readonly Visual _owner;
    private readonly List<Visual> _list = [];

    internal ChildrenCollection(Visual owner) { _owner = owner; }

    public Visual this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException("Use Insert/RemoveAt to manage children");
    }

    public int Count => _list.Count;
    public bool IsReadOnly => false;

    public void Add(Visual item)
    {
        if (item.Parent != null)
            throw new InvalidOperationException("Visual already has a parent");
        _list.Add(item);
        item.Parent = _owner;
        _owner.OnChildAdded(item);
        AttachIfNeeded(item);
        _owner.InvalidateLayout();
    }

    public void AddRange(IEnumerable<Visual> items)
    {
        foreach (var item in items) Add(item);
    }

    public void Insert(int index, Visual item)
    {
        if (item.Parent != null)
            throw new InvalidOperationException("Visual already has a parent");
        _list.Insert(index, item);
        item.Parent = _owner;
        _owner.OnChildAdded(item);
        AttachIfNeeded(item);
        _owner.InvalidateLayout();
    }

    public void InsertBefore(Visual newChild, Visual refChild)
    {
        var index = _list.IndexOf(refChild);
        if (index < 0) throw new ArgumentException("refChild not found");
        Insert(index, newChild);
    }

    public bool Remove(Visual item)
    {
        var index = _list.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        var item = _list[index];
        DetachIfNeeded(item);
        _list.RemoveAt(index);
        item.Parent = null;
        _owner.OnChildRemoved(item);
        _owner.InvalidateLayout();
    }

    public void Clear()
    {
        foreach (var item in _list)
        {
            DetachIfNeeded(item);
            item.Parent = null;
            _owner.OnChildRemoved(item);
        }
        _list.Clear();
        _owner.InvalidateLayout();
    }

    public int IndexOf(Visual item) => _list.IndexOf(item);
    public bool Contains(Visual item) => _list.Contains(item);
    public void CopyTo(Visual[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
    public IEnumerator<Visual> GetEnumerator() => _list.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();

    private void AttachIfNeeded(Visual item)
    {
        if (_owner.IsAttached) ((IComponentLifecycle)item).OnAttached();
        if (_owner.IsLoaded) ((IComponentLifecycle)item).OnLoaded();
    }

    private void DetachIfNeeded(Visual item)
    {
        if (item.IsLoaded) ((IComponentLifecycle)item).OnUnloaded();
        if (item.IsAttached) ((IComponentLifecycle)item).OnDetached();
    }
}
