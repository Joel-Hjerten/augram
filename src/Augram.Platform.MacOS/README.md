# Augram.Platform.MacOS

macOS implementations of the same Core ports as `Augram.Platform.Windows`. Compiles as stubs until the Mac adapter is written (no Mac available to test on yet; design-for, not test-on).

**May reference:** `Augram.Core` only. AppKit / Accessibility interop goes here and nowhere else.

**Must never contain:** Avalonia, SharpHook, business rules, or Windows types.

Out of scope for plan 0001. Empty apart from this plan.

## Window operations (plan)

`WindowOperation` is platform-neutral by decision (Joel, 2026-10-07: anything system-related on both systems maps to each other), so this is written down where the Mac work starts: what the Mac `IWindowOperations` does for each operation, through the Accessibility API (`AXUIElement`), which the gesture hook already needs the Accessibility permission for. `WindowIdentity.RootHandle` will carry the top-level window element (found from the `CGWindowID` under the point via the owning app's `kAXWindowsAttribute`, or `_AXUIElementGetWindow`). `kAXErrorInvalidUIElement` on any call is the "window gone" case. Units are points, not pixels: `WindowSize` is interpreted in points here, and a step authored on the other platform carries its `HostPlatform` so the UI can say so.

| `WindowOperation` | Accessibility API equivalent |
|---|---|
| `Close` | `AXUIElementPerformAction(closeButton, kAXPressAction)` with `closeButton` = the window's `kAXCloseButtonAttribute`; the app runs its own close handling (save prompts), the counterpart of `SC_CLOSE` on Windows |
| `Minimize` | `AXUIElementSetAttributeValue(window, kAXMinimizedAttribute, kCFBooleanTrue)` |
| `MaximizeOrRestore` | `kAXPressAction` on the window's `kAXZoomButtonAttribute`: zoom toggles between the user size and the fitted size. Whether "maximize" should mean zoom, native full screen (`AXFullScreen`) or tiling is D6, still open; the adapter starts with zoom |
| `ToggleAlwaysOnTop` | **No public API** sets another app's window level (`NSWindow.level` is in-process only). `Supports` returns false, `Perform` returns `NotSupported`, and the UI marks the step |
| `Center` | read `kAXSizeAttribute`; set `kAXPositionAttribute` to the centre of `NSScreen.visibleFrame` of the screen containing the window. `visibleFrame` excludes the menu bar and the Dock, the work-area equivalent. AX positions are top-left origin and global while `NSScreen` frames are bottom-left origin, so the y axis is flipped first |
| `SetSize` | set `kAXSizeAttribute`, keep `kAXPositionAttribute`, clamp into `visibleFrame` by the Windows `WindowGeometry` rules ported to points |
| `SnapLeftHalf` / `SnapRightHalf` | set `kAXPositionAttribute` and `kAXSizeAttribute` to the half of `visibleFrame` (position, then size, then position again, as window-management tools do, because a size the current position cannot hold is clipped). No zoom first: a zoomed Mac window is just a window sized to the visible frame |
