# Review: configuration in and between machines (importer, git sync, App sync/transfer/import)

## Map

- **Three flows, one merge.** Core/Sync owns the item merge (`SyncItemSet` → `ThreeWayMerge`/`SyncDocumentBuilder` → repairs → `ReplaceAll`). Core/Transfer reuses it for Augram-file import (`ImportPlan` = merge with an empty base, `ImportMatcher` lines ids up by name/shape/hold key). The StrokesPlus.net importer (`Augram.Import.StrokesPlus`) is the odd one: it reads SP.net JSON into Core records (readers, one per SP.net section; `PlaceholderUpgrade` re-maps saved placeholders at every start from `EngineModule.UpgradeImportedSteps`) **and** carries a second, older merge of its own (`GestureMerge` + `MappingImport`).
- **`Augram.Sync.Git`** implements the `ISyncRepository` port over the installed git: `GitSyncRepository` (prepare/pull/publish), `GitClone` (the questions about the clone), `GitRunner` (the only process start), `GitFailure` (stderr → one line), `MachineFiles`, `RemoteUrl`, `CommitIdentity`, `CredentialScrubber`. Self-contained, well documented, nothing to fold.
- **App side of sync** is split across two folders: `Hosting/` holds the runtime (`SyncService` worker + timers + triggers, `SyncStoreThread`, `SyncFolders` URL-change reset, `SyncResolution`, `SyncHealthContributor`, `SyncModule`); `Sync/` holds only the two presenters, the conflict window and `SyncPrompter`. `ViewModels/` holds `SyncViewModel` (Options › Sync), `SyncConflictsViewModel` (shared with the Augram import), `SyncJoinViewModel`; `Tray/SyncTrayItem` is a third status reader.
- **App side of transfer**: `Transfer/` (`ExportPresenter`, `AugramImportPresenter`, `TransferFilePicker`, the review window) over `ViewModels/ExportViewModel`, `AugramImportViewModel(.Text)`; registered by `Hosting/TransferModule`; `ConfigurationViewModel` is the Options row with three buttons.
- **App side of the SP.net import**: `Import/` (`ImportPresenter` with its own file picker, `ImportDialog.axaml` + `ImportView.axaml`, `ImportConflictItem`) over `ViewModels/ImportViewModel`; registered by `GesturesModule` (it is also on the Gestures toolbar).
- Tests: Core.Tests/Import (importer, 24 files), Core.Tests/Sync + Transfer, Sync.Git.Tests (real git in temp bare repos), App.Tests/Sync + Transfer + Import (fakes for repository, pickers, windows).

## Findings (most valuable first)

**1. The StrokesPlus.net import has its own merge; Core/Transfer already does that job.** (kind 1)
Where: `src/Augram.Import.StrokesPlus/GestureMerge.cs` (whole file), `MappingImport.cs`, `MergeEntry.cs`, `MergePlan.cs`, `MergeChoice.cs`, `MergeKind.cs`, `MergeOutcome.cs`, `MappingMergeResult.cs`; `src/Augram.App/ViewModels/ImportViewModel.cs:119,154-163,212-213`; `src/Augram.App/Import/ImportConflictItem.cs`, `ImportView.axaml`, `ImportDialog.axaml(.cs)`; versus `src/Augram.Core/Transfer/ImportMatcher.cs:38-78` (gesture by name, then shape ≥ `DuplicateCutOff`: identical rule to `GestureMerge.Plan`/`ShapeChecked`), `ImportMatcher.Mapping.cs:21-47,83-133` (group by name, Global→Global, command by name among siblings, category by name, ignored app by name: identical to `MappingImport.Merge`/`Placed`/`IsTaken`), `ImportPlan.cs`, `ImportResolution.cs`.
What: two implementations of "line an imported config up with mine by name/shape, offer Keep mine / Take theirs / Keep both, rebind commands to the settled gesture ids, merge categories by name". `MergeChoice` duplicates `SyncChoice`; `ImportConflictItem.Choices` labels (`:19-21`) duplicate `SyncConflictsViewModel.KeepMineLabel…` (`:29-31`); the SP.net dialog is XAML while the Augram review and conflict dialog are code-built over the shared `SyncConflictList`. The importer's cleanup pass folded naming into `NameScope` but left the merge.
Recommendation: make the importer produce a `TransferFile` (`new TransferFile(result.Gestures, result.Mapping) { Notices = warnings as lines }`) and run it through the existing review (`AugramImportViewModel` with `SourceName = "StrokesPlus.net.json"`). Delete the eight merge files, `ImportConflictItem`, `ImportView.axaml`, `ImportDialog.axaml`, most of `ImportViewModel`, and their tests (`GestureMergeTests`, `MappingImportTests`, `MappingImportCategoryTests`); the SP.net presenter becomes "pick file → `ReadAll` → review". `ImportPlan` is a superset: a name match with different content is a choice, the same content is "Same".
Size L. Risk medium. **Not behaviour-exact**: (a) a command whose trigger is already bound in the group is today *skipped* (`MappingImport.cs:124-131`); through `SyncDocumentBuilder` it would be *added unbound* with a repair line (Core/Sync README, A7); (b) the "Found N gestures, … (K steps as placeholders)" stats line and the per-row "same shape as 'X' (score 93)" summary become the review's "In the file" / "Matched" lines; (c) one undo step per store already in both. See Q1.

**2. `SyncFolders` is pure file-system logic living in Hosting, and `SyncService` carries a flag to work around it.** (kind 3)
Where: `src/Augram.App/Hosting/SyncFolders.cs` (no Avalonia, only `IClock` + `SyncBaseStore`), `SyncFolderReset.cs`; `SyncService.cs:125-136` (`_stateCleared` hack so `PendingConflicts()` hides the coordinator's stale `_pending` after `bases.Clear()`), `:294-303`, `:275`.
What: the "state belongs to one URL; moving the clone aside and clearing the base on a URL change" rule is a Core/Sync concern (Core README "Limits" even documents that the App does it). Because Core does not own it, the coordinator's pending-conflict snapshot goes stale and the service patches around it.
Recommendation: move `SyncFolders` (+ `SyncFolderReset`, + `tests/Augram.App.Tests/Sync/SyncFoldersTests.cs`) to `Core/Sync`; let `SyncCoordinator.Run` call `Prepare(url)` itself (it already receives the URL) and refresh `_pending` after a reset; drop `_stateCleared`. Size M. Risk low-medium (the reset log line and the "old sync folder could not be moved aside" failure line must keep their wording; tests exist for both). Behaviour-exact if done carefully. See Q2.

**3. The sync runtime is in `Hosting/`, so view models depend on the composition-root folder.** (kind 2/3, map gap)
Where: `src/Augram.App/Hosting/SyncService.cs`, `SyncStoreThread.cs`, `SyncTrigger.cs`, `SyncTriggerExtensions.cs`, `SyncResolution.cs`, `SyncHealthContributor.cs` (and `SyncFolders` until finding 2); referenced from `ViewModels/SyncViewModel.cs:2`, `ViewModels/SyncConflictsViewModel.cs:6`, `Sync/SyncPrompter.cs:1`, `Sync/ISyncConflictPresenter.cs:1`, `Tray/SyncTrayItem.cs:2`.
What: `Hosting/` is 41 files mixing composition (`*Module`) with runtimes; an agent looking for "the sync worker" opens `App/Sync/` and finds only dialogs. Only `SyncModule`/`SyncModuleOptions`/`TransferModule` are composition.
Recommendation: move the six files to `App/Sync/` (namespace `Augram.App.Sync`), keep the modules in `Hosting/`, give `App/Sync/` the README the ADR amendment requires. Size M (moves + usings). Risk low. Exact.

**4. `SyncService` does two jobs: the worker/queue and the trigger policy.** (kind 2)
Where: `src/Augram.App/Hosting/SyncService.cs` (451 lines): worker + coalescing queue + `Execute` (`:208-337`); trigger policy = settings/store listeners, two timers, pause rules (`:234,351-421`); status snapshot (`:101-136,266-285`).
Recommendation: split into `SyncWorker` (thread, `Request`, `Work`, `Execute`, `Changed`, `LastReport`) and `SyncScheduler` (subscribes to `SettingsStore`/`GestureLibrary`/`MappingStore`, owns the change and poll timers and `IsPaused`, calls `worker.Request`). Each lands under 250 lines with one reason to change; `SyncServiceTests` (295 lines) split the same way. Size M. Risk medium (lock discipline and `Dispose` ordering: cancel timers, stop store thread, join worker). Exact if the lock stays one object shared or the scheduler only calls `Request`.

**5. `SyncService.Execute` holds merge-sequence logic Core should own.** (kind 3)
Where: `src/Augram.App/Hosting/SyncService.cs:310-322` (apply each resolution in order, stop at the first `Failed`, prepend resolution notes to the run's report); `SyncResolution.cs` (App record of the pair `SyncCoordinator.Resolve(conflict, choice)` takes).
Recommendation: `SyncCoordinator.Run(SyncJoin? join, IReadOnlyList<SyncResolution> resolutions)` in Core with `SyncResolution` as a Core record; the service passes the job through. Size S. Risk low. Exact (same order, same note merging). Pairs with finding 3.

**6. The "modal to the main window if visible, else a window of its own" block is copied five times; the owner lookup twelve times.** (kind 1)
Where (full block): `src/Augram.App/Components/FormDialog/FormDialogPresenter.cs:23-34`, `Sync/SyncConflictPresenter.cs:37-48`, `Sync/SyncJoinPresenter.cs:32-44` (adds `Activate()`), `Transfer/AugramImportWindows.cs:24-38` (adds `Activate()`), `UsedBy/ConfirmDialog.cs:62-71` (`ShowAloneAsync` variant); owner lookup also in `Transfer/TransferFilePicker.cs:72`, `Import/ImportPresenter.cs:35`, `UsedBy/ConfirmPresenter.cs:14`, `UsedBy/UsedByPresenter.cs:46`, `Components/GesturePicker/GesturePickerPresenter.cs:53`, `Training/TrainingPresenter.cs:38`, `Hosting/AppClipboard.cs:12`.
Recommendation: one `MainWindowHost` (in `Components/` or `Hosting/`) with `Window? Owner` and `Task ShowAsync(Window, bool activateWhenAlone)`; every presenter calls it. Decide once whether a dialog shown alone is activated (today three do, two do not). Size S. Risk low. Exact per call site if the activate flag is passed as today.

**7. `SyncJoinPresenter` re-implements `FormDialogPresenter.ShowAsync`.** (kind 1)
Where: `src/Augram.App/Sync/SyncJoinPresenter.cs:18-47` vs `Components/FormDialog/FormDialogPresenter.cs:10-37`. The only differences: `Width = 680` and `window.Activate()` when shown alone.
Recommendation: add `Width` (and the activate rule, finding 6) to `FormDialogRequest`; `SyncJoinPresenter` becomes `_dialogs.ShowAsync(new FormDialogRequest(Title, ConfirmLabel, Message, Screen: vm.Declare()) { Width = 680 })` like `ExportPresenter.Request`; `SyncModule` then takes `IFormDialogPresenter`. Size S. Risk low. Exact.

**8. Three status renderers branch on the same sync state.** (kind 1)
Where: `src/Augram.App/ViewModels/SyncViewModel.cs:215-250` (`Status`), `Tray/SyncTrayItem.cs:66-95` (`Short`), `Hosting/SyncHealthContributor.cs:25-29` (`Outcome`); the first two have the identical decision ladder (configured → running → NeedsUpdate → paused||NeedsJoinChoice → null||Off → by status with HH:mm), only the words differ.
Recommendation: `SyncService` exposes one `SyncPhase` enum (Off, Running, PausedForJoin, PausedForUpdate, NotYet, UpToDate, Applied, Failed) computed once under the lock; the three renderers become lookups over it in one `SyncStatusText` class. Size S. Risk low. Exact.

**9. `SyncConflictsViewModel.Allowed` restates Core's Keep-both rule.** (kind 1/3)
Where: `src/Augram.App/ViewModels/SyncConflictsViewModel.cs:69-75` vs `src/Augram.Core/Sync/ConflictResolution.cs:17-37` (`Effective`: Keep both only for gesture/command present on both sides). The App README even says "elsewhere Core would apply the nearest choice anyway". ADR §5a: the UI never re-implements a rule.
Recommendation: make the rule public in Core (`SyncChoiceRules.Allowed(conflict)`, with `Effective` derived from it) and have the VM call it. Size S. Risk low. Exact.

**10. The Augram import's "apply, re-plan if the stores moved, up to 3 times" loop duplicates `SyncStoreWriter`.** (kind 1/3)
Where: `src/Augram.App/ViewModels/AugramImportViewModel.cs:37,158-184` (`Attempts = 3`, loop, error text) vs `src/Augram.Core/Sync/SyncStoreWriter.cs:27-55` (`MaxAttempts = 3`, same snapshot/compare/retry, its own error text); `ImportResult.ApplyTo` (`Core/Transfer/ImportResult.cs:61-87`) repeats `SyncStoreWriter.Apply`'s reference checks and `ReplaceAll`s plus settings.
Recommendation: a Core `ImportApplier.Apply(session, file, options, choices)` that owns the loop (or `SyncStoreWriter` generalised to take settings and reused by `ImportResult`); the VM calls it once and shows the result. Size S/M. Risk low. Exact.

**11. Five noun tables and three "a, b and c" joiners for the same six item kinds.** (kind 1)
Where: `src/Augram.App/ViewModels/SyncViewModel.cs:257-284` (`Changes`, `Add`, `From`), `ViewModels/AugramImportViewModel.Text.cs:92-118` (`Changes`, `Noun`, `Count`), `src/Augram.Core/Sync/SyncCounts.cs:81-89` (`Label`), `Core/Transfer/TransferContents.cs:72-78` (`Add`), `ViewModels/ExportViewModel.cs:177-178` (`Count`); list joins at `SyncViewModel.cs:272`, `AugramImportViewModel.Text.cs:102`, `Core/Sync/SyncNewerMachine.cs:33`.
Recommendation: one Core `SyncItemKind` noun helper (singular/plural, log form) and one `Sentence.List(parts)` helper; the five sites call them. Size S. Risk low (strings must match the existing tests). Exact.

**12. `SyncViewModel` does three jobs.** (kind 2)
Where: `src/Augram.App/ViewModels/SyncViewModel.cs` (336 lines): two draft text fields with commit/revert/problem (`:55-108,145-189,300-315`); the status/conflicts/details projection (`:122-142,194-206,324-335`); pure text helpers (`:215-284`, used by tests and finding 8).
Recommendation: `SyncStatusText` (static, with finding 8), a small `DraftText` object for "typed value, problem, commit, revert, follow the store" used twice here (check `HoldRemapEditViewModel` and `CommandsViewModel.Steps`, which also have commit/revert pairs, before writing it), and the VM keeps the projection. Size S/M. Risk low. Exact.

**13. `SyncConflictsViewModel` mixes choice state with "describe a sync item for a human".** (kind 2)
Where: `src/Augram.App/ViewModels/SyncConflictsViewModel.cs:157-242` (`Side`, `GestureText`, `CommandText`, `TriggerText`, `VersionText`, `GroupText`, `CategoryText`, `HoldRemapText`, `IgnoredText`) beside `:92-130` (choices).
Recommendation: move the text half to `SyncConflictSideText` (App, since it uses `StepPlatformMarker`); the VM then reads as the choices object the import also reuses. Size S. Risk low. Exact.

**14. Two open-file pickers.** (kind 1)
Where: `src/Augram.App/Import/ImportPresenter.cs:51-69` (own `FilePickerOpenOptions`, own owner lookup, SP.net start folder from `ImportViewModel.DefaultSourcePath:49-64`) vs `Transfer/TransferFilePicker.cs:47-63`.
Recommendation: `ITransferFilePicker.PickOpenAsync(OpenRequest)` (title, filters, suggested name, start folder) serving both; the SP.net default path moves out of the view model into the presenter. Falls out of finding 1; otherwise S, low, exact.

**15. `ImportViewModel` does file IO and decides "something to import".** (kind 3)
Where: `src/Augram.App/ViewModels/ImportViewModel.cs:95-108` (`File.OpenRead` in a VM; the Augram import keeps IO in `AugramImportPresenter.cs:48-56`), `:133` (`CanApply` rule in the VM; Core's `ImportPlan.IsEmpty` is the counterpart). `Import/ImportDialog.axaml.cs:9-12` news up `GestureLibrary`/`MappingStore` for the previewer.
Recommendation: disappears with finding 1; if 1 is deferred, move the read to `ImportPresenter` and the rule to the importer. Size S. Exact.

**16. Dead public surface (callers in tests only).** (kind 4)
- `StrokesPlusImporter.ReadGestures` ×3 and `ImportResult` 3-arg ctor (`src/Augram.Import.StrokesPlus/StrokesPlusImporter.cs:16-37`, `ImportResult.cs:19-22`): M1 leftovers; App calls only `ReadAll`.
- `GestureMerge.Apply` (non-map) and the 2-arg `Plan` (`GestureMerge.cs:17-28,55-59`): gone with finding 1 anyway.
- `SourceStats.SampleCount/ActionCount/ApplicationCount/StepCount/IgnoredApplicationCount` (`SourceStats.cs`): only `GestureCount` is read in src (`ImportViewModel.cs:122`).
- `ImportPlan.Count(status)` (`Core/Transfer/ImportPlan.cs:99`), `SyncConflictsViewModel.Choices` (`:63`).
Recommendation: delete, or mark the test-only ones `internal` + `InternalsVisibleTo` to be honest about it. Size S. Risk none. Exact.

**17. App README's StrokesPlus.net section is stale.** (kind 5)
Where: `src/Augram.App/README.md:189-191`: says actions and apps "will import in a later version" and "Apply runs `GestureMerge.Apply` and `GestureLibrary.ReplaceAll` (one undo step)"; the code reads the whole file (`ReadAll`), merges the mapping through `MappingImport` and writes two stores (`ImportViewModel.cs:101,160-163`). Also README line 5 names who references `Augram.Sync.Git` but not `Augram.Import.StrokesPlus` (`ImportViewModel`, `ImportConflictItem`, `ImportView.axaml.cs`, `EngineModule.UpgradeImportedSteps`).
Recommendation: rewrite to whatever finding 1 leaves. Size S.

**18. No README in `App/Sync/`, `App/Transfer/`, `App/Import/`, `App/Hosting/`, `App/ViewModels/`.** (kind 5)
ADR-0002 §5 (amended 2026-10-11) requires one per App folder; today everything is in the 226-line App README. An agent arriving cold reads nine screens of README to learn that the sync worker is in `Hosting/`. Recommendation: write them during the moves in findings 2–3 (what lives here, threading contract, what not to put here). Size S.

**19. Importer README: 169 lines, mostly SP.net→Augram mapping reference, with one self-contradiction.** (kind 5)
Where: `src/Augram.Import.StrokesPlus/README.md:109`: "`StepReader` does not call it yet; the lead wires it" about `DisplayChangerMapping`, in the same paragraph that says it is wired through `ProgramCallMapping`. The type table (`:16-32`) is the map; the step/text/run/script/action/category/matcher tables are the spec.
Recommendation: keep the type table and merge policy in the README; move the six mapping tables to `docs/reference/strokesplus-net-import-mapping.md` and link; fix line 109. Size S.

**20. Core/Sync README "Referenced by" undercounts.** (kind 5)
Where: `src/Augram.Core/Sync/README.md:132` lists the composition root, the Options › Sync VM and Transfer; also `SyncService`, `SyncPrompter`, `SyncTrayItem`, `SyncHealthContributor`, `SyncConflictsViewModel`, `SyncJoinViewModel`. Fix with finding 3. Size S.

**Checked and left alone:** `Augram.Sync.Git` (one job per file, README matches code, no duplicate with Core); `ExportPresenter`/`ExportViewModel`/`Exporter` (clean, VM holds only the choice); `PlaceholderUpgrade` and its call from `EngineModule` (composition root, allowed by ADR §1); App's `FreeNames.Next` vs Core `NameScope` (different rule, "New command 1" vs "Orbit (2)", documented as such); `SyncStoreThread`, `SyncPrompter`, `SyncHealthContributor` (small, single-purpose); `ImportedNames` (already a thin wrapper over `NameScope`); `Themes/Wireframe` README mentions (the folder still exists pending the planned removal).

## Questions for Joel

1. **Finding 1:** may the StrokesPlus.net import become "read SP.net → the same review dialog as an Augram file"? The visible changes: a command whose trigger is already taken in the group arrives *unbound with a repair line* instead of being skipped; the "Found N gestures, … K placeholders" line and the per-row "same shape as 'X' (score 93)" become the review's "In the file" / "Matched" lines; the SP.net warnings show under "File notices". If not, findings 14–15 apply on their own.
2. **Finding 2:** Core/Sync's README says deliberately that "the state belongs to one repository and Core does not know which". Is Core owning the URL-change reset (`SyncFolders` moving to Core) acceptable, or should it stay an App rule?
3. Plan 0003 says the SP.net import "stays on the Gestures toolbar until you say otherwise". Does it stay? It decides whether `IImportPresenter` keeps being registered by `GesturesModule` or moves to `TransferModule` with the other two presenters.