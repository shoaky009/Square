namespace Square.Extensions.CodePad;

/// <summary>多光标中的一个插入点/选区（UTF-16 offset）。</summary>
public readonly record struct CodePadCursor(int Caret, int Anchor)
{
    /// <summary>折叠为单点光标。</summary>
    public static CodePadCursor Collapsed(int offset) => new(offset, offset);

    /// <summary>选区起点。</summary>
    public int SelectionStart => Math.Min(Caret, Anchor);

    /// <summary>选区长度。</summary>
    public int SelectionLength => Math.Abs(Caret - Anchor);

    /// <summary>是否无选区。</summary>
    public bool IsCollapsed => Caret == Anchor;

    /// <summary>钳制到文档长度。</summary>
    public CodePadCursor Clamp(int documentLength)
    {
        var len = Math.Max(0, documentLength);
        return new CodePadCursor(Math.Clamp(Caret, 0, len), Math.Clamp(Anchor, 0, len));
    }
}
