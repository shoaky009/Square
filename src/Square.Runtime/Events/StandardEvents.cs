namespace Square.Events;

/// <summary>
/// 标准事件类型名与工厂方法（DOM 风格小写类型 + 默认 bubbles/cancelable）。
/// </summary>
public static class StandardEvents
{
    /// <summary>指针按下。</summary>
    public const string PointerDown = "pointerdown";
    /// <summary>指针抬起。</summary>
    public const string PointerUp = "pointerup";
    /// <summary>指针移动。</summary>
    public const string PointerMove = "pointermove";
    /// <summary>滚轮。</summary>
    public const string Wheel = "wheel";
    /// <summary>键按下。</summary>
    public const string KeyDown = "keydown";
    /// <summary>键抬起。</summary>
    public const string KeyUp = "keyup";
    /// <summary>文本输入（IME/组合输入相关，框架扩展名）。</summary>
    public const string TextInput = "textinput";
    /// <summary>焦点进入祖先链（冒泡）。</summary>
    public const string FocusIn = "focusin";
    /// <summary>焦点离开祖先链（冒泡）。</summary>
    public const string FocusOut = "focusout";
    /// <summary>获得焦点（不冒泡）。</summary>
    public const string Focus = "focus";
    /// <summary>失去焦点（不冒泡）。</summary>
    public const string Blur = "blur";
    /// <summary>单击。</summary>
    public const string Click = "click";
    /// <summary>值变更。</summary>
    public const string Change = "change";
    /// <summary>输入过程中的值变化。</summary>
    public const string Input = "input";
    /// <summary>请求动画帧（Square 扩展，非标准 DOM）。</summary>
    public const string RequestFrame = "requestframe";

    private static readonly Dictionary<string, EventInit> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        [PointerDown] = BubblingCancelable(),
        [PointerUp] = BubblingCancelable(),
        [PointerMove] = BubblingCancelable(),
        [Wheel] = BubblingCancelable(),
        [KeyDown] = BubblingCancelable(),
        [KeyUp] = BubblingCancelable(),
        [TextInput] = Bubbling(),
        [FocusIn] = Bubbling(),
        [FocusOut] = Bubbling(),
        [Focus] = None(),
        [Blur] = None(),
        [Click] = BubblingCancelable(),
        [Change] = Bubbling(),
        [Input] = Bubbling(),
        [RequestFrame] = Bubbling(),
    };

    /// <summary>获取类型默认的 <see cref="EventInit"/>（未知类型返回 null）。</summary>
    public static EventInit? GetDefaultInit(string type) =>
        Defaults.GetValueOrDefault(type);

    /// <summary>按类型创建事件；未知类型默认冒泡、不可取消。</summary>
    public static Event Create(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        var init = GetDefaultInit(type) ?? Bubbling();
        return new Event(type, init);
    }

    /// <summary>创建 pointerdown 事件。</summary>
    public static Event CreatePointerDown() => Create(PointerDown);
    /// <summary>创建 pointerup 事件。</summary>
    public static Event CreatePointerUp() => Create(PointerUp);
    /// <summary>创建 pointermove 事件。</summary>
    public static Event CreatePointerMove() => Create(PointerMove);
    /// <summary>创建 wheel 事件。</summary>
    public static Event CreateWheel() => Create(Wheel);
    /// <summary>创建 keydown 事件。</summary>
    public static Event CreateKeyDown() => Create(KeyDown);
    /// <summary>创建 keyup 事件。</summary>
    public static Event CreateKeyUp() => Create(KeyUp);
    /// <summary>创建 click 事件。</summary>
    public static Event CreateClick() => Create(Click);
    /// <summary>创建 change 事件。</summary>
    public static Event CreateChange() => Create(Change);
    /// <summary>创建 input 事件。</summary>
    public static Event CreateInput() => Create(Input);
    /// <summary>创建 focus 事件（不冒泡）。</summary>
    public static Event CreateFocus() => Create(Focus);
    /// <summary>创建 blur 事件（不冒泡）。</summary>
    public static Event CreateBlur() => Create(Blur);
    /// <summary>创建 focusin 事件（冒泡）。</summary>
    public static Event CreateFocusIn() => Create(FocusIn);
    /// <summary>创建 focusout 事件（冒泡）。</summary>
    public static Event CreateFocusOut() => Create(FocusOut);

    /// <summary>创建 requestframe 帧请求事件（Square 扩展）。</summary>
    public static FrameRequestEvent CreateRequestFrame(double framesPerSecond = 60d) =>
        new(framesPerSecond);

    private static EventInit BubblingCancelable() => new() { Bubbles = true, Cancelable = true };
    private static EventInit Bubbling() => new() { Bubbles = true, Cancelable = false };
    private static EventInit None() => new() { Bubbles = false, Cancelable = false };
}
