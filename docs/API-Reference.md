# API 参考

> Version: 0.1
> 配套：`Getting-Started.md`、`Architecture.md`、`Sqx-Spec.md`

本文按模块列出 Square 框架的公共 API。所有类型签名基于源码，以 `命名空间.类型名` 组织。

---

## 1. Square.Hosting — 桌面应用宿主

### DesktopApplication

```csharp
namespace Square.Hosting;

public sealed class DesktopApplication : Application
{
    public DesktopApplication(UIDocument document, PlatformHostCreateInfo hostCreateInfo);
    public DesktopApplication(Element contentRoot, PlatformHostCreateInfo hostCreateInfo); // 兼容：内容挂到新 UIDocument.Body
    public UIDocument Document { get; }
    public Color Background { get; set; }   // 默认 Color.White
}
```

| 成员 | 说明 |
|---|---|
| 构造函数 | 接收 `UIDocument`（或兼容地接收内容 `Element` 并包进 Body）与窗口创建信息 |
| `Document` | 当前 UI 文档 |
| `Background` | 窗口背景色，默认白色 |
| `Dispatcher`（继承自 `Application`） | UI 线程调度器，用于 Signal 跨线程投递 |
| `Run()`（继承自 `Application`） | 启动应用：注册默认后端/平台/控件 → 构建文档 → 创建窗口 → 消息循环 |
| `Shutdown()`（继承自 `Application`） | 请求关闭消息循环 |

`Run()` 内部自动处理：

1. `BackendRegistration` / `PlatformRegistration` / `ControlRegistration.RegisterDefaults()`
2. `document.Build()` → `documentElement` 上 `OnAttached()`
3. 创建 `IPlatformHost` 并绑定所有输入事件
4. `OnLoaded()` → 首帧渲染 → `PumpEvents()` 消息循环
5. 退出时 `OnUnloaded()` → `OnDetached()`

---

## 2. Square.Runtime — 应用运行时

### Application

```csharp
namespace Square.Runtime;

public abstract class Application
{
    public static Application Current { get; }
    public static bool IsStarted { get; }
    public Dispatcher Dispatcher { get; }
    public bool IsRunning { get; }

    public void Run();
    public void Shutdown();

    protected abstract void RunCore();
    protected virtual void OnStart() { }
    protected virtual void OnExit() { }
}
```

| 成员 | 说明 |
|---|---|
| `Current` | 当前应用实例，未启动时抛 `InvalidOperationException` |
| `Dispatcher` | UI 线程调度器 |
| `Run()` | 设置 `IsRunning`，调用 `OnStart` → `RunCore` → `OnExit` |
| `RunCore()` | 子类实现核心循环 |
| `OnStart/OnExit` | 应用级生命周期钩子 |

### Dispatcher

```csharp
namespace Square.Runtime;

public sealed class Dispatcher
{
    public bool CheckAccess();
    public void VerifyAccess();
    public void Invoke(Action action);
    public Task InvokeAsync(Action action);
    public void Run();
    public bool HasWork { get; }
}
```

| 成员 | 说明 |
|---|---|
| `CheckAccess()` | 当前线程是否为 Dispatcher 所属线程 |
| `Invoke(action)` | 将操作排入队列 |
| `InvokeAsync(action)` | 同线程直接执行；跨线程排队并返回 `Task` |
| `Run()` | 排空队列（仅所属线程可调用） |
| `HasWork` | 队列是否有待处理操作 |

### ObservableValue\<T\>

```csharp
namespace Square.Runtime.Binding;

public sealed class ObservableValue<T>
{
    public ObservableValue(T value);
    public ObservableValue();

    public T Value { get; set; }
    public IDisposable Subscribe(Action<T> handler);
    public void Notify();

    public static implicit operator ObservableValue<T>(T value);
    public static implicit operator T(ObservableValue<T> ov);
}
```

| 成员 | 说明 |
|---|---|
| `Value` | get 返回当前值；set 在值变化时通知订阅者（相等时不通知） |
| `Subscribe(handler)` | 订阅值变化，返回 `IDisposable` 用于取消 |
| `Notify()` | 强制通知当前值 |
| 隐式转换 | `T` ↔ `ObservableValue<T>` 双向隐式转换 |

### ObservableCollection\<T\>

```csharp
namespace Square.Runtime.Binding;

public sealed class ObservableCollection<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged
{
    public T this[int index] { get; set; }
    public int Count { get; }

    public void Add(T item);
    public void AddRange(IEnumerable<T> items);
    public void Insert(int index, T item);
    public bool Remove(T item);
    public void RemoveAt(int index);
    public void Move(int oldIndex, int newIndex);
    public void Clear();
    public int IndexOf(T item);
    public bool Contains(T item);

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
}
```

| 成员 | 说明 |
|---|---|
| `Add/Insert` | 添加并触发 `CollectionChanged`（Add） |
| `Remove/RemoveAt` | 移除并触发 `CollectionChanged`（Remove） |
| `Move` | 移动并触发 `CollectionChanged`（Move） |
| `Clear` | 清空并触发 `CollectionChanged`（Reset） |
| `CollectionChanged` | 通知事件，`<For>` 原语自动订阅 |

### PropAttribute

```csharp
namespace Square.Runtime.Binding;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PropAttribute : Attribute
{
    public bool Required { get; set; }
    public object? Default { get; set; }
}
```

| 属性 | 说明 |
|---|---|
| `Required` | 标记为必填 Prop，编译期校验 |
| `Default` | 默认值（也可用初始化器） |

### IComponentLifecycle

```csharp
namespace Square.Runtime;

public interface IComponentLifecycle
{
    void OnPropChanged(string name);
    void OnAttached();
    void OnDetached();
    void OnLoaded();
    void OnUnloaded();
}
```

`Element` 实现此接口。通过显式接口调用触发生命周期。

### Signal\<T\>

```csharp
namespace Square.Runtime.Signals;

public sealed class Signal<T>
{
    public Signal(T initialValue);
    public Signal();

    public T Value { get; set; }
    public bool Publish(T value, bool force = false);
    public T Update(Func<T, T> update, bool force = false);
    public IDisposable Subscribe(Action<T> handler, Dispatcher? dispatcher = null, bool emitCurrent = false);
}
```

| 成员 | 说明 |
|---|---|
| `Value` | get 线程安全读取；set 等同 `Publish(value)` |
| `Publish(value, force)` | 更新值并通知订阅者；同值默认不通知，`force: true` 强制通知 |
| `Update(fn, force)` | 在锁内原子计算新值，锁外通知 |
| `Subscribe(handler, dispatcher, emitCurrent)` | 订阅值变化；指定 `dispatcher` 时跨线程回调排队到该 Dispatcher；`emitCurrent: true` 订阅时立即投递当前值 |

### SignalHub

```csharp
namespace Square.Runtime.Signals;

public sealed class SignalHub
{
    public static SignalHub Default { get; }

    public Signal<T> Get<T>(string name, T initialValue = default!);
    public bool Remove<T>(string name);
}
```

| 成员 | 说明 |
|---|---|
| `Default` | 进程级共享实例 |
| `Get<T>(name, initial)` | 按名称获取或创建强类型 Signal；同名类型冲突抛 `InvalidOperationException` |
| `Remove<T>(name)` | 移除指定 Signal |

---

## 3. Square.Events — DOM 事件（Web API）

对齐 MDN：`EventTarget` / `Event` / `addEventListener` / `dispatchEvent`。  
**已删除** WPF 风格 `RoutedEvent` / `RaiseEvent` / `Handled` / `RoutingStrategy`（非改名，硬切）。

### EventPhase

```csharp
namespace Square.Events;

public enum EventPhase
{
    None = 0,
    CapturingPhase = 1,
    AtTarget = 2,
    BubblingPhase = 3
}
```

### Event / EventInit

```csharp
namespace Square.Events;

public sealed class EventInit
{
    public bool Bubbles { get; init; }
    public bool Cancelable { get; init; }
    public bool Composed { get; init; }
}

public class Event
{
    public Event(string type, EventInit? init = null);

    public string Type { get; }
    public EventTarget? Target { get; }
    public EventTarget? CurrentTarget { get; }
    public EventPhase EventPhase { get; }
    public bool Bubbles { get; }
    public bool Cancelable { get; }
    public bool Composed { get; }
    public bool DefaultPrevented { get; }
    public bool IsTrusted { get; }
    public double TimeStamp { get; }

    public void PreventDefault();
    public void StopPropagation();
    public void StopImmediatePropagation();
    public IReadOnlyList<EventTarget> ComposedPath();
}
```

| 成员 | 说明 |
|---|---|
| `Target` | 派发目标（原 `OriginalSource`/`Source`） |
| `CurrentTarget` | 当前处理该事件的 `EventTarget` |
| `EventPhase` | 捕获 / 目标 / 冒泡 |
| `StopPropagation` | 停止继续传播（替代旧 `Handled`） |
| `PreventDefault` | 可取消事件上阻止默认行为 |

### EventTarget

```csharp
namespace Square.Events;

public class EventTarget
{
    public void AddEventListener(string type, Action<Event>? listener, AddEventListenerOptions? options = null);
    public void AddEventListener(string type, Action<Event>? listener, bool useCapture);
    public void AddEventListener(string type, Action handler, AddEventListenerOptions? options = null);
    public void AddEventListener<TEvent>(string type, Action<TEvent> handler, AddEventListenerOptions? options = null)
        where TEvent : Event;

    public void RemoveEventListener(string type, Action<Event>? listener, bool useCapture = false);
    public void RemoveEventListener(string type, Action handler, bool useCapture = false);

    public bool DispatchEvent(Event e);
    public bool DispatchTrusted(Event e); // IsTrusted = true（平台输入）
}
```

`AddEventListenerOptions`：`Capture`、`Once`、`Passive`、`Signal`（`CancellationToken`）。

传播：捕获 → 目标 →（若 `Bubbles`）冒泡。父链由 `GetEventParent()` 提供（`Element.Parent`，否则 `OwnerDocument`）。

### StandardEvents

```csharp
namespace Square.Events;

public static class StandardEvents
{
    public const string PointerDown = "pointerdown";
    public const string PointerUp = "pointerup";
    public const string PointerMove = "pointermove";
    public const string Wheel = "wheel";
    public const string KeyDown = "keydown";
    public const string KeyUp = "keyup";
    public const string Click = "click";
    public const string Change = "change";
    public const string Input = "input";
    public const string Focus = "focus";      // 不冒泡
    public const string Blur = "blur";        // 不冒泡
    public const string FocusIn = "focusin";  // 冒泡
    public const string FocusOut = "focusout";
    public const string RequestFrame = "requestframe"; // 框架扩展

    public static Event Create(string type);
    public static Event CreateClick();
    public static Event CreatePointerDown();
    // … 其它 Create* 工厂
    public static FrameRequestEvent CreateRequestFrame(double framesPerSecond = 60d);
}
```

### FrameRequestEvent

```csharp
namespace Square.Events;

public sealed class FrameRequestEvent : Event
{
    public double FramesPerSecond { get; }
    public double IntervalSeconds { get; }
}
```

---

## 4. Square.UI — 元素树 API

### Element

```csharp
namespace Square.UI;

public abstract class Element : EventTarget, IComponentLifecycle, ILayoutLifecycle
{
    public PropertyStore Properties { get; }
    public StyleAccessor Style { get; }
    public ClassListAccessor ClassList { get; }
    public ChildrenCollection Children { get; }
    public virtual string TagName { get; }
    public string? Id { get; set; }
    public Document? OwnerDocument { get; }
    public Element? Parent { get; }
    public Element? ParentNode { get; }      // 同 Parent
    public Element? ParentElement { get; }   // 同 Parent
    public Element? FirstElementChild { get; }
    public Element? LastElementChild { get; }
    public int ChildElementCount { get; }
    public Rect Geometry { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLayoutDirty { get; }
    public bool NeedsPaint { get; }
    public virtual int ZIndex { get; set; }
    public ElementState State { get; }
    public bool IsAttached { get; }
    public bool IsLoaded { get; }

    public void SetState(ElementState flag, bool on);
    public bool HasState(ElementState flag);

    public Element AppendChild(Element child);
    public Element InsertBefore(Element newChild, Element? referenceChild);
    public Element RemoveChild(Element child);
    public void ReplaceChildren(params Element[] nodes);
    public Rect GetBoundingClientRect();

    public T? GetProperty<T>(string name);
    public void SetProperty<T>(string name, T value);
    public void BindProperty<T>(string name, Func<T> getter);
    public void BindProperty<T>(string name, ObservableValue<T> source);

    // 事件：继承 EventTarget（AddEventListener / DispatchEvent）

    public virtual Element? HitTest(Point point);\n    public bool ClipsOverflow();
    public Rect GetOverflowClipRect();

    public T? Query<T>(string? className = null) where T : Element;
    public List<T> QueryAll<T>(string? className = null) where T : Element;

    public void InvalidateLayout();
    public void InvalidatePaint();
    public void ClearLayoutDirty();
    public void ClearPaintDirty();

    public virtual Size Measure(Size availableSize);
    public virtual void Arrange(Rect finalRect);
    public virtual void Paint(IRenderContext ctx);
    public virtual void BuildElementTree();

    protected virtual void OnPropertyChanged(string name);
    protected virtual void OnPropChanged(string name);
    protected virtual void OnAttachedCore();
    protected virtual void OnDetachedCore();
}
```

### Document / UIDocument

```csharp
namespace Square.UI;

public abstract class Document : EventTarget
{
    public Element DocumentElement { get; }   // 只读
    public string Title { get; set; }
    public Element? GetElementById(string id);
    public T? GetElementById<T>(string id) where T : Element;
    public List<T> GetElementsByTagName<T>(string tagName) where T : Element;
    public List<T> GetElementsByClassName<T>(string className) where T : Element;
    public T? Query<T>(string? className = null) where T : Element;
    public List<T> QueryAll<T>(string? className = null) where T : Element;
}

public sealed class UIDocument : Document
{
    public UIRootElement Ui { get; }     // documentElement，TagName "UI"
    public UIHeadElement Head { get; }   // 元数据；本阶段不参与布局
    public UIBodyElement Body { get; }   // 窗口客户区内容宿主

    public static void RegisterElement(string tagName, Func<Element> factory);
    public Element CreateElement(string tagName);
    public T CreateElement<T>() where T : Element, new();
    public void Build();                 // 对 Body 子树 BuildElementTree
}
```

壳结构：`DocumentElement = UI`（只读）→ 子节点 `Head` + `Body`。应用内容挂在 `Body` 下，**不是** documentElement。

### HTMLElement / SVGElement（占位）

```csharp
namespace Square.UI;

public abstract class HTMLElement : Element
{
    public override string? NamespaceURI => "http://www.w3.org/1999/xhtml";
}

public abstract class SVGElement : Element
{
    public override string? NamespaceURI => "http://www.w3.org/2000/svg";
}
```

### UIElement

```csharp
namespace Square.UI;

public abstract class UIElement : Element
{
    public SlotCollection Slots { get; }
    public HorizontalAlignment HorizontalAlign { get; set; }
    public VerticalAlignment VerticalAlign { get; set; }

    public float Width { get; set; }       // float.NaN = auto
    public float Height { get; set; }      // float.NaN = auto
    public float MinWidth { get; set; }
    public float MinHeight { get; set; }
    public float MaxWidth { get; set; }
    public float MaxHeight { get; set; }

    public float MarginLeft { get; set; }
    public float MarginTop { get; set; }
    public float MarginRight { get; set; }
    public float MarginBottom { get; set; }

    public float PaddingLeft { get; set; }
    public float PaddingTop { get; set; }
    public float PaddingRight { get; set; }
    public float PaddingBottom { get; set; }

    public bool IsDisabled { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsFocused { get; }
    public string? Tooltip { get; set; }

    public void Focus();
    public void Unfocus();
}
```

### ElementState

```csharp
namespace Square.UI;

[Flags]
public enum ElementState : byte
{
    None = 0, Hover = 1, Focus = 2, Active = 4, Disabled = 8, Checked = 16, Empty = 32
}
```

### StyleAccessor

```csharp
namespace Square.UI.ElementApi;

public sealed class StyleAccessor
{
    public void Set(string property, string value);
    public bool SetCascaded(string property, string value, int specificity);
    public string? Get(string property);
    public void Remove(string property);
    public void Clear();
    public void ClearCascaded();
    public IReadOnlyDictionary<string, string> GetAll();
}
```

### ClassListAccessor

```csharp
namespace Square.UI.ElementApi;

public sealed class ClassListAccessor
{
    public void Add(string className);
    public void Remove(string className);
    public void Toggle(string className);
    public void Toggle(string className, bool force);
    public bool Contains(string className);
    public string ToClassString();
    public void Clear();
    public IReadOnlyCollection<string> GetAll();
}
```

### ChildrenCollection

```csharp
namespace Square.UI.ElementApi;

public sealed class ChildrenCollection : IList<Element>
{
    public Element this[int index] { get; }
    public int Count { get; }

    public void Add(Element item);
    public void AddRange(IEnumerable<Element> items);
    public void Insert(int index, Element item);
    public void InsertBefore(Element newChild, Element refChild);
    public bool Remove(Element item);
    public void RemoveAt(int index);
    public void Clear();
    public int IndexOf(Element item);
    public bool Contains(Element item);
}
```

> 添加子节点时自动触发 `OnAttached`/`OnLoaded`；移除时触发 `OnUnloaded`/`OnDetached`。

### SlotCollection

```csharp
namespace Square.UI;

public delegate void RenderFragment(Element parent);

public sealed class SlotCollection
{
    public void Set(string? name, RenderFragment fragment);
    public bool Contains(string? name);
    public bool Render(string? name, Element parent);
}
```

### PropertyStore

```csharp
namespace Square.UI.Properties;

public sealed class PropertyStore
{
    public bool TryGetValue<T>(string name, out T value);
    public void SetValue<T>(string name, T value);
    public bool HasValue(string name);
    public void MarkBound(string name);
}
```

---

## 5. Square.Controls — 控件

### 内置控件一览

| 控件 | 基类 | 关键属性 |
|---|---|---|
| `View` | `UIElement` | — |
| `Text` | `UIElement` | `TextContent`, `Color`, `FontSize` |
| `ListItem` | `UIElement` | `TextContent`, `Marker`, `Color`, `FontSize`（类似 HTML `li`） |
| `Link` | `UIElement` | `TextContent`, `Href`, `Color`, `FontSize`, `Underline`（类似 HTML `a`；路由导航见 `Square.Router.Link`） |
| `Button` | `UIElement` | `TextContent`, `Background`, `Foreground` |
| `CheckBox` | `UIElement` | `IsChecked`, `TextContent` |
| `Radio` | `UIElement` | `IsChecked`, `TextContent`, `GroupName` |
| `Select` | `UIElement` | `Value`, `Options`, `Placeholder` |
| `Image` | `UIElement` | `Source`, `ImageContent` |
| `Canvas` | `UIElement` | `DrawContent` |
| `Input` | `TextEditorBase` | `Value`, `Placeholder` |
| `TextArea` | `TextEditorBase` | `Value`, `Placeholder` |

### Text

```csharp
namespace Square.Controls.Controls;

public class Text : UIElement
{
    public string TextContent { get; set; }
    public Color Color { get; set; }        // 默认 Black
    public float FontSize { get; set; }     // 默认 16f

    public Text() { }
    public Text(string text);

    public override Size Measure(Size availableSize);
    public override void Render(IRenderContext ctx);
}
```

### Button

```csharp
namespace Square.Controls.Controls;

public class Button : UIElement
{
    public string TextContent { get; set; }
    public Color Background { get; set; }   // 默认 #0078d4
    public Color Foreground { get; set; }   // 默认 White

    public Button() { }
    public Button(string text);

    public override Size Measure(Size availableSize);
    public override void Render(IRenderContext ctx);
}
```

### CheckBox

```csharp
namespace Square.Controls.Controls;

public class CheckBox : UIElement
{
    public bool IsChecked { get; set; }
    public string TextContent { get; set; }
}
```

点击时自动切换 `IsChecked` 并触发 `change` 事件。

### Radio

```csharp
namespace Square.Controls.Controls;

public class Radio : UIElement
{
    public bool IsChecked { get; set; }
    public string TextContent { get; set; }
    public string GroupName { get; set; }
}
```

同组 `GroupName` 内互斥选中。

### Select

```csharp
namespace Square.Controls.Controls;

public class Select : UIElement
{
    public string Value { get; set; }
    public string[] Options { get; set; }
    public string Placeholder { get; set; }  // 默认 "Select"
    public bool IsOpen { get; }

    public override int ZIndex { get; set; }  // 打开时自动提升到 1000

    public void HandlePointerDown(Point point);
    public bool HandlePointerMove(Point point);
    public void CloseDropDown();
}
```

### Image

```csharp
namespace Square.Controls.Controls;

public class Image : UIElement
{
    public string Source { get; set; }
    public Square.Graphics.Image? ImageContent { get; set; }
}
```

### Canvas

```csharp
namespace Square.Controls.Controls;

public class Canvas : UIElement
{
    public Action<IRenderContext, Rect>? DrawContent { get; set; }

    public void RequestFrame(double fps = 60d);
    public void RequestAnimationFrame(Action<IRenderContext, Rect> callback);
    public void RequestAnimationFrame(Action<IRenderContext, Rect> callback, double fps);
}
```

`RequestFrame()` 通过 `StandardEvents.RequestFrame` 冒泡并合并，`DesktopApplication` 在 Tick 中检查调度帧并触发重绘。

### ITextEditor / TextEditorBase

```csharp
namespace Square.Controls.Controls;

public interface ITextEditor
{
    int CaretIndex { get; }
    int SelectionStart { get; }
    int SelectionLength { get; }
    string SelectedText { get; }
    Rect CaretRect { get; }

    void HandleTextInput(string text);
    void HandleKey(int keyCode, bool shift = false, bool control = false);
    void HandlePointerDown(Point point, bool extendSelection = false);
    void HandlePointerMove(Point point);
    void HandlePointerUp(Point point);
    void SelectAll();
    bool DeleteSelection();
    bool ToggleCaretBlink();
    void ResetCaretBlink();
}

public abstract class TextEditorBase : UIElement, ITextEditor
{
    public string Value { get; set; }
    public string Placeholder { get; set; }
    public Color SelectionBackground { get; set; }
    public Color SelectionForeground { get; set; }
}

public class Input : TextEditorBase { protected override bool IsMultiline => false; }
public class TextArea : TextEditorBase { protected override bool IsMultiline => true; }
```

### 结构原语

```csharp
namespace Square.Controls.Primitives;

public sealed class ShowNode : IDisposable
{
    public ShowNode(ObservableValue<bool> source, Func<Element?> build);
    public ShowNode(Func<bool> condition, Func<Element?> build);
    public void AttachTo(Element parent);
    public void Update();
    public void Dispose();
}

public static class ForNode
{
    public static IForNode Create<T>(ObservableCollection<T> source, Func<T, Element?> build);
    public static IForNode Create<T>(IEnumerable<T> source, Func<T, Element?> build);
}

public sealed class SwitchNode : IDisposable
{
    public SwitchNode(Func<int> selector);
    public void AddBranch(Func<bool> condition, Func<Element?> build);
    public void AddDefault(Func<Element?> build);
    public void AttachTo(Element parent);
    public void Update();
    public void Dispose();
}
```

---

## 6. Square.Graphics — 绘图抽象

### IRenderContext

```csharp
namespace Square.Graphics;

public interface IRenderContext : IDisposable
{
    Size CanvasSize { get; }
    float DpiScale { get; }

    void PushTransform(Matrix3x2 matrix);
    void PopTransform();

    void PushClip(Rect rect);
    void PushClip(Geometry geometry);
    void PopClip();

    void FillRect(Rect rect, Brush brush);
    void DrawRect(Rect rect, Pen pen);
    void FillPath(PathGeometry path, Brush brush);
    void DrawPath(PathGeometry path, Pen pen);
    void FillGeometry(Geometry geometry, Brush brush);
    void DrawGeometry(Geometry geometry, Pen pen);
    void DrawText(TextLayout text, Point origin, Brush brush);
    void DrawImage(Image image, Rect dest, Rect? source = null);

    void PushLayer(Rect bounds, float opacity);
    void PopLayer();

    void Clear(Color color);
    void Flush();
    void Present();
}
```

### Color

```csharp
namespace Square.Graphics;

public readonly struct Color : IEquatable<Color>
{
    public readonly byte R, G, B, A;

    public Color(byte r, byte g, byte b, byte a = 255);

    public static Color FromRgb(byte r, byte g, byte b);
    public static Color FromRgba(byte r, byte g, byte b, byte a);
    public static Color Parse(string hex);

    public static readonly Color Transparent, Black, White, Red, Green, Blue;

    public uint ToPackedBgra();
}
```

`Parse` 支持 `#RGB`、`#RRGGBB`、`#AARRGGBB` 格式。

### Rect / Size / Point

```csharp
namespace Square.Graphics;

public readonly struct Rect : IEquatable<Rect>
{
    public readonly float X, Y, Width, Height;

    public Rect(float x, float y, float width, float height);
    public Rect(Point pos, Size size);

    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }
    public Point Position { get; }
    public Size Size { get; }
    public Point Center { get; }
    public bool IsEmpty { get; }

    public bool Contains(Point p);
    public bool Contains(float px, float py);
    public bool IntersectsWith(Rect other);

    public static Rect Union(Rect a, Rect b);
    public static Rect Intersect(Rect a, Rect b);

    public Rect Offset(float dx, float dy);
    public Rect Inflate(float dx, float dy);

    public static readonly Rect Empty;
}

public readonly struct Size
{
    public readonly float Width, Height;
    public Size(float width, float height);
    public static readonly Size Zero;
}

public readonly struct Point
{
    public readonly float X, Y;
    public Point(float x, float y);
}
```

### Brush

```csharp
namespace Square.Graphics;

public abstract class Brush
{
    public static SolidColorBrush FromColor(Color color);
}

public sealed class SolidColorBrush : Brush
{
    public Color Color { get; set; }
    public SolidColorBrush(Color color);
    public SolidColorBrush(byte r, byte g, byte b, byte a = 255);
}

public sealed class LinearGradientBrush : Brush
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public GradientStop[] Stops { get; set; }
    public GradientSpreadMethod SpreadMethod { get; set; }
}

public sealed class RadialGradientBrush : Brush
{
    public Point Center { get; set; }
    public float Radius { get; set; }
    public GradientStop[] Stops { get; set; }
    public GradientSpreadMethod SpreadMethod { get; set; }
}

public sealed class GradientStop
{
    public float Offset { get; set; }
    public Color Color { get; set; }
}
```

### Pen

```csharp
namespace Square.Graphics;

public sealed class Pen
{
    public Brush Brush { get; set; }
    public float Width { get; set; }
    public StrokeStyle? StrokeStyle { get; set; }

    public Pen(Brush brush, float width = 1f, StrokeStyle? style = null);
    public static Pen FromColor(Color color, float width = 1f);
}
```

### Font

```csharp
namespace Square.Graphics;

public sealed class Font
{
    public string Family { get; set; }
    public float Size { get; set; }
    public FontWeight Weight { get; set; }
    public FontStyle Style { get; set; }

    public Font();
    public Font(string family, float size);
    public Font(string family, float size, FontWeight weight, FontStyle style = FontStyle.Normal);

    public Font WithSize(float size);
    public Font WithWeight(FontWeight weight);
}

public enum FontWeight : ushort { Thin=100, ExtraLight=200, Light=300, Normal=400, Medium=500, SemiBold=600, Bold=700, ExtraBold=800, Black=900 }
public enum FontStyle : byte { Normal, Italic, Oblique }
public enum TextAlignment : byte { Left, Center, Right, Justify }
```

### Geometry / PathGeometry

```csharp
namespace Square.Graphics;

public abstract class Geometry { }

public sealed class RectGeometry : Geometry { public Rect Rect { get; set; } }
public sealed class RoundedRectGeometry : Geometry { public Rect Rect; public float RadiusX; public float RadiusY; }
public sealed class EllipseGeometry : Geometry { public Point Center; public float RadiusX; public float RadiusY; }

public sealed class PathGeometry : Geometry
{
    public IReadOnlyList<PathCommand> Commands { get; }

    public PathGeometry MoveTo(Point p);
    public PathGeometry LineTo(Point p);
    public PathGeometry ArcTo(Rect oval, float startAngle, float sweepAngle);
    public PathGeometry Close();

    public static PathGeometry Create();
}
```

### TextLayout

```csharp
namespace Square.Graphics;

public sealed class TextLayout
{
    public string Text { get; set; }
    public Font Font { get; set; }
    public Size MaxSize { get; set; }
    public TextAlignment Alignment { get; set; }
    public float LineHeight { get; set; }   // 倍数，默认 DefaultLineHeight

    public Size Measure();
    public static float DefaultLineHeight { get; }
    public static float MeasureRuneAdvance(Rune rune, float fontSize);
}
```

### IRenderBackendFactory / RenderBackendRegistry

```csharp
namespace Square.Graphics;

public interface IRenderBackendFactory
{
    string Name { get; }
    IRenderContext CreateContext(RenderContextCreateInfo info);
}

public static class RenderBackendRegistry
{
    public static void Register(IRenderBackendFactory factory);
    public static IRenderBackendFactory Get(string name);
    public static IRenderBackendFactory Default { get; }
    public static bool TryGet(string name, out IRenderBackendFactory? factory);
    public static IReadOnlyCollection<string> AvailableNames { get; }
}
```

---

## 7. Square.Platform — 平台宿主

### IPlatformHost

```csharp
namespace Square.Platform;

public interface IPlatformHost
{
    Size ClientSize { get; }
    float DpiScale { get; }
    bool IsRunning { get; }
    CursorKind Cursor { get; set; }
    KeyModifiers Modifiers { get; }

    event Action<Size>? SizeChanged;
    event Action<Point, MouseAction>? MouseEvent;
    event Action<Point, int>? WheelEvent;
    event Action<int, KeyAction>? KeyEvent;
    event Action<string>? TextInput;
    event Action? Tick;

    void Show();
    void Close();
    IRenderContext CreateRenderContext();
    void PumpEvents();
    void SetTextInputRect(Rect rect);
    string GetClipboardText();
    void SetClipboardText(string text);
}
```

| 成员 | 说明 |
|---|---|
| `ClientSize` | 窗口客户区逻辑像素尺寸 |
| `DpiScale` | 当前 DPI 缩放 |
| `Modifiers` | 当前键盘修饰键状态 |
| `MouseEvent` | `(point, action)` 鼠标事件 |
| `KeyEvent` | `(keyCode, action)` 键盘事件 |
| `TextInput` | `(text)` 文本输入（IME） |
| `Tick` | 平台消息循环空闲回调 |
| `PumpEvents()` | 阻塞消息循环，直到窗口关闭 |
| `SetTextInputRect` | 设置 IME 候选框位置 |

### PlatformHostCreateInfo

```csharp
namespace Square.Platform;

public sealed class PlatformHostCreateInfo
{
    public required string Title { get; set; }
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
}
```

### 枚举

```csharp
public enum MouseAction { Down, Up, Move, Wheel }
public enum KeyAction { Down, Up }
public enum CursorKind { Arrow, Text }

[Flags]
public enum KeyModifiers { None = 0, Shift = 1, Control = 2, Alt = 4 }
```

### IPlatformFactory / PlatformRegistry

```csharp
namespace Square.Platform;

public interface IPlatformFactory
{
    string Name { get; }
    IPlatformHost CreateHost(PlatformHostCreateInfo info);
}

public static class PlatformRegistry
{
    public static void Register(IPlatformFactory factory);
    public static IPlatformFactory Get();
}
```

### PlatformRegistration

```csharp
namespace Square.Platform;

public static class PlatformRegistration
{
    public static void RegisterDefaults();
}
```

根据 `PLATFORM_WIN32` 等编译常量注册对应平台工厂。`DesktopApplication` 在启动时自动调用。

---

## 8. Square.Rendering — 布局引擎

### LayoutEngine

```csharp
namespace Square.Rendering;

public sealed class LayoutEngine
{
    public void Measure(Element element, Size availableSize);
    public void Arrange(Element element, Rect finalRect);
}
```

布局流程：`Measure`（计算期望尺寸） → `Arrange`（确定最终位置与尺寸） → 写入 `Element.Geometry`。

### ComputedStyle

```csharp
namespace Square.Rendering;

public sealed class ComputedStyle
{
    public DisplayMode Display { get; set; }
    public FlexDirection FlexDirection { get; set; }
    public JustifyContent JustifyContent { get; set; }
    public AlignItems AlignItems { get; set; }
    public float FlexGrow { get; set; }
    public float FlexShrink { get; set; }
    public float FlexBasis { get; set; }
    public float Gap { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float Padding { get; set; }
    public float Margin { get; set; }
    public string GridTemplateColumns { get; set; }
    public string GridTemplateRows { get; set; }
    public int GridColumn { get; set; }
    public int GridRow { get; set; }
    public int GridColumnSpan { get; set; }
    public int GridRowSpan { get; set; }
    public string GridArea { get; set; }
}

public enum DisplayMode { Block, Flex, Grid, None }
public enum FlexDirection { Row, Column, RowReverse, ColumnReverse }
public enum JustifyContent { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }
public enum AlignItems { Stretch, FlexStart, Center, FlexEnd }
```

支持的 CSS 属性读取自 `Element.Style.Get(...)`：`display`、`flex-direction`、`justify-content`、`align-items`、`gap`、`padding`、`margin`、`width`、`height`、`flex-grow`、`flex-shrink`、`flex-basis`、`grid-template-columns`、`grid-template-rows`、`grid-column`、`grid-row`、`grid-area`、`font-size`。

长度单位：`px`、`%`、`rem`、`em`、`vw`、`vh`、`rp`、`auto`、`min-content`、`max-content`、`fit-content`。

---

## 9. Square.Rendering — Display Tree

### DisplayTree

```csharp
namespace Square.Rendering;

public sealed class DisplayTree
{
    public void BuildFrom(Element element);
    public void Invalidate(Rect rect);
    public void UpdateDirty();
    public void Render(IRenderContext ctx);
}
```

| 成员 | 说明 |
|---|---|
| `BuildFrom(element)` | 从 Element Tree 全量构建 Display Tree |
| `UpdateDirty()` | 按 `NeedsPaint` 增量标记脏节点 |
| `Render(ctx)` | 遍历 DisplayNode，收集并提交 DrawCommand |

`DesktopApplication` 在 `RenderFrame()` 中按需调用 `BuildFrom` 或 `UpdateDirty`，随后 `Render`。`DisplayNode.Source` / `Element` 指向对应文档元素。


---

## 10. Square.Router — 路由

### Router

```csharp
namespace Square.Router;

public sealed class Router : View
{
    public string InitialPath { get; set; }
    public List<RouteDefinition> Routes { get; }
    public INavigationHistory? History { get; set; }
    public RouteContext? Current { get; }
    public event Action<RouteContext>? Navigated;

    public void Start();
    public bool Navigate(string location, bool replace = false);
    public bool Replace(string location);
    public bool Back();
    public bool Forward();
}
```

| 成员 | 说明 |
|---|---|
| `Routes` | 路由声明列表 |
| `History` | 导航历史；默认 `MemoryNavigationHistory` |
| `Current` | 当前 `RouteContext` |
| `Navigate(location, replace)` | 导航到指定路径；路径不匹配返回 false |
| `Back/Forward` | 历史导航 |

### RouteContext

```csharp
namespace Square.Router;

public sealed class RouteContext
{
    public string Location { get; }
    public string Path { get; }
    public IReadOnlyDictionary<string, string> Params { get; }
    public IReadOnlyDictionary<string, string> Query { get; }

    public static RouteContext ParseLocation(string location);
    public static string PropertyName { get; }
}
```

### RouteDefinition

```csharp
namespace Square.Router;

public sealed class RouteDefinition
{
    public string Path { get; set; }
    public Func<UIElement>? ComponentFactory { get; set; }
    public List<RouteDefinition> Children { get; } = [];
}
```

### INavigationHistory

```csharp
namespace Square.Router;

public interface INavigationHistory
{
    string Current { get; }
    event Action<string> Changed;

    void Push(string location);
    void Replace(string location);
    bool Back();
    bool Forward();
}
```

### Link（控件，类似 `a`）

```csharp
namespace Square.Controls.Controls;

public class Link : UIElement
{
    public string TextContent { get; set; }
    public string Href { get; set; }
    public Color Color { get; set; }      // 默认 #0066CC
    public float FontSize { get; set; }   // 默认 16
    public bool Underline { get; set; }   // 默认 true
}
```

### ListItem（类似 `li`）

```csharp
namespace Square.Controls.Controls;

public class ListItem : UIElement
{
    public string TextContent { get; set; }
    public string Marker { get; set; }    // 默认 "• "
    public Color Color { get; set; }
    public float FontSize { get; set; }
}
```

### Link（路由，继承控件 Link）

```csharp
namespace Square.Router;

public sealed class Link : Square.Controls.Controls.Link
{
    public string To { get; set; }      // 应用内路径；同步到 Href
    public bool Replace { get; set; }
}
```

`.sqx` 中的 `<Link>` 映射为 `Square.Router.Link`（需引用 Router）。纯样式超链接可直接 `new Square.Controls.Controls.Link(...)`。

---

## 11. Square.Controls.Animation — 动画

### Animation\<T\>

```csharp
namespace Square.Controls.Animation;

public sealed class Animation<T>
{
    public Animation(
        Func<T, T, float, T> interpolate,
        T from, T to, float duration,
        Func<float, float> easing,
        Action<T> onUpdate);

    public bool IsComplete { get; }
    public void Start();
    public void Stop();
    public void Update(float deltaSeconds);
}
```

### Easing

```csharp
namespace Square.Controls.Animation;

public static class Easing
{
    public static float Linear(float t);
    public static float EaseIn(float t);
    public static float EaseOut(float t);
    public static float EaseInOut(float t);
    public static float EaseInQuad(float t);
    public static float EaseOutQuad(float t);
    public static float EaseInOutQuad(float t);
}
```

### Clock

```csharp
namespace Square.Controls.Animation;

public sealed class Clock
{
    public double ElapsedSeconds { get; }
    public double DeltaSeconds { get; }
    public void Start();
    public void Stop();
}
```

---

## 12. 枚举与对齐辅助

### HorizontalAlignment / VerticalAlignment

```csharp
namespace Square.UI;

public enum HorizontalAlignment { Left, Center, Right, Stretch }
public enum VerticalAlignment { Top, Center, Bottom, Stretch }
```

---

## 13. 注册与初始化

应用启动时 `DesktopApplication` 自动调用以下注册：

| 注册器 | 方法 | 条件 |
|---|---|---|
| `BackendRegistration` | `RegisterDefaults()` | `BACKEND_SOFTWARE` 等编译常量 |
| `PlatformRegistration` | `RegisterDefaults()` | `PLATFORM_WIN32` 等编译常量 |

应用代码通常不需要手动调用这些方法。仅在自定义后端或平台时才需要额外注册。

---

## 14. 事件签名约定

SQX 中的事件处理方法支持（由 Source Generator 适配）：

```csharp
private void OnClick() { }
private void OnClick(Event e) { }
```

事件名映射规则：DOM 风格小写 → SQX `on` 前缀 + PascalCase。例如 `click` → `onClick`、`textinput` → `onTextInput`、`requestframe` → `onRequestFrame`。

---

## 15. 命名空间速查

| 命名空间 | 主要类型 |
|---|---|
| `Square.Hosting` | `DesktopApplication` |
| `Square.Runtime` | `Application`, `Dispatcher`, `IComponentLifecycle` |
| `Square.Runtime.Binding` | `ObservableValue<T>`, `ObservableCollection<T>`, `PropAttribute` |
| `Square.Runtime.Signals` | `Signal<T>`, `SignalHub` |
| `Square.Events` | `EventTarget`, `Event`, `EventInit`, `EventPhase`, `StandardEvents`, `FrameRequestEvent` |
| `Square.Directives` | `SqxDirectiveAttribute`（编译期指令发现） |
| `Square.UI` | `Node`, `Element`, `UIElement`, `ElementState`, `Document`, `UIDocument`, `UIRootElement`, `UIHeadElement`, `UIBodyElement`, `HTMLElement`, `SVGElement`, `SlotCollection`, `RenderFragment` |
| `Square.UI.ElementApi` | `StyleAccessor`, `ClassListAccessor`, `ChildrenCollection` |
| `Square.UI.Properties` | `PropertyStore` |
| `Square.Controls.Controls` | `View`, `Text`, `ListItem`, `Link`, `Button`, `Input`, `TextArea`, `CheckBox`, `Radio`, `Select`, `Image`, `Canvas` |
| `Square.Controls.Primitives` | `ShowNode`, `ForNode`, `SwitchNode` |
| `Square.Graphics` | `IRenderContext`, `Color`, `Rect`, `Size`, `Point`, `Brush`, `Pen`, `Font`, `PathGeometry`, `TextLayout`, `RenderBackendRegistry` |
| `Square.Rendering` | `LayoutEngine`, `ComputedStyle`, `DisplayMode`, `FlexDirection`, `DisplayTree`, `DisplayNode` |
| `Square.Platform` | `IPlatformHost`, `IPlatformFactory`, `PlatformHostCreateInfo`, `PlatformRegistry`, `PlatformRegistration` |
| `Square.Router` | `Router`, `RouteContext`, `RouteDefinition`, `Link`, `INavigationHistory` |
| `Square.Controls.Animation` | `Animation<T>`, `Clock`, `Easing` |
