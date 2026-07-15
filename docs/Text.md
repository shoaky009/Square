# 字体与排版

> Version: 0.2  
> 配套：`Architecture.md`、`Graphics.md`

---

## 1. 定位

`Square.Text` 为独立文本模块，负责文本测量、排版、渲染支持。

字体系统优先采用纯 C# 实现。

---

## 2. 职责

| 功能 | M1 | M7 |
|---|---|---|
| Unicode | ✅ | ✅ |
| Font Manager | ✅ 基础 | ✅ 完整 |
| Glyph 缓存 | ✅ | ✅ |
| 单行排版 | ✅ | ✅ |
| 多行排版 | ✅ 基础 | ✅ 完整 |
| 文本测量 | ✅ | ✅ |
| 命中测试 | ✅ 基础 | ✅ 完整 |
| Caret | M3 | ✅ |
| Selection | M3 | ✅ |
| Line Break | ✅ 基础 | ✅ 完整 |
| Font Fallback | M7 | ✅ |
| BiDi | M7 | ✅ |

---

## 3. Font Manager

### 3.1 职责

- 加载系统字体
- 字体匹配（family / weight / style）
- 字体缓存

### 3.2 M1 实现

- 读取系统字体目录
- 按 family name 匹配
- 最小缓存

### 3.3 接口

```csharp
public sealed class FontManager
{
    public static FontManager Instance { get; }
    public Font Match(string family, float size, FontWeight weight, FontStyle style);
    public IReadOnlyList<string> AvailableFamilies { get; }
}
```

---

## 4. Glyph

### 4.1 Glyph 缓存

- 按字体 + 字符码点缓存
- 缓存字形位图（Software Backend）或路径（向量 Backend）
- LRU 淘汰

### 4.2 Glyph 信息

```csharp
public readonly struct GlyphInfo
{
    public int CodePoint;
    public float AdvanceWidth;
    public float AdvanceHeight;
    public Rect Bounds;
    public float LeftBearing;
    public float TopBearing;
}
```

---

## 5. Text Layout

### 5.1 单行（M1）

```csharp
public sealed class TextLayout
{
    public string Text;
    public Font Font;
    public Size MaxSize;
    public TextAlignment Alignment;
    public Size Measure();
    public IReadOnlyList<GlyphRun> GetRuns();
}
```

### 5.2 多行（M1 基础）

- 按宽度自动换行
- 行高 = `font.Size * line-height`
- 对齐：left / center / right

### 5.3 完整排版（M7）

- BiDi 算法
- Font Fallback
- 复杂脚本整形
- 段落分割

---

## 6. 命中测试

### 6.1 M1 基础

- 文本坐标 → 字符索引
- 字符索引 → 文本坐标

### 6.2 M7 完整

- 多行命中测试
- 跨行选择

---

## 7. Caret 与 Selection

### 7.1 M3

- Caret 位置计算
- Caret 绘制
- 单行选择

### 7.2 M7 完整

- 多行选择
- 选择高亮绘制
- 剪贴板联动

---

## 8. Line Break

### 6.1 M1 基础

- 按宽度断行
- 空格断词

### 8.2 M7 完整

- Unicode Line Break Algorithm (UAX #14)
- CJK 断行规则
- 非断行字符

---

## 9. Font Fallback（M7）

- 缺字时自动回退到备用字体
- 回退链可配置

---

## 10. BiDi（M7）

- Unicode BiDi Algorithm (UAX #9)
- 段落方向自动检测
- 嵌入方向覆盖

---

## 11. 与渲染集成

- `IRenderContext.DrawText(TextLayout, Point, Brush)`
- Software Backend：从 Glyph 缓存取位图混合
- 向量 Backend：从 Glyph 缓存取路径填充