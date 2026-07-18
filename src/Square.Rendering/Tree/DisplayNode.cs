using Square.Graphics;
using Square.UI;
using System.Numerics;
using Square.Rendering.Commands;

namespace Square.Rendering.Tree;

public sealed class DisplayNode
{
    public Rect Bounds { get; set; }
    /// <summary>Source document element for this display node.</summary>
    public Element? Source
    {
        get => Element;
        set => Element = value;
    }

    public Element? Element { get; set; }
    public List<DisplayNode> Children { get; } = [];
    public List<DrawCommand> Commands { get; } = [];

    public bool IsDirty { get; set; } = true;

    public void Render(IRenderContext ctx) => Render(ctx, dirtyClip: null);

    /// <summary>
    /// 渲染本节点及子树。<paramref name="dirtyClip"/> 非 null 时跳过与脏区不相交的分支。
    /// </summary>
    public void Render(IRenderContext ctx, Rect? dirtyClip)
    {
        // 使用最新 Geometry 作为 Bounds（局部 Present 依赖）
        if (Element != null)
            Bounds = Element.Geometry;

        // 根或 Geometry 仍为空时：不要因不相交测试而丢弃整枝（layout 后首帧常见）
        // Bounds.IsEmpty 时仍继续遍历子节点
        if (dirtyClip is { } clip && !Bounds.IsEmpty && !Bounds.IntersectsWith(clip))
            return;

        if (IsDirty || Commands.Count == 0)
        {
            Commands.Clear();
            CollectCommands(Element, Commands);
            SortChildrenByZIndex();
            // 帧循环：Paint 内 RequestAnimationFrame 会再 InvalidatePaint；
            // Host Tick 在到期时也会 InvalidatePaint。此处清除本轮脏标记即可。
            Element?.ClearPaintDirty();
            IsDirty = false;
        }

        // 仅当本节点与脏区相交（或全帧）时执行自身命令
        var executeSelf = dirtyClip is null
            || Bounds.IsEmpty
            || Bounds.IntersectsWith(dirtyClip.Value);
        if (executeSelf)
            ExecuteCommands(ctx);

        var overflowClip = Element?.GetOverflowClipRect() ?? Rect.Empty;
        var clipsChildren = !overflowClip.IsEmpty;
        if (clipsChildren) ctx.PushClip(overflowClip);
        foreach (var child in Children)
            child.Render(ctx, dirtyClip);
        if (clipsChildren) ctx.PopClip();
    }

    private static void CollectCommands(Element? element, List<DrawCommand> commands)
    {
        if (element == null || !element.IsVisible) return;
        element.Paint(new CommandCollector(commands));
    }

    private void SortChildrenByZIndex()
    {
        if (Children.Count < 2) return;
        Children.Sort(static (left, right) =>
            (left.Element?.ZIndex ?? 0).CompareTo(right.Element?.ZIndex ?? 0));
    }

    private void ExecuteCommands(IRenderContext ctx)
    {
        foreach (var cmd in Commands)
        {
            ExecuteCommand(ctx, cmd);
        }
    }

    private static void ExecuteCommand(IRenderContext ctx, DrawCommand cmd)
    {
        switch (cmd)
        {
            case ClearCommand c: ctx.Clear(c.Color); break;
            case FillRectCommand f: ctx.FillRect(f.Rect, f.Brush); break;
            case DrawRectCommand d: ctx.DrawRect(d.Rect, d.Pen); break;
            case FillPathCommand f: ctx.FillPath(f.Path, f.Brush); break;
            case DrawPathCommand d: ctx.DrawPath(d.Path, d.Pen); break;
            case FillGeometryCommand f: ctx.FillGeometry(f.Geometry, f.Brush); break;
            case DrawGeometryCommand d: ctx.DrawGeometry(d.Geometry, d.Pen); break;
            case DrawTextCommand t: ctx.DrawText(t.Text, t.Origin, t.Brush); break;
            case DrawImageCommand i: ctx.DrawImage(i.Image, i.Dest, i.Source); break;
            case PushClipCommand p: ctx.PushClip(p.Rect); break;
            case PushGeometryClipCommand p: ctx.PushClip(p.Geometry); break;
            case PopClipCommand: ctx.PopClip(); break;
            case PushTransformCommand pt: ctx.PushTransform(pt.Matrix); break;
            case PopTransformCommand: ctx.PopTransform(); break;
            case PushLayerCommand p: ctx.PushLayer(p.Bounds, p.Opacity); break;
            case PopLayerCommand: ctx.PopLayer(); break;
        }
    }
}

internal sealed class CommandCollector : IRenderContext
{
    private readonly List<DrawCommand> _commands;
    private Size _canvasSize = new(1920, 1080);

    public CommandCollector(List<DrawCommand> commands) { _commands = commands; }

    public Size CanvasSize => _canvasSize;
    public float DpiScale => 1f;

    public void PushTransform(Matrix3x2 matrix) => _commands.Add(new PushTransformCommand(matrix));
    public void PopTransform() => _commands.Add(new PopTransformCommand());
    public void PushClip(Rect rect) => _commands.Add(new PushClipCommand(rect));
    public void PushClip(Geometry geometry) => _commands.Add(new PushGeometryClipCommand(geometry));
    public void PopClip() => _commands.Add(new PopClipCommand());
    public void FillRect(Rect rect, Brush brush) => _commands.Add(new FillRectCommand(rect, brush));
    public void DrawRect(Rect rect, Pen pen) => _commands.Add(new DrawRectCommand(rect, pen));
    public void FillPath(PathGeometry path, Brush brush) => _commands.Add(new FillPathCommand(path, brush));
    public void DrawPath(PathGeometry path, Pen pen) => _commands.Add(new DrawPathCommand(path, pen));
    public void FillGeometry(Geometry geometry, Brush brush) => _commands.Add(new FillGeometryCommand(geometry, brush));
    public void DrawGeometry(Geometry geometry, Pen pen) => _commands.Add(new DrawGeometryCommand(geometry, pen));
    public void DrawText(TextLayout text, Point origin, Brush brush) => _commands.Add(new DrawTextCommand(text, origin, brush));
    public void DrawImage(Image image, Rect dest, Rect? source = null) => _commands.Add(new DrawImageCommand(image, dest, source));
    public void PushLayer(Rect bounds, float opacity) => _commands.Add(new PushLayerCommand(bounds, opacity));
    public void PopLayer() => _commands.Add(new PopLayerCommand());
    public void Clear(Color color) => _commands.Add(new ClearCommand(color));
    public void Clear(Color color, Rect rect) => _commands.Add(new FillRectCommand(rect, new SolidColorBrush(color)));
    public void Flush() { }
    public void Present() { }
    public void Present(IReadOnlyList<Rect>? dirtyRects) { }
    public void Dispose() { }
}
