namespace Square.UI;

/// <summary>将内容投影到父元素的委托（组件 Slot 片段）。</summary>
/// <param name="parent">插入子节点的父元素。</param>
public delegate void RenderFragment(Element parent);

/// <summary>
/// 组件插槽集合：调用方设置具名/默认片段，组件内 <c>Slot</c> 出口渲染一次。
/// </summary>
public sealed class SlotCollection
{
    private readonly Dictionary<string, RenderFragment> _fragments = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rendered = new(StringComparer.Ordinal);

    /// <summary>设置具名或默认（name 为空）插槽内容；已渲染后不可再改。</summary>
    public void Set(string? name, RenderFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var key = NormalizeName(name);
        if (_rendered.Contains(key))
            throw new InvalidOperationException($"Slot '{DisplayName(key)}' has already been rendered.");
        _fragments[key] = fragment;
    }

    /// <summary>是否存在指定插槽片段。</summary>
    public bool Contains(string? name) => _fragments.ContainsKey(NormalizeName(name));

    /// <summary>
    /// 渲染插槽到 <paramref name="parent"/>；无内容返回 false（调用方应渲染 fallback）。
    /// 每个插槽每个实例仅允许渲染一次。
    /// </summary>
    public bool Render(string? name, Element parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var key = NormalizeName(name);
        if (!_fragments.TryGetValue(key, out var fragment)) return false;
        if (!_rendered.Add(key))
            throw new InvalidOperationException($"Slot '{DisplayName(key)}' can only be rendered once.");
        fragment(parent);
        return true;
    }

    private static string NormalizeName(string? name) => name?.Trim() ?? "";
    private static string DisplayName(string name) => name.Length == 0 ? "default" : name;
}
