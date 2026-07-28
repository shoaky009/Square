# Square.Extensions.CodePad — 开发文档

本文档是 **CodePad** 包的设计与实现指南，随源码维护。面向贡献者与后续迭代。

---

## 1. 定位

| 项 | 说明 |
|---|---|
| 程序集 | `Square.Extensions.CodePad`（独立项目，**不**并入 `Square.Extensions`） |
| 依赖 | 仅 `Square` |
| 控件标签 | `<CodePad />`（需 `CodePadRegistration.RegisterDefaults()`） |
| 对标 | 编辑内核自研；语言层对齐 **Monaco Monarch** + **VS Code language-configuration / 主题子集** |
| 非目标 | Roslyn、LSP、补全、诊断、完整 TextMate、嵌 WebView Monaco / Avalonia |

与 `Square.Extensions`（Markdown / RichText / Routing）并列，按需引用。

---

## 2. 架构

```
CodePad (UIElement + ITextEditor)
  ├── Model/          PieceTable · TextModel · EditStack
  ├── View/           Viewport · Painter · HitTest · Gutter
  ├── Language/       Registry · Configuration · Monarch · Themes
  └── Commands/       内置键位 → 编辑命令
```

数据流：

```
Edit → Model.ContentChanged
     → Tokenizer.Invalidate (Phase 3)
     → Viewport 可见行
     → Theme 上色
     → Painter
```

### 2.1 与 Square 其它模块

| 模块 | 关系 |
|---|---|
| `Square` | 唯一项目依赖：UIElement、ITextEditor、Graphics、宿主焦点/IME |
| `Square.Extensions` | **无**引用；**不**通过 `ExtensionRegistration` 注册 |
| `TextEditorBase` / `TextArea` | 不作为内核基类（全串 + 全量绘制不适合大文件） |
| `RichText` | 不同文档模型；不共享实现 |

---

## 3. 语言方案（对齐 Monaco / VS Code）

| 层 | 对齐 | 实现计划 |
|---|---|---|
| languageId / extensions / aliases | Monaco + VS Code | `LanguageRegistry` |
| 编辑配置 | VS Code `language-configuration.json` | `LanguageConfiguration` 解析与消费 |
| 语法分词 | Monaco **Monarch** | `ITokenizer` + Monarch 运行时（.NET Regex 子集） |
| 主题 | VS Code Color Theme 子集 | `CodePadTheme` + token 规则 |
| 语义高亮 / LSP | — | **不做** |

### 3.1 注册概念映射

| Monaco / VS Code | Square.Extensions.CodePad |
|---|---|
| `languages.register` | `LanguageRegistry.Register` |
| `setLanguageConfiguration` | `LanguageContribution.Configuration` |
| `setMonarchTokensProvider` | Monarch 定义 → `ITokenizer`（Phase 3） |
| Color Theme | `CodePadThemeRegistry` |
| `editor.language` | `CodePad.Language` |

### 3.2 Language Configuration（Phase 2）

优先实现 VS Code schema 字段：

- `comments`（行/块注释切换）
- `brackets`（括号匹配）
- `autoClosingPairs` / `autoCloseBefore`
- `surroundingPairs`
- `wordPattern`
- `indentationRules` / `onEnterRules`（基础）

### 3.3 Monarch（Phase 3）

- 定义格式兼容 Monaco Monarch **常用子集**
- 按行增量 tokenize，保留行末状态
- 输出 `TokenSpan { start, length, type }`
- 语言包：`Languages/{id}.monarch.json` + `{id}.language-configuration.json`
- 从 monaco-editor 语言定义移植时保留许可证与归因

**不实现：** 完整 Oniguruma TextMate、injection grammars、语义 token。

---

## 4. 分阶段路线图

### Phase 0 — 工程骨架（当前）

- [x] 独立 csproj + solution 条目
- [x] `CodePadRegistration` / `CodePad` 壳 / 模型占位
- [x] `LanguageRegistry` / `CodePadThemeRegistry` 内置 plaintext + 默认主题
- [x] 本开发文档
- [ ] 测试项目与基础单测

### Phase 1 — 大文件编辑内核

**必达**

| 模块 | 内容 |
|---|---|
| PieceTable | 替换 `CodePadTextModel` 内部实现，公开 API 不变 |
| 行表 | `GetLineContent` / offset↔(line,col) O(log n) 或摊销高效 |
| 增量 Undo/Redo | 操作逆元，禁止全文 snapshot |
| 视口虚拟化 | 只布局/绘制可见行 ± overscan |
| 固定行高 + monospace | `tab-size` 列网格展开 `\t` |
| 编辑 | 输入、Enter、BS/Del、方向/Home/End、词跳转、Tab、单选区、指针 |
| 宿主 | 完整 `ITextEditor`、IME caret、纯文本剪贴板 |

**验收：** 万行级打开/滚动/键入可测；禁止每帧 `DrawText(全文)`。

### Phase 2 — Language Configuration

- 解析 VS Code configuration JSON
- 自动闭合、注释切换、wordPattern 选词、onEnter、括号匹配
- 内置若干语言 configuration 资源

### Phase 3 — Monarch + 主题

- Monarch 运行时 + 脏行增量 tokenize
- 视口按 token 分段绘制
- VS Code 主题子集；批量语言包（3a 常用 → 3b 扩展）

### Phase 4 — Chrome

- 行号 gutter、当前行高亮、查找替换、soft wrap（可选）
- Glyph margin、行 decoration、只读模式

### Phase 5 — 性能 · Chrome · 查找

- `Element.InvalidatePaint(Rect)` 局部脏区；CodePad caret 闪烁走局部失效
- Overview ruler（装饰色 / 查找匹配 / 视口指示）
- Find 面板状态：`FindPanelVisible`、`FindMatchCount`、`FindMatchIndex`、`GetFindMatchLines`
- 主题 `OverviewRulerBackground` / `OverviewRulerBorder`；装饰 `OverviewRulerColor`
- 折叠块整块编辑：`SelectCollapsedFoldAt` / `TryGetFoldDocumentRange` / `SelectRange`
  - Shift+点击折叠槽、双击 `⋯` 选中折叠
  - 选区与折叠相交时，`SelectedText` 与删除/输入会扩展到隐藏行
  - 折叠头上 Delete/Backspace 删除整块折叠
- 多光标：`AddCursor` / `ClearExtraCursors` / `SetCursors` / `CursorCount`
  - Alt+点击添加/切换光标；普通点击或 Esc 清除附加光标
  - 输入、Delete、Backspace、方向键对各光标同步生效
  - Shift+方向键 / Shift+Home/End 扩展各光标选区；Ctrl+Shift+方向键按词扩展

### 计划外

- Roslyn / LSP / 补全 / 诊断 / Code Fix
- 完整 TextMate、完整 minimap、diff 编辑器

---

## 5. 目录约定

```
src/Square.Extensions.CodePad/
  CodePad.cs
  CodePadRegistration.cs
  Model/                 # 文档缓冲与编辑事务
  View/                  # 视口与绘制（Phase 1+）
  Language/              # 注册、配置、分词、主题
  Languages/             # 嵌入 JSON 语言包（Phase 2+）
  Commands/              # 可选
  docs/
    DEVELOPMENT.md       # 本文
    ROADMAP.md           # 阶段检查清单（可与本文同步）
  README.md              # 包说明与快速开始
```

测试：

```
tests/Square.Extensions.CodePad.Tests/
```

---

## 6. 公共 API 约定

### 注册

```csharp
using Square.Extensions.CodePad;

CodePadRegistration.RegisterDefaults();
```

**不要**依赖 `Square.Extensions.ExtensionRegistration` 注册 CodePad。

### 控件

```csharp
var pad = new CodePad
{
    Language = "csharp",
    TabSize = 4,
    InsertSpaces = true,
    ShowLineNumbers = true,
};
pad.Model.SetValue(source); // 大文件优先
// 或 pad.Value = source;
```

### SQX / SQV

```xml
<CodePad Language="json" ShowLineNumbers="true" />
```

应用须先 `CodePadRegistration.RegisterDefaults()`，并引用本项目（及生成器若需要）。

### 大文件

- 宿主应优先 `Model.SetValue` / 后续 `ApplyEdits`，避免每键整串 `Value` get/set。
- Phase 0 模型为整串占位；Phase 1 换 PieceTable 后 API 不变。

---

## 7. 实现原则

1. **内核语言无关**：高亮/配置可插拔，plaintext 永远可用。
2. **视口优先**：布局与绘制复杂度相对可见行，不相对全文。
3. **增量变更**：Model 发 `ContentChanged`；tokenizer/layout 只失效脏区。
4. **AOT 友好**：无反射发现语言包；工厂与注册显式调用。
5. **公开 API 稳定**：`ICodePadTextModel` / Registry 行为可扩展，勿随意改签名。
6. **无 Roslyn**：任何 C# 智能留待未来独立包，不进本程序集。

---

## 8. 测试策略

| 层级 | 内容 |
|---|---|
| 模型 | SetValue、行边界、ApplyEdits、undo（P1） |
| 视口 | 可见行范围、滚动、overscan（P1） |
| 注册 | RegisterDefaults 幂等、ElementRegistry 可创建 |
| Configuration | 解析 + 自动闭合/注释（P2） |
| Monarch | 关键字/字符串/跨行块注释状态（P3） |
| 主题 | token → 颜色（P3） |

集成测试可不绑 `Square.UI.Tests` 全量依赖；本包自有测试项目即可。

---

## 9. 构建与引用

```bash
dotnet build src/Square.Extensions.CodePad
dotnet test tests/Square.Extensions.CodePad.Tests
```

应用 csproj：

```xml
<ProjectReference Include="..\..\src\Square.Extensions.CodePad\Square.Extensions.CodePad.csproj" />
```

---

## 10. 文档同步

| 文档 | 职责 |
|---|---|
| 本文件 `docs/DEVELOPMENT.md` | 架构、阶段、实现约定（源码内权威） |
| 包 `README.md` | 快速开始、依赖、非目标 |
| 仓库 `docs/API-Reference.md` | 对外 API 摘要（实现稳定后更新） |
| 仓库 `docs/Architecture.md` | 程序集表补一行（可选） |

变更阶段状态时，更新本文 §4 检查清单。

---

## 11. 当前代码状态

| 类型 | 状态 |
|---|---|
| `CodePadRegistration` | 已实现，幂等 |
| `CodePad` | 完整编辑 + 视口绘制 + 高亮 + 行号 |
| `CodePadTextModel` / `PieceTable` | 已实现 + 增量 undo |
| `LanguageRegistry` | 多语言内置 + Monarch |
| `CodePadThemeRegistry` | default-light / default-dark |
| Soft wrap / Roslyn | 未做 |

实现细节以源码与 `ROADMAP.md` 为准。
