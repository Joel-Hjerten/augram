# Augram.Import.StrokesPlus

Importer for StrokesPlus.net's live JSON (`%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`, UTF-8 with BOM; schema in `docs/reference/strokesplus-net-config.md` §2). Produces Core records plus an import report of what was skipped and why, and pure merge policies for bringing the result into an existing library and mapping (requirements F8).

**May reference:** `Augram.Core` only. `System.Text.Json` is BCL and fine.

**Must never contain:** UI, OS calls, SharpHook, or any package reference. The architecture test in `tests/Augram.Core.Tests/Architecture` checks this.

## Entry points

| Call | Produces |
|---|---|
| `StrokesPlusImporter.ReadAll(string \| Stream \| StrokesPlusDocument)` | `ImportResult` with `Gestures`, `Warnings`, `Stats` (the gestures in the source) and `Mapping`, a validated `MappingDocument` (Global group, app groups, ignored apps) whose gesture triggers reference `Gestures` by id; `AppGroupCount`, `CommandCount`, `PlaceholderStepCount`, `IgnoredAppCount` summarise it (M2 step 8) |

## Readers (one file per SP.net section)

| Type | Role |
|---|---|
| `StrokesPlusJson` | every SP.net member and method name used by the readers, in one place |
| `StrokesPlusDocument` | parses text or a stream tolerantly (BOM skipped, comments and trailing commas allowed); throws `ImportFormatException` only for malformed JSON or a non-object root |
| `JsonRead`, `MethodParameterReader`, `RegexAlternation` (internal) | the tolerant member reads every reader shares (`JsonRead.Name`: an item's name, or "Action 3", "App 2"… when blank); `MethodParameters[]` as name → value text (numbers, bools and objects kept as JSON text); the "plain alternation of literals" regex splitter |
| `ImportedNames` (internal) | the names one kind of item takes in one scope (gestures, app groups with "Global" taken, ignored apps, one group's commands): `Claim(name)` renames a repeat `Name (2)`, `Name (3)`… by Core's rule (`Core/Mapping/NameScope`, case-insensitive) with one warning "Duplicate *kind* name; imported as '…'.". A reader that names items claims through it rather than keeping its own set |
| `GestureReader` (internal) | `Gestures[]` to `Gesture` records: fresh `GestureId`, trimmed `Name`, `IsActive` from `Active`, one `GestureSample` per `PointPattern` ordered by `Order` |
| `StepReader` (internal) | one `Steps[]` entry to one `CommandStep` authored on Windows, per the mapping doc; counts placeholders per method and reports them once per file |
| `ActionReader` (internal) | one `Action` to one `Command`: name = `Description` (fallback "Action N", unique in the group with a numbered suffix), trigger, steps, script, `Active`; hands back each action's `Category` name beside its command |
| `TriggerReader` (internal) | an action's trigger with its keys, buttons and capture mode, and the file's `SecondaryStrokeButton` (mapping doc) |
| `CategoryReader` (internal) | an application's `Categories[]` and its actions' `Category` names to the group's `CommandCategory` list and each command's `CategoryId`, per the mapping doc |
| `MatcherReader` (internal) | the shared matcher fields to an `AppMatcher`, per the mapping doc; `ReadWithActive` adds the entry's `Active` flag, false with the "needs an app definition" warning when the matcher ends up empty (app groups and ignored apps alike) |
| `ApplicationReader`, `IgnoredApplicationReader` (internal) | `GlobalApplication` to the Global group's commands; `Applications[]` to `AppGroup`s (`NoGlobalActions` → `SuppressGlobals`); `IgnoredApplications[]` to `IgnoredApp`s (`DisableOnFocus` → `DisableEntirely`). An entry whose matcher ends up empty is imported **inactive** with a warning ("needs an app definition") |
| `MappingAssembler` (internal) | validates each group and ignored app on its own through `MappingRules`; a failing group is dropped with a warning (Global falls back to empty) and the rest still import |
| `SourceStatsReader` (internal) | counts the gestures in the source (`SourceStats.GestureCount`, the dialog's "Found N gestures") |

## SP.net → Augram mapping

The six mapping tables (steps, SendKeys syntax, script actions, actions and triggers, categories, matchers) are the importer's spec and live in [docs/reference/strokesplus-net-import-mapping.md](../../docs/reference/strokesplus-net-import-mapping.md). Change a table there in the same commit as the reader it describes.

## Text methods (`SendKeys`, `SendString`)

`TextMapping` is the `HotkeyMapping` of the text methods; `StepReader` and `PlaceholderUpgrade` call it. Pure and stateless.

| Call | Produces |
|---|---|
| `TextMapping.FromSendKeys(parameters, textMethod)` | `sendKeysString` through `SendKeysSyntax.Parse`: a `SendKeysResult` (`Steps`, `Warnings`, `IsClean`, and `IsComplete`: clean and at least one step, the one test every caller uses before replacing a placeholder); null when the parameter is missing or empty. One SP.net step becomes several Augram steps, so the caller splices them in |
| `TextMapping.FromSendString(parameters, method)` | `characters` as one `TypeTextStep`, exactly as written (SendString is text, not key syntax); null when missing or empty |
| `TextMapping.TryUpgrade(ImportedStep, textMethod)` | a saved `SendKeys` or `SendString` placeholder as the steps that replace it; null for any other method |

`SendKeysSyntax.Parse(keys, textMethod)` (the state machine is the internal `SendKeysParser`, the token names `SendKeysNames`) reads .NET `SendKeys` syntax plus the classic StrokesPlus extras; it never throws, it reports a part it cannot map in `Warnings` (one sentence each, naming the part; the caller adds the action's name) and goes on. The syntax it reads is in the mapping doc.

## Run helpers

Pure; `StepReader` (the `Run` step) and `ScriptMapping` (a script-only action, for `ActionReader` and `PlaceholderUpgrade`) call them. A recognised `sp.RunProgram` call goes through `ProgramCallMapping.ToStep`, the one place that decides which step a call becomes.

| Type | Role |
|---|---|
| `RunMapping` | `FromRun(parameters)`: the `Run` step's one parameter `command` to a `RunStep` (null when missing or blank); `SplitCommand` cuts a command line into file and arguments the way a person reads it (a quoted first part; a URI whole; the shortest run of words ending in `.exe`, `.com`, `.bat`, `.cmd`, `.lnk`, `.msc` or `.cpl`, so `C:\Program Files\x\x.exe -flag` keeps its path; a rooted path with no such ending whole, for documents and folders with spaces; otherwise the first blank). `FromRunProgram(call)`: verb `runas` → elevated, style `hidden` → hidden; `noWindow`, `waitForExit`, the styles minimized and maximized and verbs other than open/runas have no equivalent and are dropped (`RunProgramCall.HasPlainVerb` says when a verb would be lost). `TryUpgrade(ImportedStep)` for a saved `Run` placeholder |
| `RunProgramScript` | `TryRecognize(script, out call, out reason)`: true when the script's only statement, comments and blanks aside, is one `sp.RunProgram(fileName, arguments, verb, style, useShellExecute, noWindow, waitForExit)` call with literal strings (quotes, backticks, `String.raw` templates, `+` joins, JavaScript escapes) and `true`/`false` flags; in the file name `sp.ExpandEnvironmentVariables("%X%")` reads as `%X%`, which the Run step expands itself. Anything else is refused with a reason for the report |
| `RunProgramCall` | the seven arguments; `IsElevated`, `IsHidden`, `HasPlainVerb` |
| `ScriptReader` (internal) | the cursor over the script: trivia, words, string literals with their escapes, raw templates, and `TryToken` (one word, number, string or character at a time) for `ScriptTokens` |

Against the reference config (read 2026-10-08, never copied): both `Run` steps map (`explorer`; `ms-settings:display`, an inactive action), and 13 of the 14 scripts that mention `RunProgram` are recognised: ten display-changer calls (the Display step's to route), the elevated hidden taskkill, SP.net's `String.raw` example and the `sp.ExpandEnvironmentVariables("%SystemRoot%")+"\\explorer.exe"` script beside the Open Explorer step (SP.net ignores a script beside steps). The one refused is SP.net's "Process.Start" example, which drives .NET's `Process` class and names RunProgram only in a comment.

## Script actions (`ScriptMapping`)

A script-only action becomes steps when its script is one of the shapes below; anything else stays a `Script` placeholder with the "script-only action" note. `ScriptMapping.TryMap(script)` is pure and the one place that decides: `ActionReader` (a fresh import) and `PlaceholderUpgrade` (a saved placeholder) both call it, so the same script always becomes the same steps. It recognises code, never a command's name. Comments, blanks, line breaks and semicolons never matter (`ScriptTokens` reads the code as tokens without them). The shapes it recognises are in the mapping doc.

## Upgrading saved placeholders

`PlaceholderUpgrade.Upgrade(MappingDocument)` turns the placeholders an earlier import saved into the step types that exist now, so a config imported before a type landed starts working without a re-import: `SendHotKey`/`SendVKey` (`HotkeyMapping.TryUpgrade`), `SendKeys`/`SendString` (`TextMapping.TryUpgrade`, only when every part maps), `Run` (`RunMapping.TryUpgrade`), and a `Script` placeholder of a shape `ScriptMapping` recognises (the command's "script-only action" note line goes with it; a comments-only script is removed, leaving a command with no steps). Each replacement keeps the placeholder's platform and active flag; what still does not map stays. It returns the same mapping instance and a count of 0 when there is nothing to do, so it is idempotent. The App runs it once per start right after the config loads (`ConfigModule.UpgradeImportedSteps`), commits through `MappingStore.ReplaceAll`, clears undo (it is not the user's edit), saves at once and logs `config` "Imported steps upgraded" with the count per method; sync then publishes it like any edit.

**Display Changer commands.** Wired through `ProgramCallMapping`, so a fresh import and `PlaceholderUpgrade` both turn them into Display mode steps. Joel runs 12noon Display Changer from `sp.RunProgram("…\dc64cmd.exe", "-refresh=120", …)` scripts (ten commands). `DisplayChangerMapping.FromInvocation(fileName, arguments)` turns such a call into a `DisplayModeStep` and gives null for anything else: the program must be `dc64cmd.exe` or `dccmd.exe` (any path, quoted or not); `-width=N -height=N` (both or neither), `-refresh=N` through Windows' whole-hertz rule (`RefreshRate.FromLegacyHertz`: 23 → 23.976, 120 → 120) and `-quiet` are understood; `max`, `-depth`, `-monitor`, `-force`, `-test`, a repeated switch or a program to run afterwards give null so the command stays a Run step. The target is the step default (display under the gesture). Only scripts reach it (`ScriptMapping` → `ProgramCallMapping.ToStep`): a `Run` step goes through `RunMapping.FromRun` and stays a Run step even when it names `dc64cmd.exe`. Rules and sources: `docs/learnings/0002-display-modes.md`.

## Merge policies (pure; the App applies them)

| Type | Role |
|---|---|
| `GestureMerge` | `Plan(existing, imported, RecognitionOptions)` classifies each imported gesture as `Add` or `Conflict` (name clash, case-insensitive), then scores each addition's first sample against every existing gesture (inactive included) and makes a match at or above `ConfusionCheck.DuplicateCutOff` (90; `ConfusionCheck.Duplicate`, the rule the Augram import matches shapes with too) a `SameShape` entry with the twin and the score (A7: a re-import reuses the existing gesture). `ApplyWithMap(plan, choices)` resolves conflicts with `KeepMine`, `TakeTheirs` (existing id kept, content replaced) or `KeepBoth` (a name clash is renamed `Name (2)` by `Core/Mapping/NameScope`; a shape match keeps its name) and returns `MergeOutcome`: the merged library and `IdMap`, imported id → final id (existing id for KeepMine and TakeTheirs, own id for Add and KeepBoth) |
| `MappingImport` | `Rebind(document, idMap)` points the imported gesture triggers at the final ids (`Command.WithGesturesReplaced`, an own version's trigger included). `Merge(existing, imported)` is **add-only in this slice**: groups merge by name (case-insensitive; Global always into Global; an existing group keeps its own matcher and flags), a command whose name is already taken among that group's ordinary commands (an import never makes a hold remap, and a command under one does not hold the name: `Core/Mapping/CommandNames`, Joel 2026-10-10) or whose bound trigger is taken in that group is skipped and counted, ignored apps merge by name; categories merge by name: an added command lands in the existing group's category of the same name (case-insensitive) or brings its own category along when the group has none by that name (a fresh id if its id is already taken there), so only categories an added command uses arrive, and a new group arrives with its categories whole; returns `MappingMergeResult` (validated document + groups added, commands added/skipped, ignored added/skipped). A per-group replace/overwrite choice is a later slice |

Report lines (`ImportWarning`): gesture with no usable sample skipped (warning); sample with fewer than 2 distinct points dropped (warning); duplicate source name imported as `Name (2)` (warning, for gestures, commands, apps and ignored apps alike); stock 2-point gesture detected (info only); no `Gestures` array (warning); no `GlobalApplication` (info); a category an action names but the list lacks (info); plus the warnings the mapping doc's tables name. Unknown members and nulls are ignored.

## Tests: no real data

Tests read only hand-written fixtures under `tests/Augram.Core.Tests/Fixtures/StrokesPlusNet/` (`sample-config.json` for gestures, `sample-config-full.json` for the whole mapping, including Global with several categories, apps with only "General" and an app with "General" plus another; every name except "General" starts with `Synthetic`). Joel's real `StrokesPlus.net.json` and the backups under `J:\` are never copied into the repo, referenced by path, or used as a fixture. If a real-world shape needs a test, reproduce it synthetically (the real file was read once to learn the member shapes: `MethodParameters[].Value` can be a string, a number, a bool, an object or null; one real step has a null `Method`; `Categories[]` is an array of strings and every real `Action.Category` is a string naming one of them).
