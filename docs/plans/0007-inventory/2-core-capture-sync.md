# Review: Core/Capture, Recognition, Gestures, HoldRemaps, Sync, Transfer, Diagnostics (+ their Core.Tests)

Read: CLAUDE.md, ADR-0002 (incl. 2026-10-11 amendments), all seven area READMEs, every .cs in the seven folders, the Engine shadows (`SuppressionShadow`, `KeySuppressionShadow`, `HoldRemapShadow`), `StrokeRecognizer`, `Import.StrokesPlus/GestureMerge|MappingImport|ImportedNames`, App `ImportViewModel`/`TrainingRouting`, `Config/SyncItemJson`, `Mapping/NameScope|CommandNames`, and the test support folders. Nothing was edited, built or run.

## Map

- **Capture**: `CaptureStateMachine` (two partials: buttons / motion+wheel+tick+keys), a pure object fed `CaptureEvent`s returning `CaptureOutcome`s; the value types it reads from the hook (`AnchorPlan`, `AnchorDragDistances`, `HeldButtons`, `PressHold`, `CaptureThresholds`). The hook never runs it: `Engine/Input/SuppressionShadow` re-derives only the Suppress/PassThrough column and the worker runs the machine for everything else.
- **Recognition**: verbatim MIT port in a pipeline `StrokeResampler → AngleSequence → SampleScorer → GestureMatcher` with `TemplateCache`; `ConfusionCheck` is the A7 "same shape" rule on top. Rotation-sensitive by design; runs on button-up only (Engine `StrokeRecognizer` calls `Rank`).
- **Gestures**: model (`Gesture`, `GestureSample` raw points), `GestureRules` (the one home of the rules), `GestureLibrary` (store with undo), `StarterGestures`, and `Cleanup/` (ShortStraw corner finder + piece fitting, wrapped by `GestureCleanup` for a whole gesture).
- **HoldRemaps**: model + `HoldRemapRules` (run by `MappingRules`), `HoldRemapPlan/Entry/Binding/KeySet` (what the hook reads, immutable), and `HoldRemapMachine` (pure, like the capture machine). `Engine/Input/HoldRemapShadow` mirrors the machine on the hook thread.
- **Sync**: a document is split into `SyncItem`s whose identity is text content (`Config/SyncItemJson` writes it with the config file's writers); `ThreeWayMerge.Decide` is the one merge table; `SyncDocumentBuilder` + `CommandPlacement` rebuild and repair; `SyncPlanner` chains per-machine merges with base bookkeeping; `SyncCoordinator` orchestrates the port, `SyncBaseStore` (local state files), `SyncStoreWriter` (store-thread apply), `SyncPublisher`.
- **Transfer**: an export is the config file minus members (`Exporter`, `TransferSerializer` over Config's writers/readers); an import is the sync merge with an empty base: `ImportMatcher` lines ids up, `ImportPlan` classifies items New/Same/Different, `ImportResolution` applies choices then `SyncDocumentBuilder.Build`, `ImportResult.ApplyTo` commits.
- **Diagnostics**: data only (`LogEvent`, `EventLogExtensions`, `RingLog<T>`, `RecognitionLog`, `HealthRegistry/Snapshot`); the Engine owns sinks and queueing.

## Findings (most valuable first)

**1. Kind 1 — Three "merge with Keep mine / Take theirs / Keep both" implementations.**
Where: `src/Augram.Core/Sync/ConflictResolution.cs:20-58` (one conflict), `src/Augram.Core/Transfer/ImportResolution.cs` (all choices at once), and `src/Augram.Import.StrokesPlus/GestureMerge.cs` (`Plan`/`ApplyWithMap`, with its own `MergeChoice` = `SyncChoice`, `MergeKind`, `MergePlan`, `MergeEntry`, `MergeOutcome`) plus `src/Augram.Import.StrokesPlus/MappingImport.cs:34-93` (groups/categories/ignored by name, commands skipped when name or trigger taken) and `MappingMergeResult`; driven from `src/Augram.App/ViewModels/ImportViewModel.cs:119-220`. The StrokesPlus path predates Transfer and behaves differently (a taken command is *skipped*, not renamed/unbound as the sync and the Augram import do; categories merge by name only).
Recommendation: make the StrokesPlus importer end in a `TransferFile` (gestures + mapping it already assembles with `MappingAssembler`) and run it through `ImportPlan` with `ImportOptions.MatchShapes`; delete `GestureMerge`, `MappingImport`, `MergeChoice/Kind/Plan/Entry/Outcome`, `MappingMergeResult` and `ImportViewModel`'s merge half (App's `AugramImportViewModel` already reviews an `ImportPlan`). Size L. Risk medium: the SP.net review screen and its log line change; command-skip becomes rename/unbind. Not behaviour-preserving (needs Joel, see questions).

**2. Kind 1 — The A7 shape-duplicate check is written twice.**
Where: `src/Augram.Core/Transfer/ImportMatcher.cs:61-79` (`MatchShapes`) and `src/Augram.Import.StrokesPlus/GestureMerge.cs:103-117` (`ShapeChecked`): both force candidates active, `Rank` the first sample, take best ≥ `ConfusionCheck.DuplicateCutOff`.
Recommendation: one `ConfusionCheck.Duplicate(sample, candidates, options, matcher)` (or `GestureMatcher.FindDuplicate`) in Recognition, both call it (finding 1 removes one caller anyway). S, low, exact.

**3. Kind 1+3 — Engine re-implements `GestureMatcher.Match`'s threshold rule; `Match` has no caller outside tests.**
Where: `src/Augram.Engine/Hosting/StrokeRecognizer.cs:59` (`ranked[0].Score > options.Threshold && ranked[0].Score > 0`) vs `src/Augram.Core/Recognition/GestureMatcher.cs:58-67`. ADR §5a: "this stroke matches gesture X" is a Core function.
Recommendation: add `GestureMatcher.Best(ranked, options)` (Match = Best(Rank())) and have StrokeRecognizer call it. S, low, exact.

**4. Kind 1 — Engine `KeyBits` duplicates Core `KeySet`.**
Where: `src/Augram.Engine/Input/HoldRemapShadow.cs:361-400` (4×ulong, `Has/With/Without/IsEmpty`) vs `src/Augram.Core/HoldRemaps/KeySet.cs` (4×ulong, `Contains/With/IsEmpty`).
Recommendation: add `Without` to `KeySet`, delete `KeyBits`. S, low. Note the one difference: `KeySet` ignores values outside 0..255, `KeyBits` folds them onto bit 0 (`KeySuppressionShadow.Slot` does the same); pick one.

**5. Kind 1 — Sync and Transfer each apply choices to items, with a duplicated `Copy`.**
Where: `ConflictResolution.Apply` (`Sync/ConflictResolution.cs:20-58`, `Copy` :52) and `ImportResolution.Take/Copy/Rebound` (`Transfer/ImportResolution.cs:69-150`): both remove-or-replace in place for Take theirs, append a new-id copy for Keep both, then `SyncDocumentBuilder.Build(items, incoming, local)`. The import adds gesture-copy rebinding and own-version following.
Recommendation: generalise `ConflictResolution.Apply` to a list of (conflict, choice) with the import's two extras; `SyncCoordinator.ResolveLocked` passes one, `ImportPlan.Resolve` passes all. M, medium (covered by ThreeWayMergeTests and ImportPlanTests; the "Keep mine ≡ Merge(Empty, …)" test holds the import side). Exact if the extras are kept.

**6. Kind 1 — Store apply written twice.**
Where: `src/Augram.Core/Transfer/ImportResult.ApplyTo` (`ImportResult.cs:77-103`) and `src/Augram.Core/Sync/SyncStoreWriter.Apply` (`SyncStoreWriter.cs:45-73`): same reference-equality staleness check, same `Counts.Gestures.Total > 0` / `Counts.MappingChanged` → `ReplaceAll` per store.
Recommendation: one store-thread apply in Sync (the writer's, made callable without the retry loop) that `ImportResult.ApplyTo` calls, then applies settings. S, low, exact.

**7. Kind 1 — The import computes New/Same/Different twice, and it is `ThreeWayMerge.Decide` with a null base.**
Where: `Transfer/ImportPlan.cs:41-58` (ctor) and again `Transfer/ImportResolution.cs:76-86` (`Take` re-finds `_local.Find` and re-compares content); the Transfer README itself states Keep-mine equals `Merge(SyncItemSet.Empty, mine, file)`.
Recommendation: `ImportResolution` reads the plan's `Entries` statuses instead of recomputing; optionally derive the statuses through `ThreeWayMerge.Decide(null, mine, theirs)` so the merge table has one home. S, low, exact.

**8. Kind 1 — "Rename the incoming item ' (2)' and add a repair line" is written six times.**
Where: `Sync/SyncDocumentBuilder.cs:55-68` (gestures), `:73-86` (ignored), `:89-117` (groups), `:119-145` (categories), `Sync/SyncDocumentBuilder.HoldRemaps.cs:19-50`, `Sync/CommandPlacement.cs:104-125` — each a loop over `IncomingLast`, `NameScope.Claim`, a `SyncRepairKind.Renamed` line, `with { Name }`.
Recommendation: one generic helper (items, incoming, scope-of, name/with-name, sentence) — the per-kind sentence stays. S, low, exact.

**9. Kind 1 — The "Global shell" (header-only group) is built three ways and tested a fourth.**
Where: `Sync/SyncItem.cs:183` (`GroupItem.Header`), `Config/SyncItemJson.cs:31` (`GroupHeader`), `Transfer/Exporter.cs:698` (`Selected`); `Transfer/TransferContents.cs:80-84` (`IsShell`) is the inverse, used by `ImportPlan.Create`.
Recommendation: `AppGroup.HeaderOnly()` and `AppGroup.IsShell` in Mapping (or on the sync item), used by all four. S, low, exact.

**10. Kind 1 — `CapturePoint → GesturePoint` conversion in Engine and App.**
Where: `src/Augram.Engine/Hosting/StrokeRecognizer.cs:43-47` and `src/Augram.App/Training/TrainingRouting.cs:26-34`, identical loops.
Recommendation: one extension in Capture (`ToGesturePoints(this IReadOnlyList<CapturePoint>)`), or have `EngineEvent.GestureRecognized/NoMatch` carry the converted points once. S, low, exact.

**11. Kind 1 — Geometry helpers repeated inside Core.**
Where: `Recognition/StrokeResampler.cs:78-96` (`PathLength` public, `Distance` private) vs `Gestures/Cleanup/ShapeCleanup.cs:380-395` (`PathLength`), `:431` (`Distance`), and `StarterGestures.Segment` (`StarterGestures.cs:62`) computing its own length. `StrokeResampler.PathLength` has no caller outside its file.
Recommendation: `ShapeCleanup` and `StarterGestures` use `StrokeResampler.PathLength` and a shared `Distance` (e.g. `GesturePoint.DistanceTo`). Keep `ShapeCleanup.ResampleEvenly` (spacing-based; the verbatim count-based port must not change). S, low, exact.

**12. Kind 2 / ADR §6 — `ShapeCleanup.cs` is 436 lines with three public types and no reason comment.**
Where: `src/Augram.Core/Gestures/Cleanup/ShapeCleanup.cs` (`PieceKind` :4, `CleanedShape` :20, `ShapeCleanup` :30).
Recommendation: `PieceKind.cs`, `CleanedShape.cs`, and partials `ShapeCleanup.cs` (pipeline) / `ShapeCleanup.Corners.cs` / `ShapeCleanup.Fitting.cs`, as the machines are split. S, low, exact.

**13. Kind 1 (deliberate) — The hook-side shadows are a second implementation of both Core machines' decision tables.**
Where: `src/Augram.Engine/Input/SuppressionShadow.cs` (174 lines) mirrors `Capture/CaptureStateMachine`'s Suppress/PassThrough column; `src/Augram.Engine/Input/HoldRemapShadow.cs` (438 lines) mirrors `HoldRemaps/HoldRemapMachine` (~400 lines) field for field (`_remap, _holding, _rolledOver, _used, _downAt, _owed, claimed, replayed, _ended`). Justified in the Engine README (the machine lags the hook by the queue depth) and guarded by equality tests plus a live cross-check, but every rule change is made twice (plan 0005 and the focus-moved rule both were).
Recommendation: leave in this refactor; but see question 2 — a pure Core "decider" (the state the decision needs + `Decide(event) → Suppress/PassThrough`) that both the hook (synchronously, O(1), no allocation) and the worker run would remove the duplication without touching invariant 1. L, high risk (hot path, pairing invariant), only with Joel's say-so.

**14. Kind 4 — Dead outside tests** (counted across `src/`): `CaptureStateMachine.IsOtherButtonDown` (`CaptureStateMachine.cs:104`), `PressHold.OfStroke` (`PressHold.cs:30`), `HealthRegistry.ContributorCount` (`HealthRegistry.cs:18`), `SyncItemKey.Parse` (`SyncItemKey.cs:334`; `TryParse` is used), `HoldRemapPlan.For(mapping, foreground, platform)` (`HoldRemapPlan.cs:306`; Engine calls `ForGroup`), `SyncItemSet.CountOf` (`SyncItemSet.cs:421`), `TemplateCache.Clear` and the `GestureMatcher(TemplateCache)` ctor (`TemplateCache.cs:37`, `GestureMatcher.cs:21`), `RingLog.Clear` (`RingLog.cs:74`), `KeySet.Empty`/`IsEmpty` (`KeySet.cs:12-14`), `GestureMatcher.Match` (see 3).
Recommendation: delete (update the HoldRemaps README row for `For`). S, low, exact.

**15. Kind 5 — Recognition README is wrong about the default.** `src/Augram.Core/Recognition/README.md:15` says `ScoringMode.Legacy` "is the default"; `RecognitionOptions.cs:14`, `ScoringMode.cs` and the `AngleSequence.cs` header say Corrected is (Joel, 2026-10-09). Fix the README. S.

**16. Kind 5/1 — Three lists of log sources that disagree.** `Diagnostics/README.md:23` (`hook, capture, recognition, exec, ignore, steps, config, overlay, import, app, window, log`), `Diagnostics/LogEvent.cs:7` (names `activate`, which no longer exists), `src/Augram.Engine/Hosting/LogSources.cs` (`hook, capture, recognition, engine, exec, ignore, hold`), and `SyncCoordinator.LogSource = "sync"`. An agent adding a source cannot tell which is canonical.
Recommendation: one `LogSources` constants class in Core/Diagnostics (Engine's moved there, `sync` added), README and `LogEvent` doc point at it. S, low, exact (values unchanged).

**17. Kind 5 — Diagnostics README says `RecognitionCandidate` is `(Name, Score)`; it is `(Name, Score, Id)` (`RecognitionCandidate.cs:9`). S.**

**18. Kind 3 — HoldRemaps type-tests a step kind.** `HoldRemaps/HoldRemapPlan.cs:117` (`step.Stored.Step is RemapStep`) and `HoldRemaps/HoldRemapRules.Commands.cs:133` (`step.Step is RemapStep`), plus App `CommandsViewModel.Input.cs:66`. ADR §4 forbids switching over step kinds; `Transfer/TransferContents` shows the sanctioned way (`IStepType.MayHoldPrivateText`).
Recommendation: a capability on `IStepType` (e.g. `HoldOutput`) if the plan wants the rule kept strict; otherwise leave, since the Remap step exists only for hold remaps. S, low.

**19. Kind 2 — `ImportMatcher.Gestures` hand-writes what `ImportMatcher.Pair` generalises.** `Transfer/ImportMatcher.cs:39-58` repeats the claimed-set / first-unclaimed-twin-by-name loop that `ImportMatcher.Mapping.cs:231-259` does generically, then adds the shape pass.
Recommendation: `Gestures = Pair(name rule)` + the shape pass (finding 2). S, low, exact.

**20. Kind 5 — Sync's file and item JSON live in Config, out of sight.** `src/Augram.Core/Config/SyncItemJson.cs` (155 lines, used only by `Sync/SyncItem.cs`), `SyncFile.cs`, `SyncFileHeader.cs`, `SyncFileSerializer.cs`, `SyncAcknowledgement.cs` sit in Config because Config must not reference Sync. `SyncItemJson` depends only on Config's writers and nothing in Config uses it, so it can move to `Sync/` without a cycle; the file serializer is also used by `Transfer` and `Config/SyncFile`, so it stays.
Recommendation: move `SyncItemJson` to `Sync/`; give the remaining sync-file types a `Config/Sync/` subfolder (or a README row) so an agent starting in Sync finds "how an item's content is written". S, low, exact.

**21. Kind 5 — Test fixtures build the same world twice.** `tests/Augram.Core.Tests/Sync/Support/SyncSamples.cs` and `tests/Augram.Core.Tests/Transfer/Support/TransferSamples.cs` both make Up/Down/Left, Global with Window/Minimize/Close, Chrome/Close tab, Game (Transfer's adds Blender and settings). Fold Sync's `Setup` into a parameterised `TransferSamples` (or vice versa). S, low.

**22. Kind 5 — Map gaps an arriving agent hits** (beyond 15–17, 20): the Capture README does not say that the Engine's `SuppressionShadow`/`KeySuppressionShadow` exist and must be changed in step with the contract table (the HoldRemaps README does say it for its shadow); the Transfer README's "Reading" row does not name `TransferContents.IsShell`'s role in dropping the Global header, which `ImportPlan.Create` relies on; the Sync README should say in one line where the *one* merge decision is (`ThreeWayMerge.Decide`) and that `ImportPlan` is that table with no base (finding 7). S each.

Checked and dropped: `GestureRules`/`MappingRules`/`NameScope`/`CommandNames` are used as the single rule homes everywhere I looked (App, Sync, Transfer, StrokesPlus importer all claim through `NameScope`); `SyncCounts.Between` is shared by Sync and Transfer; `HoldRemapRules.Detached` is reused by `CommandPlacement`; `GestureCleanup.FromStroke` is used by `TrainingSession` rather than re-done; no Core file references anything forbidden; no business rule found re-implemented in App view models for this area.

## Questions for Joel

1. (Finding 1) May the StrokesPlus.net import go through the Augram import (`ImportPlan`)? Behaviour change: a command whose name or trigger is already taken would be renamed " (2)" / unbound with a repair line, as a sync does, instead of skipped and counted; the SP.net review screen would become the Augram import review.
2. (Finding 13) Is the hook/worker split for the two machines open to a "pure decider in Core, run on both threads" design, or is the shadow-plus-cross-check pattern settled? If settled, the Capture README should say so (as the HoldRemaps README does) and the duplication is accepted.
3. (Finding 18) Does ADR §4's "no switch over step kinds" apply to the hold-remap subsystem's knowledge of the Remap step, or is Remap an intrinsic part of hold remaps and exempt?