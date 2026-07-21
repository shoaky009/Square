# Impeller GPU Backend Plan

## 1. Goal

Add a real GPU drawing backend powered by Flutter Impeller for Square desktop applications.

Phase 1 targets:

- Windows with Vulkan and a Win32 surface.
- Linux with Vulkan and an X11 surface.
- A system-provided prebuilt native library.
- Explicit backend selection. Software remains the default.
- Clear failure when the native library, ABI, Vulkan runtime, or required device features are unavailable.

The backend must render directly to a Vulkan swapchain. A GPU-to-CPU bitmap readback on every frame is not an acceptable presentation path.

## 2. Integration Boundary

Flutter Engine includes the Impeller Standalone SDK at `flutter/impeller/toolkit/interop`. It is a single-header C API intended for non-Flutter applications. Square binds this API directly rather than maintaining a duplicate drawing ABI.

The integration boundary is:

```text
Square managed backend
        |
        | Impeller Standalone SDK C API
        v
impeller.dll / libimpeller.so
        |
        v
Impeller Vulkan backend
```

The Standalone SDK currently has no cross-version ABI stability guarantee. Square therefore pins the Flutter commit and validates required exports at startup. The initial reference checkout is Flutter `cb07e449603530815f8caf08dbf05408546cda34`, whose Standalone SDK API version is `1.4.0`.

## 3. Confirmed Decisions

| Area | Decision |
|---|---|
| Renderer | Real Impeller GPU renderer |
| Initial platforms | Windows Vulkan and Linux/X11 Vulkan |
| Native distribution | Official prebuilt SDK or locally built `impeller` shared library |
| Missing native library | Fail with a detailed error |
| Automatic fallback | None |
| Default backend | Software |
| Registration | Explicit, NativeAOT-safe registration |
| Flutter source | External to this repository |
| ABI ownership | Flutter Impeller Standalone SDK |

## 4. Architecture Changes

### 4.1 Native render targets

The current `PresentFrameHandler` is retained for bitmap backends. GPU backends receive an optional `INativeRenderTarget` through `RenderContextCreateInfo`.

Initial target kinds:

- `Win32Vulkan`: `HWND` and `HINSTANCE`.
- `X11Vulkan`: `Display*`, screen number, and X11 `Window`.

The core API exposes only opaque platform handles. Vulkan and Impeller types remain private to `Square.Backends.Impeller`.

### 4.2 Explicit backend selection

`PlatformHostCreateInfo.RenderBackend` selects the registered factory by name. Its default value is `Software`.

The platform host must not silently replace a requested backend. Selecting `Impeller` without registering it, or without a usable native library, is an application startup error.

### 4.3 Resource lifetime

GPU contexts own native devices, queues, swapchains, textures, and pipelines. The host and render context must be disposed deterministically.

`DesktopApplication` disposes the render context before disposing the platform host so the native surface remains valid while GPU resources are released.

## 5. Native ABI

The official SDK uses opaque handles, fixed-width integers, and plain C structs. No C++ standard library type crosses the boundary.

Required groups:

- SDK version and required-export validation.
- Vulkan context, platform surface, and swapchain creation.
- Logical size and DPI tracking.
- Frame begin, flush, and present.
- Transform and clip stacks.
- Rectangle, rounded rectangle, ellipse, path, image, and glyph drawing.
- Opacity layers.
- On-demand framebuffer readback.
- Deterministic destruction.

The SDK version is returned by `ImpellerGetVersion()` and passed back to `ImpellerContextCreateVulkanNew()`. The current SDK documentation requires an exact version match.

## 6. Managed Backend

The managed project is `Square.Backends.Impeller`.

Responsibilities:

- Load `impeller.dll` on Windows or `libimpeller.so` on Linux.
- Honor `SQUARE_IMPELLER_LIBRARY` or an explicit factory library path.
- Validate the required Standalone SDK exports before creating a context.
- Convert Square primitives into ABI structs.
- Preserve logical-pixel semantics and pass DPI separately.
- Turn failed SDK operations into `ImpellerException` with operation diagnostics.
- Never fall back to Software.
- Remain compatible with trimming and NativeAOT.

## 7. Rendering Semantics

- Layout and drawing commands use logical pixels.
- Swapchain dimensions use physical pixels.
- Colors use premultiplied alpha.
- The preferred swapchain color space is sRGB.
- `Present(null)` means a full frame.
- `Present([])` is a no-op.
- Dirty rectangles are damage hints; Phase 1 may submit the complete swapchain image.
- Text measurement and wrapping remain owned by Square.
- Glyph rasterization initially reuses Square's existing glyph metrics and coverage so hit testing and selection remain consistent.

## 8. Delivery Phases

### Phase A: integration foundation

- [x] Add this plan.
- [x] Add explicit backend selection.
- [x] Add native render target abstractions.
- [x] Expose Win32 and X11 native handles to backend creation.
- [x] Add deterministic host/context disposal.
- [x] Add the managed Impeller project and native loader.
- [x] Identify and bind the official Standalone SDK C API.
- [x] Test registration, selection, export validation, and missing-library failures.

### Phase B: Vulkan surface and frame lifecycle

- [x] Bind directly to the Impeller Standalone SDK API from the pinned Flutter revision.
- [x] Create managed bindings for Impeller Vulkan contexts, swapchains, surfaces, display lists, and paints.
- [x] Create Win32 and X11 `VkSurfaceKHR` instances from Square platform handles.
- [x] Create the initial swapchain.
- [ ] Recreate swapchains for resize and surface loss.
- [x] Map begin-frame, clear, flush, present, and resize to the official SDK.
- [x] Render and capture a real Win32 Vulkan frame using the official Windows x64 SDK.
- [ ] Handle zero-sized windows, out-of-date swapchains, and device loss.

### Phase C: drawing commands

- [x] Map solid rectangles, rounded rectangles, and ellipses to Impeller display lists and paints.
- [x] Map line/arc paths with fill, stroke cap, join, and miter styles.
- [x] Map linear and radial gradients with pad, repeat, and reflect spread modes.
- [ ] Map dashed paths. The current Standalone C API exposes only a two-point dashed-line primitive, not a general path effect.
- [x] Map transform and rectangle, rounded rectangle, ellipse, and path clip stacks to Impeller display-list save/restore operations.
- [x] Upload and cache Square bitmaps as Impeller RGBA textures.
- [x] Map opacity layers to `ImpellerDisplayListBuilderSaveLayer`.

### Phase D: text and tooling

- [x] Register system fonts and draw Impeller paragraphs.
- [x] Map family, size, weight, italic, alignment, line height, width, and UTF-8 text.
- [x] Register Windows CJK and emoji fallback fonts when available.
- [x] Transfer font mapping ownership with an Impeller release callback so asynchronous font use remains valid.
- [x] Use shared system-font glyph advances for Square measurement, caret positioning, hit-testing, and backend rendering fallbacks.
- [ ] Add full shaping-aware metrics for kerning, ligatures, bidi text, and non-BMP fallback fonts.
- GPU framebuffer readback remains unavailable in the current Standalone C API; automated screenshots use deterministic DisplayTree replay instead.
- Render diagnostics.
- Backend comparison screenshots with documented tolerances.

### Phase E: deployment

- [x] Add `tools/impeller/download-sdk.ps1` for official SDK downloads.
- [x] Validate the Linux SDK download and runtime path with Ubuntu 24.04 on WSLg Vulkan.
- NativeAOT publish smoke tests.
- CI runners with Vulkan support.
- Version compatibility table for Flutter Engine and the Impeller Standalone SDK.

## 9. Failure Policy

The following conditions must fail explicitly:

- `Impeller` was selected but not registered.
- The shared library cannot be loaded.
- Required exports are missing.
- The library does not expose the required Standalone SDK API.
- Context creation rejects the SDK version returned by `ImpellerGetVersion()`.
- Vulkan loader, instance extensions, device extensions, or a suitable queue are unavailable.
- Surface or swapchain creation fails.

Error messages must include the selected backend, attempted library path, ABI information where available, and the native diagnostic string.

## 10. Acceptance Criteria

- Software remains the default and all existing tests pass.
- `RenderBackend = "Impeller"` selects only the Impeller factory.
- No automatic Software fallback occurs.
- Windows and X11 provide the handles needed to create Vulkan surfaces.
- The official Impeller SDK renders directly to a Vulkan swapchain.
- Resize and DPI changes recreate physical targets without changing logical layout semantics.
- Missing or incompatible native libraries produce deterministic errors.
- The managed backend and registration path work in a NativeAOT application.
- Basic scene output is visually consistent with Software within documented pixel tolerances.

## 11. Win32 Smoke Test

Download the pinned official SDK:

```powershell
pwsh tools/impeller/download-sdk.ps1
```

Run the GPU-only smoke scene and capture the window:

```powershell
dotnet run --project samples/Square.Sample.Impeller -- \
  --library artifacts/impeller-sdk/windows-x64/extracted/lib/impeller.dll \
  --screenshot artifacts/impeller-smoke.png
```

The smoke scene verifies Vulkan context creation, Win32/X11 surface creation, swapchain acquisition, display-list rendering, paths, bitmap textures, typography, linear and radial gradients, stroke cap/join/miter styles, rectangle/rounded/ellipse/path clipping, opacity layers, presentation, and process-internal render-command capture.

## 12. Full Sample Comparison

The regular `Square.Sample` accepts the backend and Impeller SDK path:

```powershell
dotnet run --project samples/Square.Sample -- \
  --backend Impeller \
  --impeller-library artifacts/impeller-sdk/windows-x64/extracted/lib/impeller.dll \
  --screenshot artifacts/square-sample-impeller.png
```

The same scene can be captured with Software using `--backend Software`. After switching Square measurement and hit-testing from fixed half/full-em estimates to shared system glyph advances, the Win32 comparison still produced matching `900x980` images, a sampled mean absolute RGB difference of `2.63`, and a significant-difference sample ratio of `3.79%`. The remaining differences are primarily GDI versus Impeller/Skia text rasterization, shaping, and edge antialiasing rather than layout or control color differences.

## 13. Linux/X11 Validation

Cross-publishing must set the target platform explicitly so project references compile the X11 implementation instead of inheriting the Windows build host:

```powershell
dotnet restore samples/Square.Sample.Impeller -r linux-x64 -p:SquareTargetPlatform=X11
dotnet publish samples/Square.Sample.Impeller -c Release -r linux-x64 `
  -p:SquareTargetPlatform=X11 --self-contained false --no-restore
```

The fixed Linux SDK was validated on Ubuntu 24.04 under WSLg with `libX11.so.6`, `libvulkan.so.1`, and `/dev/dxg`. The X11 host created a Vulkan surface and rendered and presented the complete smoke scene.

Automated screenshots now use `DesktopApplication.CaptureRendererBitmapAsync()`. This replays the retained display tree into an in-process Software bitmap, so it does not enumerate windows, use a PID, depend on the desktop compositor, or include native window borders. The Impeller C API does not currently expose surface pixel readback, so this is a deterministic render-command capture rather than a GPU framebuffer readback. Software replay supports the gradients and geometry clips used by the Impeller smoke scene.

## 14. SDK Discovery And Tooling

下载固定 SDK：

```powershell
pwsh tools/impeller/download-sdk.ps1
```

`Square.Sample` 按以下顺序解析原生库：

1. `--impeller-library`
2. `SQUARE_IMPELLER_LIBRARY`
3. 从当前目录和应用目录向上查找 `artifacts/impeller-sdk/<platform>/extracted/lib/`
4. 未找到显式路径时，由系统动态库搜索路径解析 `impeller.dll` 或 `libimpeller.so`

因此从仓库根目录下载一次 SDK 后，可以直接运行：

```powershell
dotnet run --project samples/Square.Sample/Square.Sample.csproj -- --backend=Impeller --tooling
```

控制台会打印实际加载路径。SDK 二进制位于被 `.gitignore` 排除的 `artifacts/`，不会提交到仓库。

Impeller Standalone C API 当前不提供 surface framebuffer readback。`--tooling` 的 screenshot endpoint 会使用 Software RenderContext 重放 Display Tree，因此适合验证命令流、布局和控件状态，但不能代表 Impeller GPU 输出的逐像素结果。实际窗口仍由 Impeller GPU 后端渲染。

## 15. Known Vulkan Synchronization Issue

固定 SDK `cb07e449603530815f8caf08dbf05408546cda34`（Standalone API 1.4.0）在部分 validation layer / driver 组合上可能报告：

```text
UNASSIGNED-non-acquired-swapchain-image-used
vkQueueSubmit() performs a layout transition on a presentable image,
but the semaphore signaled by image acquire was not waited on.
```

这表示 Impeller 内部提交了对 swapchain image 的修改，但该次 queue submit 没有等待 `vkAcquireNextImageKHR` 对应的 acquire semaphore。可能表现为 validation error、闪烁、白帧、resize 后异常，极端情况下可能导致 device lost。

Square 的 Standalone C API 调用顺序与 SDK 自带 `example_vk.c` 一致：

```text
ImpellerVulkanSwapchainAcquireNextSurfaceNew
ImpellerSurfaceDrawDisplayList
ImpellerSurfacePresent
ImpellerSurfaceRelease
```

Square 无法从该 C API 获得内部的 `VkSwapchainKHR`、image index、`VkImage`、acquire semaphore、render-finished semaphore 或 queue submit，因此不能在 C# wrapper 中补加缺失的等待。不要把 surface acquire 移到更早的位置；SDK 文档要求尽量延迟 acquire。

处理策略：

- 优先升级到包含后续 Vulkan KHR swapchain/frame-synchronizer 修复的 Impeller SDK。
- 升级时必须使用同一 artifact 中的 `impeller.h` 重新核对 ABI；Standalone SDK 当前不保证 API/ABI 稳定。
- 升级前保持每帧只 acquire 一个 surface，并严格执行 draw、present、release。
- 关闭 validation 只能隐藏日志，不是同步修复。
- 若错误伴随实际渲染故障，使用 Square 原生 Vulkan 或 Software 后端，直到 Impeller SDK pin 完成升级验证。

## 16. Out of Scope for Phase 1

- macOS and Metal.
- Android and iOS.
- WebGPU or OpenGL.
- Automatic backend probing and fallback.
- Shipping Flutter Engine source or binaries in the Square repository.
- Using Impeller to perform text shaping or layout.
