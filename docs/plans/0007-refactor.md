# Plan 0007: Refactor: one implementation per job, a map for every folder

**Status: STARTED 2026-10-11. Features are paused until this plan is done** (Joel, 2026-10-11). Bug fixes Joel asks for still go in; new features wait. The Mac catch-up for plans 0005 and 0006 waits too.

## Why

Joel, 2026-10-11: compartmentalise everything as it should be, and avoid the same functionality implemented more than once: one implementation used everywhere. A measurement the same day showed a second reason: agents spent a large part of every task relearning the area (5 of 12 minutes on a one-line UI change), and App, the biggest project (34k lines, 382 files), had one README while Core had 27.

## Decisions (Joel, 2026-10-11)

1. **Features pause** while the refactor runs, so slices never collide with feature work.
2. **The Wireframe theme is retired** ([ADR-0002](../adr/0002-code-and-repo-structure.md) §5b, amended). It was Default's base layer: what Default still uses moves into `Themes/Default/`, what Default overrides is deleted. UiShots renders every screen and gallery page before and after; they must match.
3. **Every App folder gets a README** (ADR-0002 §5, amended), written by the agent that reorganises that folder.
4. **Behaviour stays exactly as it is.** A slice that would change what Joel sees or what a gesture does is not a refactor; it goes back to Joel as a question.
5. **Models.** The inventory runs on Fable 5.1 at `high` (finding the same job written two different ways is its strength). The slices run on Opus 5.5 at the effort CLAUDE.md sets per area. Risky slices (Engine, capture, recognition, hold remaps, sync) get a separate Fable review before they merge. For merging the inventory into one list, the lead asks Joel to switch the session to Fable and back.

## Steps

1. **Wireframe folded into Default** (one agent; screenshot comparison). Handoff doc cut to the current state (lead).
2. **Inventory, read only** (Fable agents, one per area): Core model and steps; Core capture, recognition, hold remaps, sync, transfer; Engine and the Platform projects; importer, Sync.Git and App's sync/transfer/import; App Components; App view models and screens; App hosting, tray, overlay and tools. (No separate Themes review: the fold agent's "left alone" notes serve as it, see S17.) Each reports a map of its area and up to 25 findings: the same job in two places, types doing two jobs, ADR-0002 breaks, dead code, map gaps. Each finding has a recommendation, size and risk.
3. **One list** (lead, on Fable): the findings merged, deduplicated across areas, grouped into slices, each with a recommendation. Joel picks.
4. **Slices** (Opus agents, one per slice, in parallel where they don't share files): fold, split or move; tests green after each slice; the folder's README written or updated in the same commit.
5. **Close-out:** read-first table in CLAUDE.md updated, this plan moved to `completed/`, and the agent timing measurement repeated against the 2026-10-11 baseline (median agent run, share of time spent thinking).

## Progress (keep this current: a new lead resumes from here)

Updated 2026-10-11, late. Step 1 (Wireframe fold, merged `4a022f1`) and step 2 (inventory) are done; step 3's list is below; step 4 runs slice by slice.

| Slice | State | Merge | What later slices reuse instead of redoing |
|---|---|---|---|
| S1 | merged | `a380bbc` | `Core/Abstractions/RunningPlatform.Current` (tests set it, `Reset()` in `finally`, in a non-parallel collection: `RunningPlatformTests`), `HostPlatformExtensions.DisplayName()/Other()`, `EnginePorts.Platform` |
| S2 | merged | `d32c870` | `Core/Diagnostics/LogSources` (18 sources, `All` alphabetical); never a literal source |
| S3 | merged | `d883e59` | `Core/Text/Sentence` (`Count`, `List`, `Capitalised`), `Core/Sync/SyncItemNouns`, `SyncCounts.Phrase`, `Core/Mapping/FreeNames` |
| S4 | merged (Fable-reviewed) | `c0700be` | `AppGroup.ActiveCommandsOn/OrdinaryCommandsOn/HoldRemapCommandsOn`; `IgnoreList.Under`; `HoldRemapRules.RemapOutputOn` and `FittedTo(step, trigger)` (there is no `StepOffer.Fitted`); `MappingDocument.FindCommand`, `HoldRemapEntry.FindBinding`; `Core/Json/JsonMember` (strict: numeric, padded and multi-name values refused); `Unique.Ensure`; `ConfigRecovery`; `GeneralSettings.IgnoreKey` is `KeyModifiers` |
| S5 | merged (Fable-reviewed) | `8978606` | `ConflictResolution.Apply(list)`, `SyncStoreWriter.StillHolds/Replace`, `SyncChoiceRules`, `ImportApplier`, `AppGroup.HeaderOnly()/IsShell`, `ConfusionCheck.Duplicate`; `SyncFolders` in Core; sync-file types in `Core/Sync/Files/` |
| S6 | merged (Fable-reviewed after) | `11ef93c` | `GestureMatcher.Best`, `CapturePointExtensions.ToGesturePoints`, `KeySet` on the hook (Engine `KeyBits` gone), `GesturePoint.DistanceTo` |
| S8 | merged | `e81920b` | `Transfer/FileOpenRequest` + `ITransferFilePicker.PickOpenAsync` for every open dialog; SP.net mapping tables in `docs/reference/strokesplus-net-import-mapping.md` |
| S9 | merged (Fable-reviewed) | `059cc43` | `App/Sync/`: `SyncWorker`, `SyncScheduler`, `SyncService` facade, `SyncPhase`/`SyncSnapshot`/`SyncStatusText`, `SyncConflictSideText`; `ViewModels/DraftText` (does not fit `HoldRemapEditViewModel` or `CommandsViewModel.Steps`); `TimerSaveScheduler` stays in Hosting for S11's `OneShotTimer` |
| S7 | merged (Fable-reviewed) | `3db8e91` | `HotkeyKeys.Keys/ModifierOf` (the one modifier rule, hook-safe); `Core/Abstractions/InputSimulation` (`Worst`, `WithModifiersHeld`, `PressKeys/ReleaseKeys`); `Engine/Input/OwnInjections` (one ledger, slots); `IgnoredInputLog`; `InputGate.HoldRemaps.cs`, `EngineWorker.HoldRemaps.Log.cs`; `ISystemEvents.ReportsForeground`; Q7: "Window activated" logged once, by the executor |
| S10–S15, S14b | not started (wave 4, one at a time, in the order listed) | | |
| S16, S17 | not started (wave 5) | | S17 also: CLAUDE.md's read-first row for the SP.net import names the new mapping doc; the Engine README's hook row says "Trace only, apart from IgnoredInputLog's Info lines" in "Does" but still "log above Trace" in "Must not" (and "Hook installed"/"Hook lost" are hook-thread Info once per generation): make the row agree; a test pinning `ClickRelay.Replay`'s whole call sequence (keys down, click, Ctrl tap, keys up) as one list (S7 review note) |

**Wave 3 done 2026-10-12** (main `3db8e91`, 3,385 tests). **Next: wave 4, starting with S10 (Dialogs)**, one slice at a time in the listed order, in a fresh context.

**Questions** 1, 10, 11 answered by Joel; the rest run on the lead's recommendation unless he overrules (2: yes; 3: settled; 4: exception documented; 5: refuse; 6: moved; 7: executor's line; 8: one undo step; 9: one label).

**How a slice runs (what worked):** one worktree agent per slice, base commit named in the brief, its findings cited from `0007-inventory/` instead of re-verified, effort per CLAUDE.md, scratch commit-message files named `sN-…` (agents share the scratchpad). Slices in Engine, capture, recognition, hold remaps or sync get a Fable review agent (read-only, `model: fable`) before merge. The lead reads the diff's riskiest file, merges with `--no-ff` and a message file, and runs build + full suite + format itself only when main moved under the slice; then pushes, waits for CI in the background, removes the worktree. Keep shell commands simple: a compound merge-and-push line was refused by the permission check; separate calls are not.

**Outside this plan, noted while it ran:** plan 0008 (keys lost after a Minimize, Joel, PROPOSED for after the refactor); the handoff's "one app, defined once" backlog item.

## The list (step 3, written 2026-10-11 on Fable 5.1)

Seven read-only reviews, one per area, are in [0007-inventory/](0007-inventory/): about 150 findings with file:line for each. This list merges them. A finding is cited as *report finding*, e.g. `Imp 1` = [4-import-sync.md](0007-inventory/4-import-sync.md) finding 1. Report names: **Core-A** (model, steps, config), **Core-B** (capture, recognition, hold remaps, sync, transfer), **Eng** (engine, platforms), **Imp** (importer, git sync, App sync/transfer/import), **Comp** (App components), **VM** (App view models, screens), **Host** (App hosting, tray, overlay, tools).

### Found by several reviewers independently

These are the cross-cutting ones, the kind Joel asked about: the same job written in two shapes in different areas.

| Job | Written | Reports |
|---|---|---|
| "Which platform am I on" and the platform's name | 8 ways to ask, 13 copies of `"macOS" : "Windows"`, 4 of `Other()` | Core-A 1–2, Eng 8, Comp 13, VM 11, Host 6 |
| Find the main window; show a dialog owned by it or alone; the dialog shell; Escape | 13 owner lookups, 6 show-or-wait blocks, 5 hand-built shells, 2 confirm presenters | Imp 6–7, Comp 6–7, 19, VM 17, Host 2, 13 |
| Marshal to the UI thread; the view-model plumbing ADR §5a said would be one base class | 13 marshals, Guard/Message ×3, delete-with-confirm ×6 | VM 4–5, 19, Host 4 |
| The StrokesPlus.net import's own merge and review, where the Augram import already uses the sync merge (kept: Q1) | 8 merge files + a XAML dialog; the shape-duplicate check twice | Imp 1, 14–17, Core-B 1–2 |
| Log source names | 3 disagreeing lists; the Diagnostics filter cannot select 4 engine sources | Eng 4, Core-B 16 |
| "New command N" naming; pluralising; noun tables; "a, b and c"; `Capitalised` | FreeNames ×3, Count ×7, nouns ×5, joins ×3, Capitalised ×4 | VM 9, 12, Host 7, Imp 11, Comp 21 |
| Sync and the Augram import applying choices and writing stores | apply ×2, store write ×2, New/Same/Different ×2, rename-with-repair ×6, Global shell ×4 | Core-B 5–9, Imp 10 |
| Fakes for the ports in the test projects | FakeInputSimulator ×3, seven more ×2; gallery fakes repeat test fakes | Core-A 16, Host 14–15 |

### Slices

Each slice is one agent in its own worktree, behaviour kept exactly unless a question below says otherwise, all tests green, the touched folders' READMEs written or updated in the same commit (ADR-0002 §5, amended). Effort per CLAUDE.md: `medium` for App and most of Core, `high` for Engine, platforms, capture, recognition, hold remaps, sync. Waves run in order; slices inside a wave run in parallel (disjoint files).

**Wave 1: foundations (small, many files; run first so later slices build on them)**

| # | Slice | What | Findings | Size / risk |
|---|---|---|---|---|
| S1 | Platform identity | `HostPlatformExtensions.DisplayName()/Other()` in Core/Abstractions; one `RunningPlatform.Current` (OS probe by default, set once by the root and by tests); `EnginePorts.Platform`; every probe and literal folds into them. SharpHook's two `IsMacOS` quirks stay in the adapter, named in the README. | Core-A 1–2, Eng 8, Comp 13, VM 11, Host 6 | M / low |
| S2 | Log sources | `LogSources` moves to Core/Diagnostics with every source the solution emits (`sync`, `window`, `steps`, `training` included); Platform and App read it; the Diagnostics filter list is built from it; README and `LogEvent` doc point at it. | Eng 4, Core-B 16 | S / low |
| S3 | Text helpers | `FreeNames.Next` moves to Core beside `NameScope` (comparer as a parameter; Training calls it); one `Count(n, noun)` with the "no Xs" form; one noun table for the sync item kinds; one `Sentence.List`; one `Capitalised`. Strings byte-identical (tests assert them). | VM 9, 12, Host 7, Imp 11, Comp 21 | S / low |

**Wave 2: Core (parallel; disjoint folders)**

| # | Slice | What | Findings | Size / risk |
|---|---|---|---|---|
| S4 | Core Mapping, Steps, Config | `Command.RemapOutputOn(platform)` (3 computations → 1); `AppGroup.ActiveCommandsOn(platform)` (6 predicates → 1); `IgnoreList` owns the ignore rule; `CategoryRules.Carried` and a `StepOffer.Fitted` the App can call; one `Unique.Ensure`; one JSON member reader behind `JsonMembers` and `StepParameters` (Q5); `IgnoreKeys` → `KeyModifiers` with a converter that keeps `"Win"` in the file; `ConfigSession` split from `ConfigRecovery`; the sync-file types out of `Config/` into `Core/Sync/Files/`; `MappingDocument.FindCommand`, `HoldRemapEntry.FindBinding`, `WithCommandReplaced`; dead members deleted; README drift fixed, and the Mapping README says plainly that Global is an app group that matches every window and has its own tab (its name and matcher are unused; Joel asked why on 2026-10-11); one schema/format history table. The `RemapStep` type tests collapse to the one or two HoldRemaps keeps (Q4). | Core-A 3–14, 17–19; Core-B 18, 20 | M / low–medium |
| S5 | Core Sync and Transfer | One `ConflictResolution.Apply` over a list with the import's two extras; one store-thread apply the import calls; `ImportResolution` reads the plan's statuses; one rename-with-repair helper; `AppGroup.HeaderOnly()/IsShell`; `ImportMatcher.Gestures = Pair(...)` + the shape pass; `ConfusionCheck.Duplicate` in Recognition for both callers; `SyncChoiceRules.Allowed` public for the VM; `SyncCoordinator.Run(join, resolutions)` so the App passes the job through; a Core `ImportApplier` owning the apply-and-retry loop; `SyncFolders` and the URL-change reset move to Core (Q6), the `_stateCleared` flag goes; `SyncItemJson` beside its user; one sample world for the Sync and Transfer tests; README lines on where the one merge decision is. | Core-B 2, 5–9, 19–22; Imp 2, 5, 9–10 | M / medium (well tested) |
| S6 | Core Capture, Recognition, Gestures | `GestureMatcher.Best(ranked, options)` for the Engine; `CapturePoint → GesturePoint` once; `KeySet.Without` replaces Engine's `KeyBits`; geometry helpers shared (the verbatim resampler untouched); `ShapeCleanup` split into its three types and partials; dead members; README fixes (Corrected is the default; `RecognitionCandidate` has an Id; the Capture README names the Engine shadows and says the shadow pattern is settled, Q3). | Core-B 3–4, 10–15, 17, 22 | S / low |

**Wave 3: Engine, importer, App sync (after wave 2)**

| # | Slice | What | Findings | Size / risk |
|---|---|---|---|---|
| S7 | Engine and platforms (`high`; Fable review before merge) | The modifier rule lives once in `HotkeyKeys` (Engine's two copies go; worker and executor stop reaching into the SharpHook adapter); `SimulationResult.Worst` once; one `WithModifiersHeld` bracket (injection order kept: Blender reads modifiers at the down); one `OwnInjections` ledger for wheel and buttons; `InputGate.HoldRemaps.cs` and `EngineWorker.HoldRemaps.Log.cs` partials; one `IgnoredInputLog`; Core lookups instead of hand scans; Mac display enumeration and pointer read once each; Windows root-or-self once; the activation log line once (Q7); dead members (log `WatchesForeground`/`IsObserving` once at start instead); README rows (`EnginePorts`, the hook-side decision table in the method's row order, `MainThread`, `Ax.IsOwn`, the watch's real job). Nothing adds work to a hook handler. | Eng 1–3, 5, 7, 9–16, 18–19 | M / medium (hot path; tests pin order) |
| S8 | StrokesPlus.net importer tidy (Q1: its merge stays) | Dead leftovers deleted (`ReadGestures` ×3, the 3-arg `ImportResult`, `GestureMerge.Apply` and the 2-arg `Plan`, unread `SourceStats` counts); the file read moves from `ImportViewModel` to `ImportPresenter`; one open-file picker shared with Transfer; the App README's stale SP.net section rewritten; importer README keeps the type table and merge policy, the six mapping tables move to `docs/reference/`, line 109 fixed. The shape-duplicate check is shared through S5's `ConfusionCheck.Duplicate`. | Imp 14–17, 19 | S / low, exact |
| S9 | App sync runtime | The six runtime files from `Hosting/` to `Sync/` (modules stay in Hosting); `SyncService` split into `SyncWorker` and `SyncScheduler`; one `SyncPhase` computed under the lock, three renderers read it through `SyncStatusText`; `SyncViewModel` keeps the projection, its drafts become a `DraftText` (checked against the other commit/revert pairs first); `SyncConflictsViewModel`'s text half to `SyncConflictSideText`; `Sync/README`. | Imp 3–4, 8, 12–13, 18, 20; Host 17 (sync part) | M / medium (lock and Dispose order) |

**Wave 4: App structure (sequential; these share files)**

| # | Slice | What | Findings | Size / risk |
|---|---|---|---|---|
| S10 | Dialogs | One `DialogHost` (owner, show owned or alone, show-replacing); `FormDialogRequest` gains `Width` and the activate rule, `SyncJoinPresenter` uses it; a lookless `DialogShell` (or a `Content` slot on `FormDialog`) hosts Confirm, UsedBy, SyncConflict and the import review, Escape once; one `IConfirmPresenter` with `ConfirmAsync` + `InformAsync`, the take-over presenter folded in; `Dialog.*` size tokens; a `Dialogs/` folder; `UsedBy/` keeps the popup. Kept rules: a confirmation with no visible main window answers no; which alone-shown dialogs activate stays per call site. | Imp 6–7; Comp 6–7, 16, 19; VM 17, 23; Host 2, 13 | M / low–medium |
| S11 | View-model base and the UI thread | `UiThread` (Post, RunOrPost) and one marshal registered in the root; `StoreViewModel` base (Message, Guard with the exception set, store change → UI, `DeleteAsync`, the export null branch); `OneShotTimer` for save, sync and stroke detection; `SettingsFollower` for the three "remember, diff, push" listeners; `CommandsModule.Create` stops wiring at runtime. | VM 4–5, 19, 22; Host 4, 9, 16 | M / low |
| S12 | Hosting and the composition root | `EngineModule` split into `ConfigModule`, `PlatformModule`, `EngineModule`; `FormServices.Publish` for the resource keys components look ports up by (one `ComponentPorts` resource, `OpenAppStepForm` stops reading Hosting); tab modules stop re-registering siblings' stores and presenters; every tab from a module `NavEntry` (Options and Diagnostics get modules); `LaunchArguments` parsed once in `Program.Main`; `AppState` split from `StartAtLoginLink`; `SingleInstanceGuard` split into listener and client under `Hosting/Instance/`; `Hosting/Modules|Launch|Startup/`; `AppTray` registered, not `new`ed; `MainWindowFactory` shared with UiShots and `NavigationRegistry.LeafKeys`; `TrainingSession` and its records to `Core/Training/` with the marshal injected, `TrainingRouting` to Engine; `Views/` keeps `MainWindow`; `EngineKeyCapture`/`EngineButtonCapture` to Hosting; dead members; a startup-order README. | Host 3, 5, 8, 10–12, 17–19; VM 14, 16, 23; Comp 8, 16 | L / low–medium (`EngineModuleTests` pin the surface; disposal order kept) |
| S13 | View models | One `PanelEdit` for the four side-panel lifecycles (the "rebuild only when another item is selected" rule kept); one `UseOnEdit`; one `CheckedSet`; `GestureBindings` becomes `MappingStore.UnbindGesture/RetargetGesture` in Core (Q8); one `CommandNames.Label` (Q9); `AppMatcher.Summary(platform)` in Core for the four summaries; the App calls `CategoryRules.Carried` and `StepOffer.Fitted` from S4; the unreachable Global rename/delete branches go (Q10); two field kinds (`ButtonsField`, `CommittedTextField`) replace the hand-laid controls in four screens; an `IDialogForm` for the five dialog flows; `ListSpec.ContextMenu/Keymap/ListKey` and `ListOrdering.UserOrdered` deleted; `FormChanges` beside `DelegateBinding`; the empty property name for "everything changed"; `ViewModels/`, `ViewModels/Commands/` (intents table, trigger-draft states), `ViewModels/Ignored/`, `Screens/`, `Declarations/`, `Navigation/` READMEs. | VM 1–3, 7–8, 10, 13, 15, 18, 20–22; Core-A 7–8 (App side) | L / medium (focus-keeping behaviour; existing tests cover) |
| S14 | Components | `GestureTile : ItemRow` (one rename editor); one `ActionMenu<TAction>` and one `EditKeymap` (Q11); `ItemRow`, `AskingDropdown` and the keymap to neutral folders, `SyncConflictRow` uses `AskingDropdown`; `CommandHeader.NotIn/AlsoIn` one parameterised row; `CommandHeaderItem` so rows stop carrying header-only fields; the `From` factories and `Summarise` to `ViewModels/Commands/Projection/` (the wording enums stay with the components); `StepFormState<TStep>` and `HotkeyChoices` for the step forms (Delay and MediaKey stop emitting a no-op edit, as the others already do); `HotkeyCapture/`, `ButtonDetect/` and the Engine adapters under one `Capture/` folder; `CommandTree/` split from `CommandHeader/`; `TemplateParts.OnClick` and `ToolbarButton.Create`; dead code (`GestureGlyph.Geometry`, `StepDragReorder.IsDragging`); a Capture-fields gallery page; `Components/README` with the five kinds and the editable-list contract, and one README per component folder. | Comp 1–3, 5, 9–12, 14–15, 17–18, 20, 22 | L / low–medium |
| S14b | One list host | `RowListHost` by composition (the ListBox, selected-id sync, rebuild with focus restore, right-click selects, the editing stand-down, one double-click rule) under CommandTree, MasterDetail, StepList, GestureGrid, GesturePicker. Its own slice: focus and selection timing is where past incidents were; the five test files must pass unchanged. | Comp 4 | L / medium |
| S15 | `CommandsViewModel` split | `TriggerEditor`, `SectionPanel` (after S13's `PanelEdit`), `StepEditor` as internal collaborators; the view model keeps dispatch, selection, projection; the public surface `CommandsTestData` builds stays. Last, because S11, S13 and S14 all touch it. | VM 6 | L / medium |

**Wave 5: tests and close-out**

| # | Slice | What | Findings | Size / risk |
|---|---|---|---|---|
| S16 | Shared test fakes (Q2) | `tests/Augram.TestSupport` with one fake per port (the Engine's as the base); the gallery's fakes and the tests' become one set under `DevGallery/` or there; `RecordingEventLog` deleted; ADR-0002 §1 gains the line. | Core-A 16; Host 14–15 | M / low |
| S17 | Themes tidy and docs sweep | From the Wireframe fold's notes: unused tokens pruned (`Header.LabelWidth`, Wireframe-only `Brush.*`), the moved rules that still use fixed `StaticResource` brushes (tile-partner box, cleaned stroke, swatch ring, `Border.row`) switched to the theme's dynamic brushes and checked in the light shots, `DrawsTitleBar` always true so `MainWindow` stops branching, the `Forms.axaml` header comment; UiShots skips the log tab in byte compares. Then the sweep: `src/Augram.App/README.md` cut to a map pointing at the folder READMEs; the Platform READMEs and ADR-0002 §2 list `MacStatusItem`, `MacDock`, `MacLaunchEvent` as the documented App-facing exceptions; CLAUDE.md's read-first table; this plan to `completed/`; the timing measurement repeated. | fold report; Eng 17; Imp 18 | S / low |

**Deferred out of this plan** (real findings, wrong moment):
- **The two trail overlays are one class written twice** (Host 1): `ITrailSurface` becomes the one abstraction and `TrailOverlayWindow` an implementation of it. Invariant 6 code, Windows-only path, medium-high risk. After the Mac runs the current build, so one surface is stable while the other is folded; Joel runs the first stroke; the lead does it, never an agent.
- **The three macOS observer trampolines → one `ObjCTarget`** (Eng 6): after the Mac session has verified `MacSystemEvents` and `MacLaunchEvent` live; nothing testable headless.
- **`AppMatcher`'s 29 hand-enumerated fields** (Core-A 15): with the next matcher-form change, keeping the JSON keys.
- **`CommandHeader` into `TriggerEditor`/`InputEditor` child controls** (Comp 10b): template surgery for a 1,460-line control; after S14 and S14b have settled.
- **A pure Core decider run on both the hook and the worker** (Core-B 13): the hook-side shadows are a deliberate second implementation of the two machines, guarded by equality tests and a live cross-check; it stays (Q3). Revisit only if a third rule change bites.

### Questions for Joel (each with the lead's recommendation)

Behaviour or ADR, so Joel's call. Everything else above is the lead's call and is already decided in the slice text.

1. **S8, the StrokesPlus.net import through the Augram review.** **Answered (Joel, 2026-10-11): no.** The SP.net import is a one-time migration tool that works; it only moves if that is clearly prudent. Lead's call: not now. The reader already turns SP.net's format into Augram records, and only the merge behind it duplicates Transfer; folding it is large, changes behaviour, and buys little for a tool that has done its job. S8 is a tidy instead. If the importer ever needs real work, route it through the Augram review then.
2. **S16, a shared `tests/Augram.TestSupport` project.** Outside ADR-0002's layout; needs one line in §1. *Recommend yes.*
3. **Core-B 13, the hook-side shadows.** Keep the shadow-plus-cross-check pattern as settled (and say so in the Capture README), or open the design to one decider run on both threads? *Recommend settled.* It is invariant-1 territory and the duplication is guarded.
4. **Core-A 13 / Core-B 18, the Remap step named outside its folder.** After S4 one or two type tests remain in HoldRemaps. Keep them as the one documented exception (Remap exists only for hold remaps), or add a step capability so nothing names a type? *Recommend the exception, documented in Steps/README.*
5. **Core-A 3, JSON strictness.** The mapping reader accepts a numeric enum string (`"buttons": "3"`); step parameters refuse one. When the readers fold, which rule? *Recommend refuse:* Augram never writes numeric strings, so no real file changes.
6. **Imp 2, `SyncFolders` to Core.** Core/Sync's README says deliberately that Core does not know which repository the state belongs to. Moving the URL-change reset into Core means Core knows a folder and a URL string, still no git. *Recommend move:* the `_stateCleared` flag in `SyncService` is the cost of Core not owning it.
7. **Eng 5, "Window activated" logged twice.** Keep the executor's line (`exec`, carries `focusMoved`, both platforms) and drop the Windows adapter's two final Info lines, or the other way round? *Recommend keep the executor's.* Which line do you read when you debug?
8. **VM 7, deleting a used gesture or merging duplicates.** Today each affected command is its own undo step; as a Core store method it would be one undo step for the whole action. *Recommend one step:* one action, one undo.
9. **VM 8, one command label everywhere.** "Used by" would show a hold remap in the path ("Blender › Space › Orbit") and the sync conflict text would show a Global command's category. Both change visible text. *Recommend yes.*
10. **VM 15, "the Global group cannot be renamed".** **Answered (Joel, 2026-10-11):** the categories under Global must stay renameable (they are groupings of Global commands); "Global" itself is a tab title and is never renamed. The refusal moves to `MappingRules`; the unreachable view-model branches go.
11. **Comp 3, one keymap.** **Answered (Joel, 2026-10-11):** F2 on Windows and Return on macOS everywhere, each platform's convention; on Windows, Enter stays as a second rename key wherever the Mac renames with Return, so it now works on the Gestures tab too ("we can at least try it").

### Order and who

Wave 1 first (S1 alone, then S2 and S3 together). Wave 2's three Core slices in parallel. Wave 3: S7, S8 and S9 in parallel. Questions not answered by the time their slice starts take the lead's recommendation by default; Joel can overrule any of them before or after. Wave 4 one at a time. Wave 5 last. The lead merges each slice after reading its diff; S7 gets a Fable review before merge; the lead runs the full suite only when main moved under a slice. A slice that finds it cannot keep behaviour stops and reports instead of choosing.
