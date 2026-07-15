# 布局引擎

> Version: 0.2  
> 配套：`Architecture.md`、`CSS-Spec.md`

---

## 1. 定位

`Square.Layout` 负责 Visual Tree 的几何计算。

采用 CSS 盒模型思想。

---

## 2. 布局流程

```
Visual Tree
  ↓
Measure（测量：计算期望尺寸）
  ↓
Arrange（排列：确定最终位置与尺寸）
  ↓
写入 Visual.Geometry
```

---

## 3. 盒模型

```
┌───────────────────────────────────┐
│             margin                │
│  ┌─────────────────────────────┐  │
│  │           border            │  │
│  │  ┌───────────────────────┐  │  │
│  │  │         padding       │  │  │
│  │  │  ┌─────────────────┐  │  │  │
│  │  │  │     content     │  │  │  │
│  │  │  └─────────────────┘  │  │  │
│  │  └───────────────────────┘  │  │
│  └─────────────────────────────┘  │
└───────────────────────────────────┘
```

- `content`：内容区域
- `padding`：内边距
- `border`：边框
- `margin`：外边距

---

## 4. display（M1）

| 值 | 说明 | M1 |
|---|---|---|
| `block` | 块级 | ✅ |
| `flex` | 弹性 | ✅ |
| `inline` | 行内 | M2 |
| `grid` | 网格 | M2 |
| `none` | 不渲染 | ✅ |

---

## 5. Flex 布局（M1）

### 5.1 容器属性

| 属性 | 值 |
|---|---|
| `flex-direction` | `row` `column` `row-reverse` `column-reverse` |
| `justify-content` | `flex-start` `center` `flex-end` `space-between` `space-around` |
| `align-items` | `stretch` `flex-start` `center` `flex-end` |
| `flex-wrap` | `nowrap` `wrap` |
| `gap` | 间距 |

### 5.2 子项属性

| 属性 | 说明 |
|---|---|
| `flex-grow` | 增长比例 |
| `flex-shrink` | 收缩比例 |
| `flex-basis` | 基础尺寸 |
| `align-self` | 覆盖 align-items |

### 5.3 算法

1. 确定主轴/交叉轴
2. 测量子项基础尺寸（`flex-basis`）
3. 分配剩余空间（`flex-grow`）/ 收缩（`flex-shrink`）
4. justify-content 对齐主轴
5. align-items 对齐交叉轴

---

## 6. position（M1 基础）

| 值 | 说明 | M1 |
|---|---|---|
| `static` | 默认流式 | ✅ |
| `relative` | 相对自身 | ✅ |
| `absolute` | 相对最近定位祖先 | M2 |
| `fixed` | 相对视口 | M2 |
| `sticky` | 滚动吸顶 | M3+ |

---

## 7. 尺寸

### 7.1 单位

| 单位 | M1 |
|---|---|
| `px` | ✅ |
| `%` | ✅ |
| `auto` | ✅ |
| `rp` | ✅ |
| `vw` / `vh` | ✅ |
| `min-content` / `max-content` / `fit-content` | M2 |

### 7.2 尺寸属性

- `width` / `height`
- `min-width` / `max-width`
- `min-height` / `max-height`

---

## 8. 高 DPI

- 布局按逻辑像素
- 光栅按物理像素
- 物理像素对齐：`Math.Round(logical * dpiScale)` 取整
- 避免模糊

---

## 9. 内在尺寸（M2）

- `min-content`：最小内容宽度
- `max-content`：最大内容宽度
- `fit-content`：适应内容宽度

---

## 10. Grid（M2）

### 10.1 容器属性

| 属性 | 说明 |
|---|---|
| `grid-template-columns` | 列模板 |
| `grid-template-rows` | 行模板 |
| `gap` | 间距 |

### 10.2 子项属性

| 属性 | 说明 |
|---|---|
| `grid-column` | 列位置 |
| `grid-row` | 行位置 |
| `grid-column-span` | 列跨度 |
| `grid-row-span` | 行跨度 |

---

## 11. 后续

| 功能 | 阶段 |
|---|---|
| Container Query | M3+ |
| Subgrid | M3+ |
| intrinsic sizing 完整 | M2 |
| writing-mode | M3+ |