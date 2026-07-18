namespace Square.Runtime;

/// <summary>
/// 组件/元素生命周期（Square 扩展；非 DOM 标准接口）。
/// 挂载、卸载与 Props 变更时由框架调用。
/// </summary>
public interface IComponentLifecycle
{
    /// <summary>具名 Props 或绑定属性变更时。</summary>
    void OnPropChanged(string name);

    /// <summary>挂入活动文档树时（含子树递归）。</summary>
    void OnAttached();

    /// <summary>从文档树卸下时（含子树递归）。</summary>
    void OnDetached();

    /// <summary>宿主完成加载、首帧可用时。</summary>
    void OnLoaded();

    /// <summary>宿主卸载时。</summary>
    void OnUnloaded();
}

/// <summary>
/// 布局生命周期钩子（Square 扩展；由布局引擎触发）。
/// </summary>
public interface ILayoutLifecycle
{
    /// <summary>测量阶段。</summary>
    void OnMeasure();

    /// <summary>排列阶段。</summary>
    void OnArrange();
}
