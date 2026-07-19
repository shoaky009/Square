using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Square.UI;
using LinkControl = Square.Controls.Controls.Link;
using ListItemControl = Square.Controls.Controls.ListItem;
using TextControl = Square.Controls.Controls.Text;
using ViewControl = Square.Controls.Controls.View;

namespace Square.Extensions.Markdown;

internal static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static void Render(string? markdown, Element target)
    {
        target.Children.Clear();
        target.Style.Set("display", "flex");
        target.Style.Set("flex-direction", "column");
        target.Style.Set("gap", "8px");
        target.Style.Set("user-select", "text");
        if (string.IsNullOrWhiteSpace(markdown)) return;

        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        foreach (var block in document)
        {
            var element = CreateBlock(block);
            if (element != null) target.Children.Add(element);
        }
    }

    private static Element? CreateBlock(Block block) => block switch
    {
        HeadingBlock heading => CreateHeading(heading),
        ParagraphBlock paragraph => CreateParagraph(paragraph),
        ListBlock list => CreateList(list),
        QuoteBlock quote => CreateQuote(quote),
        FencedCodeBlock code => CreateCodeBlock(code),
        CodeBlock code => CreateCodeBlock(code),
        ThematicBreakBlock => CreateSeparator(),
        ContainerBlock container => CreateContainer(container),
        LeafBlock leaf => CreateText(GetLeafText(leaf), "markdown-paragraph"),
        _ => null
    };

    private static Element CreateHeading(HeadingBlock heading)
    {
        var text = CreateText(GetInlineText(heading.Inline), $"markdown-heading-{heading.Level}");
        text.FontSize = heading.Level switch
        {
            1 => 28f,
            2 => 24f,
            3 => 20f,
            4 => 18f,
            5 => 16f,
            _ => 14f
        };
        text.Style.Set("line-height", MathF.Round(text.FontSize * 1.25f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "px");
        return text;
    }

    private static Element CreateParagraph(ParagraphBlock paragraph)
    {
        var link = TryCreateSingleLink(paragraph.Inline);
        if (link != null) return link;
        return CreateText(GetInlineText(paragraph.Inline), "markdown-paragraph");
    }

    private static Element CreateList(ListBlock list)
    {
        var container = CreateView("markdown-list");
        container.Style.Set("display", "flex");
        container.Style.Set("flex-direction", "column");
        container.Style.Set("gap", "4px");
        var index = int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? index++ + ". " : "- ";
            var listItem = new ListItemControl
            {
                Marker = marker,
                TextContent = GetContainerText(item)
            };
            listItem.ClassList.Add("markdown-list-item");
            listItem.Style.Set("line-height", "22px");
            container.Children.Add(listItem);
        }

        return container;
    }

    private static Element CreateQuote(QuoteBlock quote)
    {
        var view = CreateView("markdown-quote");
        view.Style.Set("display", "flex");
        view.Style.Set("flex-direction", "column");
        view.Style.Set("gap", "6px");
        view.Style.Set("background", "#f3f4f6");
        view.Style.Set("padding", "8px 10px");
        foreach (var child in quote)
        {
            var element = CreateBlock(child);
            if (element != null) view.Children.Add(element);
        }
        return view;
    }

    private static Element CreateCodeBlock(LeafBlock code)
    {
        var text = GetLeafLinesText(code);
        return CreateText(text.TrimEnd(), "markdown-code");
    }

    private static Element CreateSeparator()
    {
        var separator = CreateView("markdown-separator");
        separator.Style.Set("height", "1px");
        separator.Style.Set("background", "#e5e7eb");
        return separator;
    }

    private static Element CreateContainer(ContainerBlock container)
    {
        var view = CreateView("markdown-container");
        foreach (var child in container)
        {
            var element = CreateBlock(child);
            if (element != null) view.Children.Add(element);
        }
        return view;
    }

    private static LinkControl? TryCreateSingleLink(ContainerInline? inline)
    {
        if (inline == null || inline.Count() != 1 || inline.FirstChild is not LinkInline linkInline)
            return null;

        var link = new LinkControl(GetInlineText(linkInline), linkInline.Url ?? "");
        link.ClassList.Add("markdown-link");
        return link;
    }

    private static TextControl CreateText(string text, string className)
    {
        var element = new TextControl(text);
        element.ClassList.Add(className);
        if (className == "markdown-paragraph") element.Style.Set("line-height", "22px");
        if (className == "markdown-code")
        {
            element.Style.Set("background", "#111827");
            element.Style.Set("color", "#f9fafb");
            element.Style.Set("padding", "10px");
            element.Style.Set("line-height", "20px");
        }
        return element;
    }

    private static ViewControl CreateView(string className)
    {
        var view = new ViewControl();
        view.ClassList.Add(className);
        return view;
    }

    private static string GetContainerText(ContainerBlock container) =>
        string.Join(" ", container.Select(GetBlockText).Where(text => !string.IsNullOrWhiteSpace(text)));

    private static string GetBlockText(Block block) => block switch
    {
        ParagraphBlock paragraph => GetInlineText(paragraph.Inline),
        HeadingBlock heading => GetInlineText(heading.Inline),
        LeafBlock leaf => GetLeafText(leaf),
        ContainerBlock container => GetContainerText(container),
        _ => ""
    };

    private static string GetLeafText(LeafBlock leaf)
    {
        if (leaf.Inline != null)
            return GetInlineText(leaf.Inline);
        return GetLeafLinesText(leaf);
    }

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

    private static string GetInlineText(ContainerInline? inline)
    {
        if (inline == null) return "";

        var parts = new List<string>();
        foreach (var child in inline)
            AppendInlineText(child, parts);
        return string.Concat(parts);
    }

    private static void AppendInlineText(Inline inline, List<string> parts)
    {
        switch (inline)
        {
            case LiteralInline literal:
                parts.Add(literal.Content.ToString());
                break;
            case LineBreakInline:
                parts.Add("\n");
                break;
            case CodeInline code:
                parts.Add(code.Content);
                break;
            case LinkInline link:
                parts.Add(GetInlineText(link));
                break;
            case ContainerInline container:
                parts.Add(GetInlineText(container));
                break;
        }
    }
}
