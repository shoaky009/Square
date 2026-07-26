namespace Square.Graphics;

/// <summary>可选择渲染后端的应用程序接口。</summary>
public interface IRenderBackendApplication
{
    /// <summary>渲染后端名称。</summary>
    string RenderBackend { get; set; }
}