using Square.Extensions.Markdown;
using Square.Extensions.RichText;
using Square.UI;

namespace Square.Extensions;

public static class ExtensionRegistration
{
    private static bool _registered;

    public static void RegisterDefaults()
    {
        if (_registered) return;
        _registered = true;

        UIDocument.RegisterElement("MarkdownViewer", static () => new MarkdownViewer());
        UIDocument.RegisterElement("RichTextEditor", static () => new RichTextEditor());
    }
}
