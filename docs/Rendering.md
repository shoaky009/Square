# 渲染架构

> Version: 0.4  
> 配套：`Architecture.md`、`Graphics.md`、`Layout.md`

---

## 1. 渲染模式

采用 **保留模式（Retained Mode）**。

不采用 Immediate Mode。

---

## 2. 管线

```
SQX
  ↓ (Source Generator, 编译期)
Component (C#)
  ↓ (组件构建)
Element Tree
  ↓ (Square.Rendering.Layout)
Layout (几何计算)
  ↓ (Square.Rendering)
Display Tree (DrawCommand 列表)
  ↓ (Square.Graphics)
IRenderContext
  ↓ (Square.Backends)
Backend (Software / Vulkan / Impeller / ...)
```

---

## 3. Element Tree

### 3.1 节点

- `Element`：基类型，持有几何、变换、可见性
- `UIElement`：带事件、输入、焦点的视觉节点
- 控件继承 `UIElement`

### 3.2 构建

- 由 Source Generator 生成的 `BuildElementTree()` 构建
- `<Show>` 条件子树支持**挂卸**
- `<For>` 列表支持**增量增删**（keyed）
- 命令式 `AppendChild`/`RemoveChild` 操作静态区域

### 3.3 脏标记

- 属性变化 → 标记节点脏
- 脏节点 → 触发 Layout → 触发 Display Tree 更新
- 增量更新，不全量重建

---

## 4. Layout 阶段

- 调用 `Square.Rendering` 程序集中的 `LayoutEngine` 计算几何
- 测量（Measure）→ 排列（Arrange）
- 高 DPI 物理像素对齐
- 详见 `Layout.md`

---

## 5. Display Tree

### 5.1 DrawCommand

| 命令 | 说明 |
|---|---|
| `FillRect` | 填充矩形 |
| `DrawText` | 绘制文本 |
| `DrawPath` | 绘制路径 |
| `DrawImage` | 绘制图片 |
| `PushClip` | 推入裁剪 |
| `PopClip` | 弹出裁剪 |
| `PushTransform` | 推入变换 |
| `PopTransform` | 弹出变换 |

### 5.2 构建

- Element Tree → Layout → 遍历生成 DrawCommand 列表
- 保留模式：脏区驱动增量重绘

### 5.3 提交

- 调用 `IRenderContext` 提交 DrawCommand
- Backend 负责实际绘制

---

## 6. 脏区与增量

### 6.1 脏区管理

- 节点几何变化 → 标记脏区
- 合并脏区减少重绘次数
- 仅重绘脏区范围内的 DrawCommand
- `VisualBounds` 使用 DrawCommand 的实际视觉范围，而不是只使用元素 `Geometry`
- Path、clip、transform、popup 等都会参与脏区计算，避免局部重绘漏绘或过度扩大

### 6.2 渲染模式

宿主支持三种渲染模式：

| 模式 | 说明 |
|---|---|
| `FullFrame` | 每帧全窗口清屏并重绘，默认模式，优先保证正确性 |
| `DirtyRegion` | 强制使用脏区局部重绘，用于压测和诊断脏区路径 |
| `Auto` | 根据 dirty rect 数量和面积比例自动选择脏区或全帧 |

`Auto` 会在以下情况回退全帧：

- layout dirty，需要重新布局
- 没有 dirty rect，但仍请求了渲染
- dirty rect 数量超过 `MaxDirtyRectCount`
- dirty area 比例超过 `MaxDirtyAreaRatio`

当前默认仍为 `FullFrame`，因为它是最稳定的正确性基线。DirtyRegion 和 Auto 用于逐步验证和优化局部重绘路径。

### 6.3 渲染诊断 Overlay

`DesktopApplication` 提供渲染诊断开关：

| 属性 | 说明 |
|---|---|
| `ShowRenderDiagnosticsOverlay` | 在窗口左上角绘制文字诊断信息 |
| `ShowDirtyUnionOverlay` | 在画面上绘制 dirty union 外框 |
| `LastRenderDiagnostics` | 最近一帧的渲染模式、决策原因、dirty 数量、面积比例和 union |

文字诊断 overlay 会显示：

- 当前 `RenderMode`
- 当前帧使用 full frame 还是 dirty region
- 决策原因，例如 `DirtyRegion`、`LayoutDirty`、`TooManyDirtyRects`、`DirtyAreaTooLarge`、`NoDirtyRects`
- dirty rect 数量
- dirty area 比例
- dirty union 矩形

Sample 支持命令行和环境变量配置：

```powershell
dotnet run --project "samples/Square.Sample/Square.Sample.csproj" -- --render-mode Auto --render-overlay true --dirty-overlay true
```

可用参数：

```text
--render-mode FullFrame|Auto|DirtyRegion
--render-overlay true|false
--dirty-overlay true|false
--max-dirty-area 0.35
--max-dirty-rects 16
```

对应环境变量：

```text
SQUARE_RENDER_MODE
SQUARE_RENDER_OVERLAY
SQUARE_DIRTY_OVERLAY
SQUARE_MAX_DIRTY_AREA
SQUARE_MAX_DIRTY_RECTS
```

Debug 构建的 `Square.Sample` 支持按 `F12` 切换 `ShowRenderDiagnosticsOverlay`。标题栏会显示当前状态：

```text
Square Framework - Overlay: On
Square Framework - Overlay: Off
```

### 6.4 子树挂卸

- `<Show>` 条件变化 → 子树挂载/卸载
- 挂载：构建 Element 子树 → Layout → 加入 Display Tree
- 卸载：从 Display Tree 移除 → 释放资源

### 6.5 列表增量

- `<For>` 列表变化 → keyed 增量增删
- 项移动时节点不重建，仅调整位置
- 项新增 → 创建节点；项删除 → 卸载节点

---

## 7. 后端切换

```
IRenderContext (抽象)
  ├── SoftwareBackend   (纯 C# CPU 渲染)
  ├── VulkanBackend     (Silk.NET 原生 Vulkan)
  ├── ImpellerBackend   (Flutter Impeller Vulkan)
  └── Future Backends   (Skia / Blend2D / Cairo / ...)
```

- 同一 `IRenderContext` 接口
- 构建层裁剪决定装配哪个后端
- 切换后端不影响 Display Tree 逻辑

### 7.1 原生 Vulkan 后端

`Square.Backends.Vulkan` 直接基于 Silk.NET Vulkan API，实现 swapchain、render pass、pipeline、批处理、纹理 atlas、MSAA resolve 和可选 GPU readback。Win32 与 X11 宿主通过 `NativeTarget` 提供平台 surface 信息。

在主示例中启用：

```bash
dotnet run --project samples/Square.Sample/Square.Sample.csproj -- --backend Vulkan
```

应用自行注册时调用：

```csharp
VulkanRegistration.Register();
```

Vulkan 配置均在创建 RenderContext 前通过环境变量读取：

| 环境变量 | 值 | 默认行为 |
|---|---|---|
| `SQUARE_VULKAN_VALIDATION` | `1` / `true` | 关闭；开启时需要可用的 Vulkan validation layer |
| `SQUARE_VULKAN_READBACK` | `1` / `true` | 关闭；开启后 `CaptureRendererBitmapAsync()` 可读取真实 GPU 帧 |
| `SQUARE_VULKAN_MSAA` | `1` / `2` / `4` | 小于等于约 300 万物理像素时 2x，更大窗口 1x，并受设备能力限制 |
| `SQUARE_VULKAN_ATLAS_SIZE` | `512` / `1024` / `2048` | `1024` |
| `SQUARE_VULKAN_EXTRA_SWAPCHAIN_IMAGE` | `1` / `true` | 关闭；默认请求 surface 最小图像数 |

GPU readback 默认关闭，因为它需要额外的 host-visible buffer 和 GPU 到 CPU 拷贝。关闭时截图 API自动回退为 Software RenderContext 重放；开启时截图反映真实 Vulkan framebuffer。

Shader 源码位于 `src/Square.Backends.Vulkan/Shaders/`，修改后运行以下命令重新生成内嵌 SPIR-V：

```bash
dotnet run --project tools/ShaderGen
```

---

## 8. 高 DPI

- 布局按逻辑像素，光栅按物理像素
- 物理像素对齐避免模糊
- 支持多显示器不同 DPI
- Vulkan 在普通轴对齐 DPI 变换下，将文本原点、glyph offset 和 advance 映射到整数物理像素，避免已经抗锯齿的 coverage atlas 再被线性过滤一次
- 旋转、斜切或额外缩放的文本保留浮点几何和过滤路径

---

## 9. 性能目标

- 脏区增量重绘
- DrawCommand 列表复用
- 减少全量 Layout
- 高刷新率支持（60/120/144Hz）

### 9.1 内存生命周期

- Software framebuffer 使用托管 BGRA 数组；`Bitmap.Dispose()` 会立即断开像素数组引用，使 LOH framebuffer 在无其他引用时可回收
- Software RenderContext 在 DPI 变化时清理旧物理字号的 glyph coverage cache，并复用 dirty rect 与 polygon scanline 临时缓冲
- Win32 host 关闭时解除 RenderContext、最后一帧、事件委托和静态当前宿主引用，避免窗口关闭后继续根引用 framebuffer 与 glyph cache
- Vulkan atlas 仅保留 GPU 图像和紧凑 staging uploads，不保留完整 CPU atlas 镜像
- Vulkan readback、额外 swapchain image 和更高 MSAA 均为显式或受控配置，避免默认资源占用过高
