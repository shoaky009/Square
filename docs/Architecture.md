# Square Framework 总体架构

> Version: 0.2  
> 配套：`Requirements.md`（需求）、`Sqx-Spec.md`（语言规范）、`plan.md`（分阶段计划）

---

## 1. 定位与核心约束

Square 是 **纯 C#、编译优先（Compile First）、NativeAOT 优先、渲染后端可插拔** 的跨平台 UI 框架。

六大核心原则：

1. **Compile First** —— 所有 UI 在编译期生成 C#，运行时零解析。
2. **Pure C# Core** —— 框架核心全部 C# 实现。
3. **NativeAOT First** —— 禁用 `Reflection.Emit`、运行时代码生成、`Dynamic`、运行时加载程序集。
4. **Backend Independent** —— 核心不依赖具体图形库；图形库均为可插拔 Backend。
5. **Retained Rendering** —— Visual Tree + Render Tree，非 Immediate Mode。
6. **Low Coupling / IDE Friendly** —— 模块间通过抽象接口通信；`.sqx` 提供类型检查、智能补全、编译错误定位。

---

## 2. 总体管线（保留模式）

```
.sqx (template + style + script)
      │
      ▼
[Square.SourceGenerator] ──► C# 组件类型 (编译期)
      │
      ▼
  Component (C#)
      │
      ▼
 Visual Tree   (Square.UI / Runtime)
      │
      ▼
Layout Engine  (Square.Rendering, CSS 盒/flex/grid)
      │
      ▼
 Render Tree   (Square.Rendering, DrawCommand 列表)
      │
      ▼
 IRenderContext  (Square.Graphics 抽象)
      │
      ▼
 Backend  (Square.Backends: Software → Skia/Blend2D/Cairo)
```

- **非 Immediate Mode**：保留 Visual Tree + Render Tree，支持脏区增量重绘。
- **低耦合**：除 `Square.Backends` 与 `Square.Platform` 外，所有模块仅依赖抽象接口。
- **NativeAOT 合规**：组件类型在编译期生成，运行时无反射解析；属性系统使用生成代码与强类型委托。

---

## 3. 模块划分与职责

| 模块 | 职责 | 关键设计 |
|---|---|---|
| `Square.Markup` | `.sqx` 词法/语法解析 → AST | 含 template/script/style 三段；错误带行列号 |
| `Square.SourceGenerator` | Roslyn Incremental Generator，`.sqx`→C# | Props 解析、ref 字段生成、绑定/事件编译、结构原语特判、诊断映射 |
| `Square.Runtime` | `Application`、组件生命周期、调度、信号、路由事件 | UI Dispatcher；组件树挂载；线程安全的跨组件消息投递；`Square.Events` 命名空间下的路由事件协议与标准事件目录 |
| `Square.UI` | 视觉基类型、属性、Visual Tree 节点 | 强类型属性（生成代码）；元素操作 API（Style/ClassList/Children/Event） |
| `Square.Controls` | 控件 + 结构原语 + 基础动画 | 控件 = 视觉 + 行为 + 默认样式；`Square.Controls.Animation` 命名空间下的时钟、缓动和属性动画 |
| `Square.Router` | 路由匹配、内存历史与路由控件 | 静态 RouteDefinition、参数/通配符、Link、嵌套布局；不依赖 Platform |
| `Square.CSS` | CSS 引擎 | Selector/Cascade/Specificity/Var/Inheritance；M1 子集 |
| `Square.Graphics` | `IRenderContext` 抽象 + 绘图原语 | 工厂 `IRenderBackendFactory`；原语 Geometry/Brush/Pen/Font/Path/Transform/Clip |
| `Square.Rendering` | Visual→Layout→Render Tree→DrawCommand | `Square.Rendering` 命名空间下的 Box/Flex/Grid 布局；保留模式、脏区/增量 |
| `Square.Text` | 文本引擎 | Unicode/Glyph/Font/Layout/Caret/Selection/HitTest/BiDi |
| `Square.Platform` | 平台宿主抽象 | `IPlatformHost`：窗口/消息循环/输入泵；`LibraryImport` 源生成；现含 Win32 与 X11 两个实现，按构建层 `PLATFORM_*` 裁剪 |
| `Square.Backends` | 渲染后端 | 纯 C# Software Renderer → Skia/Blend2D/Cairo |
| `Square.Hosting` | 桌面应用宿主 | `DesktopApplication`：聚合 Runtime/UI/Controls/Rendering/Platform/Backends，统一处理窗口、输入路由、焦点管理、文本编辑、剪贴板、帧调度和布局渲染循环 |

**依赖方向**：`SourceGenerator` → `Markup`；`Events` 保持平台与 UI 无关；`UI` → `Events`；`Controls/UI/Rendering/CSS/Text` → `Runtime` + `Graphics`（按实际需要引用）；`Backends`/`Platform` → 底层图形抽象。核心层禁止反向依赖 Backend/Platform。`Square.Hosting` 是聚合层，为应用提供开箱即用的桌面输入、调度、布局和渲染管线。

---

## 4. 组件模型

### 4.1 组件 = 模板 + 逻辑 + 样式

`.sqx` 使用无文件级根标签的顶级 section：

```
<template>   结构 + 绑定 + 流程控制
<script lang="csharp">  C# 逻辑 + Props 声明 + 文件级元数据
<style>  CSS 样式
```

`<template>` 必须且只能有一个；`<script>`、`<style>` 可选且各自最多一个。Source Generator 将三个 section 编译为同一个 `partial` 组件类。组件名默认取文件名，文件级元数据声明在 `<script>` 标签属性上。

### 4.2 Props（组件输入契约）

- 声明：`<script lang="csharp">` 中 `[Prop]` 特性
- 类型：`ObservableValue<T>`（生成器辅助包装）
- 数据流：父→子单向，子不可改写
- 响应：子组件订阅 prop 或重写 `OnPropChanged`
- 校验：编译期 Generator 检查必填 prop
- 内置元素属性与自定义组件 Props 共用机制

详见 `Sqx-Spec.md` §Props。

### 4.3 绑定模型

- `ObservableValue<T>`：强类型、委托订阅、零反射
- `ObservableCollection<T>`：列表原语，支撑 `<For>`
- 绑定语法：`{expr}`（文本/属性/事件/流程控制同源）
- 双向：`value={expr} onInput={Method}` 显式表达

详见 `Sqx-Spec.md` §绑定。

### 4.4 结构化流程控制

| 原语 | 用途 |
|---|---|
| `<Show when={expr}>` | 条件子树 |
| `<For each={expr}>` | 列表 |
| `<Switch>` + `<Match when={expr}>` | 多分支 |
| `<Index each={expr}>` | 索引列表（可选） |

编译为 `ObservableValue`/`ObservableCollection` 驱动的细粒度控制流，无虚拟 DOM。

详见 `Sqx-Spec.md` §流程控制。

### 4.5 生命周期钩子

| 钩子 | 触发时机 |
|---|---|
| `OnPropChanged(name)` | Props 值变化 |
| `OnAttached` | 挂载到视觉树 |
| `OnDetached` | 从视觉树卸载 |
| `OnLoaded` | 加载完成 |
| `OnUnloaded` | 卸载完成 |
| `OnMeasure` | 布局测量 |
| `OnArrange` | 布局排列 |
| `OnStart` / `OnExit` | 应用级 |

### 4.6 组件内容与插槽

- 调用处 children 编译为调用方作用域内的 `RenderFragment`。
- `<Slot>` 是生成器结构节点，不产生额外布局容器。
- 默认、具名与 fallback 内容由 `SlotOutlet` 管理为连续子节点区域。
- 嵌套路由布局复用默认 Slot；`Outlet` 只是路由语义别名。

### 4.7 路由

`Square.Router` 位于 UI/Controls 之上、Platform 之下无依赖。桌面默认使用 `MemoryNavigationHistory`，未来平台可提供 URL/系统导航适配器。路由页面类型由 Source Generator 生成静态构造委托，满足 NativeAOT 的零反射约束。

路由切换通过 `ChildrenCollection` 替换当前分支，因此沿用视觉树生命周期和布局失效机制。静态段、参数段、通配符的匹配顺序确定，不进行运行时路由程序集发现。

### 4.8 Tabs 组合组件

`Tabs` 不引入新的结构原语。调用方将页签按钮投影到 `tabs` 命名 Slot，将对应页面投影到默认 Slot；组件按索引维护按钮选中状态和页面可见性。页签与页面一一对应，Slot 不产生额外布局节点。

### 4.9 跨组件信号与线程切换

- `ObservableValue<T>` 继续承担组件局部属性绑定，不保证跨线程访问。
- `Signal<T>` 是线程安全的状态信号；发布时对订阅者使用快照，允许订阅者在回调中取消订阅。
- `SignalHub` 按名称共享强类型信号。相同名称只能绑定一种 `T`，类型冲突立即抛错。
- 未指定 `Dispatcher` 的订阅在发布线程同步执行；绑定 `Dispatcher` 后，后台发布会排队到该 Dispatcher 的所属线程。
- 组件在 `OnAttached` 订阅，在 `OnDetached` 释放订阅，避免卸载组件继续接收消息。
- Dispatcher 队列由平台消息循环在 UI 线程排空；后台线程不得直接修改 Visual Tree。

完整用法与生命周期示例见 `Composition-and-Signals.md`。

---

## 5. 元素操作管线

### 5.1 引用获取（ref）

```
模板：<Button ref={MyBtn}>Click</Button>
生成：partial 类中产出 Button MyBtn; 字段
运行：元素挂载时赋值，卸载时置 null
```

### 5.2 命令式 API

```
el.Style.Set("color", "red")
el.ClassList.Add("active")
el.AppendChild(new Text("hello"))
el.Children
el.AddEventListener("click", handler)
```

### 5.3 仲裁规则

```
声明式绑定属性  ──┐
                  ├── 同一属性：声明式优先，命令式写入会被下一次源变更覆盖
命令式写入      ──┘

<Show>/<For> 子树 ── 声明式控制流管理，命令式不侵入
静态声明区域     ── 命令式可自由增删
```

### 5.4 元素创建

`new Button()` 命令式构造 → `AppendChild` 挂载 → 接生命周期钩子。

---

## 6. 构建层裁剪

平台/后端选择由构建层在编译期完成：

- C# 逻辑内 `#if`/`#endif`
- MSBuild `DefineConstants`：`PLATFORM_*`/`BACKEND_*`
- 条件 `ProjectReference` 控制后端/宿主装配

```
PLATFORM_WIN32 / X11 / MACOS / ANDROID / IOS / WASM
BACKEND_SOFTWARE / SKIA / BLEND2D / CAIRO
```

价值：避免运行时平台判断，减小体积；被条件包含的路径不会被 trim 误删。

---

## 7. 关键技术决策

| 决策 | 选择 | 理由 |
|---|---|---|
| 绑定后端 | `ObservableValue<T>` 委托订阅 | AOT 安全、零反射、体积小 |
| 跨组件通信 | `Signal<T>` + `SignalHub` + `Dispatcher` | 强类型、线程安全、显式 UI 线程切换 |
| 流程控制 | 编译期命令式控制流，无 VDOM | 与 Retained Rendering 同构 |
| Props | `[Prop]` 特性 + `ObservableValue<T>` | C# 习惯、类型安全、编译期校验 |
| 元素操作 | ref + 强类型 API + 仲裁规则 | 声明式为主、命令式兜底 |
| 平台裁剪 | 构建层 `#if` + MSBuild | AOT 友好、编译期消除 |
| P/Invoke | `LibraryImport` 源生成 | AOT 合规 |
| 渲染后端 M1 | 纯 C# Software Renderer | 无 C++ 依赖，验证管线 |

---

## 8. 设计边界（Non-Goals）

- 不内置 JS 引擎 / WebView / JSBridge 的运行时渲染与响应式
- 不采用反射式 / Proxy 数据绑定
- 不采用运行时平台 `if/else` 判断
- 不采用虚拟 DOM 与运行时 diff
- 不提供隐式双向绑定
- 不采用运行时动态组件 / 运行时 DOM 搬运
- 命令式操作不覆盖声明式绑定（不静默回滚）
