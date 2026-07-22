namespace Square.UI.ElementApi;

/// <summary>
/// Element-only children view, aligned with DOM <c>children</c>.
/// </summary>
public sealed class ChildrenCollection : IList<Element>
{
    private readonly ChildNodeCollection _nodes;

    internal ChildrenCollection(ChildNodeCollection nodes) { _nodes = nodes; }

    public Element this[int index]
    {
        get => Elements().ElementAt(index);
        set => throw new NotSupportedException("Use Insert/RemoveAt to manage children");
    }

    public int Count => Elements().Count();

    public bool IsReadOnly => false;

    public void Add(Element item) => _nodes.Add(item);

    public void AddRange(IEnumerable<Element> items)
    {
        foreach (var item in items) Add(item);
    }

    public void Insert(int index, Element item) => _nodes.Insert(ToNodeInsertIndex(index), item);

    public void InsertBefore(Element newChild, Element refChild) => _nodes.InsertBefore(newChild, refChild);

    public bool Remove(Element item) => _nodes.Remove(item);

    public void RemoveAt(int index) => _nodes.RemoveAt(ToNodeIndex(index));

    public void Clear()
    {
        foreach (var element in Elements().ToArray()) _nodes.Remove(element);
    }

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

    public bool Contains(Element item) => IndexOf(item) >= 0;

    public void CopyTo(Element[] array, int arrayIndex)
    {
        foreach (var element in Elements()) array[arrayIndex++] = element;
    }

    public IEnumerator<Element> GetEnumerator() => Elements().GetEnumerator();

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
