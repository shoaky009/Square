using Square.Graphics;

namespace Square.Hosting;

/// <summary>渲染诊断信息。</summary>
public sealed record RenderDiagnostics(
    RenderMode Mode,
    bool UsedFullFrame,
    string Reason,
    int DirtyRectCount,
    float DirtyAreaRatio,
    Rect DirtyUnion);