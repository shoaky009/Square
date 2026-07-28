using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SquareMarkdownDocument = Square.Extensions.Markdown.MarkdownDocument;

namespace Square.Extensions.Markdown;

internal static class MarkdigMarkdownParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static SquareMarkdownDocument Parse(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return SquareMarkdownDocument.Empty;

        var document = Markdig.Markdown.Parse(source, Pipeline);
        return new SquareMarkdownDocument(document.Select(ConvertBlock).WhereNotNull());
    }

    private static MarkdownBlock? ConvertBlock(Block block) => block switch
    {
        HeadingBlock heading => new MarkdownHeading(heading.Level, ConvertInlines(heading.Inline)),
        ParagraphBlock paragraph => new MarkdownParagraph(ConvertInlines(paragraph.Inline)),
        ListBlock list => ConvertList(list),
        QuoteBlock quote => new MarkdownQuote(quote.Select(ConvertBlock).WhereNotNull()),
        Table table => ConvertTable(table),
        FencedCodeBlock code => new MarkdownCodeBlock(GetLeafLinesText(code).TrimEnd(), GetLanguage(code)),
        CodeBlock code => new MarkdownCodeBlock(GetLeafLinesText(code).TrimEnd()),
        ThematicBreakBlock => new MarkdownThematicBreak(),
        ContainerBlock container => new MarkdownContainer(container.Select(ConvertBlock).WhereNotNull()),
        LeafBlock leaf => new MarkdownParagraph([new MarkdownText(GetLeafText(leaf))]),
        _ => null
    };

    private static MarkdownList ConvertList(ListBlock list)
    {
        var start = int.TryParse(list.OrderedStart, out var value) ? value : 1;
        var items = list.OfType<ListItemBlock>().Select(item =>
        {
            var task = item.Descendants<TaskList>().FirstOrDefault();
            var blocks = item.Select(ConvertBlock).WhereNotNull().ToArray();
            if (task != null) blocks = TrimTaskPrefix(blocks);
            return new MarkdownListItem(
                blocks,
                task != null,
                task?.Checked ?? false);
        });
        return new MarkdownList(list.IsOrdered, start, items);
    }

    private static MarkdownBlock[] TrimTaskPrefix(MarkdownBlock[] blocks)
    {
        if (blocks.FirstOrDefault() is not MarkdownParagraph paragraph || paragraph.Inlines.Count == 0)
            return blocks;
        if (paragraph.Inlines[0] is not MarkdownText { Text.Length: > 0 } text || !char.IsWhiteSpace(text.Text[0]))
            return blocks;

        var inlines = paragraph.Inlines.ToArray();
        inlines[0] = new MarkdownText(text.Text[1..]);
        blocks[0] = new MarkdownParagraph(inlines);
        return blocks;
    }

    private static MarkdownTable ConvertTable(Table table)
    {
        var alignments = table.ColumnDefinitions.Select(column => column.Alignment switch
        {
            TableColumnAlign.Left => MarkdownTableAlignment.Left,
            TableColumnAlign.Center => MarkdownTableAlignment.Center,
            TableColumnAlign.Right => MarkdownTableAlignment.Right,
            _ => MarkdownTableAlignment.None
        });
        var rows = table.OfType<TableRow>().Select(row => new MarkdownTableRow(
            row.IsHeader,
            row.OfType<TableCell>().Select(cell => new MarkdownTableCell(
                cell.Select(ConvertBlock).WhereNotNull(),
                cell.ColumnSpan,
                cell.RowSpan))));
        return new MarkdownTable(alignments, rows);
    }

    private static IReadOnlyList<MarkdownInline> ConvertInlines(ContainerInline? container)
    {
        if (container == null) return [];

        var inlines = new List<MarkdownInline>();
        foreach (var inline in container)
        {
            var converted = ConvertInline(inline);
            if (converted != null) inlines.Add(converted);
        }
        return inlines;
    }

    private static MarkdownInline? ConvertInline(Inline inline) => inline switch
    {
        LiteralInline literal => new MarkdownText(literal.Content.ToString()),
        LineBreakInline lineBreak => new MarkdownLineBreak(lineBreak.IsHard),
        CodeInline code => new MarkdownCode(code.Content),
        LinkInline { IsImage: false } link => new MarkdownLink(link.Url ?? "", link.Title, ConvertInlines(link)),
        LinkInline { IsImage: true } image => new MarkdownImage(
            image.Url ?? "",
            string.Concat(ConvertInlines(image).Select(item => item.PlainText)),
            image.Title),
        TaskList => null,
        EmphasisInline emphasis => new MarkdownEmphasis(GetEmphasisKind(emphasis), ConvertInlines(emphasis)),
        ContainerInline nested => new MarkdownText(ConvertInlines(nested).Aggregate("", (text, item) => text + item.PlainText)),
        _ => null
    };

    private static MarkdownEmphasisKind GetEmphasisKind(EmphasisInline emphasis)
    {
        if (emphasis.DelimiterChar == '~') return MarkdownEmphasisKind.Strikethrough;
        return emphasis.DelimiterCount >= 2 ? MarkdownEmphasisKind.Bold : MarkdownEmphasisKind.Italic;
    }

    private static string? GetLanguage(FencedCodeBlock code)
    {
        var info = code.Info?.ToString().Trim() ?? "";
        if (info.Length == 0) return null;
        var separator = info.IndexOfAny([' ', '\t']);
        return separator < 0 ? info : info[..separator];
    }

    private static string GetLeafText(LeafBlock leaf) =>
        leaf.Inline == null ? GetLeafLinesText(leaf) : string.Concat(ConvertInlines(leaf.Inline).Select(inline => inline.PlainText));

    private static string GetLeafLinesText(LeafBlock leaf)
    {
        var lines = leaf.Lines.Lines;
        return lines is { Length: > 0 }
            ? string.Join("\n", lines.Select(line => GetSliceText(line.Slice)))
            : "";
    }

    private static string GetSliceText(StringSlice slice)
    {
        if (string.IsNullOrEmpty(slice.Text)) return "";
        var start = Math.Clamp(slice.Start, 0, slice.Text.Length);
        var end = Math.Clamp(slice.End, start - 1, slice.Text.Length - 1);
        return end < start ? "" : slice.Text.Substring(start, end - start + 1);
    }

    private static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values) where T : class =>
        values.Where(value => value != null)!;
}
