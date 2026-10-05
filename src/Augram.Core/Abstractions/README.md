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
| `IInputSimulator` | `Click`, `KeyPress`, `KeyRelease`, `Hotkey`, `TypeText`, `TypeTextByKeys` | Engine `SharpHookInputSimulator` | Returns `SimulationResult`. Keys are `KeyCode`, Core's platform-neutral enum (letters, digits, F1–F24, navigation, modifiers left/right, Win/Cmd as Meta, media, browser). Only the engine worker calls it (A19). |
| `IStrokeTrail` | `Begin`, `Extend`, `End` | App overlay; `NullStrokeTrail` in Core | ADR-0002's `IOverlay`. Called on the engine worker; must return at once. |
| `ICursorProbe` | `TryGetPosition` | Platform.* | For the hook watchdog; optional. |
| `ISystemEvents` | `Occurred` (`SystemEventKind`) | Platform.* | Session, power, display; optional. |
