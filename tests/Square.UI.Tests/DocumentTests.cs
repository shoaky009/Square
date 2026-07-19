using System;
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

    [Fact]
    public void DomTextNodeCoexistsWithTextControl()
    {
        var domText = new Square.UI.Text("hello");
        var controlText = new Square.Controls.Controls.Text("hello");

        Assert.Equal(Node.NodeType.Text, domText.NodeTypeValue);
        Assert.Equal("#text", domText.NodeName);
        Assert.Equal("hello", domText.Data);
        Assert.Equal(5, domText.Length);

        domText.AppendData(" world");
        domText.ReplaceData(0, 5, "Hi");

        Assert.Equal("Hi world", domText.Data);
        Assert.Equal(Node.NodeType.Element, controlText.NodeTypeValue);
        Assert.Equal("Text", controlText.NodeName);
        Assert.Equal("hello", controlText.TextContent);
    }

    [Fact]
    public void ElementChildNodesCanContainDomTextWithoutChangingChildrenView()
    {
        var doc = new UIDocument();
        var parent = new View();
        var textNode = new Square.UI.Text("hello");
        var childElement = new Button("button");

        doc.Body.AppendChild(parent);
        parent.AppendChild(textNode);
        parent.AppendChild(childElement);

        Assert.Equal(2, parent.ChildNodes.Count);
        Assert.Single(parent.Children);
        Assert.Same(textNode, parent.ChildNodes[0]);
        Assert.Same(childElement, parent.ChildNodes[1]);
        Assert.Same(childElement, parent.Children[0]);
        Assert.Same(parent, textNode.ParentNode);
        Assert.Same(parent, textNode.ParentElement);
        Assert.Same(doc, textNode.OwnerDocument);
        Assert.Same(doc, childElement.OwnerDocument);

        Assert.Same(textNode, parent.RemoveChild(textNode));
        Assert.Null(textNode.ParentNode);
        Assert.Single(parent.ChildNodes);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void RangeExtractsTextAcrossDomTextNodes()
    {
        var doc = new UIDocument();
        var parent = new View();
        var first = new Square.UI.Text("hello ");
        var middle = new View();
        var second = new Square.UI.Text("world");
        var third = new Square.UI.Text("!");

        doc.Body.AppendChild(parent);
        parent.AppendChild(first);
        parent.AppendChild(middle);
        middle.AppendChild(second);
        parent.AppendChild(third);

        var range = doc.CreateRange();
        range.SetStart(first, 3);
        range.SetEnd(second, 2);

        Assert.False(range.Collapsed);
        Assert.Equal("lo wo", range.ToString());
    }

    [Fact]
    public void SelectionStoresSingleRangeAndReturnsSelectedText()
    {
        var doc = new UIDocument();
        var text = new Square.UI.Text("hello");
        doc.Body.AppendChild(text);

        var range = doc.CreateRange();
        range.SetStart(text, 1);
        range.SetEnd(text, 4);

        var selection = doc.GetSelection();
        selection.AddRange(range);

        Assert.Equal(1, selection.RangeCount);
        Assert.False(selection.IsCollapsed);
        Assert.Same(text, selection.AnchorNode);
        Assert.Equal(1, selection.AnchorOffset);
        Assert.Same(text, selection.FocusNode);
        Assert.Equal(4, selection.FocusOffset);
        Assert.Equal("ell", selection.ToString());

        selection.RemoveAllRanges();

        Assert.Equal(0, selection.RangeCount);
        Assert.True(selection.IsCollapsed);
        Assert.Equal(string.Empty, selection.ToString());
    }

    [Fact]
    public void TextControlMaintainsDomTextChildNode()
    {
        var doc = new UIDocument();
        var text = new Square.Controls.Controls.Text("hello");

        doc.Body.AppendChild(text);

        var textNode = Assert.IsType<Square.UI.Text>(Assert.Single(text.ChildNodes));
        Assert.Empty(text.Children);
        Assert.Equal("hello", textNode.Data);
        Assert.Same(text, textNode.ParentNode);
        Assert.Same(doc, textNode.OwnerDocument);

        text.TextContent = "hello world";

        Assert.Equal("hello world", textNode.Data);
        var range = doc.CreateRange();
        range.SetStart(textNode, 6);
        range.SetEnd(textNode, 11);
        Assert.Equal("world", range.ToString());
    }

    [Fact]
    public void LinkAndButtonMaintainDomTextChildNodes()
    {
        var doc = new UIDocument();
        var link = new Link("docs", "/docs");
        var button = new Button("submit");

        doc.Body.AppendChild(link);
        doc.Body.AppendChild(button);

        var linkText = Assert.IsType<Square.UI.Text>(Assert.Single(link.ChildNodes));
        var buttonText = Assert.IsType<Square.UI.Text>(Assert.Single(button.ChildNodes));
        Assert.Empty(link.Children);
        Assert.Empty(button.Children);
        Assert.Equal("docs", linkText.Data);
        Assert.Equal("submit", buttonText.Data);
        Assert.Same(doc, linkText.OwnerDocument);
        Assert.Same(doc, buttonText.OwnerDocument);

        link.TextContent = "documentation";
        button.TextContent = "submit form";

        Assert.Equal("documentation", linkText.Data);
        Assert.Equal("submit form", buttonText.Data);

        var range = doc.CreateRange();
        range.SetStart(linkText, 0);
        range.SetEnd(buttonText, 6);
        Assert.Equal("documentationsubmit", range.ToString());
    }
}
