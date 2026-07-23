namespace Square.Extensions.Markdown;

public abstract record MarkdownBlock
{
    public abstract string PlainText { get; }
}

public sealed record MarkdownHeading : MarkdownBlock
{
    public MarkdownHeading(int level, IEnumerable<MarkdownInline>? inlines = null)
    {
        if (level is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        Inlines = inlines?.ToArray() ?? [];
    }

    public int Level { get; }
    public IReadOnlyList<MarkdownInline> Inlines { get; }
    public override string PlainText => string.Concat(Inlines.Select(inline => inline.PlainText));
}

public sealed record MarkdownParagraph : MarkdownBlock
{
    public MarkdownParagraph(IEnumerable<MarkdownInline>? inlines = null)
    {
        Inlines = inlines?.ToArray() ?? [];
    }

    public IReadOnlyList<MarkdownInline> Inlines { get; }
    public override string PlainText => string.Concat(Inlines.Select(inline => inline.PlainText));
}

public sealed record MarkdownList : MarkdownBlock
{
    public MarkdownList(bool isOrdered, int start, IEnumerable<MarkdownListItem>? items = null)
    {
        IsOrdered = isOrdered;
        Start = start;
        Items = items?.ToArray() ?? [];
    }

    public bool IsOrdered { get; }
    public int Start { get; }
    public IReadOnlyList<MarkdownListItem> Items { get; }
    public override string PlainText => string.Join("\n", Items.Select(item => item.PlainText));
}

public sealed record MarkdownListItem
{
    public MarkdownListItem(
        IEnumerable<MarkdownBlock>? blocks = null,
        bool isTask = false,
        bool isChecked = false)
    {
        Blocks = blocks?.ToArray() ?? [];
        IsTask = isTask;
        IsChecked = isChecked;
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; }
    public bool IsTask { get; }
    public bool IsChecked { get; }
    public string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));
}

public sealed record MarkdownQuote : MarkdownBlock
{
    public MarkdownQuote(IEnumerable<MarkdownBlock>? blocks = null)
    {
        Blocks = blocks?.ToArray() ?? [];
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; }
    public override string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));
}

public sealed record MarkdownCodeBlock(string Code, string? Language = null) : MarkdownBlock
{
    public override string PlainText => Code;
}

public sealed record MarkdownThematicBreak : MarkdownBlock
{
    public override string PlainText => "";
}

public enum MarkdownTableAlignment
{
    None,
    Left,
    Center,
    Right
}

public sealed record MarkdownTable : MarkdownBlock
{
    public MarkdownTable(
        IEnumerable<MarkdownTableAlignment>? alignments = null,
        IEnumerable<MarkdownTableRow>? rows = null)
    {
        Alignments = alignments?.ToArray() ?? [];
        Rows = rows?.ToArray() ?? [];
    }

    public IReadOnlyList<MarkdownTableAlignment> Alignments { get; }
    public IReadOnlyList<MarkdownTableRow> Rows { get; }
    public override string PlainText => string.Join("\n", Rows.Select(row => row.PlainText));
}

public sealed record MarkdownTableRow
{
    public MarkdownTableRow(bool isHeader, IEnumerable<MarkdownTableCell>? cells = null)
    {
        IsHeader = isHeader;
        Cells = cells?.ToArray() ?? [];
    }

    public bool IsHeader { get; }
    public IReadOnlyList<MarkdownTableCell> Cells { get; }
    public string PlainText => string.Join("\t", Cells.Select(cell => cell.PlainText));
}

public sealed record MarkdownTableCell
{
    public MarkdownTableCell(
        IEnumerable<MarkdownBlock>? blocks = null,
        int columnSpan = 1,
        int rowSpan = 1)
    {
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan));
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));
        Blocks = blocks?.ToArray() ?? [];
        ColumnSpan = columnSpan;
        RowSpan = rowSpan;
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; }
    public int ColumnSpan { get; }
    public int RowSpan { get; }
    public string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));
}

public sealed record MarkdownContainer : MarkdownBlock
{
    public MarkdownContainer(IEnumerable<MarkdownBlock>? blocks = null)
    {
        Blocks = blocks?.ToArray() ?? [];
    }

    public IReadOnlyList<MarkdownBlock> Blocks { get; }
    public override string PlainText => string.Join("\n", Blocks.Select(block => block.PlainText));
}
