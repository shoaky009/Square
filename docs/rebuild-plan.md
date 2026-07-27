# Square 架构重建计划（Rebuild）

> 分支：`rebuild`（已合并至 `main`）  
> 状态：已完成  
> 原则：**严格参考 Web API / MDN DOM**；大刀阔斧改架构，不为兼容旧 Visual/Routed 模型保留别名（除非实现期短暂适配）

> 后续增量说明：原计划中的 SVG 占位已经实现为 `XMLDocument -> SVGDocument`、`Square.UI.Svg` 元素树及 SQX/SQV 内联 SVG。本文中“SVGDocument（后）”“SVGElement 占位”等表述记录的是重建周期当时的范围，当前状态以 `design.md`、`Architecture.md` 和 `Roadmap.md` 为准。

配套：`Architecture.md`、`Rendering.md`、`API-Reference.md`、`plan.md`、`design.md`  
本文件是 **重建范围与实现规格** 的权威说明。

---

## 0. 目标与非目标

### 0.1 目标

1. 去掉 **`Visual` 类型与术语**，按 DOM 重建文档模型与渲染管线。  
2. 事件系统 **完全兼容 Web API**（`EventTarget` / `Event` / `addEventListener` / `dispatchEvent` + 捕获/冒泡）。  
3. 引入 **`Document` / `UIDocument`**，`documentElement` 只读，HTML 式 **UI / Head / Body** 壳。  
4. 为后续 **HTMLElement / SVGElement**、自定义标题栏、完整 DOM 子集预留扩展点。  
5. **自定义指令 SDK**：结构原语（Show/For/Slot/Switch/Route 等）由特性声明，Source Generator **编译期扫描** 集成，去掉硬编码特判。  
6. 保留现有产品能力不回归：控件 `Text`、`.sqx` 生成、布局/绘制/平台宿主。

### 0.2 非目标（本重建周期不做或仅占位）

| 项 | 说明 |
|----|------|
| 删除控件 `Text` / 文本并入 `View` | **不删**；`Text : UIElement` 保留 |
| HTML/SVG 解析与真实绘制 | 仅 abstract 占位 |
| DOM `Text` / `CharacterData` 文本节点 | 后置 |
| List / Label / Group 产品行为 | 后置 |
| 完整 `PointerEvent`/`KeyboardEvent` 字段 | 事件层先基类 `Event`，字段第二步 |
| Shadow DOM / composed 路径 | 字段预留，不实装 |
| DisplayTree 细粒度增量同步 | 本轮可全量 `BuildFrom` |
| 自定义标题栏完整 WinUI 级实现 | 模型预留 Head；本轮仅 Title 同步系统栏 |

---

## 1. 目标类型树

```
EventTarget
  └── Node
        ├── Document
        │     └── UIDocument
        │           （后）HTMLDocument
        │           （后）SVGDocument  — documentElement = <svg>，body = null
        │
        └── Element
              ├── UIElement
              │     ├── UIRootElement    // TagName "UI"  — documentElement
              │     ├── UIHeadElement    // TagName "Head"
              │     ├── UIBodyElement    // TagName "Body" — 窗口客户区
              │     ├── View
              │     ├── Text             // 控件，保留
              │     ├── ListItem         // 类似 li
              │     ├── Link             // 控件类似 a；Router.Link 继承之
              │     ├── Button, Input, TextArea
              │     ├── CheckBox, Radio, Select
              │     ├── Image, Canvas
              │     └── （后）List, Label, Group
              ├── HTMLElement            // abstract 占位
              └── SVGElement             // abstract 占位
```

要点（MDN）：

- `Document` 与 `Element` **并列**挂在 `Node` 下，**不是** `Element` 子类。  
- `EventTarget` 为事件根；树与布局/绘制分层。

---

## 2. 文档模型（严格 Web API）

### 2.1 `documentElement`（只读）

对齐 [Document.documentElement](https://developer.mozilla.org/en-US/docs/Web/API/Document/documentElement)：

- **只读**  
- 返回文档的根元素（Document 下的那个元素子节点）  
- **UI 文档**：始终为 **`<UI>`**（`UIRootElement`）  
- **禁止**业务代码 `set DocumentElement`  
- 换页面内容：操作 **`Body`** 的子节点，而不是替换 documentElement  

### 2.2 HTML vs SVG 文档形态

| 文档类型 | documentElement | body | 视口/工作区 |
|----------|-----------------|------|-------------|
| UIDocument（本轮） | `<UI>` | 有，= 客户区 | Body |
| HTMLDocument（后） | `<html>` | 有 | body |
| SVGDocument（后） | 最外层 `<svg>` | **null** | 根 svg 自身（viewBox 等） |

**结论**：桌面窗口 UI 走 **HTML 壳模型**，不走「根即 body」的伪 SVG 模型。

### 2.3 UI 文档壳结构

```
UIDocument
  └── documentElement = <UI>     // 只读
        ├── Head                   // 元数据 / 标题栏扩展点
        │     └── Title（可选元素）或 document.Title
        └── Body                   // 窗口工作区（client area）
              └── 应用内容（Main 等）
```

| Web | Square | 宿主含义 |
|-----|--------|----------|
| `<html>` | `<UI>` / `UIRootElement` | 文档根，只读 `documentElement` |
| `<head>` | `Head` | 默认不布局不绘制；扩展自定义标题栏 |
| `document.title` / `<title>` | `Title` 属性或 Head 子节点 | → 平台窗口标题 |
| `<body>` | `Body` | 客户区；布局 / 命中 / 绘制主内容入口 |

**`Body ≠ DocumentElement`。**

### 2.4 标题栏策略（对标 WinUI）

| 阶段 | 行为 | 对标 |
|------|------|------|
| **本轮 A（最小）** | `document.Title` → 系统标题栏；Head 高度 0，不参与布局 | WinUI 轻改 `Window.Title` |
| **后置 B** | `ExtendsContentIntoTitleBar` 式：Head 挂 TitleBar；拖拽区 + Passthrough + caption inset | WinUI 完整自定义标题栏 |

WinUI 参考能力（后置实现时对照）：

- `ExtendsContentIntoTitleBar` / `AppWindowTitleBar`  
- `LeftInset` / `RightInset`（系统 caption 留白）  
- `InputNonClientPointerSource.SetRegionRects(Passthrough)`（可点击 vs 拖拽）  
- `PreferredHeightOption.Tall`  

### 2.5 宿主入口

```csharp
var doc = new UIDocument();       // 内部固定创建 UI / Head / Body
doc.Title = "Square Framework";
doc.Body.AppendChild(new Main());
new DesktopApplication(doc, hostInfo).Run();
```

```csharp
public DesktopApplication(UIDocument document, PlatformHostCreateInfo info)
```

- 布局：Body 使用平台 `ClientSize`  
- 命中：从 Body（Head 高度 0 时与从 UI 等价）  
- 事件冒泡：… → Body → UI → **Document**  

---

## 3. 去掉 Visual：命名表

| 旧 | 新 |
|----|-----|
| `Visual`（类型） | `Element` |
| Visual Tree | Element Tree |
| `BuildVisualTree` | `BuildElementTree` |
| `InvalidateVisual` | `InvalidatePaint` |
| `IsVisualDirty` / `ClearVisualDirty` | `NeedsPaint` / `ClearPaintDirty` |
| `VisualState` | `ElementState` |
| `Render(...)`（元素绘制） | `Paint(...)` |
| `RenderNode.Visual` | `DisplayNode.Source`（`Element?`） |
| `RenderTree`（建议） | `DisplayTree` |

**不得误替换**：

- Markup `SqxDocument`  
- `Square.Text` 引擎、`TextLayout`、`TextArea`、`SqxText`  
- 普通英文 “text” 用词  

**不保留** 公开类型别名 `using Visual = Element`（硬切）。

---

## 4. 职责切分

### 4.1 `EventTarget`（`Square.Events` / Runtime）

见 **§5 事件系统**。仅负责监听与派发。

### 4.2 `Node`（`Square.UI`）

- `Parent`、`ChildNodes` / `Children`、`OwnerDocument`  
- `AppendChild` / `InsertBefore` / `RemoveChild` / `ReplaceChild`  
- `GetEventParent()`：`Parent ?? OwnerDocument`  
- **不** 持有 Geometry / Style / Paint  

`ChildrenCollection`：本轮以 `IList<Element>` 为主；Document 子节点规则特殊（通常仅 documentElement 一个元素子）。

### 4.3 `Element`

- `TagName`、`Id`、`ClassList`、`Style`、`Properties`  
- `NamespaceURI`（预留）  
- `Geometry`、`IsVisible`、`ZIndex`、`ElementState`  
- `NeedsLayout` / `NeedsPaint`、失效 API  
- `Measure` / `Arrange` / `Paint` / `HitTest` / `Query*`  
- `BuildElementTree`、绑定、生命周期  

### 4.4 `UIElement`

- Slot、宽高边距、对齐、Focus/Blur、`IsDisabled`  
- 现有控件全部 `: UIElement`  

### 4.5 壳元素

```csharp
UIRootElement : UIElement   // TagName "UI"
UIHeadElement : UIElement   // TagName "Head"
UIBodyElement : UIElement   // TagName "Body"
```

### 4.6 `Document` / `UIDocument`（接近 DOM 子集）

| API | 说明 |
|-----|------|
| `DocumentElement` | 只读 |
| `Body` / `Head` | UIDocument 便捷属性 |
| `Title` | 读写，同步宿主 |
| `CreateElement(string tagName)` | 内置注册表，AOT 友好；未知标签抛错 |
| `CreateElement<T>() where T : Element, new()` | 强类型工厂 |
| `GetElementById` | |
| `GetElementsByClassName` / `GetElementsByTagName` | |
| `QuerySelector` / `QuerySelectorAll` | 子集：tag、`.class`、`#id`、后代、`>` |
| `Query` / `QueryAll` 强类型 | 保留 |
| `AdoptNode` / `ImportNode` | 最小 |
| 样式表入口 | 挂 `CssEngine` |

**AOT 注册**（禁止运行时扫描程序集）：

```csharp
UIDocument.RegisterElement("View", static () => new View());
UIDocument.RegisterElement("Text", static () => new Text());
// Controls 模块 RegisterDefaults()
```

### 4.7 占位

```csharp
public abstract class HTMLElement : Element { }
public abstract class SVGElement : Element { }
```

---

## 5. 事件系统（完全兼容 Web API）

### 5.1 现状问题

当前为 WPF 风格：

- `RoutedEvent` / `RaiseEvent` / `Handled` / `RoutingStrategy`（Tunnel/Bubble）  
- 监听器 `(sender, args)`  
- 空 `IEventTarget`  

与 DOM 不兼容，必须硬切。

### 5.2 目标 API

#### EventTarget

```csharp
public class EventTarget
{
    public void AddEventListener(string type, EventListener listener, AddEventListenerOptions? options = null);
    public void AddEventListener(string type, EventListener listener, bool useCapture);
    // 便捷：Action / Action<Event> / Action<TEvent>

    public void RemoveEventListener(string type, EventListener listener, EventListenerOptions? options = null);
    public void RemoveEventListener(string type, EventListener listener, bool useCapture);

    /// <returns>false if cancelable && preventDefault was called; otherwise true</returns>
    public bool DispatchEvent(Event e);

    protected virtual EventTarget? GetEventParent() => null;
}

public delegate void EventListener(Event e);

public interface IEventListener
{
    void HandleEvent(Event e);
}

public sealed class AddEventListenerOptions
{
    public bool Capture { get; init; }
    public bool Once { get; init; }
    public bool Passive { get; init; }
    public CancellationToken Signal { get; init; }  // 最小；完整 AbortSignal 可后置
}
```

监听器身份键：`(type, listener, capture)` — 与 DOM 一致。

#### Event

```csharp
public class Event
{
    public Event(string type, EventInit? init = null);

    public string Type { get; }
    public EventTarget? Target { get; }
    public EventTarget? CurrentTarget { get; }
    public EventPhase EventPhase { get; }
    public bool Bubbles { get; }
    public bool Cancelable { get; }
    public bool Composed { get; }          // 预留
    public bool DefaultPrevented { get; }
    public bool IsTrusted { get; }
    public double TimeStamp { get; }

    public void PreventDefault();
    public void StopPropagation();
    public void StopImmediatePropagation();
    public IReadOnlyList<EventTarget> ComposedPath();
}

public enum EventPhase
{
    None = 0,
    CapturingPhase = 1,
    AtTarget = 2,
    BubblingPhase = 3
}

public sealed class EventInit
{
    public bool Bubbles { get; init; }
    public bool Cancelable { get; init; }
    public bool Composed { get; init; }
}
```

#### dispatchEvent 算法（DOM 简化）

1. `type` 未指定 → 抛异常（对标 InvalidStateError）  
2. 脚本派发：`IsTrusted = false`；平台输入：内部 `DispatchTrusted` → `true`  
3. `Target = this`  
4. 建 path：`this → GetEventParent() → …`  
5. **捕获**：从根侧到 target 之前，调用 `capture=true` 监听器，`CAPTURING_PHASE`  
6. **目标**：`AT_TARGET` — 先 capture 监听器，再 bubble 监听器  
7. **冒泡**：若 `Bubbles`，沿 path 向上，仅 non-capture，`BUBBLING_PHASE`  
8. `stopPropagation` / `stopImmediatePropagation` 中断  
9. 返回 `!(Cancelable && DefaultPrevented)`  
10. 若可取消且未 preventDefault → 可选 `OnDefaultAction`  

#### 旧 API 映射（删除）

| 旧 | 新 |
|----|-----|
| `RaiseEvent` / `RouteEvent` | `DispatchEvent` |
| `RoutedEventArgs` | `Event` |
| `Handled` | `StopPropagation` / `StopImmediatePropagation` |
| `OriginalSource` / `Source` | `Target` |
| `RoutingStrategy` / `RoutedEvent<T>` / `EventDefinition` | 删除；用 `string type` + `Event` 子类 |
| `IEventTarget` | `EventTarget` |
| `RemoveEventListener(string)` 无 handler | 删除（非 Web API） |

### 5.3 标准事件默认 bubbles / cancelable

| type | bubbles | cancelable | 备注 |
|------|---------|------------|------|
| `pointerdown` / `up` / `move` | true | true | 子类字段后置 |
| `wheel` | true | true | |
| `keydown` / `keyup` | true | true | |
| `click` | true | true | |
| `input` / `change` | true | 按 HTML | |
| `focus` / `blur` | **false** | false | 不冒泡 |
| `focusin` / `focusout` | **true** | false | 冒泡 |
| `requestframe` | true | false | **框架扩展**，非标准 DOM，文档标明 |

`Focus()` / `Unfocus()`：

- 在焦点元素上 `DispatchEvent(focus)` + `DispatchEvent(focusin)`（冒泡自动到祖先）  
- **删除** 手工 for-parent 循环 Raise  

### 5.4 本轮事件明确不做

- 完整 Pointer/Keyboard/Mouse 字段  
- Shadow / composed 裁剪  
- 完整 `AbortController`（可用 `CancellationToken`）  
- `onclick` 属性风格  
- 异步事件队列（`dispatchEvent` 保持同步）  

本轮 **应实现**：`capture`、`once`、`passive`；`signal` 可选最小。

---

## 6. 渲染架构

### 6.1 管线

```
.sqx → SourceGenerator → Element 子树（挂于 Body）
         │
         ▼
   Style（Document sheets + 组件 sheets）
         │
         ▼
   LayoutEngine
         │  UI：铺满窗口表面
         │  Head：本轮高度 0
         │  Body：ClientSize
         │  （后）SVG：不同 LayoutMode
         ▼
   DisplayTree / DisplayNode（Source : Element?）
         │  脏节点 Paint → DrawCommand 列表
         ▼
   IRenderContext → Backend
```

### 6.2 Display 层

```
DisplayTree
  └── DisplayNode
        Element? Source
        Rect Bounds
        List<DrawCommand> Commands
        List<DisplayNode> Children
        bool NeedsRepaint
```

- 保留模式：脏节点重建命令列表  
- `CommandCollector` 继续实现 `IRenderContext`  
- 本轮同步：可全量 `BuildFrom`  

### 6.3 扩展点（后置）

| 扩展点 | 用途 |
|--------|------|
| `LayoutMode` / 虚 Measure·Arrange | SVG 不用 CSS 盒 |
| `NamespaceURI` / `TagName` | 选择器与多文档 |
| `Paint` 虚方法 | SVG/HTML 不同绘制 |
| DrawCommand Path/Transform/Clip | 已具备，SVG 可复用 |

---

## 7. 控件与生成器

### 7.1 控件

- 全部保持 `: UIElement`  
- **保留 `Text`**（`TextContent` / `Color` / `FontSize`）  
- `Paint` 替换 `Render`  
- `InvalidatePaint` 替换 `InvalidateVisual`  

### 7.2 Source Generator

- 组件：`partial class X : UIElement`  
- `BuildElementTree` 替换 `BuildVisualTree`  
- `ApplyComponentStyles(Element root)`  
- 事件：`AddEventListener("click", Method)`；Method 支持 `()` 或 `(Event e)`  
- 内置控件标签表（View/Text/Button…）与 `CreateElement` 注册表对齐  
- **结构原语** 不再硬编码：走 **§8 自定义指令 SDK**  

---

## 8. 自定义指令 SDK（Directive SDK）

### 8.1 目标

把写死在 Source Generator 中的结构原语抽成可发现 SDK：

| 标签 | 现状硬编码位置 | 运行时 |
|------|----------------|--------|
| `Show` | Parser + `EmitShow` | `ShowNode` |
| `For` | Parser + `EmitFor` | `ForNode` |
| `Switch` / `Match` | Parser + `EmitSwitch` | `SwitchNode` |
| `Slot` / `Outlet` | Parser + `EmitSlot` | `SlotCollection` |
| `Router` / `Route` | Parser + `EmitRouter` / `EmitRouteDefinition` | `Router` / `RouteDefinition` |

决策（已锁定）：

| 项 | 选择 |
|----|------|
| 发现机制 | **`[SqxDirective]` 特性 + SG 编译期扫描引用程序集元数据** |
| 能力边界 | **结构原语为主**（不负责 View/Button 等控件映射） |
| 与 rebuild | **并入本总计划**，非独立优先于 DOM |

原则：

1. **Compile First / AOT**：只读编译期元数据；**零运行时反射发现**  
2. **指令 ≠ 控件**：控件继续内置标签表 / `CreateElement`  
3. **内置与扩展同一通道**  
4. 发射默认用 **声明式 EmitSpec/Pattern**（避免 SG 执行任意用户代码）；复杂逻辑仅框架白名单 `IDirectiveEmitter`  

### 8.2 现状问题

- `TemplateParser`：`tagName == "Show"|"For"|…`  
- `SqxNodeKind`：每原语一个枚举值  
- `ComponentEmitter`：专用 `EmitShow` / `EmitFor` / …  
- 字段 `_showN` / `_forN` / `_switchN` 计数写死  

扩展新原语 = 改 Parser + Kind + Emitter + 文档，无法模块级扩展。

### 8.3 架构

```
[编译期]
  引用程序集（Controls / Router / 用户包）
       │  类型标记 [SqxDirective("For", …)]
       ▼
  SqxGenerator 扫描 Metadata → DirectiveCatalog
       ▼
  TemplateParser：tag ∈ catalog ⇒ Directive 节点
       ▼
  ComponentEmitter：catalog → 通用 Emit 模板 / 白名单 Emitter
       ▼
  生成 C#（调用运行时 ShowNode / ForNode / …）

[运行时]
  原语执行体仍在 Controls.Primitives / Router / UI
  （指令描述「如何生成」+「依赖哪些运行时类型」）
```

### 8.4 SDK 表面

建议位置：`Square.Directives` 独立包，或暂放 `Square.Runtime` / `Square.UI` 的 `Directives` 命名空间（实现时二选一，优先可被 Controls/Router 引用且 SG 能扫到）。

#### 特性

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SqxDirectiveAttribute : Attribute
{
    public SqxDirectiveAttribute(string tagName) => TagName = tagName;

    public string TagName { get; }
    public string[] Aliases { get; set; } = [];           // 如 Outlet → Slot
    public string? ParentTag { get; set; }               // Match 仅 Switch 下
    public string[] AllowedChildTags { get; set; } = [];
    public bool SkipStandaloneEmit { get; set; }         // Route 仅在 Router 内
    public DirectiveEmitPattern Pattern { get; set; }
    // Spec：Runtime 类型全名、字段前缀、关键属性 when/each/name/path …
}

public enum DirectiveEmitPattern
{
    ControlFlowAttach,  // Show/For/Switch：字段 + new Runtime + AttachTo
    SlotOutlet,         // Slot：Slots.Render + fallback
    RouterTree,         // 历史方案，现已删除
    CustomSource        // 仅框架白名单 IDirectiveEmitter
}
```

可选：`[assembly: SqxDirectiveAssembly]` 缩小扫描范围。

#### SG 内部描述符

```csharp
sealed record DirectiveDescriptor(
    string TagName,
    ImmutableArray<string> Aliases,
    string? ParentTag,
    ImmutableArray<string> AllowedChildTags,
    bool SkipStandaloneEmit,
    DirectiveEmitPattern Pattern,
    DirectiveEmitSpec Spec);

// DirectiveCatalog：TryGet(tag) → descriptor；别名归一到主 Tag
```

#### 发射器（高级，框架白名单）

```csharp
// 仅 Source Generator 进程 / 框架程序集
interface IDirectiveEmitter
{
    void Emit(DirectiveEmitContext ctx);
}
```

第三方默认只提供特性 + Spec；**不**默认允许任意 DLL 在 SG 内执行。

### 8.5 编译期扫描

`SqxGenerator.Initialize`：

```
CompilationProvider
  → SourceModule + ReferencedAssemblySymbols
  → 带 SqxDirectiveAttribute 的类型
  → DirectiveCatalog（Immutable，进 incremental 缓存键）
  → 与 AdditionalTexts(.sqx) Combine → Generate
```

- 只扫 **当前编译引用** 到的程序集  
- 重复 Tag → `SQX_D001_DuplicateDirective`  
- catalog 变更必须触发重新生成  

### 8.6 Parser / AST 改造

去掉硬编码 `if (tagName == "Show")`：

```csharp
enum SqxNodeKind { Element, Text, Expression, Directive }

// SqxElement
//   TagName, DirectiveId（别名解析后主 tag）
```

- `catalog.IsDirective(tag)` → `Kind = Directive`，`DirectiveId` 归一  
- 父子校验：`ParentTag`、`AllowedChildTags`、`SkipStandaloneEmit`  

### 8.7 Emitter 改造

```csharp
if (element.Kind == SqxNodeKind.Directive)
{
    var d = catalog.Get(element.DirectiveId!);
    if (d.SkipStandaloneEmit) continue;
    DirectiveEmitPipeline.Emit(d, element, ctx);
}
```

| 指令 | Pattern | 生成要点（与现逻辑等价） |
|------|---------|--------------------------|
| Show | ControlFlowAttach | `_showN`；`new ShowNode(when, factory)`；`AttachTo` |
| For | ControlFlowAttach | `ForNode.Create(each, it => …)`；localName=`it` |
| Switch | ControlFlowAttach | `SwitchNode` + 子 Match `AddBranch`/`AddDefault` |
| Match | 子处理 | 不单独 emit |
| Slot / Outlet | SlotOutlet | `Slots.Render` + fallback |
| Router | RouterTree | 历史方案，已由 `AppWindow.UseRouter` + `RouterView` 替代 |
| Route | 嵌套 | path / component 静态 factory |

字段计数：`FieldPrefix` + 通用递增，删除专用 counter。

### 8.8 内置指令迁移

| 标签 | 别名 | 运行时 | 建议标注位置 |
|------|------|--------|--------------|
| Show | | `ShowNode` | Controls.Primitives |
| For | | `ForNode` | Controls.Primitives |
| Switch | | `SwitchNode` | Controls.Primitives |
| Match | Parent=Switch | — | 同上 |
| Slot | Outlet | Slot API | Square.UI |
| Router | | 历史类型，已删除 | Square.Extensions.Routing |
| Route | SkipStandalone | 历史语法，已删除 | Square.Extensions.Routing |

迁移完成后 **删除** Parser/Emitter 专用分支。

### 8.9 诊断

| Id | 含义 |
|----|------|
| SQX_D001 | 重复指令 Tag |
| SQX_D002 | 缺少必需属性（如 For 缺 each） |
| SQX_D003 | 子指令父标签不匹配 |
| SQX_D004 | 无法解析 Pattern / Runtime 类型名 |
| SQX_D005 | SkipStandalone 指令出现在非法位置 |

### 8.10 实施子阶段（D0–D4）

| 阶段 | 内容 | 依赖 |
|------|------|------|
| **D0** | 特性、Descriptor、Catalog 扫描、诊断骨架 | 可独立 |
| **D1** | Parser `Directive` 节点 + 内置指令元数据标注 | D0 |
| **D2** | 通用 Emit 模板覆盖 Show/For/Switch/Slot/Router；行为对齐现测试 | D1；可与 P1 后并行 |
| **D3** | 删硬编码；文档；可选 demo 指令验证扫描 | D2 |
| **D4** | 与 P2 命名对齐（`Element` / `BuildElementTree` 生成串） | P2 |

建议：在 **P1 事件之后** 做 D0–D2；与 Element 重命名同步改生成字符串，避免两轮大改。

### 8.11 指令 SDK 验收 ✅ 全部通过

- [x] Parser/Emitter **无** `if (tag == "Show")` 等硬编码原语表  
- [x] 内置原语行为与现 sample/测试一致  
- [x] 引用程序集 + 特性即可让 SG 识别指令（含至少一个扫描验证路径）  
- [x] 重复 Tag 有诊断  
- [x] 生成代码为静态调用，无运行时指令发现  
- [x] 与 rebuild 其余 API 命名一致  

### 8.12 风险

| 风险 | 缓解 |
|------|------|
| SG 反射执行用户 Emitter | 声明式 Spec + 框架白名单 |
| Catalog 缓存不失效 | catalog 进入 incremental key |
| Outlet/Slot 别名 | 解析期归一 `DirectiveId` |
| Router/Route 嵌套 | 历史方案；当前使用嵌套 `RouterView` |

---

## 9. 实施阶段

### P0 — 文档 ✅ 已完成

- [x] 本文件 `docs/rebuild-plan.md`  
- [x] §8 自定义指令 SDK  
- [x] 更新 `API-Reference` / `README` / Getting-Started / Sqx-Spec / Architecture / Rendering / Generator 等与代码一致  


### P1 — 事件 ✅ 已完成

1. [x] 新建 `EventTarget`、`Event`、`EventInit`、`EventPhase`、`AddEventListenerOptions`  
2. [x] 删除 `IEventTarget`、`RoutedEvent*`、`RoutingStrategy`、`EventDefinition`  
3. [x] `Element` 继承 `EventTarget`  
4. [x] 全库 `RaiseEvent` → `DispatchEvent`  
5. [x] Focus 标准 focus/focusin  
6. [x] 单测通过  
7. [x] 生成器与 sample 事件绑定  

### P1.5 / D0–D4 — 指令 SDK ✅ 已完成

- [x] **D0** `[SqxDirective]` + `DirectiveCatalog` + BuiltIn 表 + 编译期扫描骨架  
- [x] **D1** Parser 经 Catalog 解析指令（含别名）  
- [x] **D2** `DirectiveEmitPipeline` 通用模板；`ComponentEmitter` 经 Catalog 分发（无 EmitShow/EmitFor/…）  
- [x] **D3** `SqxNodeKind.Directive` + `DirectiveId`；诊断 SQXD001–005；父子/必需属性校验  
- [x] **D4** 与 P2 命名对齐（`Element` / `BuildElementTree` 生成串）


### P2 — 树与 Element（去掉 Visual）✅ 已完成

1. [x] `Element` 替代 `Visual`；[x] `EventTarget → Node → Element|Document` 分叉  
2. [x] 全库类型与术语替换  
3. [x] `UIElement : Element`  
4. [x] `BuildElementTree` / `Paint` / `NeedsPaint` / `ElementState`  
5. [x] CSS / Layout / Display / Hosting / Controls / Router  

### P3 — Document 子集 ✅ 已完成

1. [x] `Document` / `UIDocument`  
2. [x] 固定壳 UI/Head/Body；`DocumentElement` 只读  
3. [x] `CreateElement` + 注册表  
4. [x] id/class/tag 查询（QuerySelector 子集后置）  
5. [x] `Title`  
6. [x] `DesktopApplication(UIDocument)`  
7. [x] Sample 入口  

### P4 — 渲染 Display 层与占位 ✅ 已完成

1. [x] `DisplayTree` / `DisplayNode`（`Source` 属性别名）  
2. [x] `HTMLElement` / `SVGElement` abstract  
3. [x] 全量相关测试通过  
4. [x] 代码侧无公开 `Visual` / `RoutedEvent` / `RaiseEvent`  
5. [x] **D3 收尾**：SG 侧无 EmitShow 硬编码；AST 统一 Directive  

---

## 10. 主要触达程序集

| 程序集 | 改动 |
|--------|------|
| `Square.Runtime` | EventTarget、Event、标准事件元数据；（可选）Directives 特性） |
| `Square.UI` | Node、Element、Document、UIDocument、壳、Children、状态、Slot 指令元数据 |
| `Square.Rendering` | DisplayTree、Layout 入参 Element |
| `Square.CSS` | Element 树；可选 TagName 匹配 |
| `Square.Controls` | 注册表、Paint、InvalidatePaint、Show/For/Switch 指令元数据 |
| `Square.Hosting` | UIDocument 主循环、Title 同步 |
| `Square.Extensions.Routing` | 当前已替代此历史方案：UseRouter、RouterView、RouterLink、守卫与 KeepAlive |
| `Square.Compiler` | BuildElementTree、事件签名、**DirectiveCatalog + Emit 管线** |
| `Square.Directives`（可选新建） | `SqxDirectiveAttribute`、Pattern/Spec 公共类型 |
| tests / samples / docs | 同步 |

**尽量不改**：`Square.Text` 光栅核心、Backend 像素实现（仅类型边界）。

---

## 11. 验收清单

### 文档树

- [x] `EventTarget → Node → Document|Element` 分叉正确  
- [x] `DocumentElement` 只读，指向 `<UI>`  
- [x] `Body ≠ DocumentElement`；内容挂 Body  
- [x] `Title` 同步窗口标题  
- [x] 存在 `HTMLElement` / `SVGElement` 占位  

### 事件

- [x] 公开 API：`AddEventListener` / `RemoveEventListener` / `DispatchEvent` + `Event`  
- [x] 无 `RaiseEvent`、`RoutedEvent`、`RoutingStrategy`、`Handled`（代码）  
- [x] 捕获 + 冒泡 + stop* + preventDefault 符合 DOM  
- [x] focus/blur 不冒泡；focusin/focusout 冒泡  
- [x] once / passive 可用  

### 指令 SDK

- [x] Catalog + Emit 管线  
- [x] 诊断 SQXD001–005（父标签 / 必需属性 / 非法根级）

### 渲染与命名

- [x] 无公开类型 `Visual`  
- [x] 无 `BuildVisualTree` / `InvalidateVisual` / `IsVisualDirty` / `VisualState`  
- [x] 管线：Element Tree → Style → Layout → DisplayTree → Backend  
- [x] 控件 `Text` 与 sample 不回归  
- [x] 测试全绿  
- [x] 用户文档已去掉 `UIElement : Visual` 等旧签名（`rebuild-plan` 对照表除外）

### 指令 SDK

见 **§8.11**。

---

## 12. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 全局重命名误伤 Text 引擎 / SqxText | 按符号/类型替换；禁止盲目字符串 replace |
| 事件语义变化导致测试失败 | P1 专用事件测试先绿；再迁业务 |
| NativeAOT 与 CreateElement | 仅显式注册表，无反射扫描 |
| CSS 类型选择器依赖类名 | 控件类名不变则无感；逐步迁 TagName |
| 改动面过大 | 严格按 P1→P4 + D0–D4；每阶段可合并但逻辑分序 |
| SG 执行任意指令 Emitter | 声明式 Spec + 框架白名单 |
| 指令 Catalog 缓存失效 | 进入 incremental pipeline key |

---

## 13. 已锁定的默认决策

| 项 | 决策 |
|----|------|
| 基类链 | `EventTarget → Node → Element → UIElement`；Document 与 Element 分叉 |
| App 与 Document | **App 持有 UIDocument** |
| DocumentElement | **只读**，= `<UI>` |
| Body | **独立**，= 客户区，≠ documentElement |
| Head 本轮 | **A：不参与布局**，仅 Title |
| 标题栏完整自定义 | 后置（WinUI 级别 2） |
| Visual | **删除**，无别名 |
| 控件 Text | **保留** |
| 事件 | **硬切 Web API**，无 Routed 双轨长期并存 |
| Paint vs Render | 直接 `Paint` |
| Display 类名 | 建议 `DisplayTree` / `DisplayNode` |
| Children | `IList<Element>` 为主 |
| EventTarget 程序集 | `Square.Runtime`（Events） |
| once / passive | 本轮实现；signal 可选 |
| 指令发现 | **`[SqxDirective]` + 编译期元数据扫描** |
| 指令边界 | **结构原语为主**；控件不走指令 SDK |
| 指令发射 | **声明式 Pattern/Spec**；CustomSource 仅框架白名单 |
| 指令与 rebuild | **并入本计划**；D0–D2 建议在 P1 之后 |

---

## 14. 建议开工顺序（最短路径）

1. **P1 事件** — 独立、收益高、为 Node 提供基类  
2. **D0–D2 指令 SDK** — 去掉原语硬编码；Emit 可暂用旧类型名  
3. **P2 Element 重命名** — 去掉 Visual；**D4** 对齐生成串  
4. **P3 UIDocument 壳 + 宿主** — 入口与文档语义  
5. **P4 Display + 占位 + D3 收尾 + 文档/验收**  

每阶段结束：相关测试绿 + 简短 commit。

---

## 15. 参考

- [Document.documentElement](https://developer.mozilla.org/en-US/docs/Web/API/Document/documentElement)  
- [Document.body](https://developer.mozilla.org/en-US/docs/Web/API/Document/body)  
- [EventTarget](https://developer.mozilla.org/en-US/docs/Web/API/EventTarget)  
- [Event](https://developer.mozilla.org/en-US/docs/Web/API/Event)  
- [addEventListener](https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener)  
- [dispatchEvent](https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/dispatchEvent)  
- [SVGSVGElement](https://developer.mozilla.org/en-US/docs/Web/API/SVGSVGElement)  
- [WinUI Title bar customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar)  
- DOM Standard：https://dom.spec.whatwg.org/  

---

*本文档随 `rebuild` 分支演进；实现偏离时先改本文再改代码。*
