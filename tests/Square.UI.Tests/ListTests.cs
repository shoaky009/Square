using Square.Controls;
using Square.Events;
using Square.Runtime.Binding;
using Xunit;
using ListControl = Square.Controls.List;

namespace Square.UI.Tests;

public class ListTests
{
    [Fact]
    public void SingleSelectionRaisesEventsOnlyWhenSelectionChanges()
    {
        var list = new ListControl { Items = ["Alpha", "Beta", "Gamma"] };
        var changes = 0;
        var selectionChanges = 0;
        list.AddEventListener(StandardEvents.Change, () => changes++);
        list.AddEventListener(StandardEvents.SelectionChange, () => selectionChanges++);

        Assert.True(list.SelectIndex(1));
        Assert.False(list.SelectIndex(1));

        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal("Beta", list.SelectedItem?.TextContent);
        Assert.Equal([1], list.SelectedIndices);
        Assert.Equal(1, changes);
        Assert.Equal(1, selectionChanges);
        Assert.True(list.QueryAll<ListItem>()[1].HasState(ElementState.Checked));
    }

    [Fact]
    public void MultipleSelectionSupportsControlAndShiftRanges()
    {
        var list = new ListControl
        {
            Items = ["A", "B", "C", "D", "E"],
            SelectionMode = SelectionMode.Multiple
        };

        list.SelectIndex(1);
        list.SelectIndex(3, shift: true);
        list.SelectIndex(4, control: true);

        Assert.Equal([1, 2, 3, 4], list.SelectedIndices);

        list.SelectIndex(3, control: true);

        Assert.Equal([1, 2, 4], list.SelectedIndices);
    }

    [Fact]
    public void KeyboardNavigationSkipsDisabledItems()
    {
        var list = new ListControl { Items = ["A", "B", "C"] };
        list.QueryAll<ListItem>()[1].IsDisabled = true;

        Assert.True(list.HandleKey(40));
        Assert.Equal(0, list.SelectedIndex);
        Assert.True(list.HandleKey(40));
        Assert.Equal(2, list.SelectedIndex);
        Assert.True(list.HandleKey(38));
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void ItemClicksSelectAndFocusTheList()
    {
        var list = new ListControl { Items = ["A", "B"] };
        var second = list.QueryAll<ListItem>()[1];

        second.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(1, list.SelectedIndex);
        Assert.True(list.IsFocused);
    }

    [Fact]
    public void ObservableItemsSourceRebuildsAndPreservesSelectedValues()
    {
        var source = new ObservableCollection<string> { "A", "B" };
        var list = new ListControl();
        list.SetItemsSource(source);
        list.SelectIndex(1);

        source.Insert(0, "Before");

        Assert.Equal(["Before", "A", "B"], list.QueryAll<ListItem>().Select(item => item.TextContent));
        Assert.Equal(2, list.SelectedIndex);
        Assert.Equal("B", list.SelectedItem?.TextContent);
    }

    [Fact]
    public void NoneSelectionModeIgnoresInput()
    {
        var list = new ListControl { Items = ["A"], SelectionMode = SelectionMode.None };

        Assert.False(list.SelectIndex(0));
        Assert.False(list.HandleKey(40));
        Assert.Equal(-1, list.SelectedIndex);
    }

    [Fact]
    public void DuplicateValuesDoNotCreateMultipleSelectionAfterSourceChanges()
    {
        var source = new ObservableCollection<string> { "Same", "Same" };
        var list = new ListControl();
        list.SetItemsSource(source);
        list.SelectIndex(1);

        source.Insert(0, "Before");

        Assert.Equal([2], list.SelectedIndices);
        Assert.Equal("Same", list.SelectedItem?.TextContent);
    }

    [Fact]
    public void DetachedListStopsObservingItsItemsSourceUntilReattached()
    {
        var source = new ObservableCollection<string> { "A" };
        var list = new ListControl();
        list.SetItemsSource(source);
        ((Square.Runtime.IComponentLifecycle)list).OnAttached();
        ((Square.Runtime.IComponentLifecycle)list).OnDetached();

        source.Add("B");

        Assert.Single(list.QueryAll<ListItem>());

        ((Square.Runtime.IComponentLifecycle)list).OnAttached();
        source.Add("C");

        Assert.Equal(["A", "B", "C"], list.QueryAll<ListItem>().Select(item => item.TextContent));
    }
}
