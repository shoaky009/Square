using Square.Runtime.Binding;
using Xunit;

namespace Square.Runtime.Tests;

public class ObservableCollectionTests
{
    [Fact]
    public void AddAndCount()
    {
        var coll = new ObservableCollection<int>();
        coll.Add(1);
        coll.Add(2);
        coll.Add(3);
        Assert.Equal(3, coll.Count);
    }

    [Fact]
    public void Remove()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };
        Assert.True(coll.Remove(2));
        Assert.Equal(2, coll.Count);
        Assert.DoesNotContain(2, coll);
    }

    [Fact]
    public void Insert()
    {
        var coll = new ObservableCollection<int> { 1, 3 };
        coll.Insert(1, 2);
        Assert.Equal(2, coll[1]);
        Assert.Equal(3, coll.Count);
    }

    [Fact]
    public void Clear()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };
        coll.Clear();
        Assert.Empty(coll);
    }

    [Fact]
    public void Move()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };
        coll.Move(0, 2);
        Assert.Equal(2, coll[0]);
        Assert.Equal(3, coll[1]);
        Assert.Equal(1, coll[2]);
    }

    [Fact]
    public void AddRange()
    {
        var coll = new ObservableCollection<int>();
        coll.AddRange(new[] { 1, 2, 3, 4 });
        Assert.Equal(4, coll.Count);
    }

    [Fact]
    public void CollectionChangedAdd()
    {
        var coll = new ObservableCollection<int>();
        var notified = false;
        coll.CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                notified = true;
        };
        coll.Add(42);
        Assert.True(notified);
    }

    [Fact]
    public void CollectionChangedRemove()
    {
        var coll = new ObservableCollection<int> { 1, 2, 3 };
        var notified = false;
        coll.CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                notified = true;
        };
        coll.Remove(2);
        Assert.True(notified);
    }

    [Fact]
    public void Indexer()
    {
        var coll = new ObservableCollection<string> { "a", "b", "c" };
        Assert.Equal("b", coll[1]);
        coll[1] = "x";
        Assert.Equal("x", coll[1]);
    }

    [Fact]
    public void GetEnumerator()
    {
        var coll = new ObservableCollection<int> { 10, 20, 30 };
        var sum = 0;
        foreach (var item in coll) sum += item;
        Assert.Equal(60, sum);
    }
}