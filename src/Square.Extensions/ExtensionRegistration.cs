using Square.Extensions.Markdown;
using Square.Extensions.RichText;
using Square.Platform;
using Square.UI;

namespace Square.Extensions;

public static class ExtensionRegistration
{
    private static bool _registered;

    public static void RegisterDefaults()
    {
        if (_registered) return;
        _registered = true;

        ElementRegistry.Register("MarkdownViewer", static () => new MarkdownViewer());
        ElementRegistry.Register("RichTextEditor", static () => new RichTextEditor());
        FilePickerProvider.Current = new NativeFilePickerProvider();
    }
}
