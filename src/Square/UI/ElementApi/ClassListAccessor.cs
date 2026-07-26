namespace Square.UI.ElementApi;

/// <summary>
/// 元素 class 列表（对齐 DOMTokenList <c>classList</c>）。
/// </summary>
public sealed class ClassListAccessor
{
    private readonly Element _owner;
    private HashSet<string>? _classes;

    internal ClassListAccessor(Element owner) { _owner = owner; }

    /// <summary>添加 class；已存在则忽略。</summary>
    public void Add(string className)
    {
        _classes ??= [];
        if (_classes.Add(className)) _owner.Invalidate(ElementInvalidation.Style | ElementInvalidation.Layout);
    }

    /// <summary>移除 class。</summary>
    public void Remove(string className)
    {
        if (_classes == null) return;
        if (_classes.Remove(className)) _owner.Invalidate(ElementInvalidation.Style | ElementInvalidation.Layout);
    }

    /// <summary>切换 class 有无。</summary>
    public void Toggle(string className)
    {
        if (Contains(className)) Remove(className);
        else Add(className);
    }

    /// <summary>强制添加（true）或移除（false）class。</summary>
    public void Toggle(string className, bool force)
    {
        if (force) Add(className);
        else Remove(className);
    }

    /// <summary>是否包含指定 class。</summary>
    public bool Contains(string className) => _classes?.Contains(className) ?? false;

    /// <summary>空格拼接的 class 字符串。</summary>
    public string ToClassString() => _classes == null ? "" : string.Join(' ', _classes);

    /// <summary>清空全部 class。</summary>
    public void Clear()
    {
        if (_classes == null) return;
        if (_classes.Count == 0) return;
        _classes.Clear();
        _owner.Invalidate(ElementInvalidation.Style | ElementInvalidation.Layout);
    }

    /// <summary>返回全部 class 的只读集合。</summary>
    public IReadOnlyCollection<string> GetAll() => _classes ?? [];
}
