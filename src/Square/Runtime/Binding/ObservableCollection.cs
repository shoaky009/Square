using System.Collections;
using System.Collections.Specialized;

namespace Square.Runtime.Binding;

/// <summary>提供变更通知的集合（对齐 <see cref="INotifyCollectionChanged"/>）。</summary>
public sealed class ObservableCollection<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged
{
    private readonly List<T> _items = [];

    /// <summary>集合变更时触发。</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>获取或设置指定索引处的元素。</summary>
    public T this[int index]
    {
        get => _items[index];
        set
        {
            var old = _items[index];
            _items[index] = value;
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, old, index));
        }
    }

    /// <summary>集合中元素数。</summary>
    public int Count => _items.Count;
    /// <summary>集合是否只读。</summary>
    public bool IsReadOnly => false;

    /// <summary>添加元素并触发变更通知。</summary>
    public void Add(T item)
    {
        var index = _items.Count;
        _items.Add(item);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <summary>批量添加元素。</summary>
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Add(item);
    }

    /// <summary>在指定索引处插入元素。</summary>
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <summary>移除指定元素；返回是否移除成功。</summary>
    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>移除指定索引处的元素。</summary>
    public void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    /// <summary>将元素从旧索引移动到新索引。</summary>
    public void Move(int oldIndex, int newIndex)
    {
        var item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
    }

    /// <summary>清空集合并触发重置通知。</summary>
    public void Clear()
    {
        _items.Clear();
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>返回指定元素的索引。</summary>
    public int IndexOf(T item) => _items.IndexOf(item);
    /// <summary>判断集合是否包含指定元素。</summary>
    public bool Contains(T item) => _items.Contains(item);
    /// <summary>将集合元素复制到数组。</summary>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    /// <summary>返回集合枚举器。</summary>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}