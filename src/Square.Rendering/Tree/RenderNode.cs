using Square.Graphics;
using Square.UI;
using System.Numerics;
using Square.Rendering.Commands;

namespace Square.Rendering.Tree;

public sealed class RenderNode
{
    public Rect Bounds { get; set; }
    public Visual? Visual { get; set; }
    public List<RenderNode> Children { get; } = [];
    public List<DrawCommand> Commands { get; } = [];

    public bool IsDirty { get; set; } = true;

    public void Render(IRenderContext ctx)
    {
        if (IsDirty || Commands.Count == 0)
        {
            Commands.Clear();
            CollectCommands(Visual, Commands);
            SortChildrenByZIndex();
            Visual?.ClearVisualDirty();
            IsDirty = false;
        }

        ExecuteCommands(ctx);

        var clipRect = Visual?.GetOverflowClipRect() ?? Rect.Empty;
        var clipsChildren = !clipRect.IsEmpty;
        if (clipsChildren) ctx.PushClip(clipRect);
        foreach (var child in Children)
            child.Render(ctx);
        if (clipsChildren) ctx.PopClip();
    }

    private static void CollectCommands(Visual? visual, List<DrawCommand> commands)
    {
        if (visual == null || !visual.IsVisible) return;
        visual.Render(new CommandCollector(commands));
    }

    private void SortChildrenByZIndex()
    {
        if (Children.Count < 2) return;
        Children.Sort(static (left, right) =>
            (left.Visual?.ZIndex ?? 0).CompareTo(right.Visual?.ZIndex ?? 0));
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
    public void Flush() { }
    public void Present() { }
    public void Dispose() { }
}
