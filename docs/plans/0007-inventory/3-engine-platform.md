## Map (Engine + Platform.Windows + Platform.MacOS)

- **Augram.Engine** is the only project that references SharpHook. `Input/` is the hook side: `SharpHookInputSource` (hook lifecycle + raw-event translation), `SharpHookInputSimulator` (injection), `MouseButtonMap`/`KeyCodeMap` (the only SharpHook numbering), the three *shadows* (`SuppressionShadow` mouse, `KeySuppressionShadow` keys, `HoldRemapShadow` F9) that let the hook thread answer "suppress?" without the machines, `OwnWheelInjections`/`OwnButtonInjections` (telling our injected input from other programs'), `HookHealthMonitor` (install/reinstall/watchdog).
- `Hosting/` is the threads: `InputGate` (everything on the hook thread: volatiles, shadows, one `TryWrite`), `EngineWorker` (+ `.Outcomes`, `.HoldRemaps`, `.ButtonTriggers` partials: the only caller of both Core machines, injects a press's own mouse input and hold-remap outputs), `IgnoreListWatch`/`IgnoreLookup` (a third thread that keeps the pointer's window answer *and* the foreground hold-remap plan current for the hook), `KeyCaptureController` (F5), `StrokeRecognizer`, `EngineHost` (composition point, `EnginePorts`/`EngineHostOptions`).
- `Execution/` is the fourth thread: `CommandExecutor` (queue of 8, drop-oldest) → `CommandRunner` (resolve, lazy activation, settle, steps) with `ExecutionRequest` and `ClickRelay`. `Diagnostics/` is the `IEventLog` implementation.
- **Platform.Windows / Platform.MacOS** implement Core's ports only (`IWindowSystem`, `IWindowOperations`, `IProcessLauncher`, `IDisplayModes`, `IClipboard`, `ISystemEvents`, `ICursorProbe`, `IStartupRegistration`, Windows-only `IOverlayWindowStyle`/`IAppActivator`, macOS-only `ITrailSurface` and the two `IInputSimulator` wrappers). Each is split into a P/Invoke surface (`NativeMethods` / `MacNative`), thin facades (`Win32Windows`, `Ax`/`Cf`/`ObjC`), pure rules tested on any OS, and the public adapter. macOS also holds three App-facing non-port types (`MacStatusItem`, `MacDock`, `MacLaunchEvent`).
- Dependency direction is clean: Engine → Core + SharpHook; Platform.* → Core (+ `Microsoft.Win32.SystemEvents`); no Avalonia or dispatcher anywhere here; the composition root (`App/Hosting/EngineModule`) is the only place that names the adapters, except the three Mac types above.
- The hot path matches the README: `InputGate.Handle` reads volatiles, asks shadows, posts one message, measures itself. Nothing below recommends adding work there.

## Findings (most valuable first)

**1. (1 duplicate) The modifier-flag ↔ key rule lives three times.**
Where: `src/Augram.Core/Steps/Hotkey/HotkeyKeys.cs:18-25` (`ModifierOf`) and `:33-34` (`LeftKeys`); `src/Augram.Engine/Input/KeySuppressionShadow.cs:45-52` (`ModifierOf`, verbatim copy of Core's); `src/Augram.Engine/Input/SharpHookInputSimulator.cs:24-30,173-185` (`ModifierKeys(modifiers, rightHand)`, Core's `LeftKeys` plus a right-hand choice, same Ctrl/Alt/Shift/Meta order). Callers of the Engine copies: `InputGate.cs:305`, `KeySuppressionShadow.cs:67,123`, `ClickRelay.cs:30`, `EngineWorker.HoldRemaps.cs:228,281`, `SharpHookInputSimulator.cs:72,128`.
Wrong: same rule, three homes; a change to key order or a new modifier is a three-place edit.
Recommend: extend `HotkeyKeys` with `Keys(KeyModifiers modifiers, KeyModifiers rightHand)` (`LeftKeys` = `Keys(m, None)`), delete `KeySuppressionShadow.ModifierOf` and `SharpHookInputSimulator.ModifierKeys`, point callers at Core. `ModifierOf` runs on the hook thread (`InputGate.cs:305`, `HeldModifiers`): a switch either way, no cost change. Size S. Risk low (`SharpHookInputSimulatorTests` and `HoldRemapEngineTests` pin the order). Behaviour exact.

**2. (3 ADR) The worker and executor reach into the SharpHook adapter for a platform-neutral rule.**
Where: `src/Augram.Engine/Execution/ClickRelay.cs:3,30`; `src/Augram.Engine/Hosting/EngineWorker.HoldRemaps.cs:9,228,281` call the static `SharpHookInputSimulator.ModifierKeys`.
Wrong: code that only talks to `IInputSimulator` depends on one concrete adapter's static; on macOS the simulator in use is `MacRemapButtonSimulator`, so the dependency is also misleading.
Recommend: falls out of finding 1 (callers use `HotkeyKeys`). Size S. Risk low. Exact.

**3. (1 duplicate) "Modifiers down, do X, modifiers up in reverse, keep the worst result" is written five times; `Worst` four times.**
Where: `SharpHookInputSimulator.cs:70-86` (`PressRemapButton`), `:126-143` (`Hotkey`), `:236` (`Worst`); `ClickRelay.cs:22-50,52`; `EngineWorker.HoldRemaps.cs:157-184` (`PressOutput`/`ReleaseOutput` key case), `:226-232` (`TurnWheel`), `:258-278` (`PressKeys`/`ReleaseKeys`), `:283` (`Worst`); `src/Augram.Platform.MacOS/Input/MacRemapButtonSimulator.cs:162` (`Worst`). (`MacRemapButtonEvents.Modifiers` is the CoreGraphics flagsChanged variant: a different mechanism, leave it.)
Recommend: one `SimulationResult.Worst(a, b)` in Core `Abstractions/SimulationResult.cs`; one Core extension `InputSimulation.WithModifiersHeld(IInputSimulator, IReadOnlyList<KeyCode>, Func<SimulationResult>)` used by `ClickRelay.Replay`, `TurnWheel`, `Hotkey`, `PressRemapButton`. The hold-remap *key output* holds its modifiers across two outcomes (down in `PressOutput`, up in `ReleaseOutput`), so only its `PressKeys`/`ReleaseKeys` halves fold, not the bracket. Size M. Risk low–medium: injection order is behaviour (Blender reads the modifiers at the down); the existing tests pin it. Exact if order is kept.

**4. (1/5) Log source names have no single home, and the Diagnostics filter cannot select four engine sources.**
Where: `src/Augram.Engine/Hosting/LogSources.cs` (hook, capture, recognition, engine, exec, ignore, hold); literal `"window"` in `Platform.Windows/WindowSystem/ForegroundActivator.cs:16` and `Platform.MacOS/WindowSystem/MacWindowOperations.cs:29`; literal `"steps"` in `Platform.Windows/Launch/Win32ProcessLauncher.cs:105,109`; `"training"` in `App/Training/TrainingSession.cs`; `"sync"` in Core `SyncCoordinator.cs:21`; the App's filter list `src/Augram.App/ViewModels/LogViewModel.cs:41` names `activate` and `steps` but not `exec`, `engine`, `ignore`, `hold`, `window` (and nothing emits `activate`).
Recommend: move `LogSources` to `Core/Diagnostics` (its own doc comment already says the contract is Core's) with every source the solution emits; Platform and App read it; the App's dropdown is built from it. Size S. Risk low. Exact for the engine; fixes a UI gap (cross-area: the App reviewer should see this).

**5. (1 duplicate) One activation logs "Window activated" / "Activation failed" twice, from two projects.**
Where: `src/Augram.Platform.Windows/WindowSystem/ForegroundActivator.cs:46,54` (source `window`) and `src/Augram.Engine/Execution/CommandRunner.cs:155,159` (source `exec`). Both READMEs document their own line; both fire per command. macOS's `MacWindowSystem.Activate` logs nothing, so the `window` line is Windows-only.
Recommend: keep the adapter's per-technique *attempt* lines (the B1 tally) and one success/failure line. The executor's carries `focusMoved` and is platform-neutral, so drop the adapter's final two Info lines (or make them Debug). Size S. Risk none. Changes log text only. Question for Joel below.

**6. (1 duplicate) Three hand-rolled "runtime-registered NSObject subclass + static `ConcurrentDictionary<nint, instance>` + `UnmanagedCallersOnly` trampoline that swallows exceptions" in Platform.MacOS.**
Where: `MacStatusItem.cs:39-41,128-177`; `Input/MacSystemEvents.cs:26-27,103-143`; `Startup/MacLaunchEvent.cs:25-26,139-176` (`Platform.Windows/Input/Win32SystemEvents.cs:24,86-105` is the Win32 twin, by hook handle; leave it). They differ in one detail: `MacStatusItem.RegisterTargetClass` (`:130`) does not handle `objc_allocateClassPair` returning 0 (class already registered), the other two do (`MacSystemEvents.cs:112-116`, `MacLaunchEvent.cs:148-152`).
Recommend: one `Interop/ObjCTarget` (register a class once with N selector→callback pairs, instance by `self`, the try/catch wrapper); the three become a few lines each. Size M. Risk medium: native callbacks, nothing testable headless beyond compiling; `MacSystemEvents` and `MacLaunchEvent` are "compiled, not yet run on a Mac". Do it after the Mac session has verified them, not before. Exact.

**7. (1 duplicate) `OwnWheelInjections` and `OwnButtonInjections` are the same expire-and-claim ledger.**
Where: `src/Augram.Engine/Input/OwnWheelInjections.cs:13-71` (one counter), `OwnButtonInjections.cs:16-83` (one per button, plus `Withdraw`). `MacRemapButtonSimulator` takes `Action<MouseButton> announceRelease` (`EngineModule.cs:206`) because Platform may not reference Engine: fine, keep that seam.
Recommend: one `OwnInjections` keyed by a slot enum (Wheel, Left…X2) with `Expect/Withdraw/TryClaim(slot)`; two registrations become one. Size S–M. Risk low (both tested; the hook-thread claim stays one lock and the same instructions). Exact.

**8. (3/5) "Which platform is this" is answered three ways, and the engine's answer is undocumented.**
Where: the Engine treats `IWindowOperations.Platform` as the host platform (`Hosting/IgnoreListWatch.cs:78`, `Execution/CommandExecutor.cs:47`, `Hosting/EngineWorker.cs:58` `_maskWinAlt`); `IDisplayModes.Platform` answers separately; `Input/SharpHookInputSource.cs:84` and `Input/SharpHookInputSimulator.cs:107` branch on `OperatingSystem.IsMacOS()` directly (SharpHook quirks: `KeyTypedEnabled`, wheel notch).
Wrong: a cold agent asks "where does the engine learn its platform?" and the answer is "from the window-operations port", which no README says; in tests it is whatever the null adapter answers.
Recommend (lightest): a `HostPlatform Platform` on `EnginePorts` defaulting from `WindowOperations.Platform`, the three Engine sites read it, and one README sentence. The two SharpHook `IsMacOS` branches are library quirks and may stay in the adapter, named in the README. Size S. Risk low. Exact.

**9. (2 two jobs) `InputGate` (460 lines) is the mouse decision path plus the hold-remap key decision plus the publication surface.**
Where: `src/Augram.Engine/Hosting/InputGate.cs:83-170` (volatiles/publish), `:183-325` (`Handle`), `:332-459` (`EndHoldIfFocusMoved`, `DecideHoldButton`, `DecideHoldWheel`, `DecideHoldKey`, the densest logic in the file).
Recommend: a partial `InputGate.HoldRemaps.cs` for the four hold methods, exactly as `EngineWorker` is split; no new class (an extra indirection on the hot path, and the `Save/Restore` invariants would straddle two objects). `Handle` then reads in one screen. Size S. Risk none. Exact.

**10. (2 two jobs) `EngineWorker.HoldRemaps.cs` (412 lines) is acting plus ~120 lines of logging/cross-checking.**
Where: `:47-283` act; `:291-411` `CrossCheckHold`, `LogHold`, `KeyName`, `CommandName`, `Describe`.
Recommend: `EngineWorker.HoldRemaps.Log.cs` partial for the second half, if the 300-line rule is to be kept; otherwise leave. Size S. Risk none. Exact.

**11. (1/2) `SharpHookInputSource` holds the hook lifecycle and a twice-written rate-limited "ignored input" log.**
Where: `src/Augram.Engine/Input/SharpHookInputSource.cs:47-51` (two slot/timestamp tables), `:240-262` (`LogIgnoredButton`) and `:290-311` (`LogIgnoredWheel`): the same "once per 30 s per slot" throttle twice; `:73-194` lifecycle; `:196-325` translation (the hot path).
Recommend: one `IgnoredInputLog` (hook-thread-only, same arrays, one `Due(slot)` throttle) next to the source; `OnButton`/`OnWheel` then read as decide-translate-call. Size S. Risk low (same instructions on the hook thread, one more field read). Exact.

**12. (1 duplicate) Command-by-id and binding-by-id lookups are hand-rolled where Core has them.**
Where: `src/Augram.Engine/Execution/CommandExecutor.cs:181-193` scans `mapping.Groups`/`Commands` for a `CommandId`; Core already has `MappingDocument.AllCommands()` (`Mapping/MappingDocument.cs:20-29`) and `MappingStore.FindCommand`. `EngineWorker.HoldRemaps.cs:237-245` (`RunSteps`) and `:388-399` (`CommandName`) both scan `entry.Bindings` for a `CommandId`.
Recommend: `MappingDocument.FindCommand(CommandId)` in Core used by the executor; `HoldRemapEntry.FindBinding(CommandId)` in Core used twice. Size S. Risk none. Exact.

**13. (1 duplicate) macOS display enumeration is written twice.**
Where: `src/Augram.Platform.MacOS/WindowSystem/MacWindowList.cs:25-41` (`Displays`) and `Display/MacDisplayList.cs:23-46` (`Read`): the same `CGGetActiveDisplayList`/`CGDisplayBounds` loop with the same `MaxDisplays = 16` stackalloc.
Recommend: `MacDisplayList.Read` builds on `MacWindowList.Displays` (or the bounds reader moves to `Display/` and `MacWindowList` calls it). Size S. Risk low. Exact.

**14. (1 duplicate) The macOS pointer position is read two ways.**
Where: `Input/MacCursorProbe.cs:11-32` and `Input/MacRemapButtonSimulator.cs:135-153` (`TryPointer`): both create a blank `CGEvent` and read its location.
Recommend: one `Interop/MacPointer.TryRead(out CGPoint)`; both call it. Size S. Risk low. Exact.

**15. (1 duplicate) Root-owner-or-self fallback three times in Platform.Windows.**
Where: `WindowSystem/Win32WindowSystem.cs:57` (`OrSelf`), `WindowSystem/ForegroundActivator.cs:111-120` (`Root`), `WindowSystem/WindowIdentityReader.cs:124` (`Or`).
Recommend: one static in `WindowSystem/` (or a default on `IWin32Windows`). Size S. Risk none. Exact.

**16. (4 dead) Members with no caller outside their file or the tests.**
Where: `Engine/Input/SharpHookInputSource.cs:71` `GenerationCount`; `Platform.Windows/Input/Win32SystemEvents.cs:46` `WatchesForeground`; `Platform.MacOS/Input/MacSystemEvents.cs:42` `IsObserving`; `Platform.MacOS/Interop/ObjC.cs:39` `RespondsTo`; `Platform.MacOS/Interop/Ax.cs:28` `ZoomButtonAttribute` (only a doc comment names it; the README says never `AXZoomButton`). Test-only: `Engine/Input/KeySuppressionShadow.cs:91` (2-arg `Decide`), `:37-39` (`OwedCount`, `IsOwed`, documented as for tests), `Platform.MacOS/Startup/MacLoginItems.cs:66` (`Code`).
Recommend: delete the first five; keep the test-only ones (or make them `internal`). Alternative for `WatchesForeground`/`IsObserving`: log them once at engine start (question for Joel). Size S. Risk none. Exact.

**17. (3/5) The App names Platform.MacOS types directly, against the Platform READMEs' "nothing above the ports may know this project exists except the composition root".**
Where: `src/Augram.App/Tray/MacTrayHost.cs`, `Tray/TrayMenuEntries.cs` (`MacStatusItem`, `MacMenuEntry`), `Views/MacDockPresence.cs` (`MacDock`), `App.Launch.cs` (`MacLaunchEvent`, `MacDock`). Each is documented in the App README as deliberate; none has a Core port.
Recommend (lightest): amend the two Platform READMEs and ADR-0002 §2 to list the three as the documented exceptions (App-only, no Windows counterpart). Ports only if a Windows equivalent ever appears. Size S (docs). Risk none. Question for Joel.

**18. (5 map gap) `IgnoreListWatch` / `IgnoreLookup` are the engine's whole "window under the pointer and app in front" service, not just the ignore list.**
Where: `src/Augram.Engine/Hosting/IgnoreLookup.cs:111-173` computes the ignore bits, the anchor plan + drag distances + button outputs per window, and the foreground `HoldRemapPlan`; the README's Hosting row and the "Ignore list" section carry all of it under that name.
Recommend: not a split (the one deduplicated pass is the point). Either rename to `PointerWatch`/`WindowLookup` or, cheaper, retitle the README section ("The watch: ignore list, anchors per window, foreground hold remaps") and say in the `IgnoreListWatch` doc comment that its `_over`/`_pausedBy` are the cross-thread published copies while `IgnoreLookup.Over`/`PausedBy` are thread-private working state (today both exist and nothing says why). Size S. Risk none.

**19. (5 map gap) README rows a cold agent needs are missing.**
Engine README: no row for `EnginePorts` (the single most useful type for "what does the engine need from outside"), `EngineWorker.ButtonTriggers.cs`, `RecognitionResult`, nor for the two `IsMacOS` branches (finding 8). `HoldRemapShadow.DecideKey` (`Input/HoldRemapShadow.cs:208-307`) is a 100-line method with seven exits that mirrors the Core machine's table; the README's prose bullets for the hook side should be a table in the same row order as the method, so an agent can line them up (leave the method as is: row-by-row mirroring is what makes it checkable). macOS README: `Interop/MainThread` (2 s withdraw, inline on main) and `Ax.IsOwn` (`Interop/Ax.cs:216-249`: own elements hop to the main thread) are not in the "What is here" table although "Threading and permissions" depends on them. Size S. Risk none.

**Checked and clean (no finding):** dependency direction (csprojs); no Avalonia/dispatcher in Engine or Platform; `KeyCodeMap`/`MouseButtonMap` single; `Worst` absent from Core; `StrokeRecognizer.Recognize` vs App `TrainingSession.Rank` (`App/Training/TrainingSession.cs:177-183`) ask different questions (fired above threshold vs best match regardless), not a duplicate rule; the two `ISystemEvents` listeners (`HookHealthMonitor.cs:267-281`, `IgnoreListWatch.cs:302-315`) do different jobs as the thread table says; `ClickRelay.Replay` static reuse by the worker's `ReplayClick` is intended; the executor knowing `HoldCommand` is plan 0002 decision 8.

## Questions for Joel

1. Finding 5: which "Window activated" line do you read in the log, the adapter's (`window`, technique only, Windows only) or the executor's (`exec`, with `focusMoved`, both platforms)? One should go.
2. Finding 17: list `MacStatusItem`, `MacDock`, `MacLaunchEvent` as documented exceptions to "only the composition root knows Platform.*", or add ports for them?
3. Finding 6: agree to consolidate the three macOS observer trampolines only after the Mac session has verified `MacSystemEvents` and `MacLaunchEvent` live?
4. Finding 16: delete `Win32SystemEvents.WatchesForeground` / `MacSystemEvents.IsObserving`, or log them once at engine start (it would answer the Mac README's "does the plan arrive only at the 200 ms poll" check)?
5. Finding 4 touches the App's log filter list; should that go to the App reviewer's plan or stay with this area's `LogSources` move?