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
| `ImportedNames` (internal) | the names one kind of item takes in one scope (gestures, app groups with "Global" taken, ignored apps, one group's commands): `Claim(name)` renames a repeat `Name (2)`, `Name (3)`… by Core's rule (`Core/Sync/SyncNames`, case-insensitive) with one warning "Duplicate *kind* name; imported as '…'.". A reader that names items claims through it rather than keeping its own set |
| `GestureReader` (internal) | `Gestures[]` to `Gesture` records: fresh `GestureId`, trimmed `Name`, `IsActive` from `Active`, one `GestureSample` per `PointPattern` ordered by `Order` |
| `StepReader` (internal) | one `Steps[]` entry to one `CommandStep` authored on Windows, per the table below; counts placeholders per method and reports them once per file |
| `ActionReader` (internal) | one `Action` to one `Command`: name = `Description` (fallback "Action N", unique in the group with a numbered suffix), trigger, steps, script, `Active`; hands back each action's `Category` name beside its command |
| `TriggerReader` (internal) | an action's trigger with its keys, buttons and capture mode, and the file's `SecondaryStrokeButton` (table below) |
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
| `SendKeys(sendKeysString)` | the steps the key string makes through `TextMapping.FromSendKeys` (text runs → `TypeTextStep`, keys → `HotkeyStep` or `MediaKeyStep`, `{DELAY n}` → `DelayStep`), spliced in place, each with the step's active flag; a string with a part that does not map → placeholder plus a warning per part |
| `SendString(characters)` | one `TypeTextStep` through `TextMapping.FromSendString`, the text as written; no text → placeholder |
| `Run(command)` | `RunStep` through `RunMapping.FromRun` (the command line cut into file and arguments); no command → placeholder |
| `MouseClick`, `SendAltDown/Up`, `SendWinDown/Up`, `ConsumePhysicalInput`, unknown, null | `ImportedStep(method, step description, parameters as name → value text)`: shows as "not supported yet", runs (skips) now, and gets a real type later |
| a step with `Active == false` | `CommandStep.IsActive = false` |

One **Info** line per distinct placeholder method per file ("3 MouseClick step(s) imported as placeholders; mouse clicks arrive in a later version."), never one per step.

## Text methods (`SendKeys`, `SendString`)

`TextMapping` is the `HotkeyMapping` of the text methods; `StepReader` and `PlaceholderUpgrade` call it. Pure and stateless.

| Call | Produces |
|---|---|
| `TextMapping.FromSendKeys(parameters, textMethod)` | `sendKeysString` through `SendKeysSyntax.Parse`: a `SendKeysResult` (`Steps`, `Warnings`, `IsClean`); null when the parameter is missing or empty. One SP.net step becomes several Augram steps, so the caller splices them in |
| `TextMapping.FromSendString(parameters, method)` | `characters` as one `TypeTextStep`, exactly as written (SendString is text, not key syntax); null when missing or empty |
| `TextMapping.TryUpgrade(ImportedStep, textMethod)` | a saved `SendKeys` or `SendString` placeholder as the steps that replace it; null for any other method |

`SendKeysSyntax.Parse(keys, textMethod)` (the state machine is the internal `SendKeysParser`, the token names `SendKeysNames`) reads .NET `SendKeys` syntax plus the classic StrokesPlus extras; it never throws, it reports a part it cannot map in `Warnings` (one sentence each, naming the part; the caller adds the action's name) and goes on:

| Syntax | Steps |
|---|---|
| plain characters | one `TypeTextStep` per run, consecutive characters merged (also across a group without modifiers and around escaped literals), by the method asked for (default Unicode) |
| `^` Ctrl, `+` Shift, `%` Alt, `@` Win (classic) before a key, or before `( … )` for every key inside (nested groups add up) | the key as a `HotkeyStep` with those modifiers; a character under a modifier is its US-layout key (`AsciiKeyLayout`) with Shift added for a shifted character, as .NET does (`^A` is Ctrl+Shift+A, `^{+}` Ctrl+Shift+=) |
| `~`, `{ENTER}`, `{TAB}`, `{ESC}`/`{ESCAPE}`, `{BS}`/`{BKSP}`/`{BACKSPACE}`, `{DEL}`/`{DELETE}`, `{INS}`/`{INSERT}`, `{HOME}`, `{END}`, `{PGUP}`, `{PGDN}`, arrows, `{F1}`–`{F24}`, `{ADD}`/`{SUBTRACT}`/`{MULTIPLY}`/`{DIVIDE}` (keypad), `{CAPSLOCK}`, `{NUMLOCK}`, `{SCROLLLOCK}`, `{PRTSC}`; classic `{WIN}`, `{LWIN}`, `{RWIN}`, `{APPS}`, `{F_1}`…, `{NUMPAD0}`…, `{DECIMAL}`, browser and media names; `{SPACE}`; names ignore case (`SendKeysNames`) | a `HotkeyStep`, or a `MediaKeyStep` for a media key with no modifier (`{VOLUP}`) |
| `{KEY n}`, `{x n}` | the key n times (at most `SendKeysSyntax.MaxRepeat`, 100, with a warning above it); a character n times into the text |
| `{+}`, `{^}`, `{%}`, `{~}`, `{(}`, `{)}`, `{{}`, `{}}`, `{@}`, any `{x}`; classic `{PLUS}`, `{CARET}`, `{PERCENT}`, `{TILDE}`, `{AT}`, `{LPAREN}`, `{RPAREN}`, `{LBRACE}`, `{RBRACE}` | that character, as text |
| `{DELAY n}` (classic) | `DelayStep(n)`, clamped to 60000 ms with a warning |
| `{VKEY n}` (classic; decimal Windows virtual-key code, space optional) | that key through `HotkeyMapping.FromVirtualKey`, with any modifiers |
| an unknown name (`{BREAK}`, `{CLEAR}`, `{HELP}`, `{BEEP …}`), classic `{DELAY=n}`, a modifier that reaches no key, a character a US keyboard lacks under a modifier, an unmatched `(`, `)` or `{` | a warning; unmatched brackets are typed as text, everything else is skipped |

Every `SendKeys` and `SendString` value in the reference config (Joel's, 2026-10-08: game console text such as a backtick or `fov 70`, punctuation-heavy text, and in scripts Ctrl/Shift hotkeys with `{TAB}`, `{PGUP}`, `{ADD}`…) parses with no warning; the tests reproduce those shapes synthetically.

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

A script-only action becomes steps when its script is one of the shapes below; anything else stays a `Script` placeholder with the "script-only action" note. `ScriptMapping.TryMap(script)` is pure and the one place that decides: `ActionReader` (a fresh import) and `PlaceholderUpgrade` (a saved placeholder) both call it, so the same script always becomes the same steps. It recognises code, never a command's name. Comments, blanks, line breaks and semicolons never matter (`ScriptTokens` reads the code as tokens without them).

| Script | Steps |
|---|---|
| one plain `sp.RunProgram(…)` call (`RunProgramScript`) | the step `ProgramCallMapping.ToStep` makes: Display mode for a Display Changer call, else Run |
| nothing but comments and blanks (SP.net's "Ignore Close" and friends: "Do nothing here, the purpose of this is to ignore the default … action on the desktop") | **no steps**: the command does nothing here, an override to nothing that silences the Global gesture in that group |
| one `sp.SendKeys("…")` call with a plain string literal, nothing else | the steps `SendKeysSyntax.Parse` makes, when every part maps (`^{ADD}` is Ctrl + Num +, `^{SUBTRACT}` Ctrl + Num −); a part that does not map keeps the placeholder |
| `clip.Clear()` alone | `ClearClipboardStep` |
| SP.net's "Window Snap to Left / Right (half screen size)" sample, margin 0 (`SampleScripts`) | `WindowOpStep` `SnapLeftHalf` / `SnapRightHalf` |
| the forum's Ctrl + wheel zoom: `WM_MOUSEWHEEL` with `MK_CONTROL` and `WHEEL_POS` / `WHEEL_NEG` (±120) posted to `sp.WindowFromPoint(action.Start, false)` (`SampleScripts`) | `ScrollStep` up / down, one notch, Ctrl held, at the gesture start |

`SampleScripts` keeps the samples' code (comments dropped) and compares token lists: a sample someone edited (another margin, two notches, a statement added) is not guessed at and stays a placeholder. Against the reference config (read 2026-10-09 through the saved Augram config, never copied): all 12 script placeholders map: Clear Clipboard, both snaps, the Global Zoom In / Zoom Out (`sp.SendKeys("^{ADD}")` / `"^{SUBTRACT}"`, the wheel post commented out beside them), the five Windows Desktop "Ignore …" overrides (comments only) and the Explorer Zoom In / Zoom Out wheel posts.

## Upgrading saved placeholders

`PlaceholderUpgrade.Upgrade(MappingDocument)` turns the placeholders an earlier import saved into the step types that exist now, so a config imported before a type landed starts working without a re-import: `SendHotKey`/`SendVKey` (`HotkeyMapping.TryUpgrade`), `SendKeys`/`SendString` (`TextMapping.TryUpgrade`, only when every part maps), `Run` (`RunMapping.TryUpgrade`), and a `Script` placeholder of a shape `ScriptMapping` recognises (the command's "script-only action" note line goes with it; a comments-only script is removed, leaving a command with no steps). Each replacement keeps the placeholder's platform and active flag; what still does not map stays. It returns the same mapping instance and a count of 0 when there is nothing to do, so it is idempotent. The App runs it once per start right after the config loads (`EngineModule.UpgradeImportedSteps`), commits through `MappingStore.ReplaceAll`, clears undo (it is not the user's edit), saves at once and logs `config` "Imported steps upgraded" with the count per method; sync then publishes it like any edit.

**Display Changer commands.** Wired through `ProgramCallMapping`, so a fresh import and `PlaceholderUpgrade` both turn them into Display mode steps. Joel runs 12noon Display Changer from `sp.RunProgram("…\dc64cmd.exe", "-refresh=120", …)` scripts (ten commands). `DisplayChangerMapping.FromInvocation(fileName, arguments)` turns such a call into a `DisplayModeStep` and gives null for anything else: the program must be `dc64cmd.exe` or `dccmd.exe` (any path, quoted or not); `-width=N -height=N` (both or neither), `-refresh=N` through Windows' whole-hertz rule (`RefreshRate.FromLegacyHertz`: 23 → 23.976, 120 → 120) and `-quiet` are understood; `max`, `-depth`, `-monitor`, `-force`, `-test`, a repeated switch or a program to run afterwards give null so the command stays a Run step. The target is the step default (display under the gesture). `StepReader` does not call it yet; the lead wires it where the Run step parses `sp.RunProgram`. Rules and sources: `docs/learnings/0002-display-modes.md`.

## Action mapping

| SP.net | Augram |
|---|---|
| `GestureName` resolves (case-insensitive) to a gesture imported from the same file | `Trigger.ForGesture(id)`; SP.net binds by name, so a duplicated source name binds to the first |
| `GestureName` names nothing | `Trigger.None` + warning "gesture 'X' not found; command imported without a gesture" |
| `GestureName` empty and `WheelUp` / `WheelDown` | `Trigger.ForWheel`; both set → wheel up + warning |
| neither, but keys or buttons held | a **click trigger** (`Trigger.ForClick`: the stroke button clicked while holding them; SP.net's no-gesture action) |
| none of these | `Trigger.None` + warning |
| `Control`, `Alt`, `Shift`; `Left`, `Middle`, `Right`, `X1`, `X2`; `Capture` 0 / 1 / 2 | the trigger's "while holding" set (`TriggerHold`, `TriggerReader`; learnings 0003 §3.8): the keys, the buttons with the stroke button, capture Before / After / Either (SP.net's order; missing is Either). The command keeps its `Active` flag (until 2026-10-09 these imported inactive with a "needs modifier/rocker support" note) |
| `UseSecondaryStrokeButton`, a wheel action, `SecondaryStrokeButton` set in the file (WinForms `MouseButtons`) | the wheel trigger holds that button instead of the stroke button ("X1 + wheel up"), active as in the source |
| `UseSecondaryStrokeButton`, a wheel action, no secondary button set (Joel's file) | "Right + wheel" (Joel's own use, requirements F1), **inactive**, note "a secondary-stroke-button command … check the button to hold, then switch it on" |
| `UseSecondaryStrokeButton`, a gesture or click action | the stroke-button trigger, **inactive**, note "drawn or clicked with the secondary stroke button, and Augram draws and clicks with the stroke button only" |
| a click trigger whose steps re-send a mouse click (`MouseClick`; SP.net's "Shift+Right Click" workaround) | **inactive**, note "Augram passes a click with keys held through to the app itself when nothing is bound to it" (Joel, 2026-10-09) |
| a trigger already bound in the group (A7: overlapping, combinations included) | the active command keeps it; the other is imported with `Trigger.None` + warning naming the combination ("Shift + gesture 'Up'") |
| `Steps` non-empty | one `CommandStep` per entry (a script beside steps is ignored, as SP.net ignores it) |
| `Steps` empty, `Script` of a shape `ScriptMapping` recognises (above: `sp.RunProgram`, comments only, `sp.SendKeys`, `clip.Clear()`, the snap and Ctrl + wheel samples) | the steps it makes (none for comments only), no note |
| `Steps` empty, any other `Script` | one `ImportedStep("Script", name, { script })` so the script is visible on the step list, plus `Note` "Imported from StrokesPlus.net: script-only action"; `Active` carried over |
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
| `MappingImport` | `Rebind(document, idMap)` points the imported gesture triggers at the final ids (`Command.WithGesturesReplaced`, an own version's trigger included). `Merge(existing, imported)` is **add-only in this slice**: groups merge by name (case-insensitive; Global always into Global; an existing group keeps its own matcher and flags), a command whose name is already taken among that group's ordinary commands (an import never makes a hold remap, and a command under one does not hold the name: `Core/Mapping/CommandNames`, Joel 2026-10-10) or whose bound trigger is taken in that group is skipped and counted, ignored apps merge by name; categories merge by name: an added command lands in the existing group's category of the same name (case-insensitive) or brings its own category along when the group has none by that name (a fresh id if its id is already taken there), so only categories an added command uses arrive, and a new group arrives with its categories whole; returns `MappingMergeResult` (validated document + groups added, commands added/skipped, ignored added/skipped). A per-group replace/overwrite choice is a later slice |

Report lines (`ImportWarning`): gesture with no usable sample skipped (warning); sample with fewer than 2 distinct points dropped (warning); duplicate source name imported as `Name (2)` (warning, for gestures, commands, apps and ignored apps alike); stock 2-point gesture detected (info only); no `Gestures` array (warning); no `GlobalApplication` (info); a category an action names but the list lacks (info); plus the rows above. Unknown members and nulls are ignored.

## Tests: no real data

Tests read only hand-written fixtures under `tests/Augram.Core.Tests/Fixtures/StrokesPlusNet/` (`sample-config.json` for gestures, `sample-config-full.json` for the whole mapping, including Global with several categories, apps with only "General" and an app with "General" plus another; every name except "General" starts with `Synthetic`). Joel's real `StrokesPlus.net.json` and the backups under `J:\` are never copied into the repo, referenced by path, or used as a fixture. If a real-world shape needs a test, reproduce it synthetically (the real file was read once to learn the member shapes: `MethodParameters[].Value` can be a string, a number, a bool, an object or null; one real step has a null `Method`; `Categories[]` is an array of strings and every real `Action.Category` is a string naming one of them).
