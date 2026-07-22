namespace Square.UI.Properties;

/// <summary>
/// 元素强类型属性袋（Square 扩展；用于控件属性与 SQX 绑定，非 DOM Attr）。
/// </summary>
public sealed class PropertyStore
{
    private Dictionary<string, object?>? _values;
    private HashSet<string>? _boundProperties;

    /// <summary>尝试按名读取强类型值。</summary>
    public bool TryGetValue<T>(string name, out T value)
    {
        if (_values != null && _values.TryGetValue(name, out var v) && v is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>按名写入值。</summary>
    public void SetValue<T>(string name, T value)
    {
        _values ??= [];
        _values[name] = value;
    }

    /// <summary>是否已有该名属性值。</summary>
    public bool HasValue(string name) => _values?.ContainsKey(name) ?? false;

    /// <summary>移除属性值。</summary>
    public void RemoveValue(string name) => _values?.Remove(name);

    /// <summary>标记属性来自数据绑定（命令式写入可被后续源更新覆盖）。</summary>
    public void MarkBound(string name)
    {
        _boundProperties ??= [];
        _boundProperties.Add(name);
    }

    /// <summary>属性是否已绑定。</summary>
    public bool IsBound(string name) => _boundProperties?.Contains(name) ?? false;

    /// <summary>已绑定属性名枚举。</summary>
    public IEnumerable<string> GetBoundProperties() => _boundProperties ?? [];
}
