namespace Square.UI.ElementApi;

using Square.Runtime;

/// <summary>
/// 元素子节点列表（对齐 DOM 子节点集合；支持 Add/Insert/Remove）。
/// 维护 <see cref="Element.Parent"/> 与 <see cref="Node.OwnerDocument"/>，并触发生命周期挂卸。
/// </summary>
public sealed class ChildrenCollection : IList<Element>
{
    private readonly Element _owner;
    private readonly List<Element> _list = [];

    internal ChildrenCollection(Element owner) { _owner = owner; }

    /// <summary>按下标访问子元素；设置器不可用，请使用 Insert/RemoveAt。</summary>
    public Element this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException("Use Insert/RemoveAt to manage children");
    }

    /// <summary>子节点数量。</summary>
    public int Count => _list.Count;

    /// <summary>始终为 false（可修改）。</summary>
    public bool IsReadOnly => false;

    /// <summary>追加子元素（类似 appendChild；已有父节点时抛错）。</summary>
    public void Add(Element item)
    {
        if (item.Parent != null)
            throw new InvalidOperationException("Element already has a parent");
        _list.Add(item);
        item.Parent = _owner;
        item.OwnerDocument = _owner.OwnerDocument;
        if (_owner.OwnerDocument != null)
            _owner.OwnerDocument.AssignOwnerDocument(item);
        _owner.OnChildAdded(item);
        AttachIfNeeded(item);
        _owner.InvalidateLayout();
    }

    /// <summary>批量追加子元素。</summary>
    public void AddRange(IEnumerable<Element> items)
    {
        foreach (var item in items) Add(item);
    }

    /// <summary>在指定下标插入子元素。</summary>
    public void Insert(int index, Element item)
    {
        if (item.Parent != null)
            throw new InvalidOperationException("Element already has a parent");
        _list.Insert(index, item);
        item.Parent = _owner;
        item.OwnerDocument = _owner.OwnerDocument;
        if (_owner.OwnerDocument != null)
            _owner.OwnerDocument.AssignOwnerDocument(item);
        _owner.OnChildAdded(item);
        AttachIfNeeded(item);
        _owner.InvalidateLayout();
    }

    /// <summary>在参考子节点之前插入（类似 insertBefore）。</summary>
    public void InsertBefore(Element newChild, Element refChild)
    {
        var index = _list.IndexOf(refChild);
        if (index < 0) throw new ArgumentException("refChild not found");
        Insert(index, newChild);
    }

    /// <summary>移除指定子元素；不存在则返回 false。</summary>
    public bool Remove(Element item)
    {
        var index = _list.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>按下标移除子元素并触发卸载生命周期。</summary>
    public void RemoveAt(int index)
    {
        var item = _list[index];
        DetachIfNeeded(item);
        _list.RemoveAt(index);
        item.Parent = null;
        _owner.OnChildRemoved(item);
        _owner.InvalidateLayout();
    }

    /// <summary>清空全部子元素。</summary>
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

    /// <summary>子元素下标；未找到返回 -1。</summary>
    public int IndexOf(Element item) => _list.IndexOf(item);

    /// <summary>是否包含指定子元素。</summary>
    public bool Contains(Element item) => _list.Contains(item);

    /// <summary>复制到数组。</summary>
    public void CopyTo(Element[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <summary>枚举子元素。</summary>
    public IEnumerator<Element> GetEnumerator() => _list.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();

    private void AttachIfNeeded(Element item)
    {
        if (_owner.IsAttached) ((IComponentLifecycle)item).OnAttached();
        if (_owner.IsLoaded) ((IComponentLifecycle)item).OnLoaded();
    }

    private void DetachIfNeeded(Element item)
    {
        if (item.IsLoaded) ((IComponentLifecycle)item).OnUnloaded();
        if (item.IsAttached) ((IComponentLifecycle)item).OnDetached();
    }
}
