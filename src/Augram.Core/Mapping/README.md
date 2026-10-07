# Core/Mapping

The mapping model (requirements F5, F5a): **app group › command › step**, the matcher that identifies an app, the resolver the engine asks on button-up, and the store that owns the live mapping. Landed in M2 step 1. Persistence lives in `../Config/`; nothing here knows about storage.

## Types

| Type | Role |
|---|---|
| `GroupId`, `CommandId` | strongly typed Guids, stable across renames and moves (F8); `GroupId.Global` is a fixed value every install agrees on |
| `AppMatcher` | F5 app identification: `ProcessNames` (any-of, case-insensitive, the primary field), `ProcessPath` + `ProcessPathIsRegex`, `Title` + `TitleIsRegex`, `ClassChain` (Windows escape hatch: every listed class must appear in `WindowIdentity.ClassChain`; an entry may list alternatives with `\|`, "Progman\|WorkerW"), `IgnoreWhenFullScreen` (A21). Set fields are ANDed, unset ones ignored, nothing set matches nothing. Regexes: case-insensitive, culture-invariant, 100 ms timeout, cached by pattern in `MatcherRegexCache` (internal); an invalid pattern is no match here and a validation error at save |
| `Trigger` | closed set with value equality: `Trigger.GestureTrigger(GestureId)`, `Trigger.WheelTrigger(WheelDirection)`, `Trigger.NoTrigger` (`Trigger.None`, a command not bound yet). Factories `ForGesture`, `ForWheel`; `IsBound`; `Describe()` for log lines |
| `CommandStep` | the F8 envelope: `Step` (an `IStep`), `AuthoredOn`, `WindowsOverride`, `MacOsOverride`, `IsActive`; `ResolveFor(platform)` = override if present else the authored step; `WithOverride` |
| `Command` | `Id`, `Name`, `Trigger`, `IsActive`, `Steps`, `Note` (read-only free text; the importer keeps a script there). `IsOverrideToNothing` = no steps |
| `AppGroup` | `Id`, `Name`, `IsActive`, `SuppressGlobals` (SP.net "No Global Actions"), `Matcher` (null for Global), `Commands` sorted by name. `AppGroup.EmptyGlobal` |
| `IgnoredApp` | `Id`, `Name`, `IsActive`, `Matcher`, `DisableEntirely`: false = gestures do not fire over it, true = Augram switches off while it has focus |
| `MappingDocument` | `Groups` (Global first, then by name), `Ignored`; `Empty`; `Global`; `AllCommands()` |
| `MappingRules` | the business rules and the normaliser (`ValidDocument`); nothing else re-implements them |
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
| `AddGroup`, `UpdateGroup` (by id, commands included), `RemoveGroup` | Global cannot be removed (`MappingValidationException`); `KeyNotFoundException` for an unknown id |
| `AddCommand(groupId, command)`, `UpdateCommand(groupId, command)`, `RemoveCommand(id)`, `MoveCommand(id, toGroupId)` | one undo step each; a move keeps the id (paste into another group) |
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

## Threading

Single-writer: the UI thread mutates the store. The engine worker takes `Current` as a snapshot when `ConfigSession.DocumentChanged` tells it the document moved, and calls `CommandResolver` against that snapshot; it never calls a mutator. No locks inside. `MatcherRegexCache` is the one shared mutable thing and is a `ConcurrentDictionary`.

**May reference:** `Abstractions` (`WindowIdentity`, `HostPlatform`), `Capture` (`WheelDirection`), `Gestures` (`GestureId`, the name comparer), `State`, `Steps` (`IStep`). **Referenced by:** `Config`, the engine (read-only snapshots), view models, the importer.
