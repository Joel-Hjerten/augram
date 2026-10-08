# Augram.Platform.Windows

Windows implementations of the Core ports that need the OS: `IWindowSystem` (M1 step 5), `ICursorProbe`, `ISystemEvents`, `IStartupRegistration` and `IOverlayWindowStyle` (M1 final wiring), `IWindowOperations` and `IProcessLauncher` (M2), and later the layered-window overlay fallback if the Avalonia overlay fails the B2 decision.

**May reference:** `Augram.Core` and the `Microsoft.Win32.SystemEvents` package (session, power and display notifications; `Microsoft.Win32.Registry` is in the shared framework). Win32 goes through `P/Invoke` in `Interop/NativeMethods.cs` and nowhere else; types that touch it carry `[SupportedOSPlatform("windows")]`. The assembly has `[DisableRuntimeMarshalling]` so `LibraryImport` generates direct calls (blittable structs and `Span<char>` buffers only).

**Must never contain:** Avalonia, SharpHook, business rules, or anything Core could do without the OS. Nothing above the ports may know this project exists except the composition root, which registers `Win32WindowSystem` as `IWindowSystem`, `Win32WindowOperations` as `IWindowOperations`, `Win32ProcessLauncher` as `IProcessLauncher` and the adapters below through `EngineModule`.

## Layout

| Folder | Holds |
|---|---|
| `Interop/` | `NativeMethods` (the P/Invoke surface, no logic) and `Win32Windows` (the real `IWin32Windows` + `IWin32Foreground` + `IWin32WindowControl`, thin wrappers) |
| `WindowSystem/` | `Win32WindowSystem` (public adapter) and everything it is made of: `WindowIdentityReader`, `ForegroundActivator`, and the pure rules `ActivationPolicy`, `DesktopRule`, `FullScreenRule`, `UwpHostRule`; `Win32WindowOperations` (public adapter for `IWindowOperations`) with the pure `WindowGeometry` |
| `Input/` | `Win32CursorProbe` (`GetCursorPos`, physical pixels, for the hook watchdog) and `Win32SystemEvents` (`SystemEvents.SessionSwitch` / `PowerModeChanged` / `DisplaySettingsChanged` / `SessionEnding` mapped to `SystemEventKind`; the static `Map` functions are the tested part) |
| `Launch/` | `Win32ProcessLauncher` (public adapter for `IProcessLauncher`, the Run step) with the pure `ShellStartInfo` (what ShellExecuteEx is asked for) and `ShellExecuteErrors` (Win32 error → one log line); see "Process launcher" below |
| `Startup/` | `RunKeyStartupRegistration`: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `Augram` = the quoted executable path; `IsEnabled` is "the value exists", `Set(true)` always rewrites the command so toggling repairs a moved exe. Per-user, no elevation |
| `Overlay/` | `OverlayWindowStyle`: ORs `WS_EX_LAYERED` + `WS_EX_TRANSPARENT` (the documented click-through pair for a top-level window; alpha set opaque), `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` into the overlay window, re-asserts topmost without activating, and returns an `OverlayStyleReport` read back from `GWL_EXSTYLE`. Avalonia rewrites the style on every `Show()` (learnings 0001 B2), so the App applies before and after showing and hides the window unless the report says click-through |

The three facade interfaces (`IWin32Windows` queries, `IWin32Foreground` and `IWin32WindowControl` side effects) exist so the rules, the reader and the operations are unit-tested against a scripted window tree (`tests/Augram.Platform.Windows.Tests`); one smoke test touches real Win32 at the cursor, nothing else in the tests creates, moves or closes a real window.

## Threading rule

**Never block, never sleep on the hook thread.** Every `IWindowSystem` call makes several Win32 calls (and `WindowFromPoint` crosses into other processes' windows), `ProcessImagePath` opens a process handle, and `Activate` sleeps while polling. The engine worker calls these; a hook handler only appends a point and returns (invariant 1). `Activate` is bounded: at most 3 techniques × 50 ms of polling ≈ 150 ms worst case, typically one call and 0–5 ms.

## Identity (`WindowAt`, `Foreground`)

`WindowIdentityReader.Read(handle)` builds a `WindowIdentity`:

- `RootHandle` = `GetAncestor(GA_ROOTOWNER)`: the root **owner**, so a find dialog resolves to its editor and A20 compares the right thing (SP.net rule, reference strokesplus-net-config §9).
- `ClassChain` = classes of own → parent (`GetParent`) → root (`GA_ROOT`) → root owner, distinct and non-empty. Matchers test "chain contains X"; the desktop is `SysListView32 › SHELLDLL_DefView › Progman|WorkerW`, Explorer's file view is `DirectUIHWND › … › CabinetWClass`.
- `ProcessName` = file name of `QueryFullProcessImageName` (opened with `PROCESS_QUERY_LIMITED_INFORMATION`, which works for elevated targets); falls back to `Process.ProcessName + ".exe"`, then `"unknown"`. It is never empty. `ProcessPath` is null when even the limited query is denied.
- `Title` is the root owner's caption (null when empty).

**UWP special case.** The exe behind an `ApplicationFrameWindow` root is `ApplicationFrameHost.exe`; the real app owns a `Windows.UI.Core.CoreWindow` child (hidden siblings exist, so the first *visible* one is taken). `UwpHostRule.ProcessWindow` returns that child whenever the root class is the frame, so a point on the title bar names `CalculatorApp.exe`, not the host. A point inside the app already hits the CoreWindow and needs no rewrite. `RootHandle` stays the frame, which is what activation needs.

**Desktop special case** (`DesktopRule`). `IsDesktop` is true when the root owner is `GetShellWindow()` / `GetDesktopWindow()` or any class on the chain is `Progman` or `WorkerW`. The taskbar is *not* the desktop; A20 only excludes the desktop.

**Full-screen rule** (`FullScreenRule`, A21). `GetWindowRect(rootOwner)` equals `GetMonitorInfo(MonitorFromWindow(rootOwner, NEAREST)).rcMonitor` exactly, and the root class is not a desktop class. A maximized window fails (its frame overhangs the monitor), a borderless game passes. `IsFullScreen` is always false for the desktop.

## Activation (`Activate`)

`Win32WindowSystem.Activate` applies **A20** through `ActivationPolicy` before anything else: if `target.IsDesktop` or `target.RootHandle` equals the current foreground's root owner, it returns `ActivationResult.NotNeeded` and touches nothing. That is what keeps popup and find windows working (SP.net 0.3.3.x, [7083]).

Otherwise `ForegroundActivator` tries, in order, each verified by polling `GetForegroundWindow` → root owner every 5 ms for up to 50 ms:

1. `set-foreground`: plain `SetForegroundWindow`. Works when Augram is allowed to (it received the last input, or the foreground process is ours).
2. `attach-thread-input`: `AttachThreadInput(foregroundThread, ourThread, true)` → `SetForegroundWindow` → detach in `finally`. The StrokeIt / GestureSign technique; borrowing the foreground thread's input queue satisfies the foreground-lock check. Skipped when the foreground thread is ours or unknown.
3. `alt-tap`: `SendInput` Alt down/up, then `SetForegroundWindow`. The synthetic key press makes us "the process that last received input", which lifts the lock. Last because it can open a menu bar in the target.

Each attempt is logged at `Info` with source `window` and properties `technique`, `elapsedMs`, `process`; total failure logs a `Warning` with `technique=none`. The result's `Technique` lets the Diagnostics tab tally which one wins per app, which closes checklist B1 during M2 use (N4).

Differences from the Spike2 `ActivateMode` probe: the "attach foreground **and** target threads" variant was dropped (it never adds rights beyond attaching the foreground thread and risks deadlocking against a hung target); the 50 ms fixed sleep became a 5 ms poll so the common case returns in one check; and the own-console skip is gone because A20's foreground-root comparison already covers our own windows.

## Window operations (`Win32WindowOperations`)

The `IWindowOperations` adapter for the F5 window actions (SP.net reference §5 item 6). `Platform` is `Windows` and `Supports` is true for every `WindowOperation`. Every operation targets `WindowIdentity.RootHandle` (the root owner, so a gesture over a find dialog acts on its editor, as SP.net does) and begins with `IsWindow`: a handle that no longer names a window returns `Failed("window gone")` before anything is touched. Coordinates are physical pixels (the app is PerMonitorV2); the work area is `GetMonitorInfo(MonitorFromWindow(root, MONITOR_DEFAULTTONEAREST)).rcWork`, the monitor minus the taskbar.

| Operation | Win32 calls |
|---|---|
| `Close` | `PostMessage(root, WM_SYSCOMMAND, SC_CLOSE, 0)`: what the window's own close button sends, so the app runs its own close handling (save prompts); never `TerminateProcess` |
| `Minimize` | `ShowWindow(root, SW_MINIMIZE)` |
| `MaximizeOrRestore` | `IsZoomed(root)` ? `ShowWindow(root, SW_RESTORE)` : `ShowWindow(root, SW_MAXIMIZE)` |
| `ToggleAlwaysOnTop` | `GetWindowLongPtr(root, GWL_EXSTYLE) & WS_EX_TOPMOST` decides the direction; `SetWindowPos(root, HWND_TOPMOST` or `HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE \| SWP_NOSIZE \| SWP_NOACTIVATE)` |
| `Center` | restore first (below); `GetWindowRect` + work area → `WindowGeometry.Center` (size kept) → `SetWindowPos(root, 0, x, y, w, h, SWP_NOZORDER \| SWP_NOACTIVATE)` |
| `SetSize` | `size` is required (`null` → `Failed("no size")`, a non-positive side fails too); restore first; `WindowGeometry.Resize` keeps the top-left corner and shifts the window back inside when its right or bottom edge would leave the work area; then `SetWindowPos` as for `Center` |
| `SnapLeftHalf` / `SnapRightHalf` | restore first; `WindowGeometry.LeftHalf` / `RightHalf` of the work area (an odd width gives the right half the extra pixel); then `SetWindowPos` as for `Center` |

**Restore first.** The three placement operations call `ShowWindow(root, SW_RESTORE)` when `IsZoomed` or `IsIconic` before reading the rectangle, so a maximized window is placed at its normal size and a minimized one is not placed at −32000. `ShowWindow` and `SetWindowPos` wait for the target's thread to process the messages (they are synchronous, unlike `ShowWindowAsync`), which is what makes restore-then-move safe; the cost is that a hung target blocks the executor for as long as it hangs, so this runs on the executor thread and never on the hook thread.

**Failures.** Nothing throws. `PostMessage` and `SetWindowPos` failures become `Failed("<call> failed (<Win32 error>)")`, for example `SetWindowPos failed (5)`; `ShowWindow` reports no error (its return value is "was previously visible"), so `Minimize` and `MaximizeOrRestore` succeed once the call is made. A window of a higher-integrity (elevated) process refuses `PostMessage` with error 5 (UIPI) and may refuse `SetWindowPos` too; the reason says which call.

`WindowGeometry` is pure (over `ScreenRect`) and holds all the arithmetic. A window larger than the work area cannot fit; it keeps its top-left inside the work area and overhangs the far edge, so the title bar stays reachable.

## Process launcher (`Win32ProcessLauncher`)

The `IProcessLauncher` adapter for the Run step (table of both platforms in `Core/Steps/Run/README.md`). Always `Process.Start` with `UseShellExecute = true` (ShellExecuteEx), so a bare name on the PATH or in App Paths (`explorer`, `mspaint.exe`), a document, a folder and a URI (`ms-settings:display`) work as from Win+R. `ShellStartInfo` expands `%VAR%` in the file and the Start in folder (never in a URI, never in the arguments), uses the user's profile folder when Start in is empty, sets `Verb = "runas"` when elevated and `WindowStyle = Hidden` when hidden, and turns the shell's error dialog off.

**Bounded wait.** ShellExecuteEx blocks for as long as a UAC prompt is open (or a network path takes to answer), so each start runs on its own background STA thread (`augram-process-start`; STA so .NET does not spin up yet another thread for the shell call) and the executor waits at most 2 s. An answer in time is the result; no answer yet is `Started` with the note "still starting after 2 s …", and the thread logs the late outcome (`steps` / `Run started late` at Info, or `Run did not start` at Warning with outcome and reason). The program itself is never waited for; the returned `Process` (null when a running app took the request, a link in an open browser) is disposed at once.

**Answers.** Win32 error 1223 (ERROR_CANCELLED, the declined UAC prompt) → `Cancelled` "the administrator prompt was declined", which the step turns into Skipped, not Failed. Every other Win32 error → `Failed` naming the file and the code: 2 "was not found", 3 "the path … was not found", 5 "access denied", 267 "the Start in folder is not valid", 1155 "no app is associated with it", anything else the system message. Any other exception from the start thread is caught (an escape would end the process) and becomes `Failed`. No reason ever contains the arguments.

Tests drive `Win32ProcessLauncher` through its internal constructor with a fake start delegate, so no test starts a process.

## System events thread

`Microsoft.Win32.SystemEvents` creates its broadcast window on the thread that first touches it when that thread is STA (the App's UI thread, which pumps messages, so `ISystemEvents.Occurred` fires there); otherwise on its own hidden-window thread. Either way a listener must return at once; the hook health monitor only stamps a time and, on resume or unlock, posts a reset to the engine worker.
