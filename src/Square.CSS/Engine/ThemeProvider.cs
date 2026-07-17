using Square.UI;

namespace Square.CSS.Engine;

public sealed class ThemeProvider
{
    private readonly CssEngine _engine;
    private readonly Visual _root;

    public ThemeProvider(CssEngine engine, Visual root)
    {
        _engine = engine;
        _root = root;
    }

    public string? ActiveTheme { get; private set; }

    public void ApplyTheme(string? name)
    {
        ActiveTheme = name;
        _engine.SetTheme(name);
        ClearComputedStyles(_root);
        _engine.ApplyStylesToTree(_root);
    }

    private static void ClearComputedStyles(Visual visual)
    {
        visual.Style.ClearCascaded();
        foreach (var child in visual.Children)
            ClearComputedStyles(child);
    }
}
