using System.Collections;
using System.Text.RegularExpressions;

namespace Square.Text.Fonts;

/// <summary>
/// 字体面集合（对齐 CSS Font Loading <c>FontFaceSet</c> / <c>document.fonts</c> 子集）。
/// 支持 Add、异步 Load、Check 与 Ready。
/// </summary>
public sealed class FontFaceSet : IReadOnlyCollection<FontFace>
{
    private static readonly Lazy<FontFaceSet> DefaultLazy = new(() => new FontFaceSet());
    private static readonly Regex FontShorthandRegex = new(
        @"^\s*(?:(?<size>\d+(?:\.\d+)?)\s*px\s+)?(?<family>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly object _gate = new();
    private readonly List<FontFace> _faces = [];

    /// <summary>进程默认字体集（对齐单页应用的 <c>document.fonts</c>）。</summary>
    public static FontFaceSet Default => DefaultLazy.Value;

    /// <summary>集合中的字体面数量（对齐 <c>size</c>）。</summary>
    public int Count
    {
        get { lock (_gate) return _faces.Count; }
    }

    /// <summary>添加字体面（对齐 <c>add</c>）；不自动 load。</summary>
    public void Add(FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        lock (_gate)
        {
            if (!_faces.Contains(face))
                _faces.Add(face);
        }
    }

    /// <summary>移除字体面（对齐 <c>delete</c>）。</summary>
    public bool Delete(FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        lock (_gate)
            return _faces.Remove(face);
    }

    /// <summary>清空集合（对齐 <c>clear</c>）。</summary>
    public void Clear()
    {
        lock (_gate)
            _faces.Clear();
    }

    /// <summary>是否包含该字体面实例。</summary>
    public bool Contains(FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        lock (_gate)
            return _faces.Contains(face);
    }

    /// <summary>
    /// 检查是否已有可用于描述的字体（对齐 <c>check</c> 简化版）。
    /// <paramref name="font"/> 形如 <c>16px MyFont</c> 或 <c>MyFont</c>。
    /// </summary>
    public bool Check(string font)
    {
        var family = ParseFamilyFromFont(font);
        if (string.IsNullOrEmpty(family)) return false;

        lock (_gate)
        {
            foreach (var face in _faces)
            {
                if (string.Equals(face.Family, family, StringComparison.OrdinalIgnoreCase) &&
                    face.Status == FontFaceLoadStatus.Loaded)
                    return true;
            }
        }

        return FontManager.FontManager.Instance.IsFamilyKnown(family);
    }

    /// <summary>
    /// 加载匹配的字体面（对齐 <c>load</c> 简化版）。
    /// 对集合中族名匹配且未加载的 <see cref="FontFace"/> 调用 <see cref="FontFace.LoadAsync"/>。
    /// </summary>
    public async Task LoadAsync(string font, string text = " ", CancellationToken cancellationToken = default)
    {
        _ = text; // 完整实现可按字符子集加载；当前加载整个 face
        var family = ParseFamilyFromFont(font);
        List<FontFace> toLoad;
        lock (_gate)
        {
            toLoad = _faces
                .Where(f =>
                    (string.IsNullOrEmpty(family) ||
                     string.Equals(f.Family, family, StringComparison.OrdinalIgnoreCase)) &&
                    f.Status != FontFaceLoadStatus.Loaded)
                .ToList();
        }

        foreach (var face in toLoad)
            await face.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 等待集合中全部字体面加载结束（成功或失败均算完成）（对齐 <c>ready</c> 简化版）。
    /// </summary>
    public Task Ready
    {
        get
        {
            List<Task> tasks;
            lock (_gate)
            {
                tasks = _faces
                    .Where(f => f.Status is FontFaceLoadStatus.Unloaded or FontFaceLoadStatus.Loading)
                    .Select(f => f.LoadAsync())
                    .ToList();
            }

            if (tasks.Count == 0)
                return Task.CompletedTask;

            return Task.WhenAll(tasks.Select(async t =>
            {
                try { await t.ConfigureAwait(false); }
                catch { /* ready 不因单个失败而失败 */ }
            }));
        }
    }

    /// <summary>枚举字体面。</summary>
    public IEnumerator<FontFace> GetEnumerator()
    {
        lock (_gate)
            return _faces.ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>从 <c>16px Family</c> 或 <c>Family</c> 解析族名。</summary>
    public static string ParseFamilyFromFont(string font)
    {
        if (string.IsNullOrWhiteSpace(font)) return "";
        var match = FontShorthandRegex.Match(font.Trim());
        if (!match.Success) return font.Trim().Trim('\'', '"');
        var family = match.Groups["family"].Value.Trim().Trim('\'', '"');
        // 去掉可能的 weight/style 前缀词（极简）
        foreach (var token in new[] { "bold", "italic", "normal", "oblique" })
        {
            if (family.StartsWith(token + " ", StringComparison.OrdinalIgnoreCase))
                family = family[(token.Length + 1)..].Trim().Trim('\'', '"');
        }
        return family;
    }
}
