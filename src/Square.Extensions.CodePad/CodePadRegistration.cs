using Square.UI;

namespace Square.Extensions.CodePad;

/// <summary>
/// 注册 <see cref="CodePad"/> 控件标签与内置语言/主题。
/// 与 <c>Square.Extensions.ExtensionRegistration</c> 独立；引用本程序集后需显式调用。
/// </summary>
public static class CodePadRegistration
{
    private static bool _registered;

    /// <summary>
    /// 幂等注册：<c>CodePad</c> 元素标签，以及内置语言/主题贡献。
    /// </summary>
    public static void RegisterDefaults()
    {
        // Always ensure language/theme built-ins (idempotent).
        LanguageRegistry.EnsureBuiltIns();
        CodePadThemeRegistry.EnsureBuiltIns();

        if (_registered) return;
        _registered = true;
        ElementRegistry.Register("CodePad", static () => new CodePad());
    }
}
