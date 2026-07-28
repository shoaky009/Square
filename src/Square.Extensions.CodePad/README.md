# Square.Extensions.CodePad

Square 可选代码编辑控件：

- **PieceTable** 文档模型 + 增量 undo/redo
- **视口虚拟化**绘制（只绘可见行）
- **Monaco Monarch** 语法高亮（内置多语言）
- **VS Code 风格** language configuration（注释、自动闭合等）
- 行号、当前行、查找下一处、Tab 缩进
- 括号 / HTML·XML 标签层级折叠（gutter 可开关）
- Soft wrap（`WordWrap`，按可视宽度换行，不改文档）
- 查找/替换（`FindNext` / `FindPrevious` / `ReplaceNext` / `ReplaceAll`）
- 关闭 wrap 时长行横向滚动
- 滚动条（`ShowScrollBars`，可开关；支持拖拽与轨道点击）
- 括号匹配高亮、查找匹配高亮
- Glyph margin + 行 decoration（断点图标、git 色条、行背景、`GutterClick`）
- 只读模式（`ReadOnly` / `ToggleReadOnly`）
- Overview ruler（装饰 / 查找标记）
- Find 面板状态（`FindPanelVisible`、匹配计数/序号）
- 局部绘制失效（`InvalidatePaint(Rect)`，caret 闪烁用）
- 折叠块整块选中与编辑：`SelectCollapsedFoldAt`、Shift+点折叠槽、双击 `⋯`；Delete/Backspace/输入会覆盖隐藏行
- 多光标：Alt+点击添加、Esc 清除；同步输入/删除；Shift+方向键选区、Ctrl+Shift+方向键按词选区

**不包含** Roslyn、LSP、补全/诊断。独立于 `Square.Extensions`。

## 快速开始

```xml
<ProjectReference Include="path\to\Square.Extensions.CodePad\Square.Extensions.CodePad.csproj" />
```

```csharp
using Square.Extensions.CodePad;

CodePadRegistration.RegisterDefaults();

var pad = new CodePad
{
    Language = "csharp",
    ThemeId = "default-dark",
    ShowLineNumbers = true,
};
pad.Model.SetValue("public class App { }");
pad.SetDecoration(new CodePadLineDecoration
{
    Id = "bp-0",
    Line = 0,
    Glyph = "●",
    GlyphColor = Color.FromRgb(229, 57, 53),
});
pad.GutterClick += (_, e) => { /* e.Line / e.Lane */ };
```

SQX/SQV：

```xml
<CodePad Language="json" ShowLineNumbers="true" />
```

应用启动时调用 `CodePadRegistration.RegisterDefaults()`（**不是** `ExtensionRegistration`）。

## 内置 languageId

`plaintext`, `csharp`, `javascript`, `typescript`, `json`, `python`, `html`, `css`, `xml`, `sql`, `markdown`, `shellscript`, `yaml`

自定义：

```csharp
LanguageRegistry.Register(new LanguageContribution
{
    Id = "mylang",
    Extensions = [".my"],
    Configuration = LanguageConfiguration.CLike("//", ("/*", "*/")),
    Tokenizer = MonarchTokenizer.FromJson(monarchJson),
});
```

## 文档

| 文档 | 说明 |
|---|---|
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 架构与实现约定 |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 阶段清单 |

## Sample

```bash
dotnet run --project samples/Square.Sample.CodePad
```

演示语言切换、主题、行号、撤销/重做、注释与样例代码加载。

## 测试

```bash
dotnet test tests/Square.Extensions.CodePad.Tests
```
