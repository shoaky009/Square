# Square

Square 是一个用纯 C# 编写的实验性跨平台 UI 框架。它借鉴 HTML 与 CSS 的开发体验，但不是浏览器，也不在运行时解析模板。

UI 使用 `.sqx`（Square 原生语法）或 `.sqv`（Vue 3 模板语法前端）描述，由 Roslyn Incremental Source Generator 在编译期生成普通 C# 类型。框架以 NativeAOT、可裁剪、保留模式渲染和后端可替换为主要设计约束。

> `.sqv` 是 Vue 3 模板语法的兼容前端：`{{ }}` 插值、`:prop`、`@event`、`v-if` / `v-for` 等在编译期规范化为与 `.sqx` 相同的中间表示，运行时仍是纯 C#，不引入 Vue 运行时或 JavaScript 引擎。设计与阶段规划见 [`docs/vue-plan.md`](docs/vue-plan.md)。

> Square 仍处于早期开发阶段，API 和 SQX 语法可能调整。目前支持 Windows / Win32 与 Linux / X11 两个桌面平台宿主。

## 设计目标

- **Compile First**：`.sqx` 在编译期转换为 C#，运行时不解析模板。
- **Pure C# Core**：Markup、Runtime、事件、CSS、布局、渲染树和文本等核心模块使用 C# 实现。
- **NativeAOT First**：避免动态代码生成、`Reflection.Emit`、`dynamic` 和运行时程序集发现。
- **Backend Independent**：UI 核心不绑定具体图形库，渲染通过 `IRenderContext` 抽象提交。
- **Retained Rendering**：使用 Element Tree、Layout、Display Tree 和 DrawCommand 管线。
- **Low Coupling**：平台、渲染后端和框架核心保持明确的依赖边界。

## 当前能力

仓库目前包含以下实现：

- `.sqx` 词法分析、语法分析和 Source Generator
- `template`、C# `script`、组件级 `style`
- 强类型属性和 `ObservableValue<T>` / `ObservableCollection<T>`
- `<Show>`、`<For>`、`<Switch>`、`<Match>` 编译期结构原语
- 默认插槽、具名插槽和 fallback
- `ref` 元素引用和命令式元素 API
- View、Text、ListItem、Link、Button、Input、TextArea、CheckBox、Radio、Select、Image、Canvas
- CSS 选择器、级联、变量、伪类、属性选择器及基础样式
- Box / Flex / Grid 布局（Flex 经 Yoga.Net，Grid 内置实现）
- 纯 C# Software Renderer
- Flutter Impeller GPU Renderer：Windows / Linux(X11) Vulkan，支持形状、Path、Bitmap、Typography、渐变、透明层和 Geometry clip
- Win32 窗口宿主、键盘、鼠标、文本输入、IME 和剪贴板
- X11 窗口宿主（Linux）、键盘、鼠标、滚轮、剪贴板（CLIPBOARD + PRIMARY）和 Software Renderer 上屏
- DOM 风格事件系统：`EventTarget` / `Event` / `addEventListener` / `dispatchEvent` + 捕获/冒泡
- 内存路由、参数、通配符、嵌套布局和 Link
- `Signal<T>`、`SignalHub` 和 Dispatcher 跨线程投递
- 基础文本编辑、光标和选择区域
- `Canvas.RequestFrame()` 下一帧重绘请求
- CSS Animation / `@keyframes` 基础支持
- Theme 系统（`ThemeProvider` 主题切换）
- `Document` / `UIDocument` 文档模型（UI/Head/Body 壳）
- `FontFace` / `FontFaceSet` CSS Font Loading 子集
- 自定义指令 SDK（`[SqxDirective]` + `DirectiveCatalog` + `DirectiveEmitPipeline`）
- `Reconciler` 批量脏标记与更新调度
- `.sqv` Vue 模板语法前端：`{{ }}` 插值、`:prop` / `v-bind`、`@event` / `v-on`、`v-if` / `v-else-if` / `v-else`、`v-for`、`:key`、`ref` 及事件修饰符（`.stop` / `.prevent`）
- `Square.Extensions` 扩展模块：`MarkdownViewer` 控件（基于 Markdig，将 Markdown 渲染为 Square 元素树）
- `Square.Extensions.RichText`：富文本文档模型、跨 run 布局、软换行、selection/caret/hit test、格式命令与 `RichTextEditor` 控件
- `Square.Tooling`：localhost HTTP 调试服务，支持 renderer PNG 截图和鼠标、键盘、文本、滚轮模拟输入
- 进程内 renderer 截图：`DesktopApplication.CaptureRendererBitmapAsync()` 将保留的 DisplayTree 离屏重放为 Bitmap，不依赖 PID、窗口枚举或桌面合成器
- PNG 编码（`BitmapPngEncoder`）与 BMP 解码（`BmpPngConverter`），纯 C# 无外部依赖
- 平台截图（`PlatformScreenshot`，Win32 / X11 按进程 ID 捕获窗口位图）
- DOM `Range` 文本选择模型与 `TextFragment` 字符级命中测试
- Software Renderer 性能优化（位图像素/裁剪区域缓存、批量 BGRA 填充）

完整状态和后续计划见 [`docs/Roadmap.md`](docs/Roadmap.md)。
多目标渲染、原生 UI 输出、SVG 导出和 Godot 嵌入路线见 [`docs/Rendering-Targets.md`](docs/Rendering-Targets.md)。

## SQX 示例

```xml
<template>
    <View class="page">
      <Text class="title">Hello Square</Text>

      <Input
        value={Name}
        onInput={OnNameChanged} />

      <Button
        ref={SaveButton}
        onClick={OnSave}>
        Save
      </Button>

      <Show when={Saved}>
        <Text>Saved</Text>
      </Show>
    </View>
</template>

<script lang="csharp">
    public ObservableValue<string> Name = new("");
    public ObservableValue<bool> Saved = new(false);

    private void OnNameChanged(Event e)
    {
      Name.Value = ((Input)e.Target!).Value;
    }

    private void OnSave(Event e)
    {
      Saved.Value = true;
    }
</script>

<style>
    .page {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 16px;
    }

    .title {
      color: #202124;
      font-size: 20px;
    }

    Button {
      background: #0078d4;
      color: #ffffff;
    }
</style>
```

SQX 不需要 `<sqx>` 文件级根标签。`<template>` 必须且只能有一个；`<script>` 和 `<style>` 可选且各自最多一个。组件名默认取文件名，文件级元数据可放在唯一的 `<script>` 标签上：

```xml
<script lang="csharp" namespace="MyApp.Components" name="UserCard" access="internal">
</script>
```

SQX 支持无参和 `Event` 参数事件处理方法：

```csharp
private void OnClick() { }
private void OnClick(Event e) { }
```

## CSS 支持情况

Square 实现自己的 CSS 解析、级联和样式应用管线，不使用浏览器引擎。目标是兼容常用的现代 CSS 语义，而不是完整复刻 Web CSS。

| 功能 | 当前状态 |
|---|---|
| 类型、类、ID、后代、子代、兄弟、通用选择器 | 已支持 |
| Cascade 与 Specificity | 已支持 |
| `!important` | 已支持 |
| CSS Variables 与 `var()` fallback | 已支持 |
| 样式继承 | 已支持 |
| 内联 `style` 和 `class` | 已支持 |
| `:hover`、`:focus`、`:active`、`:disabled`、`:checked`、位置伪类 | 基础支持 |
| `:not(...)` | 部分支持（简单参数） |
| 属性选择器 `[name]` / `[name=value]` | 基础支持 |
| 颜色、背景、字体、边框、间距、尺寸 | 基础支持 |
| `display: block` / `flex` / `grid` | 基础支持 |
| Flex 方向、对齐、伸缩、换行和 `gap` | 基础支持 |
| `px`、`%`、`auto`、`rp`、`vw`、`vh`、`rem`、`em` | 基础支持 |
| Grid（`grid-template-*`、`fr`、`gap`、`grid-column`/`grid-row` span、`minmax()`、`grid-template-areas`） | 基础支持 |
| CSS Animation / `@keyframes` | 基础支持 |
| Theme 系统（`ThemeProvider` 主题切换） | 基础支持 |
| 属性选择器高级操作符、伪元素 | 计划支持 |
| Container Query、Subgrid | 长期计划 |

目前不支持浏览器私有扩展、CSS Houdini、怪异模式以及完整的 `@media` / `@supports`。具体属性、单位和阶段规划见 [`docs/CSS-Spec.md`](docs/CSS-Spec.md)。

## 架构

```text
.sqx
  │
  ▼
Square.SourceGenerator ──► C# Component
                              │
                              ▼
                         Element Tree
                              │
                              ▼
                         Layout Engine
                              │
                              ▼
                          Display Tree
                              │
                              ▼
                         DrawCommand
                              │
                              ▼
                        IRenderContext
                              │
                              ▼
                     Rendering Backend
```

主要项目：

| 项目 | 职责 |
|---|---|
| `Square.Markup` | SQX 词法、语法和 AST |
| `Square.SourceGenerator` | 将 SQX 编译为 C# 组件 |
| `Square.Runtime` | 应用生命周期、绑定、调度、信号，以及 `Square.Events` 命名空间下的 DOM 风格事件协议（`EventTarget`/`Event`/`addEventListener`/`dispatchEvent`） |
| `Square.UI` | Element Tree、属性系统和元素操作 API |
| `Square.Controls` | 控件、结构原语和基础动画时钟 |
| `Square.Router` | 内存路由、历史、Link 和 RouteContext |
| `Square.CSS` | CSS 解析、选择器、级联和样式应用 |
| `Square.Graphics` | 绘图接口和基础图形类型 |
| `Square.Rendering` | Box / Flex / Grid 布局、Display Tree 和 DrawCommand |
| `Square.Text` | 字形、测量和文本布局 |
| `Square.Platform` | 平台宿主与输入采集 |
| `Square.Backends` | Software Renderer |
| `Square.Backends.Impeller` | Flutter Impeller Standalone SDK Vulkan GPU 后端 |
| `Square.Hosting` | 桌面应用宿主：窗口、输入、焦点、剪贴板、帧调度、布局渲染循环 |
| `Square.Extensions` | 可选扩展组件与集成：`MarkdownViewer`、`RichTextEditor` 等，按需引用 |
| `Square.Tooling` | 本地 HTTP 调试与自动化：截图、输入模拟，按需引用 |

更详细的模块关系见 [`docs/Architecture.md`](docs/Architecture.md)。

## 环境要求

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 或更高版本（运行 Win32 示例）
- Linux 桌面（X11，运行 X11 示例；需安装 `libX11`，Debian/Ubuntu 系可用 `sudo apt install libx11-6`）
- Git

检查 SDK：

```bash
dotnet --version
```

## 快速开始

完整的入门指南见 [`docs/Getting-Started.md`](docs/Getting-Started.md)。API 参考见 [`docs/API-Reference.md`](docs/API-Reference.md)。Tooling 调试服务见 [`docs/Tooling.md`](docs/Tooling.md)。

### 创建第一个 Square 应用

一个最小桌面应用只需根组件和 `DesktopApplication`：

```csharp
using Square.Hosting;
using Square.Platform;

var app = new DesktopApplication(new Main(), new PlatformHostCreateInfo
{
    Title = "My App",
    Width = 800,
    Height = 600
});
app.Run();
```

`Main` 是由 `.sqx` 文件在编译期生成的组件。`DesktopApplication` 自动处理窗口创建、鼠标命中测试、焦点管理、文本编辑、剪贴板、帧调度和布局渲染循环。

### 从源码运行

克隆仓库：

```bash
git clone https://github.com/wuldas/Square.git
cd Square
```

还原并构建：

```bash
dotnet restore Square.slnx
dotnet build Square.slnx
```

运行示例：

```bash
dotnet run --project samples/Square.Sample/Square.Sample.csproj
```

使用 Impeller 运行常规示例：

```bash
dotnet run --project samples/Square.Sample/Square.Sample.csproj -- \
  --backend Impeller \
  --impeller-library /path/to/impeller.dll-or-libimpeller.so
```

Impeller 专用冒烟示例：

```bash
dotnet run --project samples/Square.Sample.Impeller/Square.Sample.Impeller.csproj -- \
  --library /path/to/impeller.dll-or-libimpeller.so \
  --screenshot artifacts/impeller-smoke.png
```

`--screenshot` 使用进程内 DisplayTree 离屏重放，不按 PID 捕获平台窗口。它输出客户区内容，适合自动化和 UI 回归。当前 Impeller Standalone C API 不提供 surface readback，因此该截图不是 GPU framebuffer readback；GPU 与 Software 的文字抗锯齿和 shaping 可能存在像素差异。

运行 Vue 模板语法示例（`.sqv`）：

```bash
dotnet run --project samples/Square.Sample.Vue/Square.Sample.Vue.csproj
```

运行 RichText 编辑器示例：

```bash
dotnet run --project samples/Square.Sample.RichText/Square.Sample.RichText.csproj
```

RichText 示例包含格式工具栏、颜色、清除格式、撤销/重做、全选、软换行、鼠标选择、键盘导航和纯文本预览。

RichText 示例会同时启动 Tooling 服务：

```text
http://127.0.0.1:<自动分配端口>/api/v1
X-Square-Tooling-Token: square-richtext-demo
```

实际地址会在启动时输出。多个程序并行运行时，各实例由操作系统分配独立端口；调用方应读取 `ToolingServer.BaseAddress`，不要假设固定端口。

常用接口：

```text
GET  /health
GET  /screenshot
POST /input/pointer
POST /input/key
POST /input/text
POST /input/wheel
```

完整接口、认证和自动化示例见 [`docs/Tooling.md`](docs/Tooling.md)。

## 测试

运行全部测试：

```bash
dotnet test Square.slnx
```

运行事件或 UI 测试：

```bash
dotnet test tests/Square.Runtime.Tests/Square.Runtime.Tests.csproj
dotnet test tests/Square.UI.Tests/Square.UI.Tests.csproj
```

## NativeAOT 发布

当前示例项目已启用 NativeAOT。Windows x64 发布命令：

```bash
dotnet publish samples/Square.Sample/Square.Sample.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true
```

输出通常位于：

```text
samples/Square.Sample/bin/Release/net10.0/win-x64/publish/
```

Linux x64 发布命令：

```bash
dotnet publish samples/Square.Sample/Square.Sample.csproj \
  -c Release \
  -r linux-x64 \
  -p:SquareTargetPlatform=X11 \
  --self-contained true
```

输出通常位于：

```text
samples/Square.Sample/bin/Release/net10.0/linux-x64/publish/
```

### 平台裁剪

Square 通过 `SquareTargetPlatform=Win32|X11`、构建层 `DefineConstants`（`PLATFORM_WIN32` / `PLATFORM_X11`）和条件包含源文件来裁剪平台代码。该属性会传播到项目引用，避免在 Windows 上交叉发布 Linux 时误编译 Win32 类库：

- 在 Windows 上构建 → 编译 `Square.Platform/Win32/`，注册 Win32 宿主
- 在 Linux 上构建，或使用 `-r linux-x64 -p:SquareTargetPlatform=X11` 交叉编译 → 编译 `Square.Platform/X11/`，注册 X11 宿主

无需运行时平台判断；未包含的平台代码不会被 trim 误删。

## 文档

- [总体架构](docs/Architecture.md)
- [入门指南](docs/Getting-Started.md)
- [API 参考](docs/API-Reference.md)
- [SQX 语言规范](docs/Sqx-Spec.md)
- [SQV / Vue 模板计划](docs/vue-plan.md)
- [CSS 规范](docs/CSS-Spec.md)
- [布局](docs/Layout.md)
- [渲染](docs/Rendering.md)
- [多目标渲染与宿主路线](docs/Rendering-Targets.md)
- [图形](docs/Graphics.md)
- [文本](docs/Text.md)
- [Tooling 调试服务](docs/Tooling.md)
- [组件组合与信号](docs/Composition-and-Signals.md)
- [Source Generator](docs/Generator.md)
- [编码规范](docs/CodingStyle.md)
- [开发路线](docs/Roadmap.md)
- [需求说明](docs/Requirements.md)

## 项目状态

Square 目前适合框架设计验证、实验和贡献开发，不建议用于生产项目。当前工作的重点是完成核心输入/事件管线、扩展 CSS 与布局能力、完善控件行为，并继续验证 NativeAOT 和模块边界。

## 贡献

提交改动前请确保：

```bash
dotnet build Square.slnx
dotnet test Square.slnx
```

代码风格和模块依赖约束见 [`docs/CodingStyle.md`](docs/CodingStyle.md) 与 [`docs/Architecture.md`](docs/Architecture.md)。新增功能应附带对应测试，并避免引入运行时反射、动态代码生成或不必要的跨层依赖。

## License

Square 使用 [MIT License](LICENSE)。
