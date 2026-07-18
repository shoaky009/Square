using Square.Controls.Controls;
using Square.Controls.Registration;
using Square.UI;
using Xunit;

namespace Square.UI.Tests;

public class DocumentTests
{
    public DocumentTests()
    {
        ControlRegistration.RegisterDefaults();
    }

    [Fact]
    public void UIDocumentHasReadonlyShell()
    {
        var doc = new UIDocument();

        Assert.Same(doc.Ui, doc.DocumentElement);
        Assert.Equal("UI", doc.DocumentElement.TagName);
        Assert.Equal("Head", doc.Head.TagName);
        Assert.Equal("Body", doc.Body.TagName);
        Assert.Contains(doc.Head, doc.Ui.Children);
        Assert.Contains(doc.Body, doc.Ui.Children);
        Assert.Same(doc, doc.Body.OwnerDocument);
    }

    [Fact]
    public void BodyHostsApplicationContent()
    {
        var doc = new UIDocument();
        var view = new View();
        doc.Body.Children.Add(view);

        Assert.Same(doc.Body, view.Parent);
        Assert.Same(doc, view.OwnerDocument);
    }

    [Fact]
    public void CreateElementUsesRegistry()
    {
        var doc = new UIDocument();
        var text = doc.CreateElement("Text");

        Assert.IsType<Square.Controls.Controls.Text>(text);
        Assert.Same(doc, text.OwnerDocument);
    }

    [Fact]
    public void CreateElementUnknownTagThrows()
    {
        var doc = new UIDocument();
        Assert.Throws<InvalidOperationException>(() => doc.CreateElement("NoSuchTag"));
    }

    [Fact]
    public void GetElementByIdFindsDescendant()
    {
        var doc = new UIDocument();
        var item = new ListItem { Id = "item-1", TextContent = "One" };
        doc.Body.Children.Add(item);

        Assert.Same(item, doc.GetElementById("item-1"));
        Assert.Same(item, doc.GetElementById<ListItem>("item-1"));
    }

    [Fact]
    public void TitleRoundTrips()
    {
        var doc = new UIDocument { Title = "Hello" };
        Assert.Equal("Hello", doc.Title);
    }

    [Fact]
    public void EventFromBodyBubblesToDocument()
    {
        var doc = new UIDocument();
        var button = new Button();
        doc.Body.Children.Add(button);
        var seen = 0;
        doc.AddEventListener("click", _ => seen++);

        button.DispatchEvent(Square.Events.StandardEvents.CreateClick());

        Assert.Equal(1, seen);
    }

    [Fact]
    public void AppendChildAndRemoveChildMatchDomSemantics()
    {
        var parent = new View();
        var a = new Square.Controls.Controls.Text("a");
        var b = new Square.Controls.Controls.Text("b");

        Assert.Same(a, parent.AppendChild(a));
        parent.AppendChild(b);
        Assert.Equal(2, parent.ChildElementCount);
        Assert.Same(a, parent.FirstElementChild);
        Assert.Same(b, parent.LastElementChild);
        Assert.Same(parent, a.ParentNode);
        Assert.Same(parent, a.ParentElement);

        Assert.Same(a, parent.RemoveChild(a));
        Assert.Null(a.Parent);
        Assert.Equal(1, parent.ChildElementCount);
        var ex = Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(a));
        Assert.Contains("not a child", ex.Message);
    }

    [Fact]
    public void InsertBeforeAndReplaceChildrenWork()
    {
        var parent = new View();
        var a = new Square.Controls.Controls.Text("a");
        var b = new Square.Controls.Controls.Text("b");
        var c = new Square.Controls.Controls.Text("c");
        parent.AppendChild(b);
        parent.InsertBefore(a, b);
        Assert.Equal(2, parent.Children.Count);
        Assert.Same(a, parent.Children[0]);
        Assert.Same(b, parent.Children[1]);

        parent.ReplaceChildren(c);
        Assert.Single(parent.Children);
        Assert.Same(c, parent.FirstElementChild);
        Assert.Null(a.Parent);
        Assert.Null(b.Parent);
    }

    [Fact]
    public void GetBoundingClientRectReturnsGeometry()
    {
        var view = new View { Geometry = new Square.Graphics.Rect(10, 20, 30, 40) };
        Assert.Equal(view.Geometry, view.GetBoundingClientRect());
    }

    [Fact]
    public void NodeInheritanceForkIsDocumentAndElement()
    {
        var doc = new UIDocument();
        var view = new View();
        doc.Body.AppendChild(view);

        Assert.IsAssignableFrom<Node>(doc);
        Assert.IsAssignableFrom<Node>(view);
        Assert.Equal(Node.NodeType.Document, doc.NodeTypeValue);
        Assert.Equal(Node.NodeType.Element, view.NodeTypeValue);
        Assert.Equal("#document", doc.NodeName);
        Assert.Equal("View", view.NodeName);
        Assert.Same(doc, view.OwnerDocument);
        Assert.Same(doc.Body, view.ParentNode);
        Assert.Same(doc.Body, view.ParentElement);
        Assert.Null(doc.ParentNode);
        Assert.Null(doc.OwnerDocument);

        // 事件：Parent 为空时经 OwnerDocument 冒泡到 Document
        var hops = 0;
        doc.AddEventListener("click", _ => hops++);
        view.DispatchEvent(Square.Events.StandardEvents.CreateClick());
        Assert.Equal(1, hops);
    }
}
