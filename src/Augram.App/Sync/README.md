# App/Sync

The App's part of machine-to-machine sync (requirements F8 sync): when a run happens, on which thread, and what the user sees of it. The merge, the base state and the run itself are Core's (`../../Augram.Core/Sync/README.md`); the git side is `Augram.Sync.Git`; the registration is `../Hosting/Modules/SyncModule.cs`. Options › Sync, the tray item and the dialogs' view models live with their kind (below) and read this folder.

## Types

| Type | Role |
|---|---|
| `SyncService` | The facade everything else uses: `Start`, `SyncNow`, `Join`, `Resolve`, `Changed`, `IsConfigured`, `IsRunning`, `IsPaused`, `LastReport`, `LastTrigger`, `OtherMachines`, `RunCount`, `PendingConflicts()`, `Snapshot`, `Dispose`. Owns the worker and the scheduler; the user's triggers go straight to the worker |
| `SyncWorker` (internal) | The "Augram sync" thread: the coalescing queue (`Request`), each run as one `SyncCoordinator.Run(join, resolutions)`, an exception reported as `Failed`, `Changed` when a run starts and ends, the last report, trigger and machine names. Owns the one lock, `Gate`. Decides nothing about when |
| `SyncScheduler` (internal) | When: listens to `SettingsStore`, `GestureLibrary` and `MappingStore`, owns the change timer (`ChangeDelay`, 20 s) and the poll timer (`PollInterval`, 5 min), the pause, and the rule for automatic triggers; asks `SyncWorker.Request` |
| `SyncTrigger`, `SyncTriggerExtensions` | Why a run happened (table below); `IsAutomatic()` |
| `SyncStoreThread` | The coordinator's `onStoreThread`: runs a run's store changes on the UI thread synchronously; `IsApplying` tells the scheduler which store changes are the sync's own; `Stop` at shutdown |
| `SyncSnapshot`, `SyncPhase` | What the status lines read, taken at one moment under the lock (`SyncService.Snapshot`); the phase is decided once, in `SyncSnapshot.Of` |
| `SyncStatusText` | Every sync status string: `Status` (Options › Sync), `Short` (the tray tooltip), `Outcome` (Diagnostics › Health), `NeedsUpdateText`, `Changes` |
| `SyncHealthContributor` | Fills `HealthSnapshot.LastSyncOutcome` / `LastSyncAt` from the last report |
| `SyncPrompter` | Asks the join question when a finished run answered `NeedsJoinChoice` (once per report, one dialog at a time) and hands the answer to `SyncService.Join` |
| `ISyncJoinPresenter` / `SyncJoinPresenter` | The join question through the shared `IFormDialogPresenter` over `SyncJoinViewModel` (`Request`: wider, activated when alone) |
| `ISyncConflictPresenter` / `SyncConflictPresenter`, `SyncConflictWindow` | Resolve…: the conflict list over `SyncConflictsViewModel`, Cancel / Apply |
| `SyncConflictSideText` | One version of a conflicting item in words ("Chrome › Close tab: gesture Zig → Wait 30 ms", "deleted"); `SyncConflictsViewModel` builds its entries with it, for sync and for the Augram file import |

Elsewhere: `Hosting/Modules/SyncModule` + `SyncModuleOptions` (composition: what is registered and the test seams), `ViewModels/SyncViewModel` (Options › Sync; its two text fields are `ViewModels/DraftText`s), `ViewModels/SyncConflictsViewModel` (the choices; shared with the import), `ViewModels/SyncJoinViewModel`, `Screens/OptionsSyncSection`, `Tray/SyncTrayItem`, `Components/SyncConflictList`.

## When a run happens

One run at a time; requests made while a run is going or queued coalesce into one more run (the user's reason wins for `LastTrigger`; join answers and resolutions accumulate).

| Trigger | When | Runs while |
|---|---|---|
| `Start` | once, from `SyncService.Start()` | repository set, automatic sync on, not paused |
| `LocalChange` | `ChangeDelay` (20 s) after the last `GestureLibrary`/`MappingStore` change the sync did not make, or a new machine name; each change restarts the delay; a run cancels a pending one (it includes the change) | same |
| `Poll` | `PollInterval` (5 min) after the end of every run | same |
| `AutoSyncOn` | automatic sync switched on | same |
| `Manual` | Sync now (Options, tray) | repository set |
| `RepositoryChanged` | the URL set, changed or cleared; lifts the pause and cancels both timers | always (a blank URL runs only the reset) |
| `Join`, `Resolve` | the join answer; conflict resolutions (each in order, then the run) | always |

**Pause.** A run that answers `NeedsJoinChoice` pauses automatic triggers (`IsPaused`) until a run answers anything else (the join choice, or a new URL). So does `NeedsUpdate` (a newer Augram wrote to the repo): only Sync now, a new URL or a restart runs again, and on the same build each answers `NeedsUpdate` again without writing anything. Automatic sync switched off cancels both timers.

## Threads and the lock

| Thread | What runs there |
|---|---|
| "Augram sync" (`SyncWorker`, background) | each run: the coordinator, git, the merge; `Changed` when a run starts and ends (handlers marshal and return at once; one that throws is logged, the worker keeps serving) |
| UI (the store thread) | every store change a run makes (`SyncStoreThread.Run`, synchronously); the stores' `Changed` and the settings' `Changed`, so the scheduler's listeners; the view models and the tray after they marshal |
| pool threads | the change and poll timers' callbacks (`SyncScheduler.Request`) |

**One lock, `SyncWorker.Gate`.** The worker's queue and status and the scheduler's timers and pause are all under it. The admission check for an automatic trigger and its queueing are one locked step. The worker raises `RunTaking` (the scheduler cancels both timers) as it takes a job and `RunEnded` (the scheduler sets the pause and schedules the poll) as it stores the report, both while it holds the lock, so a run's hand-over and its effect on the timers are atomic as they were in the one class before plan 0007. `SyncService.Snapshot` takes the status the same way.

**Shutdown** (`SyncService.Dispose`, through the provider, before the config session flushes): under the lock the scheduler cancels both timers and the worker is stopped (no request taken from then on, the thread woken); then `SyncStoreThread.Stop` (a waiting `Invoke` gives up, a queued one checks the flag on the UI thread and applies nothing); then the listeners are removed; then the worker is joined for up to 2 s (`SyncWorker.StopTimeout`). A run stuck in git is abandoned on its background thread and can no longer apply or save. Nothing runs after `Dispose`.

## Status lines

`SyncSnapshot.Of` walks one ladder and `SyncStatusText` looks the phase up:

| `SyncPhase` | When | Options › Sync | Tray tooltip |
|---|---|---|---|
| `Off` | no repository | "Off: paste a repository address above to sync gestures and commands with your other machines." | (none) |
| `Running` | a run is going | "Syncing…" | "syncing…" |
| `PausedForUpdate` | last report `NeedsUpdate` | "Paused: Mac uses a newer Augram. Update this machine (pull, rebuild, restart) to resume." | "sync paused: Mac uses a newer Augram" |
| `PausedForJoin` | paused, or last report `NeedsJoinChoice` | "Paused: choose how to join (Sync now asks again)." | "sync paused" |
| `NotYet` | no report, or `Off` | "Not synced yet." | (none) |
| `UpToDate` | last report | "Up to date (last synced 14:32)." | "synced 14:32" |
| `Applied` | last report | "Last synced 14:32: 3 commands changed from Mac." | "synced 14:32" |
| `Failed` | last report | "Sync failed at 14:32: <error line>" | "sync failed 14:32" |

Diagnostics › Health reads the last report alone (`SyncStatusText.Outcome`): its status ("UpToDate"), or "Paused (Mac uses a newer Augram)".

## What does not go here

- Merge rules, repairs, which choice is allowed, the URL-change reset: Core (`Core/Sync`). The App passes jobs through and never re-decides.
- Git or process starts: `Augram.Sync.Git`.
- Registration and the test seams: `Hosting/Modules/SyncModule`, `SyncModuleOptions`.
- View models and screens: `ViewModels/`, `Screens/` (they read this folder).
- Store writes outside `SyncStoreThread`, and anything that touches input: sync runs under `--no-engine` too.
- New status words outside `SyncStatusText`.

## Sync between machines (F8 sync, Joel 2026-10-07)

The merge, the base state and the run are Core's (`../../Augram.Core/Sync/README.md`), the git side is `Augram.Sync.Git`; this is the App's part: when a run happens, on which thread, and what the user sees.

**SyncModule** (`Hosting/`). `CompositionRoot.Build` calls `SyncModule.Register(services, options)` after the engine and tab slices; `App.StartDesktop` calls `SyncModule.Start(services)` right after `EngineModule.Start`, also under `--no-engine` (sync never touches input). `Register` adds `SyncFolders` (`<config>/sync`), `GitSyncRepository` on `<config>/sync/repo` as `ISyncRepository` (machine label = this machine's sync name), `SyncBaseStore` on the config folder (unreadable state files logged as `sync` warnings), `SyncStoreThread`, `SyncCoordinator` over the config session's stores with `SyncStoreThread.Run` as its `onStoreThread` and the `SyncFolders`, `SyncService`, the two presenters, `SyncPrompter`, `SyncViewModel` and `SyncHealthContributor`. `SyncModuleOptions` lets tests swap the repository (a fake, no git), the store thread (`(f, _) => f()`), the timers and the UI marshal. `Start` resolves the prompter and the health row, logs "Sync ready" (host only) and starts the service. The headless test app only builds; nothing runs while no repository is set.

**Runtime** (`Sync/`; the trigger table, the threads, the one lock, the shutdown order and the status lines are in `Sync/README.md`). `SyncService` is the facade the view models, the tray and the prompter use. Behind it `SyncWorker` runs each job on the "Augram sync" thread (one run at a time; requests coalesce, the user's reason wins for `LastTrigger`) and `SyncScheduler` decides when: once at start, 20 s after the last gesture or command change the sync did not make (or a new machine name), every 5 minutes, when automatic sync is switched on, and for a new URL; Sync now, the join answer and conflict resolutions always run while a repository is set. A run that answers `NeedsJoinChoice` or `NeedsUpdate` pauses the automatic triggers. Store mutations happen only on the UI thread, through `SyncStoreThread`. `Dispose` (through the provider, before the config session flushes) cancels the timers, stops the store thread and waits up to 2 s for the worker; nothing runs after it.

**Repository changes** (`Core/Sync/SyncFolders`). `sync/repository-url.txt` remembers the URL the clone and the state belong to. Before every run the coordinator calls `SyncFolders.Prepare(url)` (`SyncCoordinator.Run`; the worker passes each job through as `Run(join, resolutions)`): no marker yet → remember the URL (an existing clone is taken as this URL's); a different URL or none → move `sync/repo` aside to `sync/repo-old-<yyyyMMdd-HHmmss>` (never deleted; the git adapter refuses a clone of another URL), `SyncBaseStore.Clear()`, remember the new URL (or delete the marker), log "Sync state cleared: the repository changed". The next run then asks the join question again. Because the marker is on disk, a URL changed just before quitting is reset at the next start. A clone that cannot be moved (a file open in it) fails the run with that line and nothing changes.

**Options › Sync** (`Screens/OptionsSyncSection`, the first section of the Sync sub-tab, `OptionsScreen.DeclareSync`, when a `SyncViewModel` is registered; Export and import follows it). Repository (a private GitHub repo; the help line says sign-in is git's own and Augram stores no password), This machine, Automatic sync, Status (Sync now + the status line), Conflicts ("2 conflicts with Mac" + Resolve…, shown only while some are pending, through `Field.Visible`), Details (the last run's repairs and notes, shown only when there are some). The two text fields are drafts while typed (`ViewModels/DraftText`): every keystroke re-checks (`SyncSettingsRules.UrlProblem`, a blank name) and shows the problem under the box in `TextBlock.error`; leaving the box (or a click elsewhere, `ClickAwayFocus`) or Enter commits a valid draft as one `SettingsStore.SetSync`, Escape reverts. So a URL is never applied half-typed (each new URL resets the state). A stored value changed elsewhere replaces the draft; one being typed survives unrelated settings changes. Status lines: "Off: paste a repository address above…", "Syncing…", "Paused: choose how to join (Sync now asks again).", "Paused: Mac uses a newer Augram. Update this machine (pull, rebuild, restart) to resume.", "Not synced yet.", "Up to date (last synced 14:32).", "Last synced 14:32: 3 commands changed from Mac." (gestures, app groups, categories, hold remaps, commands and excluded apps are counted, `SyncStatusText.Changes`), "Sync failed at 14:32: <error line>"; every one of them, and the tray's, is a lookup over one `SyncPhase` in `Sync/SyncStatusText`.

**Join dialog.** `SyncPrompter` listens to the service and, when a finished run's report is `NeedsJoinChoice` (once per report, one dialog at a time), asks `ISyncJoinPresenter` on the UI thread; the answer runs `SyncService.Join`, Cancel leaves sync paused. `SyncJoinPresenter` asks the shared `IFormDialogPresenter` with `SyncJoinViewModel.Declare()` (`Dialog.Join.Width`, 680 px wide), modal to the main window when it is open, otherwise as a window of its own, activated (the start-up run usually finds Augram in the tray). The form: the machines already in the repo, newest first ("PC-HOME: Last written 7 Oct 07:40 · 105 gestures · 9 groups · 212 commands"); "Join by" with **Use the synced settings on this machine (recommended)** (default) and **Merge with this machine's settings**; a "What happens" line for the selected one (replaced by the newest machine's, previous file kept as a backup / both kept, items imported separately on each machine appear twice). Confirm is "Join".

**Conflict dialog.** Resolve… calls `ISyncConflictPresenter.ResolveAsync(pending)`; Apply returns every conflict with its choice and `SyncService.Resolve` runs them on the worker, then syncs. `SyncConflictPresenter` builds a `SyncConflictsViewModel` against the stores (names for the summaries) and shows `SyncConflictWindow`: in the `DialogShell`, the summary line with its ⓘ, the `SyncConflictList` component and Cancel / Apply. One `SyncConflictRow` per conflict (row rule: `Border.row` + `Grid.row-line`): name and "Gesture · with Mac", this machine's and the other machine's version side by side (a gesture as two `GestureGlyph.row-glyph`, anything else as a one-line summary or "deleted", worded by `SyncConflictSideText`; a category reads "Personal · Windows only" when its Use on is set; a hold remap is labelled "Hold remap" and reads "Space: hold Space, tap 180 ms", then its Use on and "(inactive)" when they apply; a command reads "Chrome › Close tab: gesture Zig → Wait 30 ms", one under a hold remap "Blender › Space › Orbit: Left → …", `CommandNames.Label`, with "another group" or "another hold remap" for one this machine lacks), and a choice dropdown, Keep mine by default. Keep both is offered only for a gesture or a command present on both machines (`SyncConflictsViewModel.Allowed`). Columns: `SyncConflict.ItemColumn` / `ChoiceColumn` tokens; theme in `Themes/Default/Lists.axaml`.

**Tray and health.** `SyncTrayItem` adds Sync now to the tray menu while a repository is set (disabled during a run) and a short status to the tooltip ("sync paused: Mac uses a newer Augram" for `NeedsUpdate`). `SyncHealthContributor` fills `HealthSnapshot.LastSyncOutcome` / `LastSyncAt`; Diagnostics › Health shows "Last sync: UpToDate at 14:32:05", or "Paused (Mac uses a newer Augram) at 14:32:05".

**First sync, per machine.** Paste the repo's https URL into Options › Sync › Repository and leave the field. The worker clones it into `<config>/sync/repo` (Git Credential Manager's sign-in window on Windows; the Keychain helper or `gh auth setup-git` on macOS, `../../Augram.Sync.Git/README.md`). The first machine finds no other file: "Up to date", and its file is pushed. The next machine finds it and asks the join question.
