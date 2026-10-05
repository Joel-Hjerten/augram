# Augram.Platform.Windows

Windows implementations of the Core ports that need the OS: `IWindowSystem` (this step, M1 step 5), later `IWindowOperations`, `IProcessLauncher` where Engine cannot, and the layered-window overlay fallback if the Avalonia overlay fails the B2 decision.

**May reference:** `Augram.Core` only. Win32 goes through `P/Invoke` in `Interop/NativeMethods.cs` and nowhere else; types that touch it carry `[SupportedOSPlatform("windows")]`. The assembly has `[DisableRuntimeMarshalling]` so `LibraryImport` generates direct calls (blittable structs and `Span<char>` buffers only).

**Must never contain:** Avalonia, SharpHook, business rules, or anything Core could do without the OS. Nothing above the ports may know this project exists except the composition root, which registers `Win32WindowSystem` as `IWindowSystem`.

## Layout

| Folder | Holds |
|---|---|
| `Interop/` | `NativeMethods` (the P/Invoke surface, no logic) and `Win32Windows` (the real `IWin32Windows` + `IWin32Foreground`, thin wrappers) |
| `WindowSystem/` | `Win32WindowSystem` (public adapter) and everything it is made of: `WindowIdentityReader`, `ForegroundActivator`, and the pure rules `ActivationPolicy`, `DesktopRule`, `FullScreenRule`, `UwpHostRule` |

The two facade interfaces exist so the rules and the reader are unit-tested against a scripted window tree (`tests/Augram.Platform.Windows.Tests`); one smoke test touches real Win32 at the cursor.

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
