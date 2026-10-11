# Hosting

Process-level services and the composition: what runs for the life of the process, outside any window. `CompositionRoot.cs` (one folder up) calls the modules; nothing here is a view or a view model. The long story of each part is in the App README ("Tray, version and channel, single instance", "Start at login", the engine slices); this page says what lives where, which type to reach for, and the order things start and stop in.

## What lives here

| Folder | Types |
|---|---|
| `Modules/` | One module per slice of the composition root, each `Register` (only registers) and, where the slice starts something, `Start` (UI thread, resolves and starts): `ConfigModule` + `ConfigModuleOptions` (the config session and its three stores), `PlatformModule` (the Windows or macOS adapters, or the null objects), `EngineModule` + `EngineModuleOptions` (input source and simulator, overlay, `EngineHost`, the engine links and captures, `MainWindowOpener`), `DialogsModule` (the confirmation, the take-over's confirmation, the form dialog: registered once for every slice), the tabs' `GesturesModule`, `CommandsModule`, `IgnoredModule`, `OptionsModule`, `DiagnosticsModule` (each with its tab's `NavEntry`), `TransferModule`, `SyncModule` + `SyncModuleOptions` (sync's runtime is in `Sync/`), and `FormServices` (publishes the `Components/ComponentPorts` resource). A module never re-registers another slice's store or presenter; it resolves it. |
| `Launch/` | What this launch is: `LaunchArguments` (the switches, parsed once in `Program.Main`), `AppInfo` + `AppChannel` (version, commit, Dev or Release), `AppPaths` (config folder, logs), `EngineKillSwitch` (`--no-engine`, `AUGRAM_NO_ENGINE=1`), `LaunchVisibility` (a hidden launch). |
| `Startup/` | Start at login (F7): `StartAtLoginLink` (keeps the OS registration in step with the setting, owns its note), `AppState` (the tray's and Options' flags projected over the settings and the link), `StartupPolicy` + `StartupAction` (who wins). |
| `Instance/` | One Augram at a time (A10): `InstanceListener` (the running side: mutex and pipe, its events), `InstanceClient` (the asking side: `Send`, `SignalExisting`), `InstancePipe` (the names and options both agree on), `InstanceStartup` + `InstanceStartupStep` (the decision at launch, the only caller of both), `InstanceIdentity`, `InstanceRequest`, `InstanceRequestEventArgs`, `PeerAnswer`, `InstanceProtocol` (the wire format), `NamedMutexOwner`. |
| (top) | Engine links: `EngineSettingsLink` (settings and mapping changes into the running engine), `EnginePauseState` (the ignore list's pause, for the tray), `StrokeButtonDetection` (detect-to-assign for the stroke button), `EngineKeyCapture` and `EngineButtonCapture` (the engine's captures as the components' `IKeyCapture` and `IButtonCapture`). The window: `MainWindowFactory` (the main window with its look, for the app and `tools/UiShots`), `MainWindowOpener` (show the window; the Open app step's "This app"). Threads and timers: `UiThread`, `OneShotTimer`, `SettingsFollower<T>`. Process-wide behaviours: `ClickAwayFocus`, `MacDockPresence`. Small services: `IClipboardText`/`AppClipboard`, `AppHealthContributor`. |

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Read a launch switch | `LaunchArguments` (registered; `LaunchArguments.Current` for a static reader) | Parsed once in `Program.Main`. `AppPaths.ConfigFolder` and `EngineKillSwitch.IsSet()` are one-line readers of it. A new switch is a property and a line in `Parse`, with its rule beside the type that owns its meaning. |
| Give a component built without services a port | `Components/ComponentPorts` via `FormServices.Publish` | One application resource (`ComponentPorts.ResourceKey`): display modes and window system always, the key capture and button capture only while the engine runs. A component reads `ComponentPorts.Near(this)` (or `OfApplication()` before it is in a window); its own explicit property wins. |
| Add a tab | A module's `NavEntry`, listed in `Navigation/AppNavigation.Build` | Its view models registered in the same module's `Register`. |
| Show the main window | `MainWindowOpener.ShowNow` (UI thread) | The tray's Open and double click, a second launch, a macOS reopen; `Open()` posts it for the engine's Open app step once `App` has attached it. |
| Hand work to the UI thread from any thread | `UiThread.Post` | The one marshal: `CompositionRoot` passes it to `ConfigModuleOptions.Marshal`, `EngineModuleOptions.Marshal` and `SyncModuleOptions.Marshal`, and every class taking an `Action<Action>` marshal defaults to it. Never write `Dispatcher.UIThread.Post` for this. |
| React to a store change that may come from a worker | `UiThread.RunOrPost` | Runs at once on the UI thread (edits, headless tests), posts otherwise. View models get it through `ViewModels/StoreViewModel.OnStoreChanged`; the training session gets it from `GesturesModule`. |
| Wait for the UI thread from a worker | `UiThread.IsCurrent` + `UiThread.Invoke(function, cancel)` | Only `Sync/SyncStoreThread` does this. |
| Run something once after a delay (debounce, timeout, poll) | `OneShotTimer.Schedule(delay, action)` | A `System.Threading.Timer` per request, the action through the timer's marshal (none: the timer thread). Dispose the result to cancel; restart = dispose and schedule again. A request disposed after its timer fired but before the marshalled action ran is skipped. Users: the config save (250 ms, marshalled to the UI thread), sync's poll and change timers (no marshal), stroke-button detection's 5 s timeout. |
| Follow part of the settings and push what moved | `SettingsFollower<T>(settings, select, apply)` | Remembers the last selected value; on a change that leaves it different, `apply(last, next)`. Dispose to unsubscribe. Users: `EngineSettingsLink` (whole `Settings`), `StartAtLoginLink` (`StartAtLogin`), `AppState` (`GeneralSettings`), `CommandsViewModel` (stroke button, drag distance). |
| Animation or UI-only timing inside a control | `DispatcherTimer` / `Dispatcher.UIThread.Post` with a priority | Components keep these (a focus after layout, a render frame); they are not marshals. |

## Startup and shutdown order

As the code runs today (`Program.Main`, `App.axaml.cs`, `App.Launch.cs`). `EngineModuleTests` pin the engine part; changing an order here is a behaviour change, not a refactor.

**Start.**

1. `Program.Main`: `VelopackApp.Build().Run()` first (the installer's hooks exit here).
2. `LaunchArguments.Parse(args, AUGRAM_NO_ENGINE)` becomes `LaunchArguments.Current`.
3. `InstanceStartup.Begin()`: takes the name (`InstanceListener`: the `augram-instance-mutex` and `augram-single-instance` threads) or asks the running Augram through `InstanceClient` and exits, or leaves the choice to `App` (a different build).
4. `RunningPlatform.UseThisMachine()`.
5. `CompositionRoot.Build`: registers only; nothing is created yet. Order: diagnostics, `AppInfo`, `LaunchArguments`, `StartAtLoginLink`, `AppState`, clipboard, then `DialogsModule`, `ConfigModule`, `PlatformModule`, `EngineModule`, `GesturesModule`, `CommandsModule`, `IgnoredModule`, `TransferModule`, `SyncModule`, `OptionsModule`, `DiagnosticsModule`, the tray, navigation, `MainWindowViewModel`, `MainWindow`. Registration order matters only where two registrations would compete, and none does.
6. Avalonia starts: `App.Initialize` (XAML, the theme, `ClickAwayFocus.Register`), then `OnFrameworkInitializationCompleted`: explicit shutdown mode; on macOS the launch event is observed; a launch that must choose (`InstanceStartup.NeedsChoice`) resolves only the take-over's confirmation (keyed `ConfirmPresenter.AloneKey`), asks, and either quits or goes on to `StartDesktop` once it holds the name.
7. `App.StartDesktop`, in this order:
   1. `IEventLog` (the first service created, so the last disposed), `AppInfo`, `LaunchArguments`; `LaunchVisibility.AtStart`; the `App started` line; the take-over notes; `AppHealthContributor`; in Debug, `--gallery` picks the first tab.
   2. `MainWindowFactory.Create`: `AppearanceLink.Follow` (resolving `SettingsStore` creates the `ConfigSession`: `Configuration loaded`, the placeholder upgrade, a first run's file), then `MainWindow` (its view model, the navigation registry, the first tab's view models), the look attached. Then the dialogs' backdrop handler, `HandOver` of the window to the lifetime (now, or as the main loop's first job), on macOS the Dock icon and `MacDockPresence`.
   3. `MainWindowOpener.Attach`.
   4. `EngineModule.Start`: under the kill switch one `Engine disabled by flag` line and nothing else (no hook, no overlay, no host). Otherwise: on macOS the Accessibility line; `EngineHost` (its ports: input source, simulator, the trail overlay created hidden, the adapters, the training session as `Intercept`); `EngineSettingsLink`; `AppState` with its `StartAtLoginLink`, then `StartAtLoginLink.SyncStartupRegistration`; `host.Start()` (the hook and the `augram-engine-worker`, `augram-command-executor`, `augram-ignore-watch` threads); `EnginePauseState.Follow(host)`.
   5. `FormServices.Publish` (the captures only when the engine runs).
   6. `SyncModule.Start` (also under `--no-engine`): `SyncPrompter`, `SyncHealthContributor`, `Sync ready`, `SyncService.Start` (the `Augram sync` thread; its first run is the start-up sync).
   7. `AppTray` resolved (created here: `AppState`, `SyncService`, `EnginePauseState`).
   8. The listener's events (`Introduced`, `ShowRequested`, `QuitRequested`), the macOS reopen, the `Exit` handler.
   9. macOS: wait for the launch event (3 s cap); otherwise `OpenAsLaunched`.
8. The lifetime shows `MainWindow` if it was handed over at once, and runs the main loop.

**Stop.** Quit (the tray, or another Augram's Quit through the listener) shuts the lifetime down:

1. `Exit`: the `App stopping` line, then `AppTray`, `AppHealthContributor` and the macOS launch event are disposed.
2. `Program.Main`'s `using` disposes the service provider: singletons in reverse order of creation. So the tray (already disposed: a no-op), the sync service (before the config session flushes; a run on its thread is abandoned), the engine host (the hook and its threads stop), the overlay, then the config session flushes a pending save, and the event log drains last.
3. `InstanceStartup` disposes the `InstanceListener`: it stops listening, then releases the mutex. Last, so a launch taking over starts only after this process has let go of everything.

## Rules

- Tests never get the real UI marshal by accident: a test that builds a module passes `Marshal = action => action()` (or a recording one); view-model tests run on the headless UI thread, where `RunOrPost` runs at once.
- `ConfigSession` stays single-writer: its save timer's marshal is the UI thread's.
- A module's tests register the siblings it resolves (`tests/.../Support/TabServices.AddSiblings` for the tabs); no module falls back to a store or presenter of its own.
