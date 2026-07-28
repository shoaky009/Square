# CodeEditor 阶段检查清单

与 [DEVELOPMENT.md](./DEVELOPMENT.md) 配套。

## Phase 0 — 工程骨架

- [x] `Square.Extensions.CodeEditor.csproj`
- [x] 加入 `Square.slnx`
- [x] `CodeEditorRegistration` / `CodeEditor`
- [x] `ICodeEditorTextModel`
- [x] `LanguageRegistry` / `CodeEditorThemeRegistry`
- [x] 开发文档
- [x] 测试项目

## Phase 1 — 大文件编辑内核

- [x] PieceTable
- [x] 增量 Undo/Redo（含输入合并）
- [x] Viewport 虚拟化绘制
- [x] 完整 `ITextEditor` 编辑路径
- [x] Tab / tab-size / 等宽
- [x] 大文档行访问测试（5000 行）

## Phase 2 — Language Configuration

- [x] VS Code 风格 configuration 模型
- [x] 自动闭合 / 注释切换 / wordPattern 选词 / Enter 缩进保留
- [x] 内置多语言 configuration

## Phase 3 — TextMate + 主题

- [x] TextMateSharp grammar 数据库与 rule stack
- [x] VS Code 扩展 grammar 导入
- [x] 增量 tokenize 缓存
- [x] 视口按 token 上色
- [x] default-light / default-dark 主题
- [x] 内置语言：plaintext, csharp, js/ts, json, python, html, css, xml, sql, markdown, shell, yaml
- [x] VS Code 扩展 `package.json` / `tmLanguage.json` 导入

## Phase 4 — Chrome

- [x] 行号 gutter / 当前行高亮
- [x] 简单 FindNext（Ctrl+F 复用当前查询/选区）
- [x] 括号/标签层级折叠（gutter ▾/▸，可开关）
- [x] 语言无关数组 `[...]` 与 Python 缩进块折叠
- [x] Soft wrap（WordWrap 开关 + 视口行布局）
- [x] Find / Replace（Next / Previous / ReplaceNext / ReplaceAll，可选 match case）
- [x] 无 wrap 时横向滚动（caret / 滚轮 DeltaX）
- [x] 滚动条显示开关（`ShowScrollBars` / 拖拽 / 轨道点击）
- [x] 括号匹配高亮（`HighlightMatchingBrackets`）
- [x] 查找匹配高亮（`HighlightFindMatches` + FindQuery）
- [x] Glyph margin + 行 decoration API（断点/git 色条/行背景/gutter 点击）

## Phase 5 — 性能 · Chrome · 查找

- [x] 局部 `InvalidatePaint(Rect)` + caret 闪烁脏区
- [x] Overview ruler（装饰 / 查找标记 / 视口指示）
- [x] Find 面板状态 API（`FindPanelVisible`、`FindMatchCount`、`FindMatchIndex`、`GetFindMatchLines`）
- [x] 装饰 `OverviewRulerColor`、主题 overview 色
- [x] Sample：Find 面板、overview、View 菜单补齐
- [x] 文档与测试
- [x] 折叠块选中 / 剪切 / 覆盖 / 删除（`SelectCollapsedFoldAt`、编辑时扩展选区）
- [x] 多光标（Alt+点击添加、Esc 清除、同步输入/删除/移动）

## 不做（记录）

- Roslyn / LSP / IntelliSense
- 语义高亮 / LSP
- 并入 `Square.Extensions` 源码
