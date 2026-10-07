# Augram.Import.StrokesPlus

Importer for StrokesPlus.net's live JSON (`%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`, UTF-8 with BOM; schema in `docs/reference/strokesplus-net-config.md` §2). Produces Core records plus an import report of what was skipped and why, and pure merge policies for bringing the result into an existing library and mapping (requirements F8).

**May reference:** `Augram.Core` only. `System.Text.Json` is BCL and fine.

**Must never contain:** UI, OS calls, SharpHook, or any package reference. The architecture test in `tests/Augram.Core.Tests/Architecture` checks this.

## Entry points

| Call | Produces |
|---|---|
| `StrokesPlusImporter.ReadGestures(string \| Stream \| StrokesPlusDocument)` | M1: `ImportResult` with `Gestures`, `Warnings`, `Stats` and the empty `Mapping` |
| `StrokesPlusImporter.ReadAll(…)` | M2 step 8: the same plus `Mapping`, a validated `MappingDocument` (Global group, app groups, ignored apps) whose gesture triggers reference `Gestures` by id; `AppGroupCount`, `CommandCount`, `PlaceholderStepCount`, `IgnoredAppCount` summarise it |

## Readers (one file per SP.net section)

| Type | Role |
|---|---|
| `StrokesPlusJson` | every SP.net member and method name used by the readers, in one place |
| `StrokesPlusDocument` | parses text or a stream tolerantly (BOM skipped, comments and trailing commas allowed); throws `ImportFormatException` only for malformed JSON or a non-object root |
| `JsonRead`, `MethodParameterReader`, `RegexAlternation` (internal) | the tolerant member reads every reader shares; `MethodParameters[]` as name → value text (numbers, bools and objects kept as JSON text); the "plain alternation of literals" regex splitter |
| `GestureReader` (internal) | `Gestures[]` to `Gesture` records: fresh `GestureId`, trimmed `Name`, `IsActive` from `Active`, one `GestureSample` per `PointPattern` ordered by `Order` |
| `StepReader` (internal) | one `Steps[]` entry to one `CommandStep` authored on Windows, per the table below; counts placeholders per method and reports them once per file |
| `ActionReader` (internal) | one `Action` to one `Command`: name = `Description` (fallback "Action N", unique in the group with a numbered suffix), trigger, modifier flags, steps, script, `Active`; hands back each action's `Category` name beside its command |
| `CategoryReader` (internal) | an application's `Categories[]` and its actions' `Category` names to the group's `CommandCategory` list and each command's `CategoryId`, per the table below |
| `MatcherReader` (internal) | the shared matcher fields to an `AppMatcher`, per the table below |
| `ApplicationReader`, `IgnoredApplicationReader` (internal) | `GlobalApplication` to the Global group's commands; `Applications[]` to `AppGroup`s (`NoGlobalActions` → `SuppressGlobals`); `IgnoredApplications[]` to `IgnoredApp`s (`DisableOnFocus` → `DisableEntirely`). An entry whose matcher ends up empty is imported **inactive** with a warning ("needs an app definition") |
| `MappingAssembler` (internal) | validates each group and ignored app on its own through `MappingRules`; a failing group is dropped with a warning (Global falls back to empty) and the rest still import |
| `SourceStatsReader` (internal) | counts gestures, samples, actions, steps, applications and ignored applications in the source |

## Step mapping (plan 0001 §C1, M2 slice)

| SP.net `Steps[].Method` | Augram step |
|---|---|
| `CloseWindow`, `MinimizeWindow`, `MaximizeOrRestoreWindow`, `ToggleWindowAlwaysOnTop` | `WindowOpStep` (Close, Minimize, MaximizeOrRestore, ToggleAlwaysOnTop) |
| `SetWindowSize(width, height)` | `WindowOpStep(SetSize, WindowSize)`; unparsable or non-positive size → placeholder + warning |
| `InvokeObjectMethodByName(methodName = Center)` | `WindowOpStep(Center)`; any other method → placeholder |
| `Delay(milliseconds)` | `DelayStep`, clamped to 0..60000 with a warning when clamped; unparsable → placeholder + warning |
| `SendVKey(virtualKey)` 173..179 | `MediaKeyStep` (173 VolumeMute, 174 VolumeDown, 175 VolumeUp, 176 NextTrack, 177 PreviousTrack, 178 Stop, 179 PlayPause); any other key → `HotkeyStep` with no modifiers through `HotkeyMapping` (VK → `KeyCode` table), placeholder when the key has no `KeyCode` |
| `SendHotKey(hotkey{LControl, RControl, LAlt, RAlt, LShift, RShift, LWin, RWin, Key})` | `HotkeyStep` through `HotkeyMapping.FromSendHotKey` (right-side modifiers are kept: `RAlt` alone becomes Alt with `RightHand` Alt, "RAlt+F9"; a modifier set on both sides is the plain one); placeholder when the key has no `KeyCode`. `HotkeyMapping.TryUpgrade(ImportedStep)` converts a placeholder saved before hotkeys existed |
| `SendKeys`, `SendString`, `Run`, `MouseClick`, `SendAltDown/Up`, `SendWinDown/Up`, `ConsumePhysicalInput`, unknown, null | `ImportedStep(method, step description, parameters as name → value text)`: shows as "not supported yet", runs (skips) now, and gets a real type later |
| a step with `Active == false` | `CommandStep.IsActive = false` |

One **Info** line per distinct placeholder method per file ("3 Run step(s) imported as placeholders; the Run step arrives in a later version."), never one per step.

## Action mapping

| SP.net | Augram |
|---|---|
| `GestureName` resolves (case-insensitive) to a gesture imported from the same file | `Trigger.ForGesture(id)`; SP.net binds by name, so a duplicated source name binds to the first |
| `GestureName` names nothing | `Trigger.None` + warning "gesture 'X' not found; command imported without a gesture" |
| `GestureName` empty and `WheelUp` / `WheelDown` | `Trigger.ForWheel`; both set → wheel up + warning |
| neither | `Trigger.None` + warning |
| a trigger already bound in the group (A7) | the active command keeps it; the other is imported with `Trigger.None` + warning |
| `Control`, `Alt`, `Shift`, `Left`, `Middle`, `Right`, `X1`, `X2` or `UseSecondaryStrokeButton` | imported **inactive**, `Note` "Imported from StrokesPlus.net: needs modifier/rocker support (deferred)" |
| `Steps` non-empty | one `CommandStep` per entry (a script beside steps is ignored, as SP.net ignores it) |
| `Steps` empty, `Script` non-empty | one `ImportedStep("Script", name, { script })` so the script is visible on the step list, plus `Note` "Imported from StrokesPlus.net: script-only action"; `Active` carried over |
| both empty | a command with no steps: the override to nothing, no warning |

## Category mapping (Joel, 2026-10-07)

| SP.net | Augram |
|---|---|
| `GlobalApplication.Categories[]`, `Applications[].Categories[]` (strings) | the group's `Categories`, fresh `CategoryId`s, the list's spelling (trimmed; empty and repeated names ignored, the first spelling wins) |
| `Action.Category` naming a listed category (case-insensitive) | the command's `CategoryId` |
| `Action.Category` empty or missing | Uncategorized (`CategoryId` null) |
| every action of the group in one category named `General` (or the group has no actions) | **no categories at all**, every command Uncategorized: SP.net gives every app "General", so this is noise (18 of the reference config's 19 apps) |
| a listed category no imported action uses | left out, no warning |
| `Action.Category` naming a category the list lacks | the category is created with the action's spelling; **Info** "Category 'X' is not in the application's category list; created for its commands.", once per category per group |

"General" beside other used categories is kept like any other (the reference Photoshop group: General plus four "Blend Mode …" categories).

## Matcher mapping (F5)

| SP.net | `AppMatcher` |
|---|---|
| `FileName` non-regex | `ProcessNames = [value]` |
| `FileName` regex that is a plain alternation of literals (`a\.exe\|b\.exe`, optionally in one group or anchored) | `ProcessNames` split into the names |
| `FileName` any other regex (`PotPlayerMini.*\.exe`) | warning; `ProcessNames` left empty (fill in by hand) |
| `FilePath` | `ProcessPath` + `ProcessPathIsRegex` |
| `RootWindowText`, else `OwnerWindowText`, `ParentWindowText`, `ControlWindowText` | `Title` + `TitleIsRegex`; a warning names the fields that differ from the one used |
| `OwnerClassName`, `RootClassName`, `ParentClassName`, `ControlClassName` non-regex | one `ClassChain` entry each; a plain-alternation regex becomes one `a\|b` entry; any other regex is skipped with a warning |
| `ControlID` set | warning, skipped |
| `IgnoreFullScreen` | `IgnoreWhenFullScreen` (A21) |
| a regex the engine refuses | dropped (path first, then title) with a warning, so the group still imports |

## Merge policies (pure; the App applies them)

| Type | Role |
|---|---|
| `GestureMerge` | `Plan(existing, imported)` classifies each imported gesture as `Add` or `Conflict` (name clash, case-insensitive). `Plan(existing, imported, RecognitionOptions)` also scores each addition's first sample against every existing gesture (inactive included) and makes a match at or above `ConfusionCheck.DuplicateCutOff` (90) a `SameShape` entry with the twin and the score (A7: a re-import reuses the existing gesture). `Apply`/`ApplyWithMap(plan, choices)` resolve conflicts with `KeepMine`, `TakeTheirs` (existing id kept, content replaced) or `KeepBoth` (a name clash is renamed `Name (imported)`; a shape match keeps its name); `ApplyWithMap` also returns `MergeOutcome.IdMap`, imported id → final id (existing id for KeepMine and TakeTheirs, own id for Add and KeepBoth) |
| `MappingImport` | `Rebind(document, idMap)` points the imported gesture triggers at the final ids. `Merge(existing, imported)` is **add-only in this slice**: groups merge by name (case-insensitive; Global always into Global; an existing group keeps its own matcher and flags), a command whose name or bound trigger is already taken in that group is skipped and counted, ignored apps merge by name; categories merge by name: an added command lands in the existing group's category of the same name (case-insensitive) or brings its own category along when the group has none by that name (a fresh id if its id is already taken there), so only categories an added command uses arrive, and a new group arrives with its categories whole; returns `MappingMergeResult` (validated document + groups added, commands added/skipped, ignored added/skipped). A per-group replace/overwrite choice is a later slice |

Report lines (`ImportWarning`): gesture with no usable sample skipped (warning); sample with fewer than 2 distinct points dropped (warning); duplicate source name imported as `Name (2)` (warning, for gestures, commands, apps and ignored apps alike); stock 2-point gesture detected (info only); no `Gestures` array (warning); no `GlobalApplication` (info); a category an action names but the list lacks (info); plus the rows above. Unknown members and nulls are ignored.

## Tests: no real data

Tests read only hand-written fixtures under `tests/Augram.Core.Tests/Fixtures/StrokesPlusNet/` (`sample-config.json` for gestures, `sample-config-full.json` for the whole mapping, including Global with several categories, apps with only "General" and an app with "General" plus another; every name except "General" starts with `Synthetic`). Joel's real `StrokesPlus.net.json` and the backups under `J:\` are never copied into the repo, referenced by path, or used as a fixture. If a real-world shape needs a test, reproduce it synthetically (the real file was read once to learn the member shapes: `MethodParameters[].Value` can be a string, a number, a bool, an object or null; one real step has a null `Method`; `Categories[]` is an array of strings and every real `Action.Category` is a string naming one of them).
