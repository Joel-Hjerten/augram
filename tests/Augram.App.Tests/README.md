# Augram.App.Tests

Avalonia headless tests (`Avalonia.Headless.XUnit`) for components, view models and host services, plus `Architecture/` rules on the source itself: no `.axaml` under `src/Augram.App/Components/` may contain a literal colour (ADR-0002 §5b), and every `XField` declaration has a renderer registered under kind `X` (§5c).

**May reference:** `Augram.App` (and everything it references), Avalonia.Headless.XUnit, xunit.

Use `[AvaloniaFact]` for anything that touches a control; plain `[Fact]` for file-system, view-model and host-service tests. `TestAppBuilder` configures the headless app with the real `CompositionRoot` (logs redirected to a temp folder) and exposes its `Services`. To exercise a component, put it in a `Window`, call `Show()`, and inspect it through `GetVisualDescendants()`; bindings apply synchronously on the test (UI) thread. `Support/` holds the fakes (`FakeOptions`, `FakeClipboard`, `FakeRow`).

`Sync/` tests the App side of machine-to-machine sync with no git and no network: `Sync/Support/SyncMachine` is one machine (real stores and coordinator, a temp config folder, a `FakeSyncRepository` on a shared in-memory `SyncRemote`, the store thread on the caller, a `SyncService` over a `ManualSchedule` so the 20 s and 5 min timers fire only when a test says so). A test waits for the worker with `WaitForRuns(n)` and compares counts relative to before (a machine that joined through `SyncDirect` has already pulled once). `FakeSyncJoinPresenter` and `FakeSyncConflictPresenter` answer the dialogs.
