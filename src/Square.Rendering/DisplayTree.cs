using Square.Graphics;
using Square.UI;
using Square.Rendering.Tree;

namespace Square.Rendering;

public sealed class DisplayTree
{
    private readonly DisplayNode _root = new();
    private readonly List<Rect> _dirtyRects = [];

    public void BuildFrom(Element element)
    {
        _root.Element = element;
        _root.Bounds = element.Geometry;
        _root.IsDirty = true;
        _root.Children.Clear();
        BuildChildren(_root, element);
        _dirtyRects.Clear();
    }

    private static void BuildChildren(DisplayNode parent, Element element)
    {
        foreach (var child in element.Children.OrderBy(child => child.ZIndex))
        {
            if (!child.IsVisible) continue;
            var node = new DisplayNode { Element = child, Bounds = child.Geometry, IsDirty = true };
            parent.Children.Add(node);
            BuildChildren(node, child);
        }
    }

    public void Invalidate(Rect rect)
    {
        if (!rect.IsEmpty)
            _dirtyRects.Add(rect);
    }

    public void UpdateDirty() => UpdateDirty(_root);

    private static void UpdateDirty(DisplayNode node)
    {
        if (node.Element != null)
        {
            node.Bounds = node.Element.Geometry;
            if (node.Element.NeedsPaint)
                node.IsDirty = true;
        }
        foreach (var child in node.Children)
            UpdateDirty(child);
    }

    /// <summary>
    /// 收集本帧需要重画的矩形（NeedsPaint / IsDirty 节点的 Geometry，1px 外扩取整）。
    /// </summary>
    public List<Rect> CollectDirtyRects()
    {
        _dirtyRects.Clear();
        CollectDirtyRects(_root, _dirtyRects);
        return MergeDirtyRects(_dirtyRects);
    }

    private static void CollectDirtyRects(DisplayNode node, List<Rect> dest)
    {
        if (node.IsDirty || (node.Element != null && node.Element.NeedsPaint))
        {
            var g = node.Element?.Geometry ?? node.Bounds;
            // Geometry 尚未 arrange 时用 Bounds；仍空则跳过（父/兄弟可能有有效区）
            if (!g.IsEmpty)
                dest.Add(PadAndSnap(g));
        }
        foreach (var child in node.Children)
            CollectDirtyRects(child, dest);
    }

    /// <summary>外扩 1 逻辑像素并 snap 到整数像素，减少抗锯齿残影。</summary>
    private static Rect PadAndSnap(Rect g)
    {
        var x0 = MathF.Floor(g.X) - 1;
        var y0 = MathF.Floor(g.Y) - 1;
        var x1 = MathF.Ceiling(g.Right) + 1;
        var y1 = MathF.Ceiling(g.Bottom) + 1;
        return new Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }

    /// <summary>
    /// 合并相交/相邻脏矩形（简单 O(n²) 迭代合并，动画场景 n 通常很小）。
    /// </summary>
    public static List<Rect> MergeDirtyRects(List<Rect> rects)
    {
        if (rects.Count <= 1) return rects;
        var list = new List<Rect>(rects.Where(r => !r.IsEmpty));
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    if (!RectsShouldMerge(list[i], list[j])) continue;
                    list[i] = Union(list[i], list[j]);
                    list.RemoveAt(j);
                    changed = true;
                    break;
                }
                if (changed) break;
            }
        }
        return list;
    }

    private static bool RectsShouldMerge(Rect a, Rect b)
    {
        // 相交或间距 ≤ 2px 的相邻矩形合并，减少 Present 次数
        var inflated = a.Inflate(2, 2);
        return inflated.IntersectsWith(b);
    }

    public static Rect Union(Rect a, Rect b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        var x0 = Math.Min(a.X, b.X);
        var y0 = Math.Min(a.Y, b.Y);
        var x1 = Math.Max(a.Right, b.Right);
        var y1 = Math.Max(a.Bottom, b.Bottom);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    public static float Area(Rect r) => r.IsEmpty ? 0 : r.Width * r.Height;

    public void Render(IRenderContext ctx) => Render(ctx, dirtyClip: null);

    /// <summary>
    /// 渲染显示树。<paramref name="dirtyClip"/> 非 null 时仅绘制与之相交的节点。
    /// </summary>
    public void Render(IRenderContext ctx, Rect? dirtyClip)
    {
        _root.Render(ctx, dirtyClip);
        _dirtyRects.Clear();
    }
}
