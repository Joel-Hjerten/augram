# Augram.TestSupport

One fake per `Augram.Core` port, shared by the test projects (plan 0007 S16; ADR-0002 §1). A class library, not a test project: it references `Augram.Core` only, holds no tests and no xunit (a fake records and answers; the test asserts). `Augram.Core.Tests`, `Augram.Engine.Tests`, `Augram.App.Tests` and `Augram.Platform.Windows.Tests` reference it with a global `using Augram.TestSupport`.

**Rules.** A test needing a Core port takes the fake here; it does not write its own, not even a private nested one. When a test needs behaviour the fake lacks, add it here as an option whose default keeps every other caller's behaviour, and list it below. Fakes of the App's own ports (presenters, `IClipboardText`, `IKeyCapture`…) stay in `tests/Augram.App.Tests`, fakes of a platform's internal facades (`FakeWin32`, `FakeStartupRegistry`) in that platform's tests. Nothing under `src/` references this project: the dev gallery (`src/Augram.App/DevGallery`, Debug only) keeps its own instant-answer stand-ins.

Thread-safe unless the table says otherwise: the engine worker, the executor and the test thread share them.

| Port | Fake | Records | Options |
|---|---|---|---|
| `IInputSimulator` | `FakeInputSimulator` | `All` (every injection in order), `Keys` (key, hotkey and text entries), `Mouse` (button, move, wheel, drag entries), `Clicks` (button and position). Entries: "press VolumeUp", "release VolumeUp", "hotkey Control+W", "hotkey Alt+F9 right Alt", "text hi", "keys hi", "click Right@10,20", "down Right@10,20", "up Right", "down Shift+Middle@10,20" (a hold remap's output), "drag Middle@15,20 by 5,0", "move 50,20", "scroll Down x1@10,20" | ctor `repostsRemapDrags` (false); `PressResult`, `ReleaseResult` (key press and release), `OtherResult` (everything else), all `Success`; `OnInjection` (called with each entry after recording: cancel a command, throw like a broken library); `BlockOn` + `Unblock()` (hold the injecting thread on one entry) |
| `IInputSource` | `FakeInputSource` | `StartCount`, `StopCount`, `IsRunning` | `Deliver(input)` runs the handler on the calling thread and returns the hook decision; `ReportLost()` fakes the OS killing the hook; `FailNextStart` |
| `IWindowSystem` | `FakeWindowSystem` | `Lookups` (points asked), `KeyLookups`, `ForegroundLookups`, `ForegroundKeyLookups`, `Activations` (with a Stopwatch timestamp) | `Window` (under any point) or `Around(x, y, window, size)` (windows as screen rectangles, front to back, before `Window`); `ForegroundWindow`; `CheapKeys` (true: keys are the handles, 0 for none); `ActivationResult` (`NotNeeded`). Factories `Identity(...)` (a Notepad window), `WindowOf(process, ...)`, `OwnWindow()` |
| `IWindowOperations` | `FakeWindowOperations` | `Calls` (operation, window, size) | `Platform` (Windows), `Unsupported`, `Result` (Ok) |
| `IProcessLauncher` | `FakeProcessLauncher` | `Launches` | `Result` (Started) |
| `IClipboard` | `FakeClipboard` | `Clears`, `ClearedOn` (thread ids); not thread-safe | `Answer` (Ok) |
| `IDisplayModes` | `FakeDisplayModes` | `Calls` ("mode \\.\DISPLAY1 1920×1080 at 24 Hz", "hdr \\.\DISPLAY1 on"); not thread-safe | `Listed`, `Platform`, `CanSwitchHdr` (true), `Result` (Ok), `ThrowOnChange` (a form test); factories `Tv(...)`, `Mode(w, h, hz)` |
| `IAppActivator` | `FakeAppActivator` | `Asked`; not thread-safe | ctor answer |
| `IAppWindow` | `FakeAppWindow` | `Opened`; not thread-safe | `Opens` |
| `IClock` | `FakeClock` | | `UtcNow`, `MonotonicMs`, `Advance(ms)`; `StepPerRead` (moves `UtcNow` on before each read); `FakeClock.Stepping()` (one second per read from 2026-10-07 09:00, the sync tests' machines) |
| `ICursorProbe` | `FakeCursorProbe` | | `X`, `Y`, `Available` |
| `ISystemEvents` | `FakeSystemEvents` | | `Raise(kind)`, `ReportsForeground` |
| `IStrokeTrail` | `RecordingTrail` | `Calls` ("begin", "extend", "end"), `BeginCount`, `EndCount` | |
| `ITrailSurface` | `FakeTrailSurface` | `Calls` in order, `IsOrderedIn`, `IsParked`, `Points`, `Style`, `IsDisposed`; not thread-safe | ctor and `Report`, `Area` |
| `IOverlayWindowStyle` | `FakeOverlayStyle` | `ApplyCount`, `LastHandle`, `Placed`; not thread-safe | ctor and `Report` |
| `IStartupRegistration` | `RecordingStartupRegistration` | `Writes`; not thread-safe | ctor status, `AfterRegister`, `WriteFailure`, `ReadFailure` |
| `IEventLog` | `ListEventLog` | `Events`, `Last`, `IsEnabledCalls`, `LogCalls`; `Has`, `Single`, `From(source)`, static `Property(e, key)` | ctor and settable `MinimumLevel` (Trace); `WaitForAny(timeout)` for a write from another thread |
| `ISyncRepository` | `FakeSyncRepository` over a `SyncRemote` | `PreparedUrls`, `Pulls`, `MaxConcurrent`; the remote's `Files` (live, a test may write one by hand) and `Messages` | ctor `folder` ("memory://clone"); `FailWith` (prepare and pull), `FailPublishWith`; `HoldPull()` + `Release()` |
| `IStepType` / `IStep` | `FakeStepType`, `FakeStep` | `Runs` (a Stopwatch timestamp per execute) | `Instance` + `Registry` (System, not platform-neutral: the mapping and sync tests; its steps are `new FakeStep("text")`); `new FakeStepType(category, isPlatformNeutral = true)` with `Step` (the executor tests, one instance per step), `Result`, `Throws`, `Converts`. Parameters `{ "text": ... }`; a non-string text is a `StepFormatException` |
