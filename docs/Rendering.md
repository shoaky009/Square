# 渲染架构

> Version: 0.2  
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
Visual Tree
  ↓ (Square.Layout)
Layout (几何计算)
  ↓ (Square.Rendering)
Render Tree (DrawCommand 列表)
  ↓ (Square.Graphics)
IRenderContext
  ↓ (Square.Backends)
Backend (Software / Skia / ...)
```

---

## 3. Visual Tree

### 3.1 节点

- `Visual`：基类型，持有几何、变换、可见性
- `UIElement`：带事件、输入、焦点的视觉节点
- 控件继承 `UIElement`

### 3.2 构建

- 由 Source Generator 生成的 `BuildVisualTree()` 构建
- `<Show>` 条件子树支持**挂卸**
- `<For>` 列表支持**增量增删**（keyed）
- 命令式 `AppendChild`/`RemoveChild` 操作静态区域

### 3.3 脏标记

- 属性变化 → 标记节点脏
- 脏节点 → 触发 Layout → 触发 Render Tree 更新
- 增量更新，不全量重建

---

## 4. Layout 阶段

- 调用 `Square.Layout` 计算几何
- 测量（Measure）→ 排列（Arrange）
- 高 DPI 物理像素对齐
- 详见 `Layout.md`

---

## 5. Render Tree

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

- Visual Tree → Layout → 遍历生成 DrawCommand 列表
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

### 6.2 子树挂卸

- `<Show>` 条件变化 → 子树挂载/卸载
- 挂载：构建 Visual 子树 → Layout → 加入 Render Tree
- 卸载：从 Render Tree 移除 → 释放资源

### 6.3 列表增量

- `<For>` 列表变化 → keyed 增量增删
- 项移动时节点不重建，仅调整位置
- 项新增 → 创建节点；项删除 → 卸载节点

---

## 7. 后端切换

```
IRenderContext (抽象)
  ├── SoftwareBackend   (纯 C# CPU 渲染, M1)
  ├── SkiaBackend       (M4)
  ├── Blend2DBackend    (M4)
  └── CairoBackend      (M4)
```

- 同一 `IRenderContext` 接口
- 构建层裁剪决定装配哪个后端
- 切换后端不影响 Render Tree 逻辑

---

## 8. 高 DPI

- 布局按逻辑像素，光栅按物理像素
- 物理像素对齐避免模糊
- 支持多显示器不同 DPI

---

## 9. 性能目标

- 脏区增量重绘
- DrawCommand 列表复用
- 减少全量 Layout
- 高刷新率支持（60/120/144Hz）