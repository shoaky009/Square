using Square.Graphics;
using Square.UI;
using Square.Rendering.Commands;
using Square.Rendering.Tree;

namespace Square.Rendering;

public sealed class RenderTree
{
    private readonly RenderNode _root = new();
    private readonly List<Rect> _dirtyRects = [];

    public void BuildFrom(Visual visual)
    {
        _root.Visual = visual;
        _root.Bounds = visual.Geometry;
        _root.IsDirty = true;
        _root.Children.Clear();
        BuildChildren(_root, visual);
    }

    private static void BuildChildren(RenderNode parent, Visual visual)
    {
        foreach (var child in visual.Children.OrderBy(child => child.ZIndex))
        {
            if (!child.IsVisible) continue;
            var node = new RenderNode { Visual = child, Bounds = child.Geometry, IsDirty = true };
            parent.Children.Add(node);
            BuildChildren(node, child);
        }
    }

    public void Invalidate(Rect rect) => _dirtyRects.Add(rect);

    public void UpdateDirty() => UpdateDirty(_root);

    private static void UpdateDirty(RenderNode node)
    {
        if (node.Visual != null && node.Visual.IsVisualDirty)
            node.IsDirty = true;
        foreach (var child in node.Children)
            UpdateDirty(child);
    }

    public void Render(IRenderContext ctx)
    {
        _root.Render(ctx);
        _dirtyRects.Clear();
    }
}
