# Tooling

> Version: 0.3  
> 配套：`Getting-Started.md`、`API-Reference.md`、`Rendering.md`

`Square.Tooling` 提供一个只监听 `127.0.0.1` 的 HTTP 调试服务，用于在运行中的 Square 桌面应用上做截图采集和输入自动化。它面向本地开发、示例演示、端到端测试和外部调试工具，不参与应用的正常 UI 渲染管线。

---

## 1. 启动服务

应用需要引用 `Square.Tooling`，然后在 `DesktopApplication.Run()` 前启动服务。`ToolingServer` 实现 `IDisposable` / `IAsyncDisposable`，通常用 `using` 保证应用退出时关闭 HTTP 服务。

```csharp
using Square.Hosting;
using Square.Platform;
using Square.Tooling;

var app = new DesktopApplication(new Main(), new PlatformHostCreateInfo
{
    Title = "My App",
    Width = 800,
    Height = 600
});

using var tooling = ToolingServer.Start(app, new ToolingOptions
{
    Port = 5128,
    AccessToken = "dev-token",
    AllowInputInjection = true
});

Console.WriteLine($"{tooling.BaseAddress}/api/v1/health");
Console.WriteLine($"{ToolingServer.TokenHeader}: {tooling.AccessToken}");

app.Run();
```

`ToolingOptions`：

| 属性 | 默认值 | 说明 |
|---|---:|---|
| `Port` | `5128` | 本地 HTTP 端口，范围为 `1..65535` |
| `AccessToken` | `null` | 访问令牌；为空时自动生成 24 字节随机 token 的十六进制字符串 |
| `AllowInputInjection` | `true` | 是否允许 `/input/*` 输入注入接口；关闭后输入接口返回 `403` |

RichText 示例已经集成 Tooling：

```bash
dotnet run --project samples/Square.Sample.RichText/Square.Sample.RichText.csproj
```

启动后控制台会输出 base address 和 token header。示例固定使用：

```text
http://127.0.0.1:5128
X-Square-Tooling-Token: square-richtext-demo
```

---

## 2. 认证与安全边界

所有 endpoint 都必须携带 header：

```text
X-Square-Tooling-Token: <access-token>
```

缺少或错误 token 时返回：

```json
{"error":"unauthorized"}
```

状态码为 `401`。Token 比较使用固定时间比较，降低本地调试场景中的时序侧信道风险。

服务只绑定 `http://127.0.0.1:{Port}`，不监听局域网地址。不要把固定 token 用在生产构建或提交到公开仓库；示例中的固定 token 仅用于本地 demo。

---

## 3. API 概览

所有路径都以 `/api/v1` 为前缀。

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `/health` | 返回服务状态、进程 ID 和输入注入开关 |
| `GET` | `/screenshot` | 返回当前 renderer bitmap 的 PNG |
| `POST` | `/input/pointer` | 注入鼠标移动/按下/抬起 |
| `POST` | `/input/key` | 注入键盘按下/抬起 |
| `POST` | `/input/text` | 注入文本输入 |
| `POST` | `/input/wheel` | 注入滚轮 |

计划中的 Inspector / Debug endpoint 见 [7. 元素调试与 Inspector 计划](#7-元素调试与-inspector-计划)。

### GET /api/v1/health

返回 JSON：

```json
{
  "status": "ok",
  "processId": 12345,
  "inputInjection": true
}
```

示例：

```bash
curl -H "X-Square-Tooling-Token: square-richtext-demo" \
  http://127.0.0.1:5128/api/v1/health
```

### GET /api/v1/screenshot

从当前 render context 捕获位图并返回 PNG，文件名为 `square-screenshot.png`。

```bash
curl -H "X-Square-Tooling-Token: square-richtext-demo" \
  -o screenshot.png \
  http://127.0.0.1:5128/api/v1/screenshot
```

截图来自 renderer bitmap，而不是平台窗口截图；它适合做 UI 回归、调试脏区渲染或采集控件状态。

### POST /api/v1/input/pointer

请求体：

```json
{
  "x": 40,
  "y": 32,
  "action": "Down",
  "modifiers": ["Shift"]
}
```

字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `x` | number | 是 | 客户区 X 坐标 |
| `y` | number | 是 | 客户区 Y 坐标 |
| `action` | string | 是 | `Down`、`Up`、`Move` |
| `modifiers` | string[] | 否 | `Shift`、`Control`、`Alt`，省略表示 `None` |

成功返回 `204 No Content`。

```bash
curl -X POST -H "X-Square-Tooling-Token: square-richtext-demo" \
  -H "Content-Type: application/json" \
  -d "{\"x\":40,\"y\":32,\"action\":\"Down\"}" \
  http://127.0.0.1:5128/api/v1/input/pointer
```

### POST /api/v1/input/key

请求体：

```json
{
  "keyCode": 65,
  "action": "Down",
  "modifiers": ["Control"]
}
```

字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `keyCode` | integer | 是 | 平台键码；字母键常用 ASCII/虚拟键码，例如 `65` 表示 A |
| `action` | string | 是 | `Down` 或 `Up` |
| `modifiers` | string[] | 否 | `Shift`、`Control`、`Alt` |

成功返回 `204 No Content`。

### POST /api/v1/input/text

请求体：

```json
{
  "text": "hello 中文"
}
```

字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `text` | string | 是 | 注入到当前焦点文本编辑器的文本 |

成功返回 `204 No Content`。该接口走文本输入路径，适合输入 Unicode 文本；快捷键请使用 `/input/key`。

### POST /api/v1/input/wheel

请求体：

```json
{
  "x": 120,
  "y": 180,
  "delta": -120,
  "modifiers": []
}
```

字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `x` | number | 是 | 客户区 X 坐标 |
| `y` | number | 是 | 客户区 Y 坐标 |
| `delta` | integer | 是 | 滚轮增量；正负方向沿用平台输入语义 |
| `modifiers` | string[] | 否 | `Shift`、`Control`、`Alt` |

成功返回 `204 No Content`。

---

## 4. 错误响应

| 状态码 | 场景 |
|---:|---|
| `400` | JSON 无效、字段缺失、字段类型错误或枚举值不支持 |
| `401` | 缺少或错误 `X-Square-Tooling-Token` |
| `403` | `AllowInputInjection=false` 时调用 `/input/*` |
| `500` | 截图或输入注入期间出现未处理异常 |

输入 JSON 使用 camelCase 字段名。枚举值解析不区分大小写。

---

## 5. 运行模型

Tooling HTTP 请求运行在 ASP.NET Core 轻量 WebApplication 中。输入注入不会直接跨线程操作 UI；`ToolingServer` 会调用 `DesktopApplication.InjectPointerAsync`、`InjectKeyAsync`、`InjectTextAsync` 和 `InjectWheelAsync`，再通过 `Dispatcher.InvokeAsync` 投递到 UI 线程。

截图通过 `DesktopApplication.CaptureRendererBitmapAsync()` 在 UI 线程读取当前 renderer bitmap。当前实现要求活动 render context 支持 `IRenderBitmapSource`；默认 Software Renderer 支持该能力。

输入注入后的行为与平台输入路径一致：鼠标命中测试、焦点、文本编辑器、键盘快捷键、滚轮路由和必要的重绘都会由 `DesktopApplication` 统一处理。

---

## 6. 自动化示例

下面示例点击 RichText 编辑器左上角并输入文本：

```bash
TOKEN=square-richtext-demo
BASE=http://127.0.0.1:5128/api/v1

curl -X POST -H "X-Square-Tooling-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"x":45,"y":230,"action":"Down"}' \
  "$BASE/input/pointer"

curl -X POST -H "X-Square-Tooling-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"x":45,"y":230,"action":"Up"}' \
  "$BASE/input/pointer"

curl -X POST -H "X-Square-Tooling-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"text":"Hello from tooling"}' \
  "$BASE/input/text"

curl -H "X-Square-Tooling-Token: $TOKEN" \
  -o after-input.png \
  "$BASE/screenshot"
```

在 Windows PowerShell 中可使用等价变量：

```powershell
$token = "square-richtext-demo"
$base = "http://127.0.0.1:5128/api/v1"
$headers = @{ "X-Square-Tooling-Token" = $token }

Invoke-RestMethod -Method Post -Headers $headers -ContentType "application/json" `
  -Body '{"x":45,"y":230,"action":"Down"}' "$base/input/pointer"
Invoke-RestMethod -Method Post -Headers $headers -ContentType "application/json" `
  -Body '{"x":45,"y":230,"action":"Up"}' "$base/input/pointer"
Invoke-RestMethod -Method Post -Headers $headers -ContentType "application/json" `
  -Body '{"text":"Hello from tooling"}' "$base/input/text"
Invoke-WebRequest -Headers $headers -OutFile "after-input.png" "$base/screenshot"
```

---

## 7. 元素调试与 Inspector 计划

Tooling 后续应承担运行时 Inspector 能力：通过坐标、元素 ID 或树查询定位 Square 元素，并返回模板源码位置、布局盒、样式、状态和绘制信息。该能力用于 IDE 跳转、可视化检查、端到端测试失败诊断和外部调试工具。

### 7.1 总体目标

Inspector 不应只暴露截图，也不应只暴露 DisplayTree。它需要把运行时对象和模板源文件连起来：

```text
.sqx / .sqv source
  -> Parser AST SourceSpan
  -> Source Generator emits ElementDebugInfo
  -> Element.DebugInfo
  -> LayoutBox / DisplayNode keeps Element reference or debug id
  -> Tooling hit test / query
  -> source location + runtime state
```

核心原则：

1. **源码位置由 Source Generator 注入**：不要依赖 C# caller info，因为 caller info 会指向 `.g.cs`，不是 `.sqx` / `.sqv`。
2. **权威调试信息挂在 Element 上**：Rendering 只传递引用或 debug id，不作为源码信息的唯一来源。
3. **Tooling 只读为主**：Inspector 默认不修改 Element Tree；未来需要样式热调试时再单独设计写入权限。
4. **Debug 信息可裁剪**：Release / NativeAOT 发布可以通过构建属性关闭详细 source path 或完全关闭 Inspector metadata。

### 7.2 编译期数据：SourceSpan 与 ElementDebugInfo

`Square.Markup` 的 AST 节点应保留模板位置：

```csharp
public readonly record struct SourceSpan(
    string FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
```

`Square.SourceGenerator` 在生成元素创建代码时注入调试信息：

```csharp
var element = new Button();
element.SetDebugInfo(ElementDebugInfo.Create(
    sourceId: 3,
    startLine: 12,
    startColumn: 5,
    endLine: 18,
    endColumn: 12,
    tagName: "Button",
    componentName: "Main",
    kind: ElementGeneratedKind.TemplateNode));
```

`sourceId` 推荐指向组件级 source table，避免每个元素重复存完整路径字符串：

```csharp
private static readonly DebugSourceFile[] __SquareDebugSources =
[
    new(3, "Components/Main.sqx")
];
```

### 7.3 运行时数据：Element 上的 DebugInfo

`Square.UI` 负责定义轻量 metadata：

```csharp
public sealed class ElementDebugInfo
{
    public int SourceId { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
    public string? TagName { get; init; }
    public string? ComponentName { get; init; }
    public ElementGeneratedKind Kind { get; init; }
}

public enum ElementGeneratedKind
{
    TemplateNode,
    ComponentRoot,
    SlotContent,
    ForItem,
    ConditionalBranch,
    GeneratedWrapper
}
```

Element 暴露只读调试入口：

```csharp
public ElementDebugInfo? DebugInfo { get; }
```

设置入口应限制在框架/生成代码可用范围，例如 `internal set`、`SetDebugInfo(...)` 或 source-generator-only helper，避免普通应用逻辑随意篡改源码位置。

### 7.4 Layout / DisplayTree 反查

Tooling 的坐标点选需要从屏幕坐标回到 Element：

```text
client point
  -> latest layout root / display tree
  -> deepest hit LayoutBox or DisplayNode
  -> source Element
  -> Element.DebugInfo
```

建议 Rendering 层保留：

| 数据 | 用途 |
|---|---|
| `LayoutBox.Element` | 布局命中、盒模型检查、尺寸定位 |
| `DisplayNode.Element` 或 `DebugElementId` | 绘制命中、截图叠加、高亮 |
| `Element.DebugId` | Tooling 返回稳定引用，后续查询详情 |

`DebugId` 只要求在单次运行期间稳定，不要求跨进程或跨构建稳定。跨构建跳转应依赖 `SourceSpan`。

### 7.5 计划 endpoint

后续 Inspector endpoint 建议挂在 `/api/v1/inspect/*` 下：

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `/inspect/tree` | 返回当前 Element / Layout 调试树摘要 |
| `GET` | `/inspect/hit-test?x=120&y=80` | 返回指定客户区坐标命中的最深元素 |
| `GET` | `/inspect/elements/{id}` | 返回元素详情 |
| `GET` | `/inspect/elements/{id}/styles` | 返回 computed style、matched rules 和 inline style |
| `GET` | `/inspect/elements/{id}/layout` | 返回 content/padding/border/margin box 与 flex/grid 信息 |
| `GET` | `/inspect/elements/{id}/source` | 返回模板源码位置 |
| `GET` | `/inspect/snapshot` | 返回 tree + layout + selected display info 的一次性快照 |

`/inspect/hit-test` 示例响应：

```json
{
  "elementId": 42,
  "tagName": "Button",
  "componentName": "Main",
  "bounds": { "x": 24, "y": 96, "width": 128, "height": 36 },
  "source": {
    "file": "Components/Main.sqx",
    "startLine": 12,
    "startColumn": 5,
    "endLine": 18,
    "endColumn": 12
  },
  "state": {
    "hover": true,
    "focus": false,
    "active": false,
    "disabled": false
  }
}
```

`/inspect/tree` 应默认返回摘要，避免大型 UI 一次性输出过多数据：

```json
{
  "root": {
    "id": 1,
    "tagName": "View",
    "componentName": "Main",
    "bounds": { "x": 0, "y": 0, "width": 800, "height": 600 },
    "children": [
      { "id": 2, "tagName": "Text", "text": "Hello", "childCount": 0 }
    ]
  }
}
```

### 7.6 IDE 跳转协议

Tooling 只返回源码位置，不直接假设 IDE。外部工具可以按响应中的 source location 调用 IDE：

```text
file: Components/Main.sqx
line: 12
column: 5
```

后续可以补充可选 endpoint：

| 方法 | 路径 | 说明 |
|---|---|---|
| `POST` | `/inspect/open-source` | 由本地开发工具注册 handler 后打开源码 |

该 endpoint 不应默认启用，避免 Tooling 服务直接执行外部命令。默认安全模型应保持“返回数据，由调用方决定如何打开 IDE”。

### 7.7 安全与隐私

Inspector 会暴露源码路径、组件名、文本内容和样式信息，因此需要比截图/输入更明确的开关：

```csharp
public sealed class ToolingOptions
{
    public bool AllowInspector { get; set; } = true;
    public bool IncludeSourcePaths { get; set; } = true;
    public bool IncludeTextContent { get; set; } = true;
}
```

建议默认策略：

| 构建 | `AllowInspector` | `IncludeSourcePaths` | 说明 |
|---|---:|---:|---|
| Debug | true | true | 本地开发默认可用 |
| Release | false | false | 除非显式打开 |
| NativeAOT publish | false | false | 避免泄露路径并减少 metadata |

即使 Inspector 启用，服务仍只监听 `127.0.0.1`，并继续要求 `X-Square-Tooling-Token`。

### 7.8 分阶段实现

| 阶段 | 内容 | 退出标准 |
|---|---|---|
| D0 | 文档计划与命名稳定 | Tooling 文档明确 Inspector 数据流和 endpoint 草案 |
| D1 | `SourceSpan` 贯通 Parser / AST | `.sqx` / `.sqv` AST 节点保留准确行列 |
| D2 | Generator 注入 `ElementDebugInfo` | 生成的元素可回溯到模板源位置 |
| D3 | `Element.DebugId` 与 runtime registry | Tooling 可通过 ID 查询当前运行时元素摘要 |
| D4 | Layout / DisplayTree hit test | `/inspect/hit-test` 可从坐标返回元素与源码位置 |
| D5 | Tree / element detail endpoint | `/inspect/tree`、`/inspect/elements/{id}` 可用 |
| D6 | Style / layout diagnostics | 可查看 computed style、matched rules、box model |
| D7 | IDE 集成 | 外部工具可基于 Tooling 响应跳转源码 |

优先级建议：先完成 D1-D4，形成“点选 UI -> 定位 `.sqx/.sqv` 源码”的闭环；样式规则解释、IDE 打开、热编辑可以后置。
