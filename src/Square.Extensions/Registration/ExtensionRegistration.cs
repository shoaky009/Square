using Square.Extensions.Markdown;
using Square.UI;

namespace Square.Extensions.Registration;

public static class ExtensionRegistration
{
    private static bool _registered;

    public static void RegisterDefaults()
    {
        if (_registered) return;
        _registered = true;

        UIDocument.RegisterElement("MarkdownViewer", static () => new MarkdownViewer());
    }
}
