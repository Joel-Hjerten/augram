# Core/Mapping

The mapping model (requirements F5, F5a): **app group › command › step**, the matcher that identifies an app, the resolver the engine asks on button-up, and the store that owns the live mapping. Landed in M2 step 1. Persistence lives in `../Config/`; nothing here knows about storage.

## Types

| Type | Role |
|---|---|
| `GroupId`, `CommandId`, `CategoryId` | strongly typed Guids, stable across renames and moves (F8); `GroupId.Global` is a fixed value every install agrees on |
| `AppMatcher` | F5 app identification: `WindowsProcessNames` and `MacProcessNames` (F8 per platform, any-of, case-insensitive, the primary field; `Matches(window, platform)` uses this platform's list, else the other list's guess from `KnownApps`; names with none for this platform and no guess match nothing here, never everything; `EffectiveProcessNames`, `IsGuessedOn`), `ProcessPath` + `ProcessPathIsRegex`, `Title` + `TitleIsRegex`, `ClassChain` (Windows escape hatch: every listed class must appear in `WindowIdentity.ClassChain`; an entry may list alternatives with `\|`, "Progman\|WorkerW"), `IgnoreWhenFullScreen` (A21). Set fields are ANDed, unset ones ignored, nothing set matches nothing. Regexes: case-insensitive, culture-invariant, 100 ms timeout, cached by pattern in `MatcherRegexCache` (internal); an invalid pattern is no match here and a validation error at save |
| `Trigger` | closed set with value equality: `Trigger.GestureTrigger(GestureId)`, `Trigger.WheelTrigger(WheelDirection)`, `Trigger.NoTrigger` (`Trigger.None`, a command not bound yet). Factories `ForGesture`, `ForWheel`; `IsBound`; `Describe()` for log lines |
| `Command.UseOn` | F8 "Use on" for one command (both by default): a command not used on a platform is absent there and its trigger falls through (an app group's to Global); `IsUsedOn(platform)`; refused when used nowhere |
| `CommandStep` | the F8 envelope: `Step` (an `IStep`), `AuthoredOn`, `WindowsOverride`, `MacOsOverride`, `IsActive`; `ForPlatform(platform)` = what runs there (the override, the step itself where authored, else the type's best-guess `StepConversion`); `ResolveFor`, `WithOverride` |
| `KnownApps` | F8: well-known apps' executable names on Windows and macOS (chrome.exe ↔ Google Chrome, explorer.exe ↔ Finder, …); `Guess(names, to)`; never stored, so a better table helps every group at once |
| `PlatformSet` | F8 "Use on": `Windows`, `MacOS`, `All`; `Includes(platform)`, `With(platform, bool)` |
| `Command` | `Id`, `Name`, `Trigger`, `IsActive`, `Steps`, `Note` (read-only free text; the importer keeps a script there), `CategoryId` (null = Uncategorized). `IsOverrideToNothing` = no steps |
| `AppGroup` | `Id`, `Name`, `IsActive`, `SuppressGlobals` (SP.net "No Global Actions"), `UseOn` (F8, both by default; a group not used on a platform never matches there; Global is always everywhere; `IsUsedOn(platform)`), `Matcher` (null for Global), `Commands` sorted by name, `Categories` sorted by name (empty by default). `FindCommand`, `FindCategory`. `AppGroup.EmptyGlobal` |
| `CommandCategory` | `Id`, `Name`: a section of one group's commands (Joel, 2026-10-07; SP.net's per-application Categories). The Commands › Global tab is organised by them the way app groups organise app commands; most app groups have none. See "Categories" below |
| `IgnoredApp` | `Id`, `Name`, `IsActive`, `Matcher`, `DisableEntirely`: false = gestures do not fire over it, true = Augram switches off while it has focus |
| `MappingDocument` | `Groups` (Global first, then by name), `Ignored`; `Empty`; `Global`; `AllCommands()` |
| `MappingRules` | the business rules and the normaliser (`ValidDocument`); nothing else re-implements them |
| `CategoryRules` | the category part of those rules, called by `MappingRules` for every group; also `FindByName(group, name)` and `Carried(id, from, to)` (the category a command takes into another group) for the store and the importer's merge |
| `MappingValidationException` | a rule was broken; the message is fit to show the user |
| `CommandResolver`, `CommandResolution`, `ResolutionOutcome` | the decision on button-up, see below |
| `MappingStore` | the store: the single mutable owner of the mapping for the running app |

## Resolver rule (`CommandResolver.Resolve(mapping, window, trigger)`), in order

1. The window belongs to an **active ignored app** → `Ignored`, reason `ignored app 'Game'`, `IgnoredBy` set. (`FindIgnored` is also what the engine asks at button-down, before any stroke, so the button can pass through.)
2. The **first active app group** in document order (Global first, then alphabetical) whose matcher claims the window is *the app group*. Order, not specificity, breaks ties.
3. That group has an **active command with this trigger** → `Matched`, reason `app override in 'Chrome'`; with no steps it is `override to nothing in 'Steam'` (matched, `Fires` false).
4. Else, the group **suppresses globals** → `None`, reason `globals suppressed by 'FF7'`.
5. Else the **Global group's** active command for the trigger → `Matched`, reason `global`. An inactive Global group gives `None`, `the Global group is inactive`.
6. Else `None`, reason `no command for this gesture` / `no command for wheel up`.

A null window (nothing under the point) skips 1 and 2. `Trigger.None` is `None`, `no trigger`. Inactive groups, commands and ignored apps are invisible throughout. `Reason` is one line meant for the recognition log and the file log as-is.

## Store contract (`MappingStore`)

| Member | Behaviour |
|---|---|
| `Current` | immutable, sorted snapshot (a new instance after every change); `Version` increments with every change, undo and redo included; `Changed` is raised synchronously after each |
| `Global`, `FindGroup(id)`, `FindCommand(id)` → `(Group, Command)?`, `FindIgnored(id)` | lookups; null when absent |
| `UsedBy(gestureId)` | every `(Group, Command)` bound to the gesture, document order: the "Used by…" popup and the delete warning |
| `AddGroup`, `UpdateGroup` (by id, commands and categories included), `RemoveGroup` | Global cannot be removed (`MappingValidationException`); `KeyNotFoundException` for an unknown id |
| `AddCommand(groupId, command)`, `UpdateCommand(groupId, command)`, `RemoveCommand(id)`, `MoveCommand(id, toGroupId)` | one undo step each; a move keeps the id (paste into another group) and keeps the category only by name: the target group's category with the same name (case-insensitive), else Uncategorized |
| `AddIgnored`, `UpdateIgnored`, `RemoveIgnored` | same shape |
| `ReplaceAll(document)` | import; validated as a whole first; one undo step |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` / `ClearHistory()` | linear history, 100 steps (`State/UndoStack`) |

Every mutation builds the next document and runs it through `MappingRules.ValidDocument`; a failure throws before anything is recorded and the store is unchanged. Mutators return the item as stored (trimmed, sorted into place).

## Validation rules (`MappingRules`)

1. Names are trimmed; empty names are rejected (groups, commands, ignored apps).
2. Group names are unique, case-insensitively (`GestureRules.NameComparer`); command names are unique within their group.
3. Exactly one group has `GroupId.Global`; it is sorted first, never has a matcher and never suppresses globals (normalised, not refused).
4. A `GestureTrigger` or `WheelTrigger` is bound at most once per group (A7); `NoTrigger` any number of times.
5. Ids are unique: groups among groups, commands across the whole document, ignored apps among ignored apps.
6. A regex field that is switched on must compile.
7. Step lists may be empty (override to nothing). Steps themselves are not validated here; a step is whatever its type read.
8. Category names are trimmed, non-empty and unique within their group, case-insensitively ("A category named 'Media' already exists in 'Global'."); category ids are unique within their group (the same name or id may appear in another group). A command whose `CategoryId` is not among its group's categories is normalised to null, never refused.

## Categories

A group's `Categories` are display sections for its commands, nothing more: the resolver ignores them, and a command's trigger, activity and steps do not depend on them. Null `CategoryId` is "Uncategorized". There are no category mutators: the UI adds, renames or deletes a category with `UpdateGroup(group with { Categories = …, Commands = … })`, one undo step, and the rules clear the `CategoryId` of every command whose category went away. `MoveCommand` carries a category across groups by name only (ids are per group). Persistence: `categories` on a group (omitted when empty) and `category` on a command (omitted when null), see `../Config/README.md`. Import: `Augram.Import.StrokesPlus` (`CategoryReader`, and `MappingImport.Merge` merges them by name).

## Threading

Single-writer: the UI thread mutates the store. The engine worker takes `Current` as a snapshot when `ConfigSession.DocumentChanged` tells it the document moved, and calls `CommandResolver` against that snapshot; it never calls a mutator. No locks inside. `MatcherRegexCache` is the one shared mutable thing and is a `ConcurrentDictionary`.

**May reference:** `Abstractions` (`WindowIdentity`, `HostPlatform`), `Capture` (`WheelDirection`), `Gestures` (`GestureId`, the name comparer), `State`, `Steps` (`IStep`). **Referenced by:** `Config`, the engine (read-only snapshots), view models, the importer.
