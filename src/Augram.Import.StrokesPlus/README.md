# Augram.Import.StrokesPlus

Importer for StrokesPlus.net's live JSON (`%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`, UTF-8 with BOM; schema in `docs/reference/strokesplus-net-config.md` §2). Produces Core records plus an import report of what was skipped and why, and a pure merge policy for bringing the result into an existing library (requirements F8).

**May reference:** `Augram.Core` only. `System.Text.Json` is BCL and fine.

**Must never contain:** UI, OS calls, SharpHook, or any package reference. The architecture test in `tests/Augram.Core.Tests/Architecture` checks this.

## Imported today (M1 step 10): gestures

| Type | Role |
|---|---|
| `StrokesPlusJson` | every SP.net member name used by the readers, in one place |
| `StrokesPlusDocument` | parses text or a stream tolerantly (BOM skipped, comments and trailing commas allowed); throws `ImportFormatException` only for malformed JSON or a non-object root |
| `GestureReader` (internal) | `Gestures[]` to `Gesture` records: fresh `GestureId`, trimmed `Name`, `IsActive` from `Active`, one `GestureSample` per `PointPattern` ordered by `Order` |
| `SourceStatsReader` (internal) | counts gestures, samples, actions (global + per app) and applications, so the UI can say "found 90 gestures, 212 actions (actions import comes later)" |
| `StrokesPlusImporter` | entry point: `ReadGestures(string | Stream | StrokesPlusDocument)` composes the readers into an `ImportResult(Gestures, Warnings, Stats)` |
| `GestureMerge` | `Plan(existing, imported)` classifies each imported gesture as `Add` or `Conflict` (name clash, case-insensitive); `Apply(plan, choices)` resolves conflicts with `KeepMine`, `TakeTheirs` (existing id kept, content replaced) or `KeepBoth` (renamed `Name (imported)`) |

Report lines (`ImportWarning`): gesture with no usable sample skipped (warning); sample with fewer than 2 distinct points dropped (warning); duplicate source name imported as `Name (2)` (warning); stock 2-point gesture detected (info only; SP.net ships `Up` as (0,500)→(0,100)); no `Gestures` array (warning). Unknown members, nulls and `MultiPointPatterns: null` are ignored.

## Planned (plan 0001 §C1)

Actions and applications land as further reader files beside `GestureReader` (`ActionReader`, `ApplicationReader`, `IgnoredApplicationReader`) and the importer composes them; the step mapping table, dangling gesture references (warn, import without a gesture) and the settings keys are all in §C1. Nothing in the gesture path changes when they arrive.

## Tests: no real data

Tests read only hand-written fixtures under `tests/Augram.Core.Tests/Fixtures/StrokesPlusNet/` (gesture names start with `Synthetic`). Joel's real `StrokesPlus.net.json` and the backups under `J:\` are never copied into the repo, referenced by path, or used as a fixture. If a real-world shape needs a test, reproduce it synthetically.
