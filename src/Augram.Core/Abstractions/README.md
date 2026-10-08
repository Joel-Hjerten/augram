# Core/Abstractions

The ports (ADR-0002 §2): `IInputSource`, `IInputSimulator`, `IWindowSystem`, `IWindowOperations`, `IProcessLauncher`, `IOverlay`, `IClock`, `IConfigStore`, plus the two diagnostics ports below. Small interfaces, 1 to 5 members each, implemented by Engine, Platform.* and App. Core owns the contract; it never sees an implementation.

## Present

| Port | Members | Implemented by | Notes |
|---|---|---|---|
| `IEventLog` | `IsEnabled(level)`, `Log(event)` | Engine `ChannelEventLog`; `NullEventLog` in Core for tests | N4. `Log` must never block the caller. Use the extension methods in `Diagnostics/EventLogExtensions` rather than building a `LogEvent` by hand; they skip the allocation when the level is off. |
| `IHealthSource` | `Current()` | Core `HealthRegistry` | N4 health summary. Read on Diagnostics tab refresh only. |

The rest land with their first consumer.

## Engine ports (M1 step 4)

| Port | Members | Implemented by | Notes |
|---|---|---|---|
| `IClock` | `UtcNow`, `MonotonicMs` | Core `SystemClock`; `FakeClock` in tests | `MonotonicMs` stamps every `CaptureEvent`. |
| `IInputSource` | `IsRunning`, `Start(handler)`, `Stop()`, `HookHealthChanged` | Engine `SharpHookInputSource`; `FakeInputSource` in tests | Delivers `RawInput` (a struct; kinds in `RawInputKind`; modifiers as `KeyModifiers`, same bits as `Config.IgnoreKeys`) to an `InputHandler` on the source's own thread and applies its bool as the suppress decision. `HookHealth` (`Installed`, `Stopped`, `Lost`) may be raised on the hook thread. |
| `IInputSimulator` | `Click`, `KeyPress`, `KeyRelease`, `Hotkey(modifiers, key, rightHand)`, `TypeText`, `TypeTextByKeys` | Engine `SharpHookInputSimulator` | Returns `SimulationResult`. Keys are `KeyCode`, Core's platform-neutral enum (letters, digits, F1–F24, navigation, modifiers left/right, Win/Cmd as Meta, media, browser). `Hotkey` presses the left modifier keys, and the right-hand key (RightAlt, RightControl…) for each modifier in `rightHand` (default none; bits outside `modifiers` ignored), so "RAlt+F9" sends RightAlt. Only the engine worker calls it (A19). |
| `IStrokeTrail` | `Begin`, `Extend`, `End` | App overlay; `NullStrokeTrail` in Core | ADR-0002's `IOverlay`. Called on the engine worker; must return at once. |
| `ICursorProbe` | `TryGetPosition` | Platform.Windows `Win32CursorProbe`, Platform.MacOS `MacCursorProbe` | For the hook watchdog; optional. |
| `ISystemEvents` | `Occurred` (`SystemEventKind`) | Platform.* | Session, power, display; optional. |
| `IOverlayWindowStyle` | `Apply(handle)` → `OverlayStyleReport` | Platform.Windows `OverlayWindowStyle`, Platform.MacOS `MacOverlayWindowStyle` (`ignoresMouseEvents`); `NullOverlayWindowStyle` default | Applies the click-through / no-activate / tool-window styles the toolkit drops on `Show()` (learnings 0001 B2) and reads them back; the App shows the overlay only when the report says click-through. The handle is opaque to Core. |
| `IStartupRegistration` | `IsEnabled`, `Set(bool)` | Platform.Windows `RunKeyStartupRegistration`; `NullStartupRegistration` for tests and platforms without one | F7 start at login as the OS sees it; `Config.GeneralSettings.StartAtLogin` is the user's choice and the App keeps them in step. |
| `IWindowOperations` | `Platform`, `Supports(op)`, `Perform(op, window, size?)` | Platform.Windows `Win32WindowOperations`, Platform.MacOS `MacWindowOperations` (close, minimize, maximize/restore so far); `NullWindowOperations` default | M2. `WindowOperation` is the semantic list (close, minimize, maximize/restore, always-on-top, center, set size, snap halves); the adapter maps each to its OS and says which it lacks, so a step is authored once for both platforms. Windows ↔ macOS table in `Steps/WindowOp/README.md`. Called on the command executor thread only. |
| `IDisplayModes` | `Platform`, `CanSwitchHdr`, `Displays()`, `SetMode(display, mode)`, `SetHdr(display, on)` | Platform.Windows `Win32DisplayModes`, Platform.MacOS `MacDisplayModes` (no HDR); `NullDisplayModes` default | The Display steps (learnings 0002). `DisplayInfo` (id, name, `DisplayBounds` in the hook's coordinates, main flag, current `VideoMode`, offered modes, `HdrState`); `VideoMode` = `DisplayResolution` (pixels on Windows, points on macOS) + `RefreshRate` (millihertz; `FromLegacyHertz` is Windows' whole-hertz rule, `IsNear` the 0.2 % twin test); `DisplayTarget` and `DisplayLookup` pick the display under the gesture start or the main one; results are `DisplayChangeResult`. Read fresh per call, from the command executor or the UI thread (step form); changes only from the executor. |
| `HostPlatform` | enum | | Where a step was authored (F8) and which adapter answered. |
