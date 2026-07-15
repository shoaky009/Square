namespace Square.Text.FontManager;

public sealed class FontManager
{
    private static FontManager? _instance;
    public static FontManager Instance => _instance ??= new FontManager();

    private readonly Dictionary<string, string> _familyCache = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Segoe UI"] = "Segoe UI",
        ["Arial"] = "Arial",
        ["sans-serif"] = "Segoe UI",
        ["serif"] = "Times New Roman",
        ["monospace"] = "Consolas"
    };

    public IReadOnlyList<string> AvailableFamilies => _familyCache.Keys.ToList();

    public string ResolveFamily(string family) =>
        _familyCache.TryGetValue(family, out var resolved) ? resolved : family;

    public Graphics.Font Match(string family, float size, Graphics.FontWeight weight, Graphics.FontStyle style)
    {
        return new Graphics.Font(ResolveFamily(family), size, weight, style);
    }
}