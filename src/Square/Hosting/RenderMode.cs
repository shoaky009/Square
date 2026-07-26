namespace Square.Hosting;

/// <summary>渲染模式。</summary>
public enum RenderMode
{
    /// <summary>每帧全窗口清屏重绘。</summary>
    FullFrame,
    /// <summary>自动选择全帧或脏区。</summary>
    Auto,
    /// <summary>强制使用脏区局部重绘。</summary>
    DirtyRegion
}