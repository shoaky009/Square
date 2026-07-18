using Square.Graphics;
using Square.UI;
using Square.Rendering.Tree;

namespace Square.Rendering;

public sealed class DisplayTree
{
    private readonly DisplayNode _root = new();
    private readonly List<Rect> _dirtyRects = [];
    private readonly List<IPopupElement> _popups = [];

    public void BuildFrom(Element element)
    {
        _root.Element = element;
        _root.Bounds = element.Geometry;
        _root.RebuildCommands();
        _root.PopupBounds = element is IPopupElement { IsPopupOpen: true } popup ? popup.PopupBounds : Rect.Empty;
        _root.IsDirty = true;
        _root.Children.Clear();
        BuildChildren(_root, element);
        RebuildPopupList();
        _dirtyRects.Clear();
    }

    private static void BuildChildren(DisplayNode parent, Element element)
    {
        foreach (var child in element.Children.OrderBy(child => child.ZIndex))
        {
            if (!child.IsVisible) continue;
            var node = new DisplayNode { Element = child, Bounds = child.Geometry, IsDirty = true };
            node.RebuildCommands();
            node.PopupBounds = child is IPopupElement { IsPopupOpen: true } popup ? popup.PopupBounds : Rect.Empty;
            node.IsDirty = true;
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

    private void UpdateDirty(DisplayNode node)
    {
        if (node.Element != null)
        {
            var bounds = node.Element.Geometry;
            var oldVisualBounds = node.VisualBounds.IsEmpty ? node.Bounds : node.VisualBounds;
            var oldPopupBounds = node.PopupBounds;
            if (node.Bounds != bounds)
            {
                _dirtyRects.Add(PadAndSnap(Union(oldVisualBounds, bounds)));
                node.Bounds = bounds;
            }
            if (node.Element.NeedsPaint)
            {
                node.IsDirty = true;
                node.RebuildCommands();
                _dirtyRects.Add(PadAndSnap(Union(oldVisualBounds, node.VisualBounds)));
            }

            var popupBounds = node.Element is IPopupElement { IsPopupOpen: true } popup
                ? popup.PopupBounds
                : Rect.Empty;
            if (oldPopupBounds != popupBounds)
            {
                _dirtyRects.Add(PadAndSnap(Union(oldPopupBounds, popupBounds)));
                node.PopupBounds = popupBounds;
            }
        }
        foreach (var child in node.Children)
            UpdateDirty(child);
    }

    private void RebuildPopupList()
    {
        _popups.Clear();
        CollectPopups(_root);
    }

    private void CollectPopups(DisplayNode node)
    {
        if (node.Element is IPopupElement popup)
            _popups.Add(popup);
        foreach (var child in node.Children)
            CollectPopups(child);
    }

    /// <summary>
    /// 收集本帧需要重画的矩形（NeedsPaint / IsDirty 节点的 Geometry，1px 外扩取整）。
    /// </summary>
    public List<Rect> CollectDirtyRects()
    {
        CollectDirtyRects(_root, _dirtyRects);
        var dirty = MergeDirtyRects(_dirtyRects);
        _dirtyRects.Clear();
        return dirty;
    }

    private static void CollectDirtyRects(DisplayNode node, List<Rect> dest)
    {
        if (node.IsDirty || (node.Element != null && node.Element.NeedsPaint))
        {
            var g = node.VisualBounds.IsEmpty ? node.Element?.Geometry ?? node.Bounds : node.VisualBounds;
            // Geometry 尚未 arrange 时用 Bounds；仍空则跳过（父/兄弟可能有有效区）
            if (!g.IsEmpty)
                dest.Add(PadAndSnap(g));
            if (node.Element is IPopupElement { IsPopupOpen: true } popup && !popup.PopupBounds.IsEmpty)
                dest.Add(PadAndSnap(popup.PopupBounds));
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
        var list = new List<Rect>(rects.Where(r => !r.IsEmpty));
        if (list.Count <= 1) return list;
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
        if (dirtyClip is { IsEmpty: true })
        {
            _dirtyRects.Clear();
            return;
        }
        if (dirtyClip is { } clip)
        {
            ctx.PushClip(clip);
            _root.Render(ctx, clip);
            RenderPopups(ctx, clip);
            ctx.PopClip();
        }
        else
        {
            _root.Render(ctx, dirtyClip);
            RenderPopups(ctx, dirtyClip);
        }
        _dirtyRects.Clear();
    }

    public Element? HitTestPopups(Point point)
    {
        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var hit = _popups[i].HitTestPopup(point);
            if (hit != null) return hit;
        }
        return null;
    }

    private void RenderPopups(IRenderContext ctx, Rect? dirtyClip)
    {
        foreach (var popup in _popups)
        {
            if (!popup.IsPopupOpen) continue;
            if (dirtyClip is { } clip && !popup.PopupBounds.IntersectsWith(clip)) continue;
            popup.PaintPopup(ctx);
        }
    }
}
