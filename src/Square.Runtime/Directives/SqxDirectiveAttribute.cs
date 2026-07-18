namespace Square.Directives;

/// <summary>
/// 将类型标记为编译期 SQX 结构指令（Show、For、Slot、Router 等）。
/// Source Generator 通过扫描引用程序集元数据发现这些指令。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SqxDirectiveAttribute : Attribute
{
    /// <summary>使用主标签名创建指令特性。</summary>
    public SqxDirectiveAttribute(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            throw new ArgumentException("Tag name is required.", nameof(tagName));
        TagName = tagName;
    }

    /// <summary>指令主标签名（如 <c>Show</c>）。</summary>
    public string TagName { get; }

    /// <summary>别名标签（如 Outlet → Slot）。</summary>
    public string[] Aliases { get; set; } = [];

    /// <summary>若设置，则仅允许作为该父标签的子指令。</summary>
    public string? ParentTag { get; set; }

    /// <summary>允许的子指令标签。</summary>
    public string[] AllowedChildTags { get; set; } = [];

    /// <summary>为 true 时不作为独立树节点发射（如 Route 仅在 Router 内处理）。</summary>
    public bool SkipStandaloneEmit { get; set; }

    /// <summary>发射模式名（ControlFlowAttach、SlotOutlet、RouterTree 等）。</summary>
    public string Pattern { get; set; } = "ControlFlowAttach";

    /// <summary>生成代码中使用的运行时类型名（如 ShowNode）。</summary>
    public string? RuntimeTypeName { get; set; }

    /// <summary>生成私有字段的前缀（如 _show）。</summary>
    public string FieldPrefix { get; set; } = "_dir";

    /// <summary>驱动指令的主属性名（when、each、name、path 等）。</summary>
    public string? PrimaryAttribute { get; set; }
}

/// <summary>可选的程序集级标记，用于纳入指令扫描范围。</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class SqxDirectiveAssemblyAttribute : Attribute;
