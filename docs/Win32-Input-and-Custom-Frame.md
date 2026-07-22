# Win32 输入与自定义窗口框架

> 配套：`Architecture.md`、`Rendering.md`

本文记录 Win32 宿主处理鼠标移动和自定义标题栏非客户区的约束，避免高频输入造成不必要的 CPU 开销，并避免窗口激活状态变化后出现系统白色边框。

---

## 1. 鼠标移动事件

### 1.1 单一事件来源

`Win32Host` 仅通过 `WM_MOUSEMOVE` 派发 `MouseAction.Move`。

`WM_SETCURSOR` 只负责应用当前光标，不再查询鼠标位置或补发移动事件。Windows 通常会在一次物理移动中同时产生 `WM_MOUSEMOVE` 和 `WM_SETCURSOR`；若两处都派发，命中测试、悬停状态计算和控件交互会重复执行。

### 1.2 悬停路径快速路径

`DesktopApplication` 保存当前 `_hoverPath`。鼠标仍位于同一个最深层 `UIElement` 时，元素祖先链不会变化，因此直接返回，不构建新的路径列表。

只有鼠标跨越元素边界时才执行完整路径比较，并更新 `ElementState.Hover`。这可以减少高采样率鼠标在大窗口内移动时的短生命周期分配。

### 1.3 弹出控件处理

普通鼠标移动不得调用 `_root.QueryAll<Select>()`。该调用会遍历整个 Element Tree 并创建结果列表，开销会随页面规模增长。

`DisplayTree` 在构建时已经维护 `_popups` 缓存。下拉框选项悬停通过该缓存处理，只检查已登记的弹出元素，同时保持关闭下拉框清除悬停状态的既有行为。

---

## 2. 自定义标题栏与非客户区

### 2.1 保留 `WS_THICKFRAME`

可缩放的自定义窗口继续使用 `WS_THICKFRAME`，并通过 `WM_NCHITTEST` 返回 `HTLEFT`、`HTRIGHT`、`HTTOP`、`HTBOTTOM` 和四个角的命中结果。

不能仅删除 `WS_THICKFRAME` 来隐藏边框，否则会破坏系统窗口缩放、最大化约束和相关窗口管理行为。

### 2.2 客户区覆盖整个窗口

自定义标题栏窗口接管以下非客户区消息：

| 消息 | 处理 |
|---|---|
| `WM_NCCALCSIZE` | 返回 `0`，让客户区覆盖整个窗口 |
| `WM_NCPAINT` | 返回 `0`，阻止系统绘制 `WS_THICKFRAME` 外框 |
| `WM_NCACTIVATE` | 返回 `1` 完成激活状态切换，但不调用 `DefWindowProc` 重绘非客户区 |
| `WM_NCHITTEST` | 保留应用定义的窗口边缘缩放命中 |

只设置 `DWMWA_BORDER_COLOR` 不足以解决焦点切换后的粗白边。该属性控制 Windows 11 DWM 合成的外边框，而 `DefWindowProc` 仍可能在 `WM_NCACTIVATE` 或 `WM_NCPAINT` 中绘制传统非客户区框架。

### 2.3 DWM 外观

自定义窗口同时设置：

- `DWMWA_WINDOW_CORNER_PREFERENCE`：普通窗口使用圆角，最大化窗口禁用圆角。
- `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE`：禁止 Windows 11 DWM 绘制额外外边框。

系统标题栏窗口不接管这些消息，继续使用 Windows 默认非客户区行为。

---

## 3. 回归检查

修改 Win32 输入或窗口样式后至少验证：

1. 鼠标在同一控件内持续移动时不会重复触发命中路径更新。
2. 下拉框打开后选项悬停和离开状态正确。
3. 自定义窗口反复获得和失去焦点后不出现白色或主题色外框。
4. 窗口四边和四角仍可拖动缩放。
5. 最大化、还原和 DPI 变化后客户区尺寸正确。
6. 系统标题栏窗口的激活边框和标题栏行为不受影响。

验证命令：

```powershell
dotnet test "tests/Square.UI.Tests/Square.UI.Tests.csproj" --no-restore
dotnet test "tests/Square.Platform.Tests/Square.Platform.Tests.csproj" --no-restore
dotnet build "Square.slnx" --no-restore
```
