# Hosting

Process-level services and the composition modules: what runs for the life of the process, outside any window. `CompositionRoot.cs` (one folder up) calls the modules; nothing here is a view or a view model. The long story of each part is in the App README ("Engine slice", "Tray, version and channel, single instance", "Start at login"); this page says what lives here and which type to reach for.

## What lives here

| Group | Types |
|---|---|
| Composition modules | `EngineModule` + `EngineModuleOptions`, `GesturesModule`, `CommandsModule`, `IgnoredModule`, `TransferModule`, `SyncModule` + `SyncModuleOptions` (sync's runtime is in `Sync/`). A module only registers services and, in its `Start`, resolves and starts them; it does not subscribe to stores itself. |
| Threads and timers | `UiThread`, `OneShotTimer`, `SettingsFollower<T>` (below). |
| Engine links | `EngineSettingsLink` (settings and mapping changes into the running engine), `EnginePauseState` (the ignore list's pause, for the tray), `StrokeButtonDetection` (detect-to-assign for the stroke button), `MainWindowOpener` (the Open app step's "This app"), `EngineKillSwitch` (`--no-engine`). |
| Process identity and launch | `AppInfo` + `AppChannel`, `AppPaths`, `LaunchVisibility`. |
| Start at login | `AppState` (tray flags over the settings store), `StartupPolicy` + `StartupAction`. |
| Single instance | `SingleInstanceGuard`, `InstanceStartup`, `InstanceStartupStep`, `InstanceIdentity`, `InstanceRequest`, `InstanceRequestEventArgs`, `PeerAnswer`, `InstanceProtocol`, `NamedMutexOwner`. |
| Small services | `IClipboardText`/`AppClipboard`, `AppHealthContributor`. |

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Hand work to the UI thread from any thread | `UiThread.Post` | The one marshal: `CompositionRoot` passes it to `EngineModuleOptions.Marshal`, `SyncModuleOptions.Marshal` and `EngineButtonCapture`, and every class taking an `Action<Action>` marshal defaults to it. Never write `Dispatcher.UIThread.Post` for this. |
| React to a store change that may come from a worker | `UiThread.RunOrPost` | Runs at once on the UI thread (edits, headless tests), posts otherwise. View models get it through `ViewModels/StoreViewModel.OnStoreChanged`. |
| Wait for the UI thread from a worker | `UiThread.IsCurrent` + `UiThread.Invoke(function, cancel)` | Only `Sync/SyncStoreThread` does this. |
| Run something once after a delay (debounce, timeout, poll) | `OneShotTimer.Schedule(delay, action)` | A `System.Threading.Timer` per request, the action through the timer's marshal (none: the timer thread). Dispose the result to cancel; restart = dispose and schedule again. A request disposed after its timer fired but before the marshalled action ran is skipped. Users: the config save (250 ms, marshalled to the UI thread), sync's poll and change timers (no marshal), stroke-button detection's 5 s timeout. |
| Follow part of the settings and push what moved | `SettingsFollower<T>(settings, select, apply)` | Remembers the last selected value; on a change that leaves it different, `apply(last, next)`. Dispose to unsubscribe. Users: `EngineSettingsLink` (whole `Settings`), `AppState` (`GeneralSettings`), `CommandsViewModel` (stroke button, drag distance). |
| Animation or UI-only timing inside a control | `DispatcherTimer` / `Dispatcher.UIThread.Post` with a priority | Components keep these (a focus after layout, a render frame); they are not marshals. |

## Rules

- Tests never get the real UI marshal by accident: a test that builds a module passes `Marshal = action => action()` (or a recording one); view-model tests run on the headless UI thread, where `RunOrPost` runs at once.
- `ConfigSession` stays single-writer: its save timer's marshal is the UI thread's.
