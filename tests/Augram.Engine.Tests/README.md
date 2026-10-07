# Augram.Engine.Tests

xunit tests for `Augram.Engine`. No real global hook is ever installed (CI runners have no interactive desktop): everything is driven through the Core ports with the fakes in `Fakes/`. The two SharpHook adapters (`SharpHookInputSource`, `SharpHookInputSimulator`) are deliberately thin and untested; the maps they rely on are.

**May reference:** `Augram.Engine` (and `Augram.Core` through it), xunit. `Augram.Engine` exposes its internals to this assembly.

| Folder | Covers |
|---|---|
| `Fakes/` | `FakeInputSource` (delivers raw input on the test thread and returns the hook decision; `ReportLost` fakes the OS killing the hook), `FakeInputSimulator` (records clicks and keys), `FakeClock`, `FakeCursorProbe`, `FakeSystemEvents`, `RecordingTrail`, `ListEventLog` (synchronous, for assertions) |
| `Input/SuppressionShadowTests` | the hook-side decision equals the machine's Suppress/PassThrough for every state and event kind, over 2,000 random sequences with button changes, disabled presses, ignore key and hold-still cancels |
| `Input/HookHealthMonitorTests` | reinstall on the source's loss signal and on the cursor watchdog, quiet while events flow, retry with backoff when a reinstall throws, system events, health contributor; `Poll()` is called directly, no timer waits |
| `Input/KeySuppressionShadowTests` | hotkey capture's per-press rule: a press is suppressed iff capture is armed when it starts, its repeats and release follow it across the capture's end; a stale record (missed release) starts fresh |
| `Input/KeyCodeMapTests` | every `Core.KeyCode` and `MouseButton` has a SharpHook counterpart and round-trips; the US layout table |
| `Hosting/EngineHostTests`, `EngineHostControlTests` | end to end through `EngineHarness` (a real `EngineHost` over fakes with a real worker thread): stroke → recognition log entry, Info line and `GestureRecognized`; no match reasons; motionless press → one replayed click from the worker; wheel while held → suppressed and raised per tick; `Enabled` false passes through and still consumes an owed release; ignore key; other-button and hold-still cancels; stroke-button and threshold changes applied in order; hook reinstall resets the capture; a random session produces no decision mismatches |
| `Hosting/KeyCaptureTests` | `EngineHost.CaptureKeys` (F5): keys swallowed and reported only while armed, owed releases swallowed after release, keys held from before pass, the mouse untouched, release by dispose, watchdog, replacement, hook reset and stop, each reported with its reason |
| `Diagnostics/` | `ChannelEventLog`, sinks and the line formatter (M1 step 4a) |

Assertions on worker results wait with `EngineHarness.WaitFor` (spin with a 5 s cap), never with sleeps. The one sleep is in `KeyCaptureTests`, pacing key events against the real watchdog timer (100 ms steps against a 1 s timeout). Test parallelization is off for this assembly (`AssemblyInfo.cs`): the Diagnostics timing tests and the hosting tests starve each other on xunit's worker threads when run together.
