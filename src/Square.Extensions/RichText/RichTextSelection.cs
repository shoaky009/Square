namespace Square.Extensions.RichText;

public readonly record struct RichTextPosition(int BlockIndex, int Offset) : IComparable<RichTextPosition>
{
    public int CompareTo(RichTextPosition other)
    {
        var block = BlockIndex.CompareTo(other.BlockIndex);
        return block != 0 ? block : Offset.CompareTo(other.Offset);
    }
}

public readonly record struct RichTextSelection(RichTextPosition Anchor, RichTextPosition Focus)
{
    public bool IsCollapsed => Anchor == Focus;
    public RichTextPosition Start => Anchor.CompareTo(Focus) <= 0 ? Anchor : Focus;
    public RichTextPosition End => Anchor.CompareTo(Focus) <= 0 ? Focus : Anchor;

    public static RichTextSelection Collapsed(RichTextPosition position) => new(position, position);
}