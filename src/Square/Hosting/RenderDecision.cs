using Square.Graphics;
using Square.Rendering;

namespace Square.Hosting;

internal static class RenderDecision
{
    /// <summary>根据脏矩形分布与渲染模式判定本帧采用全帧或脏区渲染。</summary>
    public static RenderDiagnostics Decide(
        RenderMode mode,
        IReadOnlyList<Rect> dirtyRects,
        Size clientSize,
        int maxDirtyRectCount,
        float maxDirtyAreaRatio)
    {
        if (dirtyRects.Count == 0)
        {
            return new RenderDiagnostics(
                mode,
                true,
                "NoDirtyRects",
                0,
                1f,
                new Rect(0, 0, clientSize.Width, clientSize.Height));
        }

        var clientArea = Math.Max(1f, clientSize.Width * clientSize.Height);
        var dirtyArea = 0f;
        foreach (var rect in dirtyRects) dirtyArea += DisplayTree.Area(rect);
        var dirtyRatio = dirtyArea / clientArea;
        var union = dirtyRects[0];
        for (var i = 1; i < dirtyRects.Count; i++)
            union = DisplayTree.Union(union, dirtyRects[i]);

        var shouldRenderDirty = mode == RenderMode.DirtyRegion ||
            dirtyRects.Count <= maxDirtyRectCount && dirtyRatio <= maxDirtyAreaRatio;

        if (shouldRenderDirty)
        {
            return new RenderDiagnostics(
                mode,
                false,
                "DirtyRegion",
                dirtyRects.Count,
                dirtyRatio,
                union);
        }

        return new RenderDiagnostics(
            mode,
            true,
            dirtyRects.Count > maxDirtyRectCount ? "TooManyDirtyRects" : "DirtyAreaTooLarge",
            dirtyRects.Count,
            dirtyRatio,
            union);
    }
}
