# ImpellerSharp Research Notes

Reference repository: `https://github.com/paul-selvi/ImpellerSharp`

Local checkout inspected: `C:\Users\Wuldas\.AA\temp\ImpellerSharp`

## Findings

- The project binds Flutter's existing Impeller Standalone SDK C API from `flutter/impeller/toolkit/interop/impeller.h`.
- It builds the official GN target `flutter/impeller/toolkit/interop:library` to produce `impeller.dll`, `libimpeller.so`, or `libimpeller.dylib`.
- The Flutter checkout is pinned to `cb07e449603530815f8caf08dbf05408546cda34` (`3.33.0-0.0.pre-2592-gcb07e449603`).
- That checkout exposes Impeller Standalone SDK API version `1.4.0`.
- Vulkan context creation supplies a `vkGetInstanceProcAddr` callback to `ImpellerContextCreateVulkanNew`.
- The caller obtains Impeller's Vulkan instance through `ImpellerContextGetVulkanInfo`, creates a platform `VkSurfaceKHR`, and transfers it to `ImpellerVulkanSwapchainCreateNew`.
- Each frame acquires an `ImpellerSurface`, draws an `ImpellerDisplayList`, and presents the surface.
- Display lists directly support transforms, clips, rectangles, ovals, rounded rectangles, paths, textures, paragraphs, and layers.
- SafeHandle-based wrappers are used to model the SDK's retain/release object convention.

## Maturity Notes

- The binding surface is substantial and useful as an implementation reference.
- Its compatibility matrix still marks Windows Vulkan and Linux Vulkan as planned.
- Its sample accepts an externally supplied `VkSurfaceKHR`; it does not provide complete Win32/X11 platform surface creation.
- Square therefore reuses the official SDK architecture while retaining responsibility for Win32/X11 Vulkan surface creation and host lifecycle.

## Square Decision

Square directly binds the official Standalone SDK. The previous custom `square_impeller` drawing ABI was removed because it duplicated an existing Flutter API and increased maintenance cost.

The pinned Windows x64 SDK is available from:

```text
https://storage.googleapis.com/flutter_infra_release/flutter/cb07e449603530815f8caf08dbf05408546cda34/windows-x64/impeller_sdk.zip
```

Use `tools/impeller/download-sdk.ps1` to download and extract it under `artifacts/impeller-sdk`.

Square successfully loaded this SDK and rendered a real Win32 Vulkan smoke frame. The captured `800x600` image contained the expected blue, purple, green, orange, and yellow primitives, confirming that context creation, `VkWin32SurfaceKHR`, swapchain acquisition, display-list drawing, and presentation are operational.

The smoke scene was then extended with an Impeller path and a cached bitmap texture. Pixel sampling confirmed the expected pink path (`236,72,153`) and both checkerboard texture colors, validating PathBuilder translation and `ImpellerTextureCreateWithContentsNew` uploads.

Typography was validated with Segoe UI, bold text, UTF-8 paragraph input, and Windows CJK/emoji fallback font registration. Font bytes must not be unpinned when a typography context is released: the Standalone SDK may retain mappings asynchronously. Square therefore transfers unmanaged font buffers to Impeller and frees them only through the mapping release callback.

The complete `Square.Sample` was then rendered by both Software and Impeller. Both captures were `900x980`; sampled mean RGB difference was `2.61`, with `3.77%` of sampled pixels exceeding the comparison threshold. Backgrounds, panels, and control colors matched at inspected points, indicating that the remaining variance is mostly glyph rasterization and edge antialiasing.

Square text measurement, caret positioning, and hit-testing now use an optional system-font advance provider registered by `Square.Text`, with the previous Unicode half/full-em estimates retained as a fallback. This makes the shared layout metrics match the advances used by Software rendering and closely track Impeller's registered system fonts. Full shaping features such as kerning, ligatures, bidi layout, and non-BMP font fallback still require a shaping-aware shared text engine.

Linux/X11 Vulkan was validated end to end using Ubuntu 24.04 on WSLg and the official fixed-commit `linux-x64` SDK. A cross-publish issue was found where referenced projects inherited the Windows build host instead of the Linux RID; Square now propagates an explicit `SquareTargetPlatform` property through project references. The X11 smoke scene successfully created a Vulkan surface and presented through Impeller.

Sample screenshots no longer depend on process-window lookup. `DesktopApplication.CaptureRendererBitmapAsync()` replays the retained display tree into an in-process Software bitmap. This is portable and deterministic but is not an exact Impeller framebuffer readback because the Standalone C API currently exposes no surface readback function. The replay path now covers linear/radial gradients and rectangle/rounded/ellipse/path clips used by the GPU smoke scene.
