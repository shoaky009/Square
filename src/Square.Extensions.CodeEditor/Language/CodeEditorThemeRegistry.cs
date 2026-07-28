using Square.Graphics;

namespace Square.Extensions.CodeEditor;

/// <summary>编辑器主题注册表。</summary>
public static class CodeEditorThemeRegistry
{
    private static readonly object Gate = new();

    private static readonly Dictionary<string, CodeEditorTheme> Themes =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _builtIns;

    /// <summary>注册主题。</summary>
    public static void Register(string id, CodeEditorTheme theme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(theme);
        lock (Gate)
            Themes[id.Trim()] = theme;
    }

    /// <summary>获取主题。</summary>
    public static CodeEditorTheme Get(string? id)
    {
        EnsureBuiltIns();
        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(id) && Themes.TryGetValue(id.Trim(), out var theme))
                return theme;
            if (Themes.TryGetValue("default-light", out var light))
                return light;
            if (Themes.TryGetValue("default-dark", out var dark))
                return dark;
            return CreateLight();
        }
    }

    /// <summary>确保内置主题。</summary>
    public static void EnsureBuiltIns()
    {
        lock (Gate)
        {
            if (_builtIns && Themes.ContainsKey("default-light") && Themes.ContainsKey("default-dark"))
                return;

            Themes["default-light"] = CreateLight();
            Themes["default-dark"] = CreateDark();
            _builtIns = true;
        }
    }

    private static CodeEditorTheme CreateLight() => new()
    {
        EditorBackground = Color.FromRgb(246, 248, 250),
        EditorForeground = Color.FromRgb(36, 41, 47),
        EditorLineNumberForeground = Color.FromRgb(110, 119, 129),
        EditorLineNumberActiveForeground = Color.FromRgb(36, 41, 47),
        EditorSelectionBackground = Color.FromRgba(51, 144, 255, 80),
        EditorCursorForeground = Color.FromRgb(36, 41, 47),
        EditorCurrentLineBackground = Color.FromRgb(234, 238, 242),
        EditorGutterBackground = Color.FromRgb(240, 242, 245),
        ScrollBarTrack = Color.FromRgba(220, 224, 230, 220),
        ScrollBarThumb = Color.FromRgba(140, 148, 160, 180),
        ScrollBarThumbActive = Color.FromRgba(90, 100, 115, 220),
        BracketMatchBackground = Color.FromRgba(180, 210, 255, 160),
        BracketMatchBorder = Color.FromRgb(0, 120, 215),
        FindMatchBackground = Color.FromRgba(255, 213, 0, 90),
        FindMatchCurrentBackground = Color.FromRgba(255, 170, 0, 160),
        OverviewRulerBackground = Color.FromRgb(240, 242, 245),
        OverviewRulerBorder = Color.FromRgba(140, 148, 160, 80),
        TokenColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["comment"] = Color.FromRgb(106, 153, 85),
            ["string"] = Color.FromRgb(163, 21, 21),
            ["string.escape"] = Color.FromRgb(163, 21, 21),
            ["keyword"] = Color.FromRgb(0, 0, 255),
            ["number"] = Color.FromRgb(9, 134, 88),
            ["identifier"] = Color.FromRgb(0, 16, 128),
            ["key"] = Color.FromRgb(4, 81, 165),
            ["type"] = Color.FromRgb(38, 127, 153),
            ["function"] = Color.FromRgb(121, 94, 38),
            ["constant"] = Color.FromRgb(9, 134, 88),
            ["invalid"] = Color.FromRgb(205, 49, 49),
            ["operator"] = Color.FromRgb(0, 0, 0),
            ["delimiter"] = Color.FromRgb(0, 0, 0),
            ["tag"] = Color.FromRgb(128, 0, 0),
            ["attribute.name"] = Color.FromRgb(255, 0, 0),
            ["attribute.value"] = Color.FromRgb(0, 0, 255),
            ["variable"] = Color.FromRgb(0, 16, 128),
            ["strong"] = Color.FromRgb(0, 0, 0),
            ["emphasis"] = Color.FromRgb(0, 0, 0),
        },
    };

    private static CodeEditorTheme CreateDark() => new()
    {
        EditorBackground = Color.FromRgb(30, 30, 30),
        EditorForeground = Color.FromRgb(212, 212, 212),
        EditorLineNumberForeground = Color.FromRgb(133, 133, 133),
        EditorLineNumberActiveForeground = Color.FromRgb(200, 200, 200),
        EditorSelectionBackground = Color.FromRgba(38, 79, 120, 160),
        EditorCursorForeground = Color.FromRgb(212, 212, 212),
        EditorCurrentLineBackground = Color.FromRgb(42, 42, 42),
        EditorGutterBackground = Color.FromRgb(37, 37, 38),
        ScrollBarTrack = Color.FromRgba(45, 45, 48, 220),
        ScrollBarThumb = Color.FromRgba(120, 120, 120, 170),
        ScrollBarThumbActive = Color.FromRgba(170, 170, 170, 220),
        BracketMatchBackground = Color.FromRgba(70, 100, 140, 160),
        BracketMatchBorder = Color.FromRgb(136, 192, 255),
        FindMatchBackground = Color.FromRgba(90, 80, 20, 140),
        FindMatchCurrentBackground = Color.FromRgba(180, 120, 20, 180),
        OverviewRulerBackground = Color.FromRgb(37, 37, 38),
        OverviewRulerBorder = Color.FromRgba(120, 120, 120, 80),
        TokenColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["comment"] = Color.FromRgb(106, 153, 85),
            ["string"] = Color.FromRgb(206, 145, 120),
            ["string.escape"] = Color.FromRgb(206, 145, 120),
            ["keyword"] = Color.FromRgb(86, 156, 214),
            ["number"] = Color.FromRgb(181, 206, 168),
            ["identifier"] = Color.FromRgb(156, 220, 254),
            ["key"] = Color.FromRgb(156, 220, 254),
            ["type"] = Color.FromRgb(78, 201, 176),
            ["function"] = Color.FromRgb(220, 220, 170),
            ["constant"] = Color.FromRgb(181, 206, 168),
            ["invalid"] = Color.FromRgb(244, 71, 71),
            ["operator"] = Color.FromRgb(212, 212, 212),
            ["delimiter"] = Color.FromRgb(212, 212, 212),
            ["tag"] = Color.FromRgb(86, 156, 214),
            ["attribute.name"] = Color.FromRgb(156, 220, 254),
            ["attribute.value"] = Color.FromRgb(206, 145, 120),
            ["variable"] = Color.FromRgb(156, 220, 254),
        },
    };
}

/// <summary>CodeEditor 主题。</summary>
public sealed class CodeEditorTheme
{
    /// <summary>editor.background</summary>
    public Color EditorBackground { get; init; }
    /// <summary>editor.foreground</summary>
    public Color EditorForeground { get; init; }
    /// <summary>editorLineNumber.foreground</summary>
    public Color EditorLineNumberForeground { get; init; }
    /// <summary>当前行行号颜色（editorLineNumber.activeForeground）。</summary>
    public Color EditorLineNumberActiveForeground { get; init; }
    /// <summary>editor.selectionBackground</summary>
    public Color EditorSelectionBackground { get; init; }
    /// <summary>editorCursor.foreground</summary>
    public Color EditorCursorForeground { get; init; }
    /// <summary>当前行背景</summary>
    public Color EditorCurrentLineBackground { get; init; }
    /// <summary>gutter 背景</summary>
    public Color EditorGutterBackground { get; init; }
    /// <summary>滚动条轨道</summary>
    public Color ScrollBarTrack { get; init; }
    /// <summary>滚动条滑块</summary>
    public Color ScrollBarThumb { get; init; }
    /// <summary>滚动条拖动中滑块</summary>
    public Color ScrollBarThumbActive { get; init; }
    /// <summary>匹配括号背景</summary>
    public Color BracketMatchBackground { get; init; }
    /// <summary>匹配括号边框</summary>
    public Color BracketMatchBorder { get; init; }
    /// <summary>查找匹配背景</summary>
    public Color FindMatchBackground { get; init; }
    /// <summary>当前查找匹配背景</summary>
    public Color FindMatchCurrentBackground { get; init; }
    /// <summary>overview ruler 背景</summary>
    public Color OverviewRulerBackground { get; init; }
    /// <summary>overview ruler 视口指示条</summary>
    public Color OverviewRulerBorder { get; init; }
    /// <summary>token 类型 → 颜色（最长前缀匹配）。</summary>
    public Dictionary<string, Color>? TokenColors { get; init; }

    /// <summary>解析 token 颜色。</summary>
    public Color ResolveTokenColor(string tokenType)
    {
        if (TokenColors == null || string.IsNullOrEmpty(tokenType))
            return EditorForeground;

        var type = tokenType;
        var dot = type.LastIndexOf('.');
        if (dot > 0 && dot < type.Length - 1 && type.Length - dot <= 5)
        {
            var maybeLang = type[(dot + 1)..];
            if (maybeLang.All(char.IsLetter))
                type = type[..dot];
        }

        if (TokenColors.TryGetValue(type, out var exact))
            return exact;

        while (true)
        {
            var i = type.LastIndexOf('.');
            if (i <= 0) break;
            type = type[..i];
            if (TokenColors.TryGetValue(type, out var c))
                return c;
        }

        var baseName = tokenType.Split('.')[0];
        if (TokenColors.TryGetValue(baseName, out var b))
            return b;

        return EditorForeground;
    }
}
