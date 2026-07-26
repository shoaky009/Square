namespace Square.UI.ElementApi;

/// <summary>
/// Element-only children view, aligned with DOM <c>children</c>.
/// </summary>
public sealed class ChildrenCollection : IList<Element>
{
    private readonly ChildNodeCollection _nodes;

    internal ChildrenCollection(ChildNodeCollection nodes) { _nodes = nodes; }

    /// <summary>获取或设置指定索引处的子元素（设置不支持，抛出异常）。</summary>
    public Element this[int index]
    {
        get => Elements().ElementAt(index);
        set => throw new NotSupportedException("Use Insert/RemoveAt to manage children");
    }

    /// <summary>子元素数量。</summary>
    public int Count => Elements().Count();

    /// <summary>是否只读。</summary>
    public bool IsReadOnly => false;

    /// <summary>追加子元素。</summary>
    public void Add(Element item) => _nodes.Add(item);

    /// <summary>批量追加子元素。</summary>
    public void AddRange(IEnumerable<Element> items)
    {
        foreach (var item in items) Add(item);
    }

    /// <summary>在指定索引处插入子元素。</summary>
    public void Insert(int index, Element item) => _nodes.Insert(ToNodeInsertIndex(index), item);

    /// <summary>在参考子元素之前插入子元素。</summary>
    public void InsertBefore(Element newChild, Element refChild) => _nodes.InsertBefore(newChild, refChild);

    /// <summary>将子元素从旧索引移动到新索引。</summary>
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        _nodes.Move(ToNodeIndex(oldIndex), ToNodeIndex(newIndex));
    }

    /// <summary>移除指定子元素。</summary>
    public bool Remove(Element item) => _nodes.Remove(item);

    /// <summary>移除指定索引处的子元素。</summary>
    public void RemoveAt(int index) => _nodes.RemoveAt(ToNodeIndex(index));

    /// <summary>清空所有子元素。</summary>
    public void Clear()
    {
        foreach (var element in Elements().ToArray()) _nodes.Remove(element);
    }

    /// <summary>返回指定子元素的索引。</summary>
    public int IndexOf(Element item)
    {
        var index = 0;
        foreach (var element in Elements())
        {
            if (ReferenceEquals(element, item)) return index;
            index++;
        }
        return -1;
    }

    /// <summary>是否包含指定子元素。</summary>
    public bool Contains(Element item) => IndexOf(item) >= 0;

    /// <summary>复制到数组。</summary>
    public void CopyTo(Element[] array, int arrayIndex)
    {
        foreach (var element in Elements()) array[arrayIndex++] = element;
    }

    /// <summary>返回子元素枚举器。</summary>
    public IEnumerator<Element> GetEnumerator() => Elements().GetEnumerator();

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<Element> Elements() => _nodes.OfType<Element>();

    private int ToNodeIndex(int elementIndex)
    {
        if (elementIndex < 0) throw new ArgumentOutOfRangeException(nameof(elementIndex));
        var currentElementIndex = 0;
        for (var i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i] is not Element) continue;
            if (currentElementIndex == elementIndex) return i;
            currentElementIndex++;
        }
        throw new ArgumentOutOfRangeException(nameof(elementIndex));
    }

    private int ToNodeInsertIndex(int elementIndex)
    {
        if (elementIndex < 0) throw new ArgumentOutOfRangeException(nameof(elementIndex));
        if (elementIndex == Count) return _nodes.Count;
        return ToNodeIndex(elementIndex);
    }
}
