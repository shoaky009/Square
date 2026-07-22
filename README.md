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
- `.sqx.cs` / `.sqv.cs` 同名代码后置 partial 类，可替代大部分模板内 `<script>` 逻辑
- `Store<TState>`、selector 与 `StoreScope`，支持从任意线程向多个 UI Dispatcher 投递响应式状态
- 模板可通过 `AppWindow` 操作标题、关闭、最小化、最大化、还原和系统窗口拖动
- `<Show>`、`<For>`、`<Switch>`、`<Match>` 编译期结构原语
- 默认插槽、具名插槽和 fallback
- `ref` 元素引用和命令式元素 API
- View、ScrollViewer、Popup、Dialog、MenuBar、Menu、ContextMenu、MenuItem、MenuSeparator、Text、ListItem、Link、Button、Input、TextArea、CheckBox、Radio、Select、Image、Canvas
- 模板内联 SVG DOM：`svg`、`g`、`path`、`rect`、`circle`、`ellipse`、`line`、`polyline`、`polygon`
- CSS 选择器、级联、变量、伪类、属性选择器及基础样式
- Box / Flex / Grid 布局（Flex 经 Yoga.Net，Grid 内置实现）
- 纯 C# Software Renderer
- Square 原生 Vulkan GPU Renderer：Windows / Linux(X11)，支持形状、Path、Bitmap、文本、渐变、透明层、Geometry clip、MSAA 和可选 GPU readback
- Win32 窗口宿主、键盘、鼠标、文本输入、IME 和剪贴板
- X11 窗口宿主（Linux）、键盘、鼠标、滚轮、剪贴板（CLIPBOARD + PRIMARY）和 Software Renderer 上屏
- DOM 风格事件系统：`EventTarget` / `Event` / `addEventListener` / `dispatchEvent` + 捕获/冒泡
- 内存路由、参数、通配符、嵌套布局和 Link
- `Signal<T>`、`SignalHub` 和 Dispatcher 跨线程投递
- 基础文本编辑、光标和选择区域
- `Canvas.RequestFrame()` 下一帧重绘请求
- `ScrollViewer`、可选择 `List`、层级 `Tree`、分页 `Swiper`、`Popup`、`Dialog`、`MenuBar`、多级 `Menu` 与命令式 `ContextMenu`
- CSS Animation / `@keyframes` 基础支持
- Theme 系统（`ThemeProvider` 主题切换）
- `Document` / `UIDocument` 文档模型（UI/Head/Body 壳），以及 `XMLDocument` / `SVGDocument` 嵌入文档模型
- `FontFace` / `FontFaceSet` CSS Font Loading 子集
- 内置结构指令目录与发射管线（第三方自定义指令发射仍为实验性接口）
- `Reconciler` 批量脏标记与更新调度
- `.sqv` Vue 模板语法前端：`{{ }}` 插值、`:prop` / `v-bind`、`@event` / `v-on`、`v-if` / `v-else-if` / `v-else`、`v-for`、`:key`、`ref` 及事件修饰符（`.stop` / `.prevent`）
- `Square.Extensions` 扩展模块：`MarkdownViewer` 控件（基于 Markdig，将 Markdown 渲染为 Square 元素树）
- `Square.Extensions.RichText`：富文本文档模型、跨 run 布局、软换行、selection/caret/hit test、格式命令与 `RichTextEditor` 控件
- `Square.DevTools`：localhost HTTP 调试服务，支持 renderer PNG 截图和鼠标、键盘、文本、滚轮模拟输入
- 进程内 renderer 截图：`DesktopApplication.CaptureRendererBitmapAsync()` 将保留的 DisplayTree 离屏重放为 Bitmap，不依赖 PID、窗口枚举或桌面合成器
- 纯 C# SVG：`SvgImage` 静态矢量资源，以及由 `SVGDocument` 管理、可直接写入 SQX/SQV 模板的 SVG DOM
- `Square.Images` 独立解码包：统一 `ImageDocument` 模型，支持纯 C# PNG/APNG、JPEG、BMP、GIF 动画、ICO/CUR 变体、TIFF 多页面与无损 WebP 动画；JPEG/TIFF 支持 Orientation；引用该包后 `<Image source="...">` 可异步加载本地文件并自动播放动画
- 平台截图（`PlatformScreenshot`，Win32 / X11 按进程 ID 捕获窗口位图）
- DOM `Range` 文本选择模型与 `TextFragment` 字符级命中测试
- Software Renderer 性能优化（位图像素/裁剪区域缓存、批量 BGRA 填充、圆角主体快速路径与边缘子像素光栅化）
- Software Renderer 内存生命周期优化（DPI glyph cache 清理、临时缓冲复用、关闭时及时释放 framebuffer）
- Software/Vulkan 高 DPI 文本共享逻辑 glyph advance，选择区、光标与实际字形位置保持一致
- Vulkan 文本按物理像素对齐，避免对灰度 glyph coverage 进行二次线性重采样
- Win32 在首次 Present 后显示窗口，避免启动瞬间出现未初始化黑色客户区

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

模板中可以直接声明 SVG。编译器会生成 `Square.UI.Svg` 下的浏览器式 SVG 元素类型，不使用运行时模板解析或反射：

```xml
<svg viewBox="0 0 100 100" width="100" height="100">
  <g transform="translate(10 10)" fill="#2b78ee">
    <rect x="0" y="0" width="80" height="80" rx="8" />
    <circle cx="40" cy="40" r="18" fill="#ffffff" />
    <path d="M 25 40 L 36 51 L 58 29"
          fill="none" stroke="#152241" stroke-width="5" />
  </g>
</svg>
```

每个根 `<svg>` 持有独立的 `SVGDocument : XMLDocument`。宿主 `UIDocument` 负责根 SVG 的盒布局，`SVGDocument` 负责内部节点查询、样式继承、变换、`viewBox` 和矢量绘制。当前不支持脚本、动画、滤镜、渐变、文本、外部资源和嵌入位图。

完整的内联 SVG 示例位于 `samples/Square.Sample/Components/MediaSamplesPage.sqx` 和 `samples/Square.Sample.Vue/Components/MediaSamplesPage.sqv`。

引用可选的 `Square.Images` 包后，`Image.Source` 会在后台加载本地文件路径。GIF、APNG 和动画 WebP 在控件挂载且可见时自动播放；隐藏或卸载时暂停/释放。加载失败保留占位并通过 `Image.Error` 与冒泡的 `loaderror` 事件报告：

```xml
<Image source="Assets/loading.gif" />
```

相对路径按应用当前工作目录解析。当前 `Source` 不支持 HTTP(S)、data URI 或嵌入资源。通过 `Source` 加载的文档由控件拥有；直接设置 `ImageContent` 时资源仍由调用方管理并优先显示，此时 `Source` 只作为替代文本。

也可以通过 `ImageDecoder` 手动解码为 `ImageDocument`。文档拥有其全部 `Bitmap`，图片使用期间必须保持文档未释放：

```csharp
using Square.Images;

using var document = ImageDecoder.Decode("photo.jpg");
image.ImageContent = document.PrimaryBitmap;
```

GIF87a/GIF89a、APNG 和动画 WebP 会公开所有完整合成帧、帧时长、循环信息、透明、blend 和 disposal；ICO/CUR 会公开全部尺寸/位深变体及 CUR 热点；Classic TIFF 会公开全部 IFD 页面。普通 PNG、JPEG、BMP 与静态 WebP 返回单项目文档。JPEG Exif 和 TIFF 页面 Orientation 默认应用，可通过 `ExifOrientationPolicy.Ignore` 保留原始像素方向。TIFF 当前支持大小端、未压缩 Strip、1/8 位灰度、Palette、8 位 RGB/RGBA 和 ExtraSamples Alpha；LZW/Deflate/PackBits、Tile、Planar Separate、CMYK/YCbCr 和 BigTIFF 尚未支持。WebP 支持 simple/VP8X 静态 VP8L 及 ANIM/ANMF 内嵌 VP8L 无损动画；VP8 lossy、独立 `ALPH` 和动画有损帧尚未支持。

```csharp
using var animation = ImageDecoder.Decode("animation.gif");

foreach (var frame in animation.Items)
{
    Bitmap bitmap = animation.GetBitmap(frame.Index);
    TimeSpan duration = frame.Duration;
}
```

```csharp
using var icon = ImageDecoder.Decode("app.ico");
Bitmap preferred = icon.PrimaryBitmap;

foreach (var variant in icon.Items)
    Console.WriteLine($"{variant.Width}x{variant.Height}, {variant.SourceBitDepth} bit");
```

```xml
<script lang="csharp" namespace="MyApp.Components" name="UserCard" access="internal">
</script>
```

SQX 支持无参和 `Event` 参数事件处理方法：

```csharp
private void OnClick() { }
private void OnClick(Event e) { }
```

模板逻辑也可以放在同名代码后置文件中。`Main.sqx.cs` 与生成的 `Main` 类型组成同一个 partial 类，命名空间和有效组件名必须与模板一致：

```csharp
namespace MyApp;

public partial class Main
{
    [Prop(Required = true)]
    public ObservableValue<string> Title { get; } = new("");

    private void Close(Event e) => AppWindow?.Close();
}
```

代码后置可声明 Props、事件方法、生命周期方法、Store 访问器，并可使用模板生成的 `ref` 字段。模板内 `<script>` 与代码后置允许共存。

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
Square.Compiler ────────► C# Component
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

主要程序集与逻辑模块：

| 项目 | 职责 |
|---|---|
| `Square` | 聚合桌面运行时；内部按 `Runtime`、`UI`、`Controls`、`Router`、`CSS`、`Graphics`、`Rendering`、`Text`、`Hosting`、`Platform` 和 Software Backend 等命名空间组织 |
| `Square.Compiler` | SQX/SQV 解析、语义检查和 Roslyn Source Generator，将模板编译为 C# 组件 |
| `Square.Platform.Win32` | Win32 窗口、输入、IME、剪贴板和窗口截图 |
| `Square.Platform.X11` | X11 窗口、输入、IME、剪贴板和窗口截图 |
| `Square.Backends.Vulkan` | 基于 Silk.NET 的 Square 原生 Vulkan GPU Renderer |
| `Square.Extensions` | 可选扩展组件与集成：`MarkdownViewer`、`RichTextEditor` 等，按需引用 |
| `Square.DevTools` | 本地 HTTP 调试与自动化：截图、输入模拟，按需引用 |

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

完整的入门指南见 [`docs/Getting-Started.md`](docs/Getting-Started.md)。API 参考见 [`docs/API-Reference.md`](docs/API-Reference.md)。DevTools 调试服务见 [`docs/DevTools.md`](docs/DevTools.md)。

### 创建第一个 Square 应用

一个最小桌面应用只需创建窗口、加载根组件并运行：

```csharp
using Square.Hosting;

var window = new AppWindow("My App", 800, 600);
window.Load(new Main());

new DesktopApplication(window).Run();
```

`Main` 是由 `.sqx` 文件在编译期生成的组件。引用 `Square.Platform.Win32` 或 `Square.Platform.X11` 后，平台包会自动注册默认宿主。窗口内容、渲染模式和后端配置属于 `AppWindow`；`DesktopApplication` 负责应用生命周期和消息循环。

自定义标题栏：

```csharp
window.LoadCustomTitleBar(new MyTitleBar());
window.TitleStyle = TitleStyle.Custom;
```

`MyTitleBar.sqx` 可使用内置 `<TitleBar>`。标题栏空白区域默认拖动窗口，Button、Input 等交互控件保持正常点击。

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

使用 Square 原生 Vulkan 后端：

```bash
dotnet run --project samples/Square.Sample/Square.Sample.csproj -- --backend Vulkan
```

Vulkan 默认关闭 GPU readback，以减少 host-visible buffer 和每帧拷贝。需要捕获真实 GPU 帧时可显式开启：

```powershell
$env:SQUARE_VULKAN_READBACK = "1"
dotnet run --project samples/Square.Sample/Square.Sample.csproj -- --backend Vulkan --screenshot artifacts/vulkan.png
```

更多 Vulkan 资源和诊断选项见 [`docs/Rendering.md`](docs/Rendering.md)。

运行 Vue 模板语法示例（`.sqv`）：

```bash
dotnet run --project samples/Square.Sample.Vue/Square.Sample.Vue.csproj
```

运行 RichText 编辑器示例：

```bash
dotnet run --project samples/Square.Sample.RichText/Square.Sample.RichText.csproj
```

RichText 示例包含格式工具栏、颜色、清除格式、撤销/重做、全选、软换行、鼠标选择、键盘导航和纯文本预览。

RichText 示例会同时启动 DevTools 服务：

```text
http://127.0.0.1:<自动分配端口>/api/v1
X-Square-DevTools-Token: <启动时生成的随机令牌>
```

实际地址会在启动时输出。多个程序并行运行时，各实例由操作系统分配独立端口；调用方应读取 `DevToolsServer.BaseAddress`，不要假设固定端口。

常用接口：

```text
GET  /health
GET  /screenshot
POST /input/pointer
POST /input/key
POST /input/text
POST /input/wheel
```

完整接口、认证和自动化示例见 [`docs/DevTools.md`](docs/DevTools.md)。

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

主示例通过 `SquareSamplePublishAot=true` 启用 NativeAOT。Windows x64 发布命令：

```bash
dotnet publish samples/Square.Sample/Square.Sample.csproj \
  -c Release \
  -r win-x64 \
  -p:SquareSamplePublishAot=true \
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
  -p:SquareSamplePublishAot=true \
  -p:SquareTargetPlatform=X11 \
  --self-contained true
```

输出通常位于：

```text
samples/Square.Sample/bin/Release/net10.0/linux-x64/publish/
```

NativeAOT 默认只包含 Software 后端，Vulkan 和 DevTools 项目都不会进入引用图。需要时分别显式传入 `SquareSampleUseVulkan=true` 和 `SquareSampleUseDevTools=true`：

```bash
dotnet publish samples/Square.Sample/Square.Sample.csproj \
  -c Release \
  -r win-x64 \
  -p:SquareSamplePublishAot=true \
  -p:SquareSampleUseVulkan=true \
  -p:SquareSampleUseDevTools=true \
  --self-contained true
```

Software、Vulkan 与可选 DevTools 服务均按 NativeAOT 约束设计，并已提供本地发布路径；CI 持续验证 Win32/X11 Software AOT 发布。Vulkan 与 DevTools 的组合发布仍属于实验性验证范围。Vulkan 使用系统 loader 和构建期内嵌的 SPIR-V，DevTools 使用显式 `HttpListener` 路由和 JSON 读写，均不依赖运行时代码生成。启用 Vulkan 后可直接选择 Vulkan 并启动 DevTools：

```powershell
samples/Square.Sample/bin/Release/net10.0/win-x64/publish/Square.Sample.exe --backend Vulkan --devtools
```

### 平台裁剪

Square 通过 `SquareTargetPlatform=Win32|X11`、构建层 `DefineConstants`（`PLATFORM_WIN32` / `PLATFORM_X11`）和条件包含源文件来裁剪平台代码。该属性会传播到项目引用，避免在 Windows 上交叉发布 Linux 时误编译 Win32 类库：

- 在 Windows 上构建 → 引用 `Square.Platform.Win32`，注册 Win32 宿主
- 在 Linux 上构建，或使用 `-r linux-x64 -p:SquareTargetPlatform=X11` 交叉编译 → 引用 `Square.Platform.X11`，注册 X11 宿主
- macOS 宿主尚未实现，`osx-*` 构建会明确失败，不会错误回退到 X11

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
- [DevTools 调试服务](docs/DevTools.md)
- [组件组合与信号](docs/Composition-and-Signals.md)
- [Source Generator](docs/Generator.md)
- [编码规范](docs/CodingStyle.md)
- [开发路线](docs/Roadmap.md)
- [需求说明](docs/Requirements.md)

## 项目状态

Square 目前适合框架设计验证、实验和贡献开发，不建议用于生产项目。当前工作的重点是完成核心输入/事件管线、扩展 CSS 与布局能力、完善控件行为，并继续验证 NativeAOT 和模块边界。

## 贡献

贡献流程见 [`CONTRIBUTING.md`](CONTRIBUTING.md)。安全问题请按 [`SECURITY.md`](SECURITY.md) 私下报告；一般支持范围见 [`SUPPORT.md`](SUPPORT.md)。参与项目即表示同意遵守 [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)。

提交改动前请确保：

```bash
dotnet build Square.slnx
dotnet test Square.slnx
```

代码风格和模块依赖约束见 [`docs/CodingStyle.md`](docs/CodingStyle.md) 与 [`docs/Architecture.md`](docs/Architecture.md)。新增功能应附带对应测试，并避免引入运行时反射、动态代码生成或不必要的跨层依赖。

## License

Square 使用 [MIT License](LICENSE)。
