# Square Framework 需求说明

> Version: 0.2 Draft  
> Status: Draft  
> 变更（v0.2）：新增 §18 组件 Props 系统、§19 元素操作能力；§3 SQX 补充 Props/ref 语法说明

---

# 1. 项目简介

Square 是一个基于 **C#** 的现代跨平台 UI Framework。

Square 借鉴 HTML + CSS 的开发体验，但不是浏览器，也不是 HTML 引擎。

开发者使用：

- `.sqx` 描述 UI
- `.css` 描述样式
- `C#` 编写业务逻辑

整个框架在编译期间通过 Source Generator 转换为 C# 代码，最终编译到应用程序中，运行时无需解析 UI 文件。

---

# 2. 设计目标

## Compile First

所有 `.sqx` 文件均在编译期间生成 C#。

运行时：

- 不解析 SQX
- 不解析模板
- 不解释 UI
- 不执行脚本

UI 最终全部转换为普通 C# 类型。

---

## C# First

框架只有一种开发语言：

**C#**

不支持：

- JavaScript
- TypeScript
- Lua
- Python

所有事件处理全部由 C# 完成。

例如：

```xml
<Button Click="OnClick"/>
```

```csharp
private void OnClick()
{
}
```

---

## NativeAOT First

框架设计必须天然支持：

- NativeAOT
- Trimming

避免使用：

- Reflection.Emit
- Runtime Code Generation
- Dynamic
- Runtime Assembly Loading

---

## Backend Independent

框架核心不得依赖具体图形库。

图形库均作为 Backend 存在。

例如：

```
Software
Skia
Blend2D
Cairo
```

未来允许增加：

```
Direct2D
Metal
OpenGL
Vulkan
WebGPU
```

---

## Pure C# Core

框架核心采用纯 C# 实现。

包括：

- SQX Parser
- Source Generator
- CSS Engine
- Layout Engine
- Event System
- Runtime
- Render Tree
- Animation
- Text Engine（优先）

Native Library 仅用于图形 Backend。

---

# 3. SQX

SQX 是框架自己的 UI 描述语言。

语法参考 HTML。

例如：

```xml
<View>

    <Text>Hello Square</Text>

    <Button Click="OnClick">
        Click
    </Button>

</View>
```

SQX 不是 HTML。

仅提供框架需要的元素。

---

## 3.1 Props

SQX 支持自定义组件的 **Props 声明**。

Props 是组件的输入契约。

声明方式：

- 在 `<script lang="csharp">` 中使用 `[Prop]` 特性声明

要求：

- 类型安全（编译期检查）
- 必填/默认值支持
- 父组件 → 子组件单向数据流
- 子组件不可直接改写 Props 值
- Props 变化时自动通知子组件
- 内置元素属性与自定义组件 Props 共用同一套机制

---

## 3.2 ref 引用

SQX 支持在模板中标记元素引用。

```
<Button ref={MyBtn}>Click</Button>
```

生成器在组件类中产出强类型字段，供 C# 代码命令式访问。

---

## 3.3 元素操作

框架提供完整的命令式元素操作能力：

- 获取元素引用（ref）
- 读写属性
- 操作样式与类
- 增删移动子节点
- 挂接/卸载事件
- 查询子树

约束：

- 命令式操作不覆盖已绑定属性
- 命令式操作不侵入 `<Show>`/`<For>` 管理的子树

---

# 4. 控件

第一阶段支持：

- View
- Text
- Button
- Input
- TextArea
- CheckBox
- Radio
- Select
- Image
- Canvas

后续支持：

- List
- Tree
- Menu
- Dialog
- ScrollViewer
- Grid
- Popup
- Window
- Tab

---

# 5. CSS

CSS 是框架的重要组成部分。

目标：

尽可能兼容现代 CSS。

支持：

- Selector
- Cascade
- Specificity
- Variables
- Inheritance
- Pseudo Class
- Animation
- Flex
- Grid

不要求兼容浏览器私有扩展。

---

# 6. 布局

布局采用 CSS 思想。

支持：

```
display
flex
grid
absolute
relative
fixed
sticky
```

尺寸：

```
px
%
auto
min-content
max-content
fit-content
```

后续：

```
Container Query
Subgrid
```

---

# 7. 事件系统

支持：

- Mouse
- Keyboard
- Touch
- Focus
- Wheel

后续支持：

- Gesture
- DragDrop

---

# 8. 渲染架构

采用保留模式（Retained Mode）。

```
SQX

↓

Component

↓

Visual Tree

↓

Layout

↓

Render Tree

↓

Draw Command

↓

IRenderContext

↓

Backend
```

不是 Immediate Mode。

---

# 9. Graphics

Graphics 层提供统一绘图接口。

统一接口：

```csharp
IRenderContext
```

提供：

- Geometry
- Brush
- Pen
- Image
- Bitmap
- Font
- Path
- Transform
- Clip

Graphics 不依赖：

- CSS
- Controls
- Component
- Runtime

---

# 10. Canvas

Canvas 为 Graphics 的兼容层。

内部：

采用 Render Tree + DrawCommand。

外部：

可以提供兼容 HTML Canvas API。

例如：

```
CanvasRenderingContext2D
```

最终统一转换为：

```
DrawCommand
```

---

# 11. 文本系统

文本模块独立实现。

负责：

- Unicode
- Paragraph
- Glyph
- Font Manager
- Font Collection
- Text Layout
- Caret
- Selection
- HitTest
- Line Break
- Font Fallback
- BiDi

字体系统优先采用纯 C# 实现。

必要时可参考成熟的纯 C# 开源项目。

---

# 12. Source Generator

Source Generator 是框架核心。

流程：

```
SQX

↓

AST

↓

Semantic Analysis

↓

Generate C#

↓

Compile
```

要求：

- Incremental Generator
- 编译错误映射到 SQX
- IDE 智能提示
- 编译期检查

---

# 13. Backend

Backend 为可插拔模块。

第一阶段：

- Software Renderer（纯 C#）

后续：

- Skia
- Blend2D
- Cairo

未来：

- Direct2D
- Metal
- Vulkan

Backend 不影响框架核心。

---

# 14. 性能目标

优先考虑：

- NativeAOT
- 启动速度
- 小体积
- 少依赖
- 低内存
- 高 DPI
- 高刷新率

---

# 15. 跨平台

目标平台：

- Windows
- Linux
- macOS
- Android
- iOS
- WebAssembly

平台层应保持最小实现。

---

# 16. 项目模块

```
Square.Markup
Square.SourceGenerator
Square.Runtime
Square.UI
Square.Controls
Square.CSS
Square.Layout
Square.Rendering
Square.Graphics
Square.Text
Square.Animation
Square.Platform
Square.Backends
Square.Tooling
```

---

# 17. 核心原则

## Compile First

所有 UI 编译生成 C#。

运行时零解析。

---

## Pure C# Core

框架核心全部采用 C# 实现。

---

## NativeAOT First

所有设计优先兼容 NativeAOT。

---

## Backend Independent

任何 Backend 都可以替换。

---

## Retained Rendering

采用 Visual Tree + Render Tree。

不采用 Immediate Mode。

---

## Low Coupling

所有模块通过抽象接口通信。

避免跨层依赖。

---

## IDE Friendly

SQX 应提供：

- 类型检查
- 智能补全
- 编译错误定位
- Source Generator Diagnostics

---

# 18. 长期目标

构建完整的 C# UI 技术栈：

```
SQX
        │
        ▼
Source Generator
        │
        ▼
Component Runtime
        │
        ▼
CSS Engine
        │
        ▼
Layout Engine
        │
        ▼
Render Tree
        │
        ▼
Graphics
        │
        ▼
Backend
```

整个框架以 **纯 C#、编译优先、NativeAOT、可插拔渲染后端** 为核心设计理念，为开发者提供现代化、高性能、跨平台的 UI 开发体验。

---

# 18. 组件 Props 系统

## 18.1 定位

Props 是自定义组件的输入契约。

任何自定义组件可以声明自己的 Props，调用方在模板中以属性形式传入。

## 18.2 声明

在 `<script lang="csharp">`（或后置 `.cs`）中使用 `[Prop]` 特性：

```csharp
[Prop] public ObservableValue<string> Title { get; set; } = new("");
[Prop(Required = true)] public ObservableValue<int> Count { get; set; } = new(0);
```

- 类型为 `ObservableValue<T>`（由生成器辅助包装，开发者也可手写）
- 默认值用 C# 初始化器
- `Required = true` 标记必填

## 18.3 传值

调用方在模板中以属性形式传入：

```xml
<MyComponent Title={PageTitle} Count={ItemCount} />
```

- 传入值绑定到 `ObservableValue<T>`
- 父组件源变化时，子组件 prop 自动更新
- 可传入常量：`Title="Hello"`

## 18.4 数据流

- **单向**：父 → 子
- 子组件**不可直接赋值改写** Props 值
- 子组件可订阅 prop 的 `ObservableValue` 响应变化
- 子组件提供 `OnPropChanged(string name)` 虚方法钩子

## 18.5 校验

- 编译期：Generator 检查调用方是否传齐必填 Prop
- 运行时不做反射校验

## 18.6 内置元素

内置元素（Button/Input/...）的属性与自定义组件 Props **共用同一套机制**。

---

# 19. 元素操作能力

## 19.1 目标

开发者可通过 C# 代码命令式操作元素。

## 19.2 引用获取

- 模板中 `ref={Name}` 标记
- 生成器产出强类型字段
- 字段在元素挂载时自动赋值，卸载时置 null

## 19.3 操作 API

提供强类型原生 API：

| 操作 | API |
|---|---|
| 读写属性 | `el.SetProperty(...)` / `el.GetProperty<T>(...)` |
| 样式 | `el.Style.Set("color", "red")` |
| 类 | `el.ClassList.Add("x")` / `.Remove("x")` / `.Toggle("x")` |
| 子节点 | `el.AppendChild(node)` / `el.RemoveChild(node)` / `el.InsertBefore(new, ref)` |
| 子树 | `el.Children` / `el.ClearChildren()` |
| 事件 | `el.AddEventListener(...)` / `el.RemoveEventListener(...)` |

## 19.4 元素创建

允许 `new Button()` 命令式构造并挂载，接生命周期钩子。

## 19.5 仲裁规则

- 命令式**不覆盖已绑定属性**：若命令式写入了被声明式绑定的属性，下一次源变更会覆盖命令式值，文档化此行为，不静默回滚
- 命令式**不侵入 `<Show>`/`<For>` 管理的子树**：这些子树由声明式控制流管理，命令式增删会被冲掉
- 命令式操作仅针对**未绑定属性**或**静态声明区域**

## 19.6 查询（后置）

- `el.Query<Tag.Button>(".cls")` 式查询能力
- 编译期生成匹配器，避免运行时反射
- 计划 M2 引入