# 入门指南

> Version: 0.3
> 配套：`Architecture.md`、`Sqx-Spec.md`、`API-Reference.md`

本文带你从零创建一个 Square 桌面应用，涵盖项目搭建、SQX 组件编写、事件处理、样式和发布。

---

## 1. 环境要求

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10+（当前主要验证平台）

```bash
dotnet --version
```

---

## 2. 创建项目

### 2.1 新建控制台项目

```bash
dotnet new console -n MyApp -o MyApp
cd MyApp
```

### 2.2 修改 csproj

将 `OutputType` 改为 `WinExe`，添加 Square 框架项目引用和 Source Generator 分析器引用：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="path\to\src\Square.Hosting\Square.Hosting.csproj" />
    <ProjectReference Include="path\to\src\Square.CSS\Square.CSS.csproj" />
    <ProjectReference Include="path\to\src\Square.SourceGenerator\Square.SourceGenerator.csproj"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="**\*.sqx" />
  </ItemGroup>

</Project>
```

> 如果你直接在 Square 仓库内开发，路径使用 `..\..\src\...` 相对引用。

关键配置说明：

| 配置 | 作用 |
|---|---|
| `OutputType=WinExe` | Windows 桌面应用，不弹出控制台窗口 |
| `PublishAot=true` | 启用 NativeAOT 发布 |
| `EmitCompilerGeneratedFiles=true` | 输出生成的 C# 文件（便于调试） |
| `OutputItemType="Analyzer"` | Source Generator 作为分析器引用，不输出程序集 |
| `AdditionalFiles Include="**\*.sqx"` | 将 `.sqx` 文件注册为 Source Generator 输入 |

`Square.Hosting` 提供桌面运行时及其传递依赖；组件使用 `<style>` 时仍需显式引用 `Square.CSS`。使用 `<Router>` / `<Link>` 时再添加 `Square.Router` 引用。

### 2.3 编写入口

将 `Program.cs` 替换为：

```csharp
using Square.Hosting;
using Square.Platform;

var app = new DesktopApplication(new Main(), new PlatformHostCreateInfo
{
    Title = "My First App",
    Width = 600,
    Height = 400
});
app.Run();
```

`Main` 是由 `Main.sqx` 在编译期生成的组件类。`DesktopApplication` 负责窗口创建、消息循环、输入路由、焦点管理、文本编辑、剪贴板、帧调度和布局渲染——你不需要手写任何基础设施代码。

---

## 3. 编写第一个组件

### 3.1 创建 Main.sqx

在项目根目录创建 `Main.sqx`：

```xml
<template>
  <View class="container">
    <Text class="title">Hello Square</Text>
    <Input value={Name} onInput={OnNameChanged} placeholder="输入你的名字" />
    <Button onClick={OnGreet} class="greet-btn">打招呼</Button>
    <Show when={Greeted}>
      <Text class="greeting">你好，{Name.Value}！</Text>
    </Show>
  </View>
</template>

<script lang="csharp">
  public ObservableValue<string> Name = new("");
  public ObservableValue<bool> Greeted = new(false);

  private void OnNameChanged(Event e)
  {
    Name.Value = ((Input)e.Target!).Value;
  }

  private void OnGreet()
  {
    Greeted.Value = true;
  }
</script>

<style>
  .container {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 24px;
  }

  .title {
    color: #202124;
    font-size: 20px;
  }

  .greet-btn {
    background: #0078d4;
    color: #ffffff;
  }

  .greeting {
    color: #107c10;
    font-size: 16px;
  }
</style>
```

### 3.2 文件结构

```
MyApp/
  MyApp.csproj
  Program.cs
  Main.sqx
```

### 3.3 构建和运行

```bash
dotnet build
dotnet run
```

如果 `.sqx` 有语法错误，编译时会直接报错并指向文件和行列号。运行后应看到一个窗口，包含标题、输入框、按钮和条件问候文本。

---

## 4. 理解 SQX 组件结构

`.sqx` 文件由三个顶级 section 组成，Source Generator 将它们编译为同一个 `partial` 组件类：

```xml
<template>
  <!-- 结构 + 绑定 + 流程控制 -->
</template>

<script lang="csharp">
  // C# 逻辑 + Props 声明
</script>

<style>
  /* CSS 样式 */
</style>
```

| Section | 必需 | 数量 | 职责 |
|---|---|---|---|
| `<template>` | 是 | 1 | UI 结构、绑定、事件、流程控制 |
| `<script>` | 否 | 0-1 | C# 逻辑、Props 声明、文件级元数据 |
| `<style>` | 否 | 0-1 | CSS 样式 |

### 4.1 组件名与命名空间

组件名默认取文件名。可在 `<script>` 标签属性上覆盖：

```xml
<script lang="csharp" namespace="MyApp.Components" name="HomePage" access="internal">
```

### 4.2 编译产物

Source Generator 生成类似以下的 C# 代码（在 `obj/Generated` 下可查看）：

```csharp
public partial class Main : UIElement
{
    public ObservableValue<string> Name = new("");
    public ObservableValue<bool> Greeted = new(false);

    protected override void BuildElementTree()
    {
        var view = new View();
        var title = new Text("Hello Square");
        var input = new Input();
        input.BindProperty("value", () => Name.Value);
        input.AddEventListener("input", OnNameChanged);
        // ...
    }
}
```

运行时零解析——所有 UI 在编译期已生成普通 C# 类型。

---

## 5. 数据绑定

### 5.1 ObservableValue

`ObservableValue<T>` 是绑定的基础原语：

```csharp
public ObservableValue<string> Name = new("");
public ObservableValue<int> Count = new(0);
public ObservableValue<bool> Visible = new(true);
```

### 5.2 文本插值

```xml
<Text>你好，{Name}</Text>
<Text>{Count} 次点击</Text>
```

`{expr}` 在编译期解析为 `ObservableValue<T>.Value` 读取并自动订阅。

### 5.3 属性绑定

```xml
<Text text={Title} />
<View class={ActiveClass} />
```

### 5.4 事件处理

```xml
<Button onClick={OnClick}>Click</Button>
<Input onInput={OnInput} />
```

事件名首字母大写：`click` → `onClick`。Handler 支持三种签名：

```csharp
private void OnClick() { }
private void OnClick(Event e) { }

```

### 5.5 双向绑定（显式）

Square 不提供隐式双向绑定。单向属性绑定 + 事件回写：

```xml
<Input value={Name} onInput={OnNameChanged} />
```

```csharp
private void OnNameChanged(Event e)
{
    Name.Value = ((Input)e.Target!).Value;
}
```

---

## 6. Props：组件输入

### 6.1 声明 Props

在 `<script>` 中用 `[Prop]` 特性声明：

```csharp
[Prop] public ObservableValue<string> Title { get; set; } = new("");
[Prop(Required = true)] public ObservableValue<int> Count { get; set; } = new(0);
```

### 6.2 传值

调用方在模板中以属性形式传入：

```xml
<UserCard Title={PageTitle} Count={ItemCount} />
<UserCard Title="Hello" Count={5} />
```

### 6.3 数据流

- 单向：父 → 子
- 子组件不可改写 Props 值
- 父组件源变化时子组件自动更新
- 子组件可订阅 prop 或重写 `OnPropChanged` 响应

```csharp
protected override void OnPropChanged(string name)
{
    if (name == nameof(Title))
    {
        // 响应 Title 变化
    }
}
```

### 6.4 编译期校验

必填 Prop 缺失时，编译期报诊断（带 `.sqx` 行列号）。

---

## 7. 流程控制

### 7.1 条件渲染

```xml
<Show when={IsLoggedIn}>
  <Text>欢迎回来</Text>
</Show>
```

`when` 绑定 `ObservableValue<bool>`。条件变化时增删 Element 子树。

### 7.2 列表渲染

```xml
<For each={Items}>{(it)=>
  <Text>{it.Name}</Text>
}</For>
```

`each` 绑定 `ObservableCollection<T>`。`it` 为列表项。引用键增量更新，项移动时节点不重建。

声明集合：

```csharp
public ObservableCollection<TodoItem> Items = new();
```

### 7.3 多分支

```xml
<Switch fallback={<>未知状态</>}>
  <Match when={Status == "loading"}><Text>加载中</Text></Match>
  <Match when={Status == "done"}><Text>完成</Text></Match>
</Switch>
```

---

## 8. 插槽与组合

### 8.1 默认插槽

```xml
<!-- Card.sqx -->
<View class="card">
  <View class="card-body">
    <Slot />
  </View>
</View>
```

```xml
<Card>
  <Text>这是卡片内容</Text>
</Card>
```

### 8.2 具名插槽

```xml
<!-- Panel.sqx -->
<View class="panel">
  <View class="panel-header"><Slot name="header"><Text>默认标题</Text></Slot></View>
  <View class="panel-content"><Slot /></View>
</View>
```

```xml
<Panel>
  <Text slot="header">设置</Text>
  <SettingsPage />
</Panel>
```

插槽内容保持调用方作用域——事件和绑定仍访问调用方成员。`<Slot>` 不产生额外布局容器。

---

## 9. 样式

### 9.1 组件级 `<style>`

```xml
<style>
  .container {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 16px;
  }

  Button {
    background: #0078d4;
    color: #ffffff;
  }
</style>
```

### 9.2 内联样式与类

```xml
<Button style="color: red; padding: 8px;">Click</Button>
<Button class="primary large">Click</Button>
```

### 9.3 CSS 变量

```css
:root {
  --primary: #0078d4;
  --spacing: 16px;
}

Button {
  background: var(--primary);
  padding: var(--spacing);
}
```

### 9.4 支持的选择器

| 选择器 | 示例 | 状态 |
|---|---|---|
| 类型 | `Button` | ✅ |
| 类 | `.active` | ✅ |
| ID | `#main` | ✅ |
| 后代 | `View Text` | ✅ |
| 子代 | `View > Text` | ✅ |
| 相邻兄弟 | `Text + Text` | ✅ |
| 通用 | `*` | ✅ |
| 属性 | `[disabled]` `[variant=primary]` | ✅ 基础 |
| 伪类 | `:hover` `:focus` `:active` `:disabled` `:checked` | ✅ |

详见 [`CSS-Spec.md`](CSS-Spec.md)。

---

## 10. 生命周期

### 10.1 组件级钩子

| 钩子 | 触发时机 |
|---|---|
| `OnPropChanged(name)` | Props 值变化 |
| `OnAttached` | 挂载到视觉树 |
| `OnDetached` | 从视觉树卸载 |
| `OnLoaded` | 加载完成 |
| `OnUnloaded` | 卸载完成 |

### 10.2 使用示例

```csharp
protected override void OnAttachedCore()
{
    // 订阅信号、初始化资源
}

protected override void OnDetachedCore()
{
    // 释放订阅、清理资源
}
```

---

## 11. 跨组件信号

`Signal<T>` 用于不相关组件之间的状态共享，`SignalHub` 按名称共享强类型信号。

### 11.1 定义信号

```csharp
public static class AppSignals
{
    public static Signal<string> Status { get; } =
        SignalHub.Default.Get("app.status", "Ready");
}
```

### 11.2 发布

```csharp
AppSignals.Status.Publish("Processing");
```

### 11.3 订阅（带 Dispatcher 切换）

```csharp
private IDisposable? _subscription;

protected override void OnAttachedCore()
{
    _subscription = AppSignals.Status.Subscribe(
        value => StatusText.Value = value,
        Application.Current.Dispatcher,
        emitCurrent: true);
}

protected override void OnDetachedCore()
{
    _subscription?.Dispose();
    _subscription = null;
}
```

传入 `Dispatcher` 后，后台线程发布的回调会自动排队到 UI 线程执行。

详见 [`Composition-and-Signals.md`](Composition-and-Signals.md)。

---

## 12. 命令式元素操作

### 12.1 ref 引用

```xml
<Button ref={MyBtn}>Click</Button>
```

生成器产出强类型字段 `internal Button MyBtn;`，挂载时赋值，卸载时置 null。

```csharp
MyBtn.Style.Set("color", "red");
MyBtn.ClassList.Add("active");
```

### 12.2 操作 API

```csharp
el.SetProperty("disabled", true);
el.GetProperty<bool>("disabled");
el.Style.Set("color", "red");
el.Style.Get("color");
el.ClassList.Add("active");
el.ClassList.Toggle("active");
el.AppendChild(new Text("hello"));
el.RemoveChild(child);
el.InsertBefore(newChild, refChild);
el.ClearChildren();
el.AddEventListener("click", handler);
el.RemoveEventListener("click", handler);
```

### 12.3 仲裁规则

- 命令式写入已绑定属性：允许，但下一次源变更会覆盖
- 命令式操作 `<Show>`/`<For>` 子树：不允许（会被控制流冲掉）
- 命令式操作静态声明区域：允许
- 命令式创建并挂载元素：允许，接生命周期钩子

---

## 13. 路由

### 13.1 声明路由

```xml
<Router initialPath="/">
  <Route path="/" component={HomePage} />
  <Route path="/users" component={UserList}>
    <Route path=":id" component={UserDetail} />
  </Route>
  <Route path="*" component={NotFound} />
</Router>
```

匹配优先级：静态段 > `:parameter` > `*wildcard`。

### 13.2 导航

```xml
<Link to="/users/42">用户 42</Link>
```

命令式导航：`Navigate`、`Replace`、`Back`、`Forward`。

详见 [`Sqx-Spec.md`](Sqx-Spec.md) §10。

---

## 14. NativeAOT 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

输出位于：

```
bin/Release/net10.0/win-x64/publish/
```

Square 从设计上保证 NativeAOT 兼容：不使用 `Reflection.Emit`、`dynamic`、运行时程序集加载。P/Invoke 使用 `LibraryImport` 源生成器。

---

## 15. 调试技巧

### 15.1 查看生成代码

`EmitCompilerGeneratedFiles=true` 会将生成的 C# 输出到 `obj/Generated/`。可直接查看 `BuildElementTree()` 的生成结果。

### 15.2 诊断代码

| 诊断 | 说明 |
|---|---|
| `SQX0001` | 语法错误 |
| `SQX0002` | 未定义的控件 |
| `SQX0003` | 必填 Prop 缺失 |
| `SQX0004` | 绑定表达式成员未找到 |
| `SQX0005` | 事件方法签名不匹配 |
| `SQX0006` | ref 名称冲突 |
| `SQX0007` | Prop 类型不匹配 |

### 15.3 构建层裁剪

平台和后端通过 MSBuild `DefineConstants` 在编译期选择：

| 常量 | 启用 |
|---|---|
| `PLATFORM_WIN32` | Win32 窗口宿主 |
| `BACKEND_SOFTWARE` | 纯 C# 软件渲染器 |

`DesktopApplication` 在 `RunCore()` 内自动调用 `BackendRegistration.RegisterDefaults()` 和 `PlatformRegistration.RegisterDefaults()`，根据编译常量注册对应实现。

### 15.4 启用 Tooling

需要截图、输入自动化或运行时 Inspector 时，引用 `Square.Tooling`，并在 `app.Run()` 前启动服务：

```csharp
using Square.Tooling;

using var tooling = ToolingServer.Start(app, new ToolingOptions
{
  Port = 0
});

Console.WriteLine($"Tooling: {tooling.BaseAddress}/api/v1");
Console.WriteLine($"{ToolingServer.TokenHeader}: {tooling.AccessToken}");

app.Run();
```

`Port = 0` 是推荐默认值，由操作系统为每个进程分配独立端口。多个应用或测试实例可以同时运行。连接方必须使用 `tooling.BaseAddress`，不能假设固定端口。

固定端口只用于外部系统要求稳定地址的场景；端口被占用时启动会失败，不会自动递增到其他端口。完整规则、认证和 HTTP API 见 [`Tooling.md`](Tooling.md)。

---

## 16. 下一步

- [API 参考](API-Reference.md) — 完整类型与方法签名
- [SQX 语言规范](Sqx-Spec.md) — 语法细节
- [CSS 规范](CSS-Spec.md) — 样式引擎支持范围
- [组件组合与信号](Composition-and-Signals.md) — Slot、自定义 Tabs 示例、Signal
- [总体架构](Architecture.md) — 模块划分与设计决策
- [示例代码](../samples/Square.Sample/) — 完整示例应用
