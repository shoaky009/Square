# SQX 语言规范

> Version: 0.2  
> 配套：`Architecture.md`、`Requirements.md`

---

## 1. 文件格式

`.sqx` 为单文件三段式：

```xml
<sqx>
  <template>
    <!-- 结构 + 绑定 + 流程控制 -->
  </template>
  <script lang="csharp">
    // C# 逻辑 + Props 声明
  </script>
  <style>
    /* CSS 样式 */
  </style>
</sqx>
```

- `<template>`：结构，含绑定表达式与流程控制
- `<script lang="csharp">`：C# 逻辑，Source Generator 发射进同一 `partial` 组件类
- `<style>`：CSS，由 CSS 引擎消费
- `<script>`（裸）或 `<script lang="js">`：保留给未来 JS 引擎扩展，当前不解析

Source Generator 将三段编译为同一个 `partial` 组件类。

---

## 2. 元素

### 2.1 内置元素（M1）

| 标签 | 说明 |
|---|---|
| `View` | 通用容器 |
| `Text` | 文本 |
| `Button` | 按钮 |
| `Input` | 输入框 |
| `TextArea` | 多行输入 |
| `CheckBox` | 复选 |
| `Radio` | 单选 |
| `Select` | 下拉选择 |
| `Image` | 图片 |
| `Canvas` | 画布 |

命名：PascalCase 控件类型（C# 习惯），`.sqx` 内标签同名。

### 2.2 自定义组件

任何 `.sqx` 文件即一个组件，文件名 / `<sqx name="MyComponent">` 决定组件类型名。

调用：

```xml
<MyComponent Title={PageTitle} Count={ItemCount} />
```

### 2.3 结构原语（编译期处理，非运行时组件）

| 原语 | 用途 |
|---|---|
| `<Show when={expr}>` | 条件子树 |
| `<For each={expr}>{(it)=>…}</For>` | 列表 |
| `<Switch>` + `<Match when={expr}>` | 多分支 |
| `<Index each={expr}>` | 索引列表（可选） |

详见 §6。

---

## 3. Props

### 3.1 声明

在 `<script lang="csharp">` 中使用 `[Prop]` 特性：

```csharp
[Prop] public ObservableValue<string> Title { get; set; } = new("");
[Prop(Required = true)] public ObservableValue<int> Count { get; set; } = new(0);
[Prop] public ObservableValue<bool> Visible { get; set; } = new(true);
```

- 类型为 `ObservableValue<T>`
- 默认值用 C# 初始化器
- `[Prop(Required = true)]` 标记必填
- 生成器辅助包装：开发者也可直接写 `string`，生成器自动包装为 `ObservableValue<string>`

### 3.2 传值

调用方在模板中以属性形式传入：

```xml
<MyComponent Title={PageTitle} Count={ItemCount} />
<!-- 常量 -->
<MyComponent Title="Hello" Count={5} />
```

- `{expr}` 绑定到 `ObservableValue<T>`
- 常量字面量自动包装

### 3.3 数据流

- **单向**：父 → 子
- 子组件**不可直接赋值改写** Props 的 `ObservableValue<T>` 内部值
- 父组件源变化 → 子组件 prop 自动更新
- 子组件响应变化的方式：
  - 订阅 prop 的 `ObservableValue`：`Title.Subscribe(v => ...)`
  - 重写钩子：`protected override void OnPropChanged(string name)`

### 3.4 校验

- 编译期：Generator 检查调用方是否传齐必填 Prop，缺失则报诊断（带 `.sqx` 行列）
- 运行时不做反射校验

### 3.5 内置元素属性

内置元素的属性（如 `<Button disabled>`、`<Input type="text">`）与自定义组件 Props **共用同一套机制**：

- 属性可绑定（`disabled={IsDisabled}`）或常量（`disabled`）
- 绑定属性编译为 `ObservableValue` 订阅
- 编译期类型检查

### 3.6 Prop 特性参考

| 特性 | 属性 | 说明 |
|---|---|---|
| `[Prop]` | — | 标记为组件 Prop |
| `[Prop]` | `Required` | 是否必填（默认 false） |
| `[Prop]` | `Default` | 默认值（也可用初始化器） |

---

## 4. ref 引用

### 4.1 语法

```xml
<Button ref={MyBtn}>Click</Button>
<Text ref={TitleEl}>Hello</Text>
```

### 4.2 生成

- 生成器在 `partial` 组件类中产出强类型字段：`internal Button MyBtn;`
- 元素挂载时自动赋值
- 元素卸载时置 null

### 4.3 使用

```csharp
MyBtn.Style.Set("color", "red");
MyBtn.ClassList.Add("active");
```

---

## 5. 绑定语法

### 5.1 统一语法

所有绑定使用 `{expr}` 表达式，与流程控制 `when=`/`each=` 同源。

### 5.2 文本插值

```xml
<Text>{Name}</Text>
<Text>Hello {FirstName} {LastName}</Text>
```

`{expr}` 内编译为 `ObservableValue` 读取并订阅。

### 5.3 单向属性

```xml
<Text text={Title} />
<View class={ActiveClass} />
```

编译为属性绑定并订阅源变化。

### 5.4 事件

```xml
<Button onClick={OnClick}>Click</Button>
<Input onInput={OnInput} />
```

- 事件名首字母大写：click → onClick、textChanged → onTextChanged
- 映射到 `<script lang="csharp">` 中的 C# 方法

### 5.5 双向（显式）

```xml
<Input value={UserName} onInput={OnUserNameChanged} />
```

- `value={expr}` 单向属性绑定
- `onInput={Method}` 事件处理，Method 在 C# 中写回 `ObservableValue.Value`
- 不提供隐式双向绑定，保持显式可控

### 5.6 实现约束

- 绑定后端**必须**用 `ObservableValue<T>`（强类型、委托驱动、零反射、AOT 安全）
- `{expr}` 在编译期解析成员引用并生成订阅代码
- 运行时零解析

---

## 6. 流程控制

### 6.1 `<Show>`

```xml
<Show when={LoggedIn}>
  <Text>欢迎</Text>
</Show>
```

- `when` 绑定 `ObservableValue<bool>`
- 条件变化时增删 Visual 子树（记忆化复用）
- 可选 `fallback` 属性指定条件假时的替代子树：

```xml
<Show when={LoggedIn} fallback={<>未登录</>}>
  <Text>欢迎</Text>
</Show>
```

### 6.2 `<For>`

```xml
<For each={Items}>{(it)=>
  <Text>{it.Name}</Text>
}</For>
```

- `each` 绑定 `ObservableCollection<T>`
- `it` 为列表项
- 引用键增量更新（项移动时节点不重建）
- 可选 `fallback` 属性指定空列表时的替代子树：

```xml
<For each={Items} fallback={<>无数据</>}>{(it)=>
  <Text>{it.Name}</Text>
}</For>
```

- `fallback` 在集合为空（`Count == 0`）时渲染，有项时移除
- 与 `<Show>`/`<Switch>` 的 `fallback` 语义一致：均为"无内容时的替代"，作为属性传入，不占用 children 位置（children 专属于迭代模板）

### 6.3 `<Switch>` + `<Match>`

```xml
<Switch fallback={<>未知</>}>
  <Match when={Status == "loading"}><Text>Loading</Text></Match>
  <Match when={Status == "done"}><Text>Done</Text></Match>
</Switch>
```

- 互斥，首项真即渲染
- `<Switch>` 可带 `fallback`：无 `<Match>` 命中时渲染
- children **只能是 `<Match>`**（编译器校验，非 Match 子节点报错）
- `fallback` 是属性，不混入分支层级，与"匹配分支"视觉上分离

### 6.4 `<Index>`（可选，M2）

索引键列表。

### 6.5 编译模型

`<Show>`/`<For>`/`<Switch>`/`<Match>` 为 **Source Generator 已知的结构原语**（非运行时组件实例），由生成器特判编译为 Visual Tree 的挂卸/迭代。

### 6.6 阶段

- M1：`<Show>`/`<For>` 基础形态
- M2：`<Switch>`/`<Match>`/`<Index>` + keyed 复用

---

## 7. 元素操作 API

### 7.1 引用

通过 `ref` 获取强类型引用（见 §4）。

### 7.2 属性

```csharp
el.SetProperty("disabled", true);
var v = el.GetProperty<bool>("disabled");
```

- 命令式**不覆盖已绑定属性**：若该属性已被声明式绑定，命令式写入会被下一次源变更覆盖

### 7.3 样式

```csharp
el.Style.Set("color", "red");
el.Style.Get("color");
el.Style.Remove("color");
```

### 7.4 类

```csharp
el.ClassList.Add("active");
el.ClassList.Remove("active");
el.ClassList.Toggle("active");
el.ClassList.Contains("active");
```

### 7.5 子节点

```csharp
el.AppendChild(new Text("hello"));
el.RemoveChild(child);
el.InsertBefore(newChild, refChild);
el.ClearChildren();
el.Children  // 子节点集合
```

- 命令式**不侵入 `<Show>`/`<For>` 管理的子树**

### 7.6 事件

```csharp
el.AddEventListener("click", handler);
el.RemoveEventListener("click", handler);
```

### 7.7 元素创建

```csharp
var btn = new Button();
btn.Text = "Click";
container.AppendChild(btn);
```

- 命令式构造的元素接生命周期钩子（OnAttached/OnDetached/...）

### 7.8 查询（M2）

```csharp
var btn = el.Query<Tag.Button>(".cls");
```

- 编译期生成匹配器，避免运行时反射

---

## 8. 生命周期钩子

### 8.1 组件级

| 钩子 | 触发时机 |
|---|---|
| `OnPropChanged(string name)` | Props 值变化 |
| `OnAttached` | 挂载到视觉树 |
| `OnDetached` | 从视觉树卸载 |
| `OnLoaded` | 加载完成 |
| `OnUnloaded` | 卸载完成 |
| `OnMeasure` | 布局测量 |
| `OnArrange` | 布局排列 |

### 8.2 应用级

| 钩子 | 触发时机 |
|---|---|
| `OnStart` | 应用启动 |
| `OnExit` | 应用退出 |

### 8.3 落地

编译期生成的 `partial` 组件类提供可重写虚方法，供 C# 业务逻辑订阅。

---

## 9. 仲裁规则总表

| 场景 | 规则 |
|---|---|
| 命令式写已绑定属性 | 允许写入，但下一次源变更会覆盖，不静默回滚 |
| 命令式操作 `<Show>` 子树 | 不允许（会被条件更新冲掉） |
| 命令式操作 `<For>` 子树 | 不允许（会被列表更新冲掉） |
| 命令式操作静态声明区域 | 允许 |
| 命令式创建并挂载元素 | 允许，接生命周期钩子 |
| Props 子组件改写 | 不允许（单向数据流） |

---

## 10. 按标签即用

控件按标签名即可用、免手动注册。Source Generator 按标签解析控件，无需显式 `using`/注册清单。

---

## 11. 示例

```xml
<sqx>
  <template>
    <View>
      <Show when={LoggedIn}>
        <Text>Hello {UserName}</Text>
      </Show>
      <Button ref={MyBtn} onClick={OnClick}>Click</Button>
      <For each={Items}>{(it)=>
        <Text>{it.Name}</Text>
      }</For>
    </View>
  </template>
  <script lang="csharp">
    [Prop] public ObservableValue<bool> LoggedIn { get; set; } = new(false);
    [Prop] public ObservableValue<string> UserName { get; set; } = new("");

    public ObservableCollection<Item> Items = new();

    private void OnClick()
    {
        MyBtn.ClassList.Add("clicked");
    }
  </script>
  <style>
    View { padding: 16px; }
    Button.clicked { color: red; }
  </style>
</sqx>
```