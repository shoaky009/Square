namespace Square.UI.ElementApi;

using Square.Runtime;

/// <summary>
/// Child node list, aligned with DOM <c>childNodes</c>.
/// </summary>
public sealed class ChildNodeCollection : IList<Node>
{
    private readonly Element _owner;
    private readonly List<Node> _list = [];

    internal ChildNodeCollection(Element owner) { _owner = owner; }

    public Node this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException("Use Insert/RemoveAt to manage child nodes");
    }

    public int Count => _list.Count;

    public bool IsReadOnly => false;

    public void Add(Node item)
    {
        ValidateNewChild(item);
        _list.Add(item);
        Attach(item);
    }

    public void AddRange(IEnumerable<Node> items)
    {
        foreach (var item in items) Add(item);
    }

    public void Insert(int index, Node item)
    {
        ValidateNewChild(item);
        _list.Insert(index, item);
        Attach(item);
    }

    public void InsertBefore(Node newChild, Node refChild)
    {
        var index = _list.IndexOf(refChild);
        if (index < 0) throw new ArgumentException("refChild not found");
        Insert(index, newChild);
    }

    public bool Remove(Node item)
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
        item.ParentNode = null;
        if (item is Element element) _owner.OnChildRemoved(element);
        _owner.InvalidateLayout();
    }

    public void Clear()
    {
        foreach (var item in _list)
        {
            DetachIfNeeded(item);
            item.ParentNode = null;
            if (item is Element element) _owner.OnChildRemoved(element);
        }
        _list.Clear();
        _owner.InvalidateLayout();
    }

    public int IndexOf(Node item) => _list.IndexOf(item);

    public bool Contains(Node item) => _list.Contains(item);

    public void CopyTo(Node[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public IEnumerator<Node> GetEnumerator() => _list.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();

    private void ValidateNewChild(Node item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.ParentNode != null) throw new InvalidOperationException("Node already has a parent");
        if (ReferenceEquals(item, _owner)) throw new InvalidOperationException("Cannot add an element as its own child");
    }

    private void Attach(Node item)
    {
        item.ParentNode = _owner;
        item.OwnerDocument = _owner.OwnerDocument;
        if (_owner.OwnerDocument != null)
            _owner.OwnerDocument.AssignOwnerDocument(item);
        if (item is Element element)
        {
            _owner.OnChildAdded(element);
            AttachIfNeeded(element);
        }
        _owner.InvalidateLayout();
    }

    private void AttachIfNeeded(Element item)
    {
        if (_owner.IsAttached) ((IComponentLifecycle)item).OnAttached();
        if (_owner.IsLoaded) ((IComponentLifecycle)item).OnLoaded();
    }

    private static void DetachIfNeeded(Node item)
    {
        if (item is not Element element) return;
        if (element.IsLoaded) ((IComponentLifecycle)element).OnUnloaded();
        if (element.IsAttached) ((IComponentLifecycle)element).OnDetached();
    }
}
