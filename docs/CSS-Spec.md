# CSS 支持范围

> Version: 0.2  
> 配套：`Architecture.md`、`Sqx-Spec.md`

---

## 1. 目标

尽可能兼容现代 CSS 语义，不兼容浏览器私有扩展。

CSS 是框架的重要组成部分，与 `.sqx` 的 `<style>` 段和 `style`/`class` 属性联动。

---

## 2. 分阶段支持

| 阶段 | 范围 |
|---|---|
| **M1** | Selector 子集、Cascade、Specificity、Variables、Inheritance、基础属性、Flex |
| **M2** | Pseudo Class、Animation、Grid 全量、Theme 系统 |
| **M3+** | Container Query、Subgrid |

---

## 3. Selector（M1 子集）

| 选择器 | 示例 | M1 |
|---|---|---|
| 类型 | `Button` | ✅ |
| 类 | `.active` | ✅ |
| ID | `#main` | ✅ |
| 后代 | `View Text` | ✅ |
| 子代 | `View > Text` | M2 |
| 相邻 | `Text + Text` | M2 |
| 通用 | `*` | M2 |
| 属性 | `[disabled]` | M2 |
| 伪类 | `:hover` `:focus` `:active` | M2 |
| 伪元素 | `::before` `::after` | M3+ |

---

## 4. Cascade 与 Specificity

- 级联顺序：`!important` > 内联 `style` > ID > 类/属性/伪类 > 类型
- Specificity 计算：`(id_count, class_count, type_count)`
- 同 specificity 时，后定义胜出
- Variables（`--x`）参与级联

---

## 5. Variables

```css
:root {
  --primary: #0078d4;
  --spacing: 16px;
}

Button {
  color: var(--primary);
  padding: var(--spacing);
}
```

- 定义：`--name: value`
- 使用：`var(--name)` / `var(--name, fallback)`
- 继承：变量沿 Visual Tree 继承

---

## 6. Inheritance

可继承属性：

- `color`
- `font-size` / `font-family` / `font-weight`
- `line-height`
- `text-align`
- `visibility`

不可继承（默认）：

- `margin` / `padding` / `border` / `background` / `width` / `height`

---

## 7. 属性（M1 基础集）

| 类别 | 属性 |
|---|---|
| 文本 | `color` `font-size` `font-family` `font-weight` `line-height` `text-align` |
| 背景 | `background` `background-color` |
| 边框 | `border` `border-width` `border-color` `border-radius` |
| 间距 | `padding` `margin` |
| 尺寸 | `width` `height` `min-width` `max-width` `min-height` `max-height` |
| 布局 | `display` `flex-direction` `justify-content` `align-items` `flex-grow` `flex-shrink` `flex-basis` `gap` |
| 定位 | `position` `top` `right` `bottom` `left` |
| 其他 | `opacity` `visibility` `overflow` |

---

## 8. 单位

| 单位 | 说明 | M1 |
|---|---|---|
| `px` | 物理像素（经 DPI 缩放） | ✅ |
| `%` | 相对父容器 | ✅ |
| `auto` | 自动 | ✅ |
| `rp` | 响应式单位（基准尺寸比例） | ✅ |
| `vw` / `vh` | 视口宽/高百分比 | ✅ |
| `min-content` / `max-content` / `fit-content` | 内在尺寸 | M2 |
| `rem` / `em` | 相对字号 | M2 |

---

## 9. Flex（M1）

```css
View {
  display: flex;
  flex-direction: row | column | row-reverse | column-reverse;
  justify-content: flex-start | center | flex-end | space-between | space-around;
  align-items: stretch | flex-start | center | flex-end;
  flex-wrap: nowrap | wrap;
  gap: 8px;
}
```

---

## 10. Grid（M2）

```css
View {
  display: grid;
  grid-template-columns: 1fr 2fr;
  grid-template-rows: auto;
  gap: 8px;
}
```

---

## 11. Pseudo Class（M2）

| 伪类 | 说明 |
|---|---|
| `:hover` | 鼠标悬停 |
| `:focus` | 获得焦点 |
| `:active` | 激活（按下） |
| `:disabled` | 禁用 |
| `:checked` | 选中 |
| `:empty` | 无子节点 |
| `:first-child` / `:last-child` / `:nth-child(n)` | 位置 |

---

## 12. Animation（M2）

```css
@keyframes fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

Text {
  animation: fade-in 0.3s ease;
}
```

- `@keyframes` 定义
- `animation` 简写：`name duration timing-function delay iteration-count direction`
- 与 `Square.Animation` 模块联动

---

## 13. 内联样式与类

### 13.1 内联 `style`

```xml
<Button style="color: red; padding: 8px;">Click</Button>
```

- 优先级最高（仅次于 `!important`）
- 命令式：`el.Style.Set("color", "red")`

### 13.2 `class`

```xml
<Button class="primary large">Click</Button>
```

- 空格分隔多个类
- 命令式：`el.ClassList.Add("primary")` / `.Remove("primary")` / `.Toggle("primary")`

### 13.3 绑定

```xml
<Button class={ActiveClass}>Click</Button>
<Button style={DynamicStyle}>Click</Button>
```

- `class` 绑定 `ObservableValue<string>`
- `style` 绑定 `ObservableValue<string>` 或对象

---

## 14. 不支持范围

- 浏览器私有扩展（`-webkit-` 等）
- `@media` 全量（M3+ 考虑 Container Query 替代）
- `@supports`
- CSS Houdini
- 怪异模式