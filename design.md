# Square Framework 设计文档

> 配套计划：`plan.md`（分阶段路线图、排期、风险、交付）
> 需求来源：`docs/Requirements.md`（v0.1 Draft）
> 范围：总体架构、模块划分、Phase 1（M0+M1）详细设计、模板/绑定/流程控制规范、关键技术决策

---

## 1. 项目定位与核心约束

一句话定位：**纯 C# 实现、编译优先（Compile First）、NativeAOT 优先、渲染后端可插拔** 的跨平台 UI 框架。

六大核心原则（来自需求 §17）：

1. **Compile First** —— 所有 UI 在编译期生成 C#，运行时零解析。
2. **Pure C# Core** —— 框架核心（Parser / Generator / CSS / Layout / Runtime / Render Tree / Animation / Text）全部 C# 实现。
3. **NativeAOT First** —— 禁用 `Reflection.Emit`、运行时代码生成、`Dynamic`、运行时加载程序集。
4. **Backend Independent** —— 核心不依赖具体图形库；图形库均为可插拔 Backend。
5. **Retained Rendering** —— Visual Tree + Render Tree，非 Immediate Mode。
6. **Low Coupling / IDE Friendly** —— 模块间通过抽象接口通信；`.sqx` 提供类型检查、智能补全、编译错误定位。

**设计约束**：
- **结构化流程控制编译模型**：条件/列表/分支采用编译期细粒度命令式控制流（`<Show>`/`<For>`/`<Switch>`/`<Match>`），**无虚拟 DOM**；与 `ObservableValue` + Retained Rendering 同构（详见 §4.5.5）。
- **脚本边界**：每个 `.sqx` 最多一个 `<script>`，当前只支持 `lang="csharp"`；该标签同时承载 C# 逻辑、Props 声明和文件级组件元数据。其他脚本语言不在当前范围内。

---

## 2. 总体架构（保留模式管线）

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
Layout Engine  (Square.Layout, CSS 盒/flex/grid)
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
- **低耦合**：除 `Square.Backends` 与 `Square.Platform` 外，所有模块仅依赖抽象接口，不依赖具体图形库或 OS。
- **NativeAOT 合规**：组件类型在编译期生成，运行时无反射解析；属性系统使用生成代码与强类型委托。

---

## 3. 模块划分与职责

| 模块 | 职责 | 关键设计 |
|---|---|---|
| `Square.Markup` | `.sqx` 词法/语法解析 → AST（含 template/script/style 三段） | 错误带行列号，供 Source Generator 映射诊断 |
| `Square.SourceGenerator` | Roslyn Incremental Generator，`.sqx`→C# | 缓存键严格设计（IDE 诊断不滞后）；结构原语特判编译；诊断映射回 `.sqx` 行列 |
| `Square.Runtime` | `Application`、组件生命周期、调度 | 消息循环调度；组件树挂载 |
| `Square.UI` | 视觉基类型、属性、Visual Tree 节点 | 强类型属性（生成代码，无反射依赖属性） |
| `Square.Controls` | 第一阶段控件 + 结构原语（Show/For/Switch/Match） | 控件 = 视觉 + 行为 + 默认样式；结构原语由生成器编译为非运行时实例 |
| `Square.CSS` | CSS 引擎 | Selector/Cascade/Specificity/Var/Inheritance；M1 子集 |
| `Square.Layout` | CSS 布局引擎（盒/flex/grid） | 高 DPI 物理像素对齐；M1 子集（block/flex + rp/vw/vh） |
| `Square.Graphics` | `IRenderContext` 抽象 + 绘图原语类型 | 工厂 `IRenderBackendFactory` 创建上下文；原语 Geometry/Brush/Pen/Image/Bitmap/Font/Path/Transform/Clip |
| `Square.Rendering` | Visual Tree→Layout→Render Tree→DrawCommand | 保留模式、脏区/增量；子树挂卸、keyed 增量增删 |
| `Square.Text` | 文本引擎 | Unicode/Paragraph/Glyph/Font Manager/Layout/Caret/Selection/HitTest/LineBreak/Fallback/BiDi；M1 基础 |
| `Square.Animation` | 时间线/缓动 | 与 CSS Animation 联动；M1 最小 |
| `Square.Platform` | 平台宿主抽象（M1: Win32） | `IPlatformHost`：窗口/消息循环/输入泵；P/Invoke 用 `LibraryImport` 源生成 |
| `Square.Backends` | 渲染后端（M1: Software 纯 C#） | 纯托管、无 C++ 依赖；后续 Skia/Blend2D/Cairo 同接 `IRenderContext` |
| `Square.Tooling` | 诊断 | Source Generator 诊断输出、IDE 集成 |

依赖方向：`SourceGenerator` → `Markup`；`Controls/UI/Rendering/Layout/CSS/Text/Animation` → `Runtime` + `Graphics`(抽象)；`Backends`/`Platform` → 仅依赖 `Graphics`(抽象) 与 `Runtime` 接口。核心层禁止反向依赖 Backend/Platform。

---

## 4. Phase 1 详细设计（M0 + M1）

### 4.1 解决方案与项目结构

仓库根新增 `Square.slnx`（或 `.sln`）与如下项目（均 `net10.0`，启用 AOT/Trim 友好）：

```
src/
  Square.Markup/          # .sqx 词法/语法解析 → AST（含 template/script/style 三段）
  Square.SourceGenerator/ # Roslyn Incremental Generator（.sqx → C#）
  Square.Runtime/         # Application、组件生命周期、调度
  Square.UI/              # 视觉基类型、属性、Visual Tree 节点
  Square.Controls/        # 第一阶段控件 + 结构原语（Show/For/Switch/Match）
  Square.CSS/             # CSS 引擎（M1 子集）
  Square.Layout/          # CSS 布局引擎（M1 子集）
  Square.Graphics/        # IRenderContext 抽象 + 绘图原语类型
  Square.Rendering/       # Visual Tree→Layout→Render Tree→DrawCommand
  Square.Text/            # 基础文本（字形、简单排版）
  Square.Animation/       # 时间线/缓动（M1 最小）
  Square.Platform/        # 平台宿主抽象（M1: Win32）
  Square.Backends/        # 渲染后端（M1: Software 纯 C#）
  Square.Tooling/         # 诊断（M1: 基础）
  samples/
    Square.Sample/        # Phase 1 验证 Demo（含 Main.sqx）
```

> 模板统一为无文件级根标签的单文件 **`.sqx`**：唯一 `<template>`，以及各自最多一个的 `<script lang="csharp">` / `<style>`，详见 §4.5.1。

### 4.2 各模块设计要点

**Square.Markup（.sqx Parser + AST）**
- 输入：`.sqx` 文本（含 `<template>`/`<script lang="csharp">`/`<style>` 三段）；输出：强类型 AST（`SqxDocument` → `SqxTemplate`/`SqxScript`/`SqxStyle`）。
- `<template>` 内为 HTML 风格框架元素；属性值含事件引用（`onClick={OnClick}`）与绑定表达式（`{ Name }`、`prop={ expr }`）；结构原语 `<Show>`/`<For>`/`<Switch>`/`<Match>` 作为特殊节点；`<Show>`/`<For>` 的 `when=`/`each=` 同用 `{expr}` 表达式。
- 错误带行列号，供 Source Generator 映射诊断。
- 任务：词法器、递归下降语法器、AST 模型、单元测试。

**Square.SourceGenerator（Incremental Generator）**
- `IIncrementalGenerator`：以 `.sqx` 为 `AdditionalText` 输入（内含 `<template>`/`<script lang="csharp">`/`<style>` 三段）。
- 流程：读 `.sqx` → 调 `Markup` 解析为 AST → 语义分析（绑定/事件解析到 C# 成员；`<Show>`/`<For>`/`<Switch>`/`<Match>` 识别为结构原语）→ 生成 `partial` 组件类（含 `BuildVisualTree()`）。
- 结构原语：`<Show when>`/`<For each>`/`<Switch>`+`<Match when>` 由生成器特判，编译为 `ObservableValue`/`ObservableCollection` 驱动的 Visual 子树增删，而非运行时组件实例。
- 诊断：将解析错误映射为带 `.sqx` 文件路径与行列的 `Diagnostic`，IDE 可定位。
- 平台/后端裁剪**不在生成器内处理**：由 C# `#if` + MSBuild 常量/条件引用在构建层完成（见 §4.5.2）。
- 任务：Generator 骨架、`.sqx`→AST→C# 代码发射、事件/绑定解析、结构原语编译、诊断源、增量缓存键。

**Square.CSS（当前子集）**
- 支持：Selector（类型/类/id/后代/子代/相邻兄弟/通用兄弟/通用/基础属性选择器）、Cascade、Specificity、`!important`、Variables（`--x`）、Inheritance、基础伪类、基础属性（color/background/border/padding/margin/font-size）。
- 暂不做：属性选择器高级操作符、伪元素、Animation、Grid 全量（后续补）。
- 任务：Tokenizer、Rule/AST、Selector 匹配、级联计算、属性应用到 Visual。

**Square.Layout（M1 子集）**
- 支持：`display:block/flex`、`flex-direction`、`justify-content`、`align-items`、`flex-grow/shrink`、`width/height`（px/%/auto/rp/vw/vh）、`padding/margin`。
- 高 DPI：**物理像素对齐**（避免模糊）。
- 任务：Box 模型测量/排列、Flex 算法、尺寸解析（px/%/auto/rp/vw/vh）。

**Square.Graphics（IRenderContext 抽象）**
- 接口原语：Geometry、Brush、Pen、Image、Bitmap、Font、Path、Transform、Clip。
- 定义 `IRenderContext` 与 `IRenderBackendFactory`（工厂创建上下文）。
- 任务：抽象接口与基础类型（Color、Rect、Size、Point、Matrix）、Backend 注册机制。

**Square.Backends — Software Renderer（纯 C#）**
- 纯托管渲染器思路：**无外部 C++ 依赖**，CPU 软渲染。
- 关键技术：
  - BGRA32 像素缓冲；**预乘 Alpha（Premultiplied Alpha）** 消除缩放黑边；
  - 基础 **SIMD** 加速的像素混合（`.NET` `System.Numerics`/`Vector<T>`）；
  - 设备上下文缓存，脏区重绘。
- 仅实现 M1 所需原语：填充矩形/文本/线条/基础路径。
- 任务：SoftwareBackend、光栅化基础、文本光栅（接 Square.Text）、脏区管理。

**Square.Rendering（Visual Tree → Render Tree → DrawCommand）**
- Visual Tree：由 Source Generator 生成的组件构建；`<Show>` 条件子树需支持**挂卸**，`<For>` 列表需支持**增量增删**（keyed）。
- Layout 阶段调用 `Square.Layout` 计算几何。
- Render Tree：生成 `DrawCommand` 列表（FillRect/DrawText/DrawPath/...）。
- 保留模式：脏标记驱动增量重绘。
- 任务：RenderTree 构建、DrawCommand 定义、脏区/增量机制（含子树挂卸）、调用 IRenderContext 提交。

**Square.Runtime + Square.UI**
- `Application.Run(window)`、消息循环调度。
- 视觉基类型 `Visual`/`UIElement`：强类型属性（生成代码，无反射依赖属性）。
- 任务：Application、Visual 基类、属性存储、组件树挂载；组件生命周期（OnAttached/OnDetached/OnLoaded/OnUnloaded、OnMeasure/OnArrange）、应用生命周期（OnStart/OnExit）。

**Square.Controls（M1 控件）**
- 第一阶段视觉控件：View, Text, Button, Input, TextArea, CheckBox, Radio, Select, Image, Canvas（需求 §4）。
- **编译器结构原语**（非运行时控件，由 Source Generator 特判）：`Show`（条件子树）、`For`（列表，绑定 `ObservableCollection<T>`）、`Switch`/`Match`（多分支）。
- **列表可观察原语**：新增 `ObservableCollection<T>`（引用键），作为 `<For>` 的数据源，支撑增量更新。
- 每个控件 = 视觉 + 行为 + 默认样式钩子。
- 任务：10 个控件实现、结构原语编译支持、默认 CSS、事件触发（Click/TextChanged/...）。

**Square.Text（M1 基础）**
- M1：字体加载（系统字体，纯 C# 优先）、字形测量、单行/多行简单排版、命中测试基础。
- 完整 BiDi/Fallback 留 M7。
- 任务：FontManager（最小）、Glyph 缓存、文本测量与绘制。

**Square.Platform（M1: Win32）**
- `IPlatformHost`：窗口创建、消息循环、`Mouse/Keyboard/Focus/Wheel` 输入泵。
- M1 实现 Win32（P/Invoke 用 `LibraryImport` 源生成以兼容 AOT）。
- 任务：Win32 宿主、消息循环、输入事件采集并派发到 Runtime 事件系统。

**Square.Animation（M1 最小）**
- 时间线 + 缓动函数骨架，供后续 CSS Animation 接入。
- 任务：Clock、Easing、属性动画最小实现。

**Square.Tooling（M1 基础）**
- Source Generator 诊断输出（错误映射 `.sqx` 行列）。
- 任务：诊断描述、示例项目配置。

**事件系统（贯穿 M1）**
- 支持 Mouse、Keyboard、Touch、Focus、Wheel（需求 §7）。
- M1 实现 Mouse/Keyboard/Focus/Wheel；Touch 框架预留。
- 任务：事件类型、路由（冒泡/隧道基础）、`.sqx` 事件 → C# 方法绑定。

**绑定模型**
- 引入 `ObservableValue<T>`（强类型、委托订阅、零反射），作为 Square 的数据/状态绑定基础；列表用 `ObservableCollection<T>`。
- `.sqx` 绑定/事件语法（详见 §4.5.4）：文本/属性表达式 `{expr}`、事件 `onEvent={Method}`、双向 `value={expr} onInput={Method}`；后端强制 `ObservableValue<T>`，禁止 Proxy/反射响应式。
- 结构化流程控制（详见 §4.5.5）编译为细粒度命令式控制流，无 VDOM。
- 任务：ObservableValue/ObservableCollection 原语、绑定发射、单向/双向绑定（Input/TextArea/CheckBox/Radio/Select）。

### 4.3 示例应用 + NativeAOT 验证

- `samples/Square.Sample`：一个 `Main.sqx`（`<template>` 描述窗口 + `<script lang="csharp">` 处理 `OnClick` 与绑定 + `<style>` 样式），渲染标题、Text、Button、Input、CheckBox。
- 验证：
  - `dotnet build` 通过（Source Generator 产出组件）。
  - `dotnet publish -c Release -p:PublishAot=true` 成功，无反射/动态代码警告阻断。
  - 运行：窗口渲染正确、点击 Button 触发 C# 逻辑、Input 双向绑定生效。
  - 记录可执行体积/启动耗时作为基线（目标 2–4MB 量级）。

### 4.4 Phase 1 任务清单（Checklist）

[x] M0：创建 `Square.slnx` 与全部 `Square.*` 项目 + 发布/AOT 配置
[x] `Square.Markup`：`.sqx` 解析器 + AST + 单测（严格顶级 section + script 元数据）
[x] `Square.SourceGenerator`：Incremental Generator + 诊断映射（`.sqx` 行列）+ Props 校验
[x] `Square.CSS`：Tokenizer/Selector/Cascade/Variables/Inheritance（含子代/兄弟/通用/属性选择器、`!important`、基础伪类）
[x] `Square.Layout`：Box + Flex + 尺寸解析（px/%/rp/vw/vh/auto）+ 高 DPI
[x] `Square.Graphics`：`IRenderContext`/`IRenderBackendFactory` + 基础类型
[~] `Square.Backends`：纯 C# Software Renderer（BGRA/预乘 Alpha ✓ / SIMD 待实现 / 脏区待实现）
[~] `Square.Rendering`：Visual→Render Tree→DrawCommand→提交（子树挂卸 ✓ / 增量保留模式待实现）
[x] `Square.Runtime` + `Square.UI`：Application/Visual 基类/属性 + 路由事件
[x] `Square.Controls`：10 个第一阶段控件 + 结构原语（Show/For/Switch/Match）+ 默认样式
[x] `Square.Text`：FontManager/测量/绘制（基础）
[x] `Square.Platform`：Win32 宿主 + 输入泵（`LibraryImport`）+ Mouse/Key/Wheel/IME/Clipboard
[x] `Square.Animation`：Clock/Easing 最小实现
[x] `Square.Tooling`：基础诊断输出
[x] 事件系统：Mouse/Keyboard/Focus/Wheel + `.sqx` 绑定 + Click 合成
[x] 绑定：`ObservableValue<T>` + `ObservableCollection<T>` + 生成期绑定
[x] 示例 + NativeAOT 发布验证 + 基线指标（2.53 MiB EXE，512ms 启动，32 MB 内存）
[~] 构建层裁剪：C# `#if` + MSBuild `DefineConstants` ✓ / 条件 `ProjectReference` 待实现
[x] 流程控制结构原语：`<Show>`/`<For>`/`<Switch>`/`<Match>` + `ObservableCollection<T>`
[x] 组件/应用生命周期钩子（OnAttached/OnDetached/OnLoaded/OnUnloaded + Application.OnStart/OnExit）

### 4.5 模板、绑定与流程控制规范

#### 4.5.1 统一模板格式 `.sqx`

- 模板文件**统一为 `.sqx`**，不使用 `<sqx>` 文件级根标签，以三个顶级 section 内聚：
  - `<template>`：结构，含绑定表达式 `{expr}`/`prop={expr}`/`onClick={Method}`/`value={}+onInput={}` 与流程控制 `<Show>`/`<For>`/`<Switch>`/`<Match>`。
  - `<script lang="csharp">`：可选且最多一个；包含 C# 逻辑、Props 声明和 `namespace`/`name`/`access` 等文件级元数据；Source Generator 发射进同一 `partial` 组件类。
  - `<style>`：可选且最多一个；样式由 CSS 引擎消费。
- `<template>` 必须且只能有一个，允许多个视觉根节点；生成器不自动插入包装 `View`。
- 示例结构：
  ```xml
  <template>
    <View>
      <Show when={LoggedIn}><Text>欢迎</Text></Show>
      <For each={Items}>{(it)=><Text>{it.Name}</Text>}</For>
    </View>
  </template>
  <script lang="csharp" namespace="MyApp.Components" access="public">
    // ObservableValue<bool> LoggedIn; ObservableCollection<Item> Items;
  </script>
  <style>/* CSS */</style>
  ```
- 逻辑内联于 `<script lang="csharp">`，与独立 `.cs` 功能等价（均编译为同一 `partial` 类），差异仅在编写 ergonomics。

#### 4.5.2 平台/后端裁剪（构建层）

- 平台/后端选择由 **构建层** 在编译期完成（AOT 友好、编译期消除）：
  - C# 逻辑内使用原生 `#if`/`#endif` 预处理。
  - 平台/后端选择由 **MSBuild `DefineConstants`**（如 `PLATFORM_WIN32`/`BACKEND_SKIA`）与**条件 `ProjectReference`** 控制后端/宿主装配。
  - 宏名 `PLATFORM_*`/`BACKEND_*`（`PLATFORM_WIN32`/`X11`/`MACOS`/`ANDROID`/`IOS`/`WASM`、`BACKEND_SOFTWARE`/`SKIA`/`BLEND2D`/`CAIRO`）保留为构建符号语义。
- 价值：避免运行时 `if/else` 平台判断，减小体积；被条件包含的路径不会被 trim 误删（缓解"NativeAOT 裁剪误删后端/平台代码"风险）。
- 数据驱动的流程控制由 `<Show>`/`<For>` 承担（见 §4.5.5），与构建层裁剪职责分离。

#### 4.5.3 基础组件词表

- Phase 1 已覆盖（需求 §4）：View, Text, Button, Input, TextArea, CheckBox, Radio, Select, Image, Canvas。
- 补入 Phase 3（M3）：ScrollViewer, Swiper, List, Navigator，以及延续的 Tree/Menu/Dialog/Grid/Popup/Window/Tab。
- 命名：PascalCase 控件类型（C# 习惯），`.sqx` 内小写标签。

#### 4.5.4 声明式数据绑定语法

- 模板内统一使用 `{expr}` 表达式语法，与流程控制 `when=`/`each=` 同源，无第二套绑定 DSL：
  - 文本插值：`{ expr }`（如 `<Text>{Name}</Text>`），`{...}` 内的表达式编译为 `ObservableValue` 读取并订阅；同一语法亦用于属性值。
  - 单向属性：`prop={ expr }`（如 `<Text text={Title} />`），编译为属性绑定并订阅源变化。
  - 事件：`onEvent={ Method }`（如 `onClick={OnClick}`），映射到 `<script lang="csharp">` 中的 C# 方法；事件名首字母大写（click→onClick、textChanged→onTextChanged）。
  - 双向：`value={ expr } onInput={ Method }`（如 `<Input value={UserName} onInput={OnUserNameChanged} />`），由单向属性绑定 + 输入事件处理组成；`Method` 在 C# 中写回 `ObservableValue.Value`。不提供隐式双向绑定，保持显式可控。
- 实现约束：绑定后端**必须**用 `ObservableValue<T>`（强类型、委托驱动、零反射、AOT 安全），**不得**引入 Proxy/反射响应式；`{expr}` 在编译期解析成员引用并生成订阅代码，运行时零解析。

#### 4.5.5 结构化流程控制（`<Show>`/`<For>`/`<Switch>`/`<Match>`）

- 条件/列表/分支采用编译期细粒度命令式控制流、**无虚拟 DOM**，与 `ObservableValue` + Retained Rendering 同构。
- 映射（`.sqx` 写法 → 编译产物）：
  - `<Show when={expr}>…</Show>`：条件子树；`when` 中 `{expr}` 绑定 `ObservableValue<bool>`，条件变时增删 Visual 子树（记忆化复用）；可选 `fallback` 属性指定条件假时的替代子树。
  - `<For each={expr}>{(it)=>…}</For>`：列表；`each` 中 `{expr}` 绑定 `ObservableCollection<T>`，引用键增量更新（项移动时节点不重建）；`it` 为列表项。
  - `<Switch><Match when={expr}>…</Match></Switch>`：多分支（互斥，首项真即渲染）；`Switch` 可带 `fallback`。
  - `<Index each={expr}>…</Index>`（可选）：索引键列表。
- `<Show>`/`<For>`/`<Switch>`/`<Match>` 为 **Source Generator 已知的结构原语**（非运行时组件实例），由生成器特判编译为 Visual Tree 的挂卸/迭代。
- 表达式 `when=`/`each=` 中的 `{expr}` 与绑定表达式同语法，绑定到 `ObservableValue`/`ObservableCollection`，编译期解析成员引用。
- 阶段：M1 可先支持 `<Show>`/`<For>` 基础形态；`<Switch>`/`<Match>`/`<Index>` 与 `keyed` 复用排 M2。

#### 4.5.6 组件 / 应用生命周期钩子

- 组件：`OnAttached`（挂载视觉树）/`OnDetached`（卸载）/`OnLoaded`/`OnUnloaded`；布局回调 `OnMeasure`/`OnArrange`。
- 应用：`OnStart`/`OnExit`（对标 `Application.Run`）。
- 落地：编译期生成的 `partial` 组件类提供可重写虚方法，供 C# 业务逻辑订阅。

#### 4.5.7 按标签即用

- 控件按标签名即可用、免手动注册。`Square.SourceGenerator` 已按标签解析控件，沿用"按标签即用"低仪式感；无需显式 `using`/注册清单。

#### 4.5.8 设计边界（明确不支持）

- 不内置 JS 引擎 / WebView / JSBridge 的**运行时**渲染与响应式；`<script>` 仅作为未来 AOT 编译扩展槽保留。
- 不采用反射式 / Proxy 数据绑定；绑定后端强制 `ObservableValue<T>` 零反射。
- 不采用运行时平台 `if/else` 判断；平台/后端裁剪在构建层完成（§4.5.2）。
- 不采用虚拟 DOM 与运行时 diff；采用保留模式 + 结构化流程控制编译为命令式控制流。
- 不提供隐式双向绑定（自动同步属性与状态的简写）；双向以 `value={}` + `onInput={}` 显式表达。
- 不采用运行时动态组件（`<Dynamic>` 类）/ 运行时 DOM 搬运（`<Portal>` 类）；暂列范围外。

---

## 5. 关键技术决策与权衡

- **NativeAOT/Trim 约束**：全程禁用反射式绑定、运行时代码生成；P/Invoke 用 `LibraryImport` 源生成；`ObservableValue` 委托订阅替代反射属性系统。
- **统一模板格式 `.sqx` + 细粒度命令式流程控制**：结构、逻辑和样式以内聚的顶级 section 组织，不使用文件级根标签；`<Show>`/`<For>` 编译为 `ObservableValue`/`ObservableCollection` 驱动的控制流，无 VDOM，与 Retained Rendering 同构；平台/后端裁剪下沉构建层。
- **Source Generator 诊断映射**：解析错误携带 `.sqx` 文件路径与行列，经 `Diagnostic` 回抛，保证 IDE 友好（需求 §12、§17）。
- **纯托管软件渲染器**：Phase 1 后端采用纯 C# CPU 渲染，纯托管、无 C++ 依赖；用 BGRA32 + 预乘 Alpha + SIMD 混合 + 脏区重绘达到可接受性能，并为后续 Skia/Blend2D/Cairo 预留同一 `IRenderContext`。
- **高 DPI**：布局与光栅均按物理像素对齐，避免模糊。
- **CSS 范围（M1/M2 起步）**：已覆盖静态样式、flex 布局、常用组合选择器、基础属性选择器、`!important` 与基础伪类；属性选择器高级操作符、伪元素、Animation/Grid 全量延至后续阶段，控制复杂度。
- **绑定模型选择**：采用 `ObservableValue<T>` 式零反射绑定，确保 AOT 安全与体积；列表用 `ObservableCollection<T>` 支撑 `<For>`。

---

## 6. 关联文档

- `plan.md`：分阶段路线图（M0–M8）、里程碑排期、风险与缓解、交付说明。本设计文档的 §4 对应计划中的 M0+M1 阶段。
- `docs/Requirements.md`：原始需求（v0.1 Draft）。
