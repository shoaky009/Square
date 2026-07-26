using System.Collections.Specialized;
using Square.Events;
using Square.Runtime.Binding;
using Square.UI;

namespace Square.Controls;

/// <summary>列表选择模式。</summary>
public enum SelectionMode
{
    /// <summary>不可选。</summary>
    None,
    /// <summary>单选。</summary>
    Single,
    /// <summary>多选。</summary>
    Multiple
}

/// <summary>Scrollable selectable list with declarative items or an observable string source.</summary>
public class List : ScrollViewer
{
    private ObservableCollection<string>? _itemsSource;
    private bool _itemsSourceSubscribed;
    private int _activeIndex = -1;
    private int _selectionAnchor = -1;
    private bool _rebuildingItems;

    /// <summary>初始化 <see cref="List"/> 的新实例。</summary>
    public List()
    {
        Style.SetCascaded("display", "flex", int.MinValue);
        Style.SetCascaded("flex-direction", "column", int.MinValue);
        AddEventListener(StandardEvents.Click, OnItemClick);
        AddEventListener<KeyboardEvent>(StandardEvents.KeyDown, e =>
        {
            if (!HandleKey(e.KeyCode, e.ShiftKey, e.ControlKey)) return;
            e.PreventDefault();
        });
    }

    /// <summary>静态条目数组。</summary>
    public string[] Items
    {
        get => GetProperty<string[]>(nameof(Items)) ?? [];
        set => SetProperty(nameof(Items), value ?? []);
    }

    /// <summary>选择模式。</summary>
    public SelectionMode SelectionMode
    {
        get => Properties.HasValue(nameof(SelectionMode))
            ? GetProperty<SelectionMode>(nameof(SelectionMode))
            : SelectionMode.Single;
        set => SetProperty(nameof(SelectionMode), value);
    }

    /// <summary>当前选中索引（多选时为首个）。</summary>
    public int SelectedIndex
    {
        get => SelectedIndices.Count == 0 ? -1 : SelectedIndices[0];
        set => SelectIndex(value);
    }

    /// <summary>当前选中项。</summary>
    public ListItem? SelectedItem => SelectedIndex >= 0 ? GetItems()[SelectedIndex] : null;

    /// <summary>所有选中索引。</summary>
    public IReadOnlyList<int> SelectedIndices => GetItems()
        .Select((item, index) => (item, index))
        .Where(entry => entry.item.IsSelected)
        .Select(entry => entry.index)
        .ToArray();

    /// <summary>所有选中项。</summary>
    public IReadOnlyList<ListItem> SelectedItems => GetItems().Where(item => item.IsSelected).ToArray();

    /// <summary>绑定可观察字符串集合作为数据源。</summary>
    public void SetItemsSource(ObservableCollection<string>? source)
    {
        if (ReferenceEquals(_itemsSource, source)) return;
        UnsubscribeItemsSource();
        _itemsSource = source;
        SubscribeItemsSource();
        RebuildItems(source is null ? Items : source);
    }

    /// <summary>选中指定索引的项，返回是否实际改变选择。</summary>
    public bool SelectIndex(int index, bool control = false, bool shift = false)
    {
        var items = GetItems();
        if (SelectionMode == SelectionMode.None || index < 0 || index >= items.Count || !IsSelectable(items[index]))
            return false;

        var before = SelectedIndices;
        if (SelectionMode == SelectionMode.Single)
        {
            SetOnlySelected(items, index);
        }
        else if (shift)
        {
            var anchor = _selectionAnchor >= 0 ? _selectionAnchor : index;
            if (!control) ClearSelection(items);
            var start = Math.Min(anchor, index);
            var end = Math.Max(anchor, index);
            for (var i = start; i <= end; i++)
                if (IsSelectable(items[i]))
                    items[i].IsSelected = true;
        }
        else if (control)
        {
            items[index].IsSelected = !items[index].IsSelected;
            _selectionAnchor = index;
        }
        else
        {
            SetOnlySelected(items, index);
        }

        _activeIndex = index;
        if (!shift) _selectionAnchor = index;
        return NotifySelectionChanged(before);
    }

    /// <summary>清除所有选择。</summary>
    public void ClearSelection()
    {
        var before = SelectedIndices;
        ClearSelection(GetItems());
        _activeIndex = -1;
        _selectionAnchor = -1;
        NotifySelectionChanged(before);
    }

    /// <summary>处理键盘导航，返回是否已处理。</summary>
    public bool HandleKey(int keyCode, bool shift = false, bool control = false)
    {
        var items = GetItems();
        if (!IsEnabled || SelectionMode == SelectionMode.None || items.Count == 0) return false;

        var next = keyCode switch
        {
            38 => FindSelectable(items, _activeIndex >= 0 ? _activeIndex - 1 : items.Count - 1, -1),
            40 => FindSelectable(items, _activeIndex >= 0 ? _activeIndex + 1 : 0, 1),
            36 => FindSelectable(items, 0, 1),
            35 => FindSelectable(items, items.Count - 1, -1),
            32 when _activeIndex >= 0 => _activeIndex,
            _ => -1
        };
        if (next < 0) return false;

        if (control && keyCode != 32)
        {
            _activeIndex = next;
            return true;
        }

        SelectIndex(next, control, shift);
        return true;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(string name)
    {
        base.OnPropertyChanged(name);
        if (name == nameof(Items) && _itemsSource == null) RebuildItems(Items);
        if (name == nameof(SelectedIndex)) SelectIndex(GetProperty<int>(nameof(SelectedIndex)));
        if (name != nameof(SelectionMode) || SelectionMode == SelectionMode.Multiple) return;
        if (SelectionMode == SelectionMode.None) ClearSelection();
        else if (SelectedIndices.Count > 1) SelectIndex(SelectedIndex);
    }

    /// <inheritdoc/>
    protected override void OnAttachedCore()
    {
        base.OnAttachedCore();
        SubscribeItemsSource();
    }

    /// <inheritdoc/>
    protected override void OnDetachedCore()
    {
        UnsubscribeItemsSource();
        base.OnDetachedCore();
    }

    /// <inheritdoc/>
    internal override void OnChildRemoved(Element child)
    {
        base.OnChildRemoved(child);
        if (_rebuildingItems || child is not ListItem item || !item.IsSelected) return;
        _activeIndex = Math.Min(_activeIndex, GetItems().Count - 1);
        DispatchSelectionEvents();
    }

    private void OnItemClick(Event e)
    {
        if (!IsEnabled || e.Target is not Element target) return;
        ListItem? item = null;
        for (Element? current = target; current != null && current != this; current = current.Parent)
        {
            if (current is not ListItem candidate) continue;
            item = candidate;
            break;
        }

        if (item?.Parent != this) return;
        SelectIndex(GetItems().IndexOf(item));
        Focus();
    }

    private void OnItemsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildItems(_itemsSource is null ? Items : _itemsSource, MapSelectionAfterChange(e));

    private void RebuildItems(IEnumerable<string> values, IReadOnlyList<int>? selectedIndexes = null)
    {
        selectedIndexes ??= SelectedIndices.ToArray();
        var before = SelectedIndices;
        _rebuildingItems = true;
        try
        {
            Children.Clear();
            var index = 0;
            foreach (var value in values)
            {
                Children.Add(new ListItem(value)
                {
                    Marker = "",
                    IsSelected = selectedIndexes.Contains(index)
                });
                index++;
            }
        }
        finally
        {
            _rebuildingItems = false;
        }

        var items = GetItems();
        _activeIndex = Math.Min(_activeIndex, items.Count - 1);
        _selectionAnchor = Math.Min(_selectionAnchor, items.Count - 1);
        if (SelectionMode == SelectionMode.Single && SelectedIndices.Count > 1)
            SetOnlySelected(items, SelectedIndex);
        NotifySelectionChanged(before);
    }

    private IReadOnlyList<int> MapSelectionAfterChange(NotifyCollectionChangedEventArgs change)
    {
        var selected = SelectedIndices.ToList();
        switch (change.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                var added = change.NewItems?.Count ?? 0;
                for (var i = 0; i < selected.Count; i++)
                    if (selected[i] >= change.NewStartingIndex)
                        selected[i] += added;
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                var removed = change.OldItems?.Count ?? 0;
                selected.RemoveAll(index =>
                    index >= change.OldStartingIndex && index < change.OldStartingIndex + removed);
                for (var i = 0; i < selected.Count; i++)
                    if (selected[i] >= change.OldStartingIndex + removed)
                        selected[i] -= removed;
                break;
            }
            case NotifyCollectionChangedAction.Move when change.OldStartingIndex >= 0 && change.NewStartingIndex >= 0:
            {
                for (var i = 0; i < selected.Count; i++)
                {
                    var index = selected[i];
                    if (index == change.OldStartingIndex) selected[i] = change.NewStartingIndex;
                    else if (change.OldStartingIndex < change.NewStartingIndex && index > change.OldStartingIndex &&
                             index <= change.NewStartingIndex) selected[i]--;
                    else if (change.NewStartingIndex < change.OldStartingIndex && index >= change.NewStartingIndex &&
                             index < change.OldStartingIndex) selected[i]++;
                }

                break;
            }
            case NotifyCollectionChangedAction.Reset:
                selected.Clear();
                break;
        }

        return selected;
    }

    private void SubscribeItemsSource()
    {
        if (_itemsSource == null || _itemsSourceSubscribed) return;
        _itemsSource.CollectionChanged += OnItemsSourceChanged;
        _itemsSourceSubscribed = true;
    }

    private void UnsubscribeItemsSource()
    {
        if (_itemsSource == null || !_itemsSourceSubscribed) return;
        _itemsSource.CollectionChanged -= OnItemsSourceChanged;
        _itemsSourceSubscribed = false;
    }

    private static void SetOnlySelected(IReadOnlyList<ListItem> items, int selectedIndex)
    {
        for (var i = 0; i < items.Count; i++)
            items[i].IsSelected = i == selectedIndex;
    }

    private static void ClearSelection(IEnumerable<ListItem> items)
    {
        foreach (var item in items) item.IsSelected = false;
    }

    private bool NotifySelectionChanged(IReadOnlyList<int> before)
    {
        if (before.SequenceEqual(SelectedIndices)) return false;
        DispatchSelectionEvents();
        return true;
    }

    private void DispatchSelectionEvents()
    {
        DispatchEvent(StandardEvents.CreateSelectionChange());
        DispatchEvent(StandardEvents.CreateChange());
    }

    private static bool IsSelectable(ListItem item) => item.IsVisible && item.IsEnabled;

    private static int FindSelectable(IReadOnlyList<ListItem> items, int start, int direction)
    {
        for (var i = start; i >= 0 && i < items.Count; i += direction)
            if (IsSelectable(items[i]))
                return i;
        return -1;
    }

    private System.Collections.Generic.List<ListItem> GetItems() => Children.OfType<ListItem>().ToList();
}
