using Square.Graphics;

namespace Square.Hosting;

public sealed record RenderDiagnostics(
    RenderMode Mode,
    bool UsedFullFrame,
    string Reason,
    int DirtyRectCount,
    float DirtyAreaRatio,
    Rect DirtyUnion);
