using Square.UI;
using Square.Graphics;
using Square.Graphics.Svg;
using CheckBoxControl = Square.Controls.CheckBox;
using ImageControl = Square.Controls.Image;
using LinkControl = Square.Controls.Link;
using TextControl = Square.Controls.Text;
using ViewControl = Square.Controls.View;

namespace Square.Extensions.Markdown;

internal static class MarkdownRenderer
{
    public static void Render(MarkdownDocument document, Element target)
    {
        target.ChildNodes.Clear();
        AppendBlocks(target, document.Blocks);
    }

    private static Element CreateBlock(MarkdownBlock block) => block switch
    {
        MarkdownHeading heading => CreateInlineBlock(heading.Inlines, $"markdown-heading-{heading.Level}"),
        MarkdownParagraph paragraph => CreateInlineBlock(paragraph.Inlines, "markdown-paragraph"),
        MarkdownList list => CreateList(list),
        MarkdownQuote quote => CreateBlockContainer(quote.Blocks, "markdown-quote"),
        MarkdownCodeBlock code => CreateCodeBlock(code),
        MarkdownThematicBreak => CreateView("markdown-separator"),
        MarkdownTable table => CreateTable(table),
        MarkdownContainer container => CreateBlockContainer(container.Blocks, "markdown-container"),
        _ => CreateText(block.PlainText, "markdown-paragraph")
    };

    private static Element CreateInlineBlock(IEnumerable<MarkdownInline> inlines, string className)
    {
        var container = CreateView(className);
        container.ClassList.Add("markdown-inline-block");
        foreach (var inline in inlines)
            AppendInline(container, inline, className, null);
        return container;
    }

    private static Element CreateList(MarkdownList list)
    {
        var container = CreateView("markdown-list");
        var index = list.Start;
        foreach (var item in list.Items)
        {
            var row = CreateView("markdown-list-item");
            row.Children.Add(item.IsTask
                ? CreateTaskMarker(item)
                : CreateText(list.IsOrdered ? $"{index++}." : "-", "markdown-list-marker"));
            if (list.IsOrdered && item.IsTask) index++;
            row.Children.Add(CreateBlockContainer(item.Blocks, "markdown-list-content"));
            container.Children.Add(row);
        }
        return container;
    }

    private static Element CreateTaskMarker(MarkdownListItem item)
    {
        var marker = new CheckBoxControl
        {
            IsChecked = item.IsChecked,
            IsEnabled = false
        };
        marker.ClassList.Add("markdown-task-marker");
        return marker;
    }

    private static Element CreateCodeBlock(MarkdownCodeBlock code)
    {
        var container = CreateView("markdown-code");
        var text = new MarkdownCodeText
        {
            TextContent = code.Code,
            Language = code.Language,
        };
        text.ClassList.Add("markdown-code-text");
        if (!string.IsNullOrWhiteSpace(code.Language))
            text.ClassList.Add($"language-{code.Language}");
        container.Children.Add(text);
        return container;
    }

    private static Element CreateTable(MarkdownTable table)
    {
        var container = CreateView("markdown-table");
        var columnCount = Math.Max(
            table.Alignments.Count,
            table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Cells.Sum(cell => cell.ColumnSpan)));

        foreach (var row in table.Rows)
        {
            var rowElement = CreateView("markdown-table-row");
            var column = 0;
            foreach (var cell in row.Cells)
            {
                var element = CreateBlockContainer(
                    cell.Blocks,
                    "markdown-table-cell");
                if (row.IsHeader) element.ClassList.Add("markdown-table-header");
                if (columnCount > 0)
                {
                    var width = cell.ColumnSpan * 100f / columnCount;
                    element.Style.Set("flex-basis", width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "%");
                    element.Style.Set("max-width", width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "%");
                }

                var alignment = column < table.Alignments.Count
                    ? table.Alignments[column]
                    : MarkdownTableAlignment.None;
                element.ClassList.Add(alignment switch
                {
                    MarkdownTableAlignment.Left => "markdown-align-left",
                    MarkdownTableAlignment.Center => "markdown-align-center",
                    MarkdownTableAlignment.Right => "markdown-align-right",
                    _ => "markdown-align-default"
                });
                rowElement.Children.Add(element);
                column += cell.ColumnSpan;
            }
            container.Children.Add(rowElement);
        }
        return container;
    }

    private static Element CreateBlockContainer(IEnumerable<MarkdownBlock> blocks, string className)
    {
        var container = CreateView(className);
        AppendBlocks(container, blocks);
        return container;
    }

    private static void AppendBlocks(Element container, IEnumerable<MarkdownBlock> blocks)
    {
        var list = blocks as IReadOnlyList<MarkdownBlock> ?? blocks.ToArray();
        for (var i = 0; i < list.Count; i++)
        {
            container.Children.Add(CreateBlock(list[i]));
            if (i < list.Count - 1)
                container.ChildNodes.Add(new global::Square.UI.Text("\n"));
        }
    }

    private static void AppendInline(
        Element container,
        MarkdownInline inline,
        string blockClass,
        string? inheritedClass)
    {
        switch (inline)
        {
            case MarkdownText text:
                container.Children.Add(CreateInlineText(text.Text, blockClass, inheritedClass));
                break;
            case MarkdownCode code:
                var codeContainer = CreateView("markdown-inline-code");
                codeContainer.Children.Add(CreateInlineText(
                    code.Code,
                    blockClass,
                    "markdown-inline-code-text",
                    inheritedClass));
                container.Children.Add(codeContainer);
                break;
            case MarkdownLink link:
                var element = new LinkControl(link.PlainText, link.Destination);
                AddClasses(element, blockClass, "markdown-link", inheritedClass);
                container.Children.Add(element);
                break;
            case MarkdownImage image:
                var imageElement = CreateImage(image);
                imageElement.ClassList.Add("markdown-image");
                container.Children.Add(imageElement);
                break;
            case MarkdownEmphasis emphasis:
                var emphasisClass = emphasis.Kind switch
                {
                    MarkdownEmphasisKind.Bold => "markdown-bold",
                    MarkdownEmphasisKind.Italic => "markdown-italic",
                    MarkdownEmphasisKind.Strikethrough => "markdown-strikethrough",
                    _ => null
                };
                foreach (var child in emphasis.Inlines)
                    AppendInline(container, child, blockClass, CombineClasses(inheritedClass, emphasisClass));
                break;
            case MarkdownLineBreak:
                container.Children.Add(CreateInlineText("\n", blockClass, inheritedClass));
                break;
        }
    }

    private static ImageControl CreateImage(MarkdownImage image)
    {
        if (TryParseSvgDataUri(image.Source, out var svg))
            return new ImageControl { ImageContent = SvgImage.Parse(svg) };
        return new ImageControl { Source = image.Source };
    }

    private static bool TryParseSvgDataUri(string source, out string svg)
    {
        const string prefix = "data:image/svg+xml";
        svg = "";
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var comma = source.IndexOf(',');
        if (comma < 0) return false;
        try
        {
            var metadata = source[prefix.Length..comma];
            svg = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(source[(comma + 1)..]))
                : Uri.UnescapeDataString(source[(comma + 1)..]);
            return svg.Length > 0;
        }
        catch (FormatException)
        {
            svg = "";
            return false;
        }
    }

    private static TextControl CreateInlineText(string text, params string?[] classNames)
    {
        var element = new TextControl(text);
        AddClasses(element, classNames);
        return element;
    }

    private static TextControl CreateText(string text, string className)
    {
        var element = new TextControl(text);
        element.ClassList.Add(className);
        return element;
    }

    private static ViewControl CreateView(string className)
    {
        var view = new ViewControl();
        view.ClassList.Add(className);
        return view;
    }

    private static void AddClasses(Element element, params string?[] classNames)
    {
        foreach (var className in classNames)
        {
            if (string.IsNullOrWhiteSpace(className)) continue;
            foreach (var value in className.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                element.ClassList.Add(value);
        }
    }

    private static string? CombineClasses(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first)) return second;
        if (string.IsNullOrWhiteSpace(second)) return first;
        return $"{first} {second}";
    }
}
