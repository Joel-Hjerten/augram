# Steps/WindowOp

The **Window** step (key `windowOp`, category System): one `WindowOperation` on the window under the gesture start (F5's window actions, all in Joel's SP.net config: close, minimize, maximize/restore, always on top, center, set size, snap to the left or right half). The step is authored once and runs on both platforms (F8): the operation is semantic, each platform's `IWindowOperations` adapter maps it to its own calls, and `Supports` says when it has no equivalent.

**The rule: a step is authored once; the adapter maps or declines; the UI shows the decline.** Core never branches on the platform; the step never guesses.

## Files

| File | Role |
|---|---|
| `WindowOpStep` | record: `Operation`, `Size` (for `SetSize` only) · `Summary` "Close window", "Set size 1280×720", "Snap to left half"… |
| `WindowOpStepType` | metadata and JSON: `{ "operation": "Minimize" }`; `SetSize` adds `"width"` and `"height"` (both required, 1..32767); an unknown operation name or a bad size is a `StepFormatException` naming the member |
| `WindowOpExecutor` | target null → Skipped "no window under the gesture start" · `!Supports` → Skipped with `WindowOperationResult.NotSupported(...).Reason` · `Perform` → Done, or Failed with the adapter's reason · `SetSize` without a size → Failed. One Debug line (`steps` / "Window operation": operation, outcome, reason, process) per run |

Window operations never wait (A8): the settle delay is for keystrokes after a focus change, and the executor applies it, not this step.

## Windows ↔ macOS mapping

The Windows column is what `Augram.Platform.Windows` does against the window's root handle (`WindowIdentity.RootHandle`). The macOS column is what `Augram.Platform.MacOS` does (or will do; state in its README) through the Accessibility API (`AXUIElement` of the window; needs the Accessibility permission, without which every operation fails with a reason). "Work area" is the monitor's `rcWork` (`MonitorFromWindow` + `GetMonitorInfo`) on Windows and the window's screen's `NSScreen.visibleFrame` on macOS.

| `WindowOperation` | Windows adapter | macOS adapter |
|---|---|---|
| `Close` | `PostMessage(WM_SYSCOMMAND, SC_CLOSE)`: the app gets its own close path ("save changes?"), the process is never killed | `AXPress` on the window's `AXCloseButton` |
| `Minimize` | `ShowWindow(SW_MINIMIZE)` | set `AXMinimized` = true |
| `MaximizeOrRestore` | `IsZoomed ? ShowWindow(SW_RESTORE) : ShowWindow(SW_MAXIMIZE)` | **Fill**: `AXPosition` + `AXSize` to the screen's `visibleFrame`, remembering the frame before; filled already → the remembered frame back (or `AXPress` on `AXZoomButton` when Augram did not fill it); native full screen → leave it. D6 working choice 2026-10-07, replacing zoom: zoom is per-app and often fits content, full screen takes a Space |
| `ToggleAlwaysOnTop` | read `WS_EX_TOPMOST`, then `SetWindowPos(HWND_TOPMOST or HWND_NOTOPMOST)` | **not supported**: the Accessibility API cannot change another app's window level. `Supports` returns false, the executor skips the step with "ToggleAlwaysOnTop is not supported on MacOS", the command continues, and the UI marks the step |
| `Center` | restore first if maximized; `SetWindowPos` to the centre of the work area, size kept | `AXPosition` to the centre of `visibleFrame`, `AXSize` kept |
| `SetSize` | restore first if maximized; `SetWindowPos` with the new width and height, top-left kept, clamped to the work area | `AXSize`; `AXPosition` kept |
| `SnapLeftHalf` | restore first if maximized; `SetWindowPos` to the left half of the work area | `AXPosition` + `AXSize` to the left half of `visibleFrame` |
| `SnapRightHalf` | restore first if maximized; `SetWindowPos` to the right half of the work area | `AXPosition` + `AXSize` to the right half of `visibleFrame` |

Every Windows row targets the root owner so a gesture over a child control acts on the whole window; every macOS row targets the window the point hit-tests to in the window server's list, found again among its app's `AXWindows` by `CGWindowID`. Sizes are physical pixels on Windows and points on macOS; the adapter converts, the step does not know.

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (C1: `CloseWindow`, `MinimizeWindow`, `MaximizeOrRestoreWindow`, `ToggleWindowAlwaysOnTop`, `SetWindowSize`, `Center`, and the two snap scripts), the App's `Components/Steps/WindowOp/` form.
