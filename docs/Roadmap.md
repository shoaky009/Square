# 开发路线

> Version: 0.2  
> 配套：`Architecture.md`、`plan.md`

---

## 1. 分阶段路线图

| 里程碑 | 目标 | 退出标准 |
|---|---|---|
| **M0 脚手架** | 解决方案与全部 `Square.*` 空项目、目录规范、AOT/Trim 发布配置 | 空项目可编译 |
| **M1 Phase 1 MVP** | 编译优先可运行 Demo：`.sqx`→C#、Props、ref、基础 CSS、flex 布局、纯 C# 软件渲染、基础控件、事件、Win32 宿主、构建层裁剪、生命周期、NativeAOT 验证 | `.sqx` 示例经 Source Generator 编译为 AOT 可执行，窗口渲染并响应交互；Props 传值校验、ref 操作、`<Show>`/`<For>` 可用 |
| **M2 CSS 完整化 + 组件组合 + 动画 + 主题** | 默认/具名 Slot、fallback、嵌套组件；`Signal<T>` 跨组件/跨线程通信；完整 Selector/Cascade/Pseudo/Animation；Grid；Theme；元素查询 API | 插槽保持调用方作用域且不产生隐式布局容器；后台信号经 Dispatcher 安全送达 UI；CSS 测试套件通过 |
| **M3 扩展控件 + 路由** | `Square.Router` 内存路由、参数、通配符、嵌套布局、Link；基于 Slot 的 Tabs；List/Tree/Menu/Dialog/ScrollViewer/Grid/Popup/Window/Swiper | 路由可前进/后退并正确切换生命周期；Tabs 可组合页面且保留状态；各控件可交互 |
| **M4 图形后端扩展** | Skia / Blend2D / Cairo 后端接入（`IRenderContext` 不变） | 同一 Demo 切换后端渲染一致 |
| **M5 跨平台桌面** | Linux(X11)、macOS 平台宿主；高 DPI/高刷新率打磨 | 三桌面平台 AOT 可执行均运行 |
| **M6 移动端与 WebAssembly** | Android / iOS / WASM 平台层（最小实现） | 目标平台可启动并渲染基础 UI |
| **M7 文本与 Canvas 完整** | BiDi、Font Fallback、Caret/Selection/HitTest 完整、Canvas `CanvasRenderingContext2D` 兼容层→DrawCommand | 复杂文本/编辑与 Canvas 绘图可运行 |
| **M8 工具链** | 完整 Source Generator 诊断、IDE 智能提示/补全、编译期检查 | IDE 内 `.sqx` 报错可定位、可补全 |

---

## 2. 排期建议（相对）

| 里程碑 | 预估 |
|---|---|
| M0 | 约 1 周 |
| M1 | 约 6–8 周（可并行：Generator/Markup 线、Graphics/Backend 线、Controls/Layout 线） |
| M2–M8 | 每个约 2–4 周，M1 验收后细化 |

---

## 3. M1 任务清单

[x] M0：创建 `Square.slnx` 与全部 `Square.*` 项目 + 发布/AOT 配置
[x] `Square.Markup`：`.sqx` 解析器 + AST + 单测（严格顶级 section + script 元数据）
[x] `Square.SourceGenerator`：Incremental Generator + Props 校验 + ref 生成 + 绑定编译 + 诊断映射
[x] `Square.CSS`：Tokenizer/Selector/Cascade/Variables/Inheritance（含子代/兄弟/通用选择器、`!important`、基础伪类）
[x] `Square.Layout`：Box + Flex + 尺寸解析（px/%/rp/vw/vh/auto）+ 高 DPI
[x] `Square.Graphics`：`IRenderContext`/`IRenderBackendFactory` + 基础类型
[~] `Square.Backends`：纯 C# Software Renderer（BGRA/预乘 Alpha ✓ / SIMD 待实现 / 脏区待实现）
[~] `Square.Rendering`：Visual→Render Tree→DrawCommand→提交（子树挂卸 ✓ / 增量保留模式待实现）
[x] `Square.Runtime` + `Square.UI`：Application/Visual 基类/属性/元素操作 API（Style/ClassList/Children/Event）
[x] `Square.Controls`：10 个第一阶段控件 + 结构原语（Show/For/Switch/Match）+ 默认样式
[x] `Square.Text`：FontManager/测量/绘制（基础）
[x] `Square.Platform`：Win32 宿主 + 输入泵（`LibraryImport`）+ Mouse/Key/Wheel/IME/Clipboard
[x] `Square.Animation`：Clock/Easing 最小实现
[x] `Square.Tooling`：基础诊断输出
[x] 事件系统：Mouse/Keyboard/Focus/Wheel + `.sqx` 绑定 + Click 合成
[x] 绑定：`ObservableValue<T>` + `ObservableCollection<T>` + 生成期绑定
[x] Props：`[Prop]` 特性 + `ObservableValue<T>` 包装 + 编译期校验（必填 + 类型）+ `OnPropChanged`
[x] ref：模板标记 + 强类型字段生成 + 挂载/卸载赋值 + 重复名称诊断
[x] 示例 + NativeAOT 发布验证 + 基线指标（2.53 MiB EXE，512ms 启动，32 MB 内存）
[~] 构建层裁剪：C# `#if` + MSBuild `DefineConstants` ✓ / 条件 `ProjectReference` 待实现 / trim 注解待添加
[x] 流程控制结构原语：`<Show>`/`<For>`/`<Switch>`/`<Match>` + `ObservableCollection<T>`
[x] 组件/应用生命周期钩子（OnAttached/OnDetached/OnLoaded/OnUnloaded + Application.OnStart/OnExit）

---

## 4. 风险与缓解

| 风险 | 缓解 |
|---|---|
| Source Generator 增量缓存导致 IDE 诊断滞后 | 严格设计缓存键；单测覆盖 |
| 纯 C# 软件渲染性能不足 | 预乘 Alpha + SIMD + 脏区；M4 接 Skia |
| 完整 CSS/布局工作量巨大 | M1 仅子集，M2 扩展 |
| NativeAOT 裁剪误删后端/平台代码 | 构建层裁剪 + 显式注册 + trim 注解 |
| 文本引擎复杂 | M1 仅基础，M7 引入完整 BiDi/Fallback |
| Props/ref 生成器复杂度 | 先做基础形态，查询/高级操作后置 M2 |

---

## 5. 下一步

M0（脚手架 + 解决方案 + 13 个 `Square.*` 项目）启动实施。
