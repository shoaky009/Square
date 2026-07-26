using Square.UI;

namespace Square.CSS.Engine;

/// <summary>提供主题切换能力，负责应用主题并重算样式。</summary>
public sealed class ThemeProvider
{
    private readonly CssEngine _engine;
    private readonly Element _root;

    /// <summary>初始化 ThemeProvider 的新实例。</summary>
    /// <param name="engine">关联的 CSS 引擎。</param>
    /// <param name="root">受主题管理的根元素。</param>
    public ThemeProvider(CssEngine engine, Element root)
    {
        _engine = engine;
        _root = root;
    }

    /// <summary>获取当前激活的主题名称。</summary>
    public string? ActiveTheme { get; private set; }

    /// <summary>应用指定主题并重算样式树。</summary>
    /// <param name="name">主题名称，为 null 表示取消主题。</param>
    public void ApplyTheme(string? name)
    {
        ActiveTheme = name;
        _engine.SetTheme(name);
        ClearComputedStyles(_root);
        _engine.ApplyStylesToTree(_root);
    }

    private static void ClearComputedStyles(Element Element)
    {
        Element.Style.ClearCascaded();
        foreach (var child in Element.Children)
            ClearComputedStyles(child);
    }
}