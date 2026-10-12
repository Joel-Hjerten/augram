## Map (what the area holds, as a README would say it)

- **Abstractions/** — the ports (interfaces + their result/enum records + `Null*` defaults) that Engine, Platform.* and App implement, plus a few pure helpers that sit with their port (`AsciiKeyLayout`, `DisplayLookup`, `RefreshRate`, `ProcessLaunch.SchemeOf`). `HostPlatform`/`KeyCode`/`KeyModifiers` are the shared vocabulary everything else uses.
- **Steps/** — the `IStep`/`IStepType` contract, `StepRegistry.BuiltIn` (the only list of types), `StepParameters` (the JSON member reader every type uses), and one vertical-slice folder per type (record · type · executor · README). Hotkey's `HotkeyText`/`HotkeyKeys`/`HotkeyConversion` are shared by Scroll, Remap and Mapping for key names and the Ctrl↔Cmd swap.
- **Mapping/** — the model (`MappingDocument` › `AppGroup` › `Command` › `CommandStep`, `IgnoredApp`, `AppMatcher`, `Trigger`+`TriggerHold`), the rules (`MappingRules` → `CategoryRules`, `HoldRemapRules`), the decisions the engine asks (`CommandResolver`, `AnchorPlanner`/`ButtonOutputs`, `IgnoreList`), the editor's offer (`StepOffer`), naming (`CommandNames`, `NameScope`), and the store (`MappingStore`).
- **Config/** — the on-disk file: `ConfigDocument` + source-generated `ConfigJsonContext` for settings/gestures, hand-written `MappingJsonReader/Writer` + `CommandStepJsonReader` + `JsonMembers` for the mapping, `ConfigSerializer`/`ConfigMigrations`, `FileConfigStore`+`ConfigBackups`, `SettingsStore`/`SettingsRules`, and `ConfigSession` (three stores + debounced live save + tolerant load). It also holds the **sync file's** persistence (`SyncFile*`, `SyncItemJson`, `SyncSettings*`), which reuses the same writers.
- **State/** — `UndoStack<T>`, used by the three stores.
- Dependency direction inside Core: Abstractions ← Steps ← Mapping ← Config; HoldRemaps sits between Steps and Mapping and is referenced by both Mapping and Config.

## Findings (most valuable first)

**1. Platform name / "other platform" helpers, 13 copies** — kind 1.
Core: `Mapping/Command.cs:149`, `Mapping/AppMatcher.cs:171` (`Other`), `Steps/Run/RunConversion.cs:31`, `Steps/OpenApp/OpenAppStep.cs:26,32`, `Steps/OpenApp/OpenAppExecutor.cs:30`, `Sync/SyncItem.cs:143` ("macOS"/"Windows"). App: `Components/CommandTree/CommandItem.cs:202`, `Components/StepList/StepPlatformMarker.cs:73,84`, `ViewModels/SyncConflictsViewModel.cs:215`, `ViewModels/Ignored/IgnoredViewModel.cs:207`, `ViewModels/AppMatcherEditViewModel.cs:237`.
Recommend: `Abstractions/HostPlatformExtensions` with `Other()` and `DisplayName()`; every site calls it. Size S. Risk low. Behaviour exact.

**2. Three ways to know "which platform am I on", two of them OS probes inside Core** — kinds 1 and 3.
`Steps/OpenApp/OpenAppStepType.cs:19` (`CurrentPlatform` = `OperatingSystem.IsMacOS()`), `Abstractions/NullDisplayModes.cs:12`, `Abstractions/NullWindowOperations.cs:12`; `Steps/Hotkey/HotkeyText.cs:40` (`Names`, a mutable static the App sets in `App/Program.cs:32`); App `Hosting/CommandsModule.cs:34` and `Hosting/AppState.cs:46` probe again. `HostPlatform.cs` says "Core never branches on this", yet `OpenAppStep.Summary` uses the OS probe while every other step's `Summary` uses `HotkeyText.Names`, so in tests the two can disagree. 
Recommend: one `Abstractions/RunningPlatform.Current` (default = OS probe, settable once by the composition root and by tests); `HotkeyText.Names` becomes a read of it, the three Core probes and both App statics fold into it. Size M. Risk low–medium (tests that set `HotkeyText.Names`). Behaviour exact at runtime.

**3. Four typed JSON-member readers, with two different strictness rules** — kind 1.
`Config/JsonMembers.cs` (mapping subtree, throws `ConfigFormatException`), `Steps/StepParameters.cs` (step params, throws `StepFormatException`), `Steps/Hotkey/HotkeyStepType.cs:77-99` (`ReadModifiers`, a third flags parser doing `JsonMembers.OptionalFlags`'s job), `Config/AppearanceSettingsJsonConverter.cs:80-121` (`ReadEnum/ReadInt/ReadBool/ReadColour`, plus `TrailSettingsJsonConverter.ReadColour`). `JsonMembers.OptionalEnum/OptionalFlags` use `Enum.TryParse`, which accepts a numeric string (`"buttons": "3"`); `StepParameters` deliberately refuses numbers (its doc says so). Same file, two rules.
Recommend: one generic reader (new `Core/Json/` folder with a README, or `JsonMembers` generalised with an exception factory) that both `JsonMembers` and `StepParameters` become thin fronts for; `ReadModifiers` → `OptionalFlags`. Leave the two `Utf8JsonReader` converters (different API) unless settings move to the hand-written path. Size M. Risk medium (notice texts are asserted by tests; the numeric-string rule must be chosen, see question 1). Behaviour exact only once that rule is picked.

**4. "The command's one active Remap step with a set output" computed three ways** — kind 1.
`Mapping/AnchorPlanner.cs:178-189` (`OutputOf`), `HoldRemaps/HoldRemapPlan.cs:113-124` (`Binding`), `HoldRemaps/HoldRemapRules.Commands.cs:131-133` (`BrokenStepsRule`); the App does the inverse fitting in `ViewModels/Commands/CommandsViewModel.Input.cs:57-70` (see 8). 
Recommend: one `Command.RemapOutputOn(platform)` (or a helper in Mapping) returning the `RemapOutput?` of the sole active Remap step; the three callers filter (`IsSet`, `Key`) on it. Size S. Risk low. Behaviour exact.

**5. "A command that applies here" predicate repeated six times** — kind 1.
`Mapping/CommandResolver.cs:162`, `Mapping/AnchorPlanner.cs:40` and `:140`, `HoldRemaps/HoldRemapPlan.cs:101-105`, App `ViewModels/Commands/CommandSections.cs:81,117,183` (`IsActive && HoldRemapId is null && group.IsCommandUsedOn(command, platform) && TriggerFor(platform) …`).
Recommend: `AppGroup.ActiveCommandsOn(platform)` (ordinary, used here, with their trigger for that platform) and `HoldRemapCommandsOn(holdRemap, platform)`; all six call it. Size S. Risk low. Behaviour exact.

**6. The ignore rule lives in the resolver; `IgnoreList` forwards to it, and three loops differ only by predicate** — kinds 1 and 2.
`Mapping/IgnoreList.cs:33` (`Under` → `CommandResolver.FindIgnored`), `CommandResolver.cs:118-135` (`FindIgnored`), `IgnoreList.cs:36-53` (`PausedBy`), `:70-88` (`PerCommandUnder`): the same `foreach (app in mapping.Ignored) if (active && scope && Matches)` three times, and the class named "the ignore list's rules" does not own the first one.
Recommend: move `FindIgnored` into `IgnoreList` as `Under`; the resolver calls it; one private `First(mapping, window, platform, predicate)` behind the three. Size S. Risk low. Behaviour exact.

**7. App re-implements `CategoryRules.Carried`/`FindByName`, which have no production callers** — kind 1 (the UniqueName kind).
`App/ViewModels/Commands/CommandsViewModel.Commands.cs:136-150` (`PastedCategory`: look the source category up by id, then find the target group's category by name with `NameComparer`) is exactly `Mapping/CategoryRules.cs:142` (`Carried`); `CategoryRules.FindByName(group, name)` (`:117`) has zero callers outside tests.
Recommend: `PastedCategory` calls `CategoryRules.Carried(source.CategoryId, sourceGroup, group)` for the non-Global, non-hold-remap case; make `FindByName(group,…)` internal or delete. Size S. Risk low. Behaviour exact (stored names are already trimmed).

**8. App re-fits Remap outputs on a trigger change; Core already holds the rule for a new step** — kind 1.
`App/ViewModels/Commands/CommandsViewModel.Input.cs:57-70` (`InputTrigger` → `HoldRemapRules.FittedTo`, `ButtonTrigger` → key output with no key) vs `Mapping/StepOffer.cs:130-143` (`NewStep`, the same two branches). 
Recommend: `StepOffer.Fitted(RemapOutput, Trigger)` (or in `HoldRemapRules`) used by both `NewStep` and the App. Size S. Risk low. Behaviour exact.

**9. Dead or test-only public members** — kind 4.
`Mapping/AnchorPlanner.cs:51-69` `For`, `ForGroup`, `AnswerForGroup` (Engine uses `Answer` only); `Mapping/TriggerConversion.cs:58` `Explain` (no caller anywhere; `App/Components/CommandTree/CommandItem.cs:283-284` writes its own richer sentence); `Mapping/CommandResolver.cs:30` the `Trigger` overload of `Resolve` (Engine passes a `PressedTrigger`); `Mapping/CategoryRules.cs:117` `FindByName(group, name)`; `Steps/StepRegistry.cs:69` `Require` (tests only; `CommandStepJsonReader` uses `Find` and keeps unknowns); `Abstractions/RefreshRate.cs:24` `Hertz`; `Mapping/MappingStore.cs:36` and `Config/SettingsStore.cs:26` `Version` (only tests read them; `ConfigSession` uses `Changed`).
Recommend: delete or make internal; fix the README rows that name them (Steps/README "Mapping calls `StepRegistry.Require`"; Mapping/README `AnchorPlanner` row; Config/README "increments `Version`"). Size S. Risk low. Behaviour exact.

**10. `Config/` holds two subsystems: the config file and the sync file** — kind 2.
`Config/SyncFile.cs`, `SyncFileHeader.cs`, `SyncAcknowledgement.cs`, `SyncFileSerializer.cs`, `SyncItemJson.cs`, `SyncSettings.cs`, `SyncSettingsRules.cs` (~630 lines) are the sync's persistence; the README has to explain both. `SyncItemJson.Parse` (`:133-143`) also duplicates `ConfigSerializer.ParseObject` (`:97-111`): same options, same error shape.
Recommend: move the five sync-file types to `Core/Sync/Files/` (settings may stay, they are Options-page settings) with their README rows; `SyncItemJson.Parse` calls `ConfigSerializer.ParseObject(json, what)`. Size M (moves, namespaces). Risk low. Behaviour exact.

**11. A schema bump is five edits, and one of them is a growing hard-coded sentence** — kinds 5 and 2.
`Config/ConfigDocument.cs:15-33` (history as a doc comment), `Config/ConfigMigrations.cs:34-67` (six comment-only `case`s), `Config/README.md` "Older builds" (lines 166-170), `Config/FileConfigStore.cs:97` (a notice that lists every feature ever added, by hand), `Config/SyncFile.cs:21-52` (format history). 
Recommend: the README owns one table (schema · sync format · date · what changed · why refused); code comments point at it; the `FileConfigStore` notice drops the feature list ("what only the newer schema could hold is in backup/…") or reads a one-line-per-version table beside `ConfigMigrations`. Size S. Risk low (one notice text; check `FileConfigStoreTests`). Behaviour exact except that text.

**12. `ConfigSession` does two jobs: live save and tolerant load** — kind 2.
`Config/ConfigSession.cs:1-108` is the session (three stores, debounce, flush); `:110-257` (over half the file) is the recovery loader (`ValidSettings`, `ValidLibrary`, `ValidMapping`, `LoadGroup`, `RestoreIgnoredReferences`, `AddCategory`, `Try`), a rule of its own ("what survives a broken file") that `Transfer` and `Sync` do differently (they refuse the whole file).
Recommend: split a static `ConfigRecovery` (same folder) with `Settings/Library/Mapping(…, notice)`; `ConfigSession` calls it; the README gets a row and the load-order rules move there. Size S. Risk low. Behaviour exact.

**13. Type tests on `RemapStep` outside its folder (the ADR §4 exception has spread)** — kind 3.
`Mapping/StepOffer.cs:135`, `Mapping/AnchorPlanner.cs:186`, `HoldRemaps/HoldRemapPlan.cs:117-119`, `HoldRemaps/HoldRemapRules.Commands.cs:133`, App `CommandsViewModel.Input.cs:66`. `IStepType.HoldRemapsOnly` exists so that "nothing names a type", yet the Remap step is named in five places. Plan 0002 made Remap special on purpose; the spread is the problem, not the exception.
Recommend: with 4 and 8 done, the remaining type tests collapse to one or two; if Joel wants zero, add `IStep`/`IStepType` a capability (`HeldOutput`/an `IHoldOutputStep` interface in `Steps/`) so Mapping asks the step. Size M. Risk low. Behaviour exact. (Question 2.)

**14. Four copies of "id taken / name taken among siblings"** — kind 1.
`Mapping/MappingRules.cs:155-168` (groups), `:222-243` (commands), `:311-323` (ignored apps), `Mapping/CategoryRules.cs:102-113` (categories); `HoldRemapRules` has the hold remaps' version too. Same loop, five message shapes.
Recommend: one `Unique.Ensure(others, id, name, idTaken, nameTaken)` helper in Mapping; messages passed in so they stay byte-identical. Size S. Risk low. Behaviour exact.

**15. `AppMatcher` is 29 properties enumerated by hand in four places** — kind 2.
`Mapping/AppMatcher.cs` (names, path, title ×2 platforms + 7 window fields, each with `…IsRegex`), `Config/MappingJsonReader.Matcher.cs:15-42`, `Config/MappingJsonWriter.Matcher.cs:38-61`, `Mapping/MappingRules.cs:332-348`, `AppMatcher.IsEmpty/IsEmptyOn`. `WindowFields` (`:104-113`) already shows the better shape: a `(Pattern, IsRegex)` field record and a per-platform side. Adding a field is four edits plus the App form.
Recommend: leave until the matcher form is next touched; then `MatchField` records and `PlatformSide(Names, NamesAreRegex, Path, Title)`, with the JSON keys kept exactly (the Windows side keeps the un-prefixed keys). Size M. Risk medium (byte-stable file format, App form bindings). Behaviour exact if the keys are kept.

**16. Duplicate test fakes across the three test projects** — kind 1 (tests).
`FakeInputSimulator` ×3 (`tests/Augram.Core.Tests/Steps/Support`, `tests/Augram.App.Tests/Support`, `tests/Augram.Engine.Tests/Fakes`), `FakeWindowOperations` ×2, `FakeProcessLauncher` ×2, `FakeStep`/`FakeStepType` ×2, `FakeSyncRepository` ×2, `FakeClipboard` ×2, `FakeWindowSystem` ×2, `FakeInputSource` ×2. Each project re-learns the ports' contracts.
Recommend: a `tests/Augram.TestSupport` project holding one fake per port (the Engine's, the richest, as the base); ADR-0002 §1 gains a line. Size M. Risk low. Behaviour exact. (Question 3.)

**17. `Config.IgnoreKeys` duplicates `Abstractions.KeyModifiers`** — kind 1.
`Config/IgnoreKeys.cs` (Control/Alt/Shift/Win = 1/2/4/8) exists so it "casts straight to a mask" of `KeyModifiers` (Control/Alt/Shift/Meta, same bits). Two enums, one meaning; the engine casts between them.
Recommend: use `KeyModifiers` for `GeneralSettings.IgnoreKey` with a converter that keeps writing `"Win"` in the file (otherwise the file would say `Meta`). Size S. Risk low–medium (file compatibility). Behaviour exact only with the converter. (Question 4.)

**18. README drift an arriving agent trips on** — kind 5.
- `Abstractions/README.md:14,23`: names `IOverlay` and `IConfigStore` as ports here (`IStrokeTrail` is the overlay; `IConfigStore` is in Config), then a two-row "Present" table followed by "the rest land with their first consumer" and the real table; missing rows for `ISyncRepository`, `IAppActivator`/`IAppWindow` (two public types per file each, against §6), `DisplayLookup`, `AsciiKeyLayout`; the `IInputSimulator` row omits `PressRemapButton`/`ReleaseRemapButton`/`DragRemapButton`/`RepostsRemapDrags`.
- `Steps/README.md:288`: `StepExecutionContext` row lacks `Apps`, `AppWindow`; `:347` says Mapping calls `StepRegistry.Require` (it calls `Find`); `:314` "Still to land: MouseClick".
- `Config/README.md:135`: the mapping-specifics paragraph is ~1,800 words in one cell; `Mapping/README.md` rows for `Trigger`, `AnchorPlanner`, `StepOffer` are 100–200 words each. Recommend a table per member: shape · absent means · wrong shape → (format error | dropped with notice | normalised silently).
- Nowhere says where "the running platform" is decided (finding 2) or which reader a new JSON member goes through (finding 3).
Size S (docs). Risk none.

**19. `MappingStore.UpdateCommandAddingIgnored` and `SetAllowedFor` rebuild group replacement by hand** — kind 1, minor.
`Mapping/MappingStore.cs:122-132` repeats `UpdateCommand`'s index/replace (`:107-115`) so it can add ignored apps in the same commit. Recommend a private `WithCommandReplaced(group, command)` returning the next document, used by both. Size S. Risk low. Behaviour exact.

## Questions for Joel

1. The mapping reader accepts a numeric enum string (`"buttons": "3"`), step parameters refuse one. Which rule, when the readers are folded (finding 3)?
2. Is the Remap step the one step type Mapping may name (keep, and say so in Steps/README), or should Mapping ask the step through an interface (finding 13)?
3. A shared `tests/Augram.TestSupport` project for fakes needs an ADR-0002 §1 line. Wanted?
4. `IgnoreKeys` → `KeyModifiers` would change the file's `"Win"` to `"Meta"` unless a converter keeps the old name. Worth doing, or leave the second enum?
5. Finding 15 (the matcher's 29 fields) is only worth doing with the App's matcher form; schedule it with that, or leave?
