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
