# Core/Sync

Machine-to-machine sync (requirements F8, "Sync between machines", Joel 2026-10-07): gestures and the mapping (groups, categories, commands, ignored apps) travel between Joel's machines through a private git repo; settings never do. Each machine writes only its own file, `machines/<machineId>.json` (format: `../Config/README.md`, sync file), so git never merges content. Augram merges, item by item, here. The git side is the `ISyncRepository` port (`../Abstractions/`), implemented by `Augram.Sync.Git`.

Everything in this folder is pure except `SyncBaseStore` (local files) and `SyncCoordinator` (calls the port and the stores).

## Types

| Type | Role |
|---|---|
| `SyncItemKind`, `SyncItemKey` | the kinds and an item's identity; text form `gesture:<id>`, `group:<id>`, `category:<groupId>/<id>`, `holdRemap:<groupId>/<id>`, `command:<id>`, `version:<commandId>`, `ignored:<id>` |
| `SyncItem` (+ nested `GestureItem`, `GroupItem`, `CategoryItem`, `HoldRemapItem`, `CommandItem`, `VersionItem`, `IgnoredItem`) | one item: key, name, model value and canonical `Content` |
| `SyncItemSet` | a document as items, in document order; `From(gestures, mapping)`, `Contents()`, `SameAs` |
| `ThreeWayMerge` | `Merge(base, local, remote[, held])` → `SyncMergeResult` (gestures, mapping, conflicts, repairs, counts, items) |
| `SyncDocumentBuilder` (+ `.HoldRemaps.cs`), `CommandPlacement` | rebuild merged items into a valid document, repairing (below) |
| `SyncNames` | the names taken in one scope and the rename on a clash: `Claim(name)` keeps a free name, else the first free " (2)", " (3)"…, compared by `MappingRules.NameComparer`. Public because the StrokesPlus.net importer renames a repeated source name by the same rule |
| `SyncConflict`, `SyncRepair` + `SyncRepairKind`, `SyncCounts` + `SyncKindCounts` | what a merge reports |
| `SyncMachineState`, `SyncPublished`, `SyncBaseStore` (+ internal `SyncStateJson`) | the local state under `<configFolder>/sync/state/` |
| `SyncPlanner` (internal) | one run's merges, pure |
| `ConflictResolution` (internal) | what a resolution does to the document, pure |
| `SyncCoordinator` (+ internal `SyncStoreWriter`, `SyncPublisher`, `SyncRepositoryFiles`) | a run and a resolution, end to end |
| `SyncReport`, `SyncStatus`, `SyncJoin`, `SyncChoice`, `SyncApplied`, `SyncMachineSummary`, `SyncNewerMachine` | the App's side of it |

## Items

A document splits into items keyed by kind + id. Each item's `Content` is its JSON as the config file writes it (`Config/SyncItemJson`, which calls the same writers), so equal text is equal item and nothing is compared field by field.

| Kind | Key | Content |
|---|---|---|
| gesture | `GestureId` | the gesture as in `gestures` |
| group | `GroupId` | the group header: name, active, suppress globals, matcher; no categories, hold remaps or commands |
| category | group id + `CategoryId` (category ids are unique per group only) | `{ "group", "id", "name" }`, plus `useOn` when the category is not on every platform (format 3, 2026-10-08), so a category on every platform has the same content as before |
| hold remap (`holdRemap:`) | group id + `HoldRemapId` (unique per group only, like a category's) | `{ "group", "id", "name", "holdKey", "tapTimeMs", "isActive" }`, plus `useOn` when not every platform (F9, format 11, 2026-10-10): the header only; the commands under it are command items |
| command | `CommandId` | `{ "group": <id>, "command": { … } }`: moving a command to another group is a change of the command; without its own version; a command under a hold remap carries `holdRemap` and its `input` trigger, so moving it in or out of a hold remap is a change of the command; a command's own drag distance (its trigger hold's `dragDistancePx`) and its `notIn` (format 12, plan 0004; since format 13 the ids of Ignored › Per command entries, on any command) travel in it like any member, so changing either is a change of the command; so do a button trigger (`{ "button": "Left", "hold": … }`) and a command's `alsoIn` (format 14, plan 0005: the ids of Exclusions › Global entries) |
| own steps (`version:`) | the command's `CommandId` | `{ "command": <id>, "version": { "platform", "basedOn", "changedAt", "trigger"?, "steps" } }` (F8, 2026-10-07; the own `trigger` since format 6, 2026-10-09: a trigger changed on that platform is its own, and travels here rather than with the command; a button trigger there since format 14): a command's own steps for the platform it was not authored on, apart from the command so the original changing on one machine and the own steps on the other merge without a conflict; the rebuild puts them back on their command and drops them, with a repair line (`OwnStepsDropped`), when the command is gone; counted as a command change in the log |
| ignored app | its id | the ignored app as in `ignored`, with `"scope": "PerCommand"` for an Ignored › Per command entry (format 13, plan 0004; omitted for Ignored › Global, so a Global entry has the same content as before): both lists are one item kind with one name space, and moving an entry between them is a change of the item |

Moving a category or a hold remap to another group is a delete in one group and an add in the other. Gesture order is not synced (it is not an item property).

## The merge table (`ThreeWayMerge`)

Per key, comparing contents with the base (absent = no content):

| Base | Here | There | Result |
|---|---|---|---|
| A | A | A | A |
| A | B | A | B (changed here) |
| A | A | B | B (changed there) |
| A | B | B | B (same change) |
| A | B | C | **conflict**, B kept |
| — | B | — | B (added here) |
| — | — | B | B (added there) |
| — | B | B | B, once |
| — | B | C | **conflict**, B kept |
| A | — | A | deleted |
| A | A | — | deleted |
| A | — | — | deleted |
| A | — | B | **conflict**, stays deleted |
| A | B | — | **conflict**, B kept |

A **held** key (the other machine has a pending conflict on it, below) keeps what is here and is never a conflict.

## Repairs

The merged items are rebuilt into a document (`SyncDocumentBuilder`) and repaired where `GestureRules` / `MappingRules` would refuse it, each repair reported as a `SyncRepair` line. What was already here keeps its name and trigger; the **incoming** item (taken from the other side, or displaced by the merge) gives way:

- name clash among gestures, among groups, among ignored apps, among one group's categories, among the commands of one parent (one hold remap's commands, or one group's ordinary commands: `Mapping/CommandNames`, Joel 2026-10-10; an "Orbit" under Space and one under S, or one under Space and an ordinary one, do not clash) → the incoming one takes the first free " (2)", " (3)"… (Global is never renamed); the repair line says where ("Incoming command 'Orbit' under 'Space' in 'Blender' renamed 'Orbit (2)': the name is taken.");
- a trigger bound twice in one group (A7: triggers that overlap on Windows or macOS, combinations and conversions included, `MappingRules.Overlap`) → the incoming command is unbound (`Trigger.None`);
- an own version's trigger (format 6) naming a gesture that is gone, or overlapping another command of its group → that platform's trigger is unbound (`TriggerCleared`);
- a command whose group is gone → into Global, uncategorized;
- a command whose gesture is gone → unbound, when the merge removed that gesture or the command is incoming (a reference that was already dangling here is left alone);
- a command whose category is gone → uncategorized;
- a command's "Not in" (format 13) naming an Ignored › Per command entry that is gone or now on Ignored › Global (deleted or moved on one machine while the other ticked it) → that id is dropped, silently and without a conflict, by `MappingRules.ValidDocument` below; both machines end without it;
- a command's "Also in" (format 14) naming an Exclusions › Global entry that is gone, now disables Augram while focused, or moved to Per command (changed on one machine while the other ticked it) → that id is dropped the same way, silently and without a conflict; so is the whole list when the rebuilt command has no bound trigger without the stroke button on either platform (an incoming command unbound by A7, say) or sits under a hold remap;
- a category whose group is gone → dropped;
- a hold remap whose group is gone (or is Global) → dropped (`HoldRemapDropped`); an incoming one is renamed on a name clash, and loses its hold key (`HoldKeyCleared`) when another hold remap of its group already holds it, so it does nothing until a key is chosen;
- a command whose hold remap is gone → an ordinary command of its group without its input (`HoldRemapCleared`); one moved to Global leaves its hold remap the same way; an own version bringing an input back to an ordinary command loses it (`HoldRemapCleared`);
- a command under a hold remap that breaks the hold remap rules across items (its input is now the hold key, an own version's Remap output no longer suits the input, …) → unbound on every platform (`Unbound`), never a failed merge. Inputs are unique per hold remap (A7 above, `MappingRules.Overlap`), so two hold remaps' commands never clash.

The result then goes through `GestureRules.ValidSet` and `MappingRules.ValidDocument`, so what the stores receive is always valid.

## Base state (`SyncBaseStore`)

Per other machine, `SyncMachineState.Base` holds, per item, the content both machines last shared; a missing entry means "absent then". It moves in three ways:

1. **Acknowledgement.** Every file carries `merged`: for each other machine, the revision of that machine's file it merged, the keys of that revision it did not take (`except`: its pending conflicts and the keys it held) and its pending conflicts with that machine (`pending`). When their file acknowledges a revision of ours not folded in yet, the base becomes our content at that revision (kept in `published/`, newest 20) for every key except `except`. That is the exact common ancestor: they have seen our version.
2. **After merging their file**, every settled key's base becomes their content (pre-repair), so an incoming item we renamed reads as our change next time and goes back to them, rather than as a conflict.
3. **Conflicted and held keys keep their old entry** until resolved (otherwise the next sync would silently take one side). **Resolving** sets the entry to their content: theirs has been seen and answered.

Why both: a base that is "the result of my last merge" reverts my change when the other machine publishes without having seen it (a race between two syncs, or a machine that skipped my file); a base that is only "their file as I last read it" shows every conflict on both machines and a "keep mine" never reaches the other side. With acknowledgements each machine knows which of its own versions the other has seen.

**Conflicts show on one machine.** The machine whose merge finds a conflict keeps its own version, lists the conflict and publishes the key as `pending`; the other machine holds the key (keeps its own, no conflict) until the resolution arrives. Keep mine: our version stays, and since their version is now our base the other machine takes ours on its next sync. Take theirs: we take theirs; both now agree. Keep both (gestures and commands present on both sides): ours stays and theirs is added with a new id, renamed on a name clash and unbound if its trigger is taken; for other kinds, or when one side deleted the item, the nearest choice is applied and the report's note says so. Two machines that found the same conflict at the same moment both list it; the last resolution wins.

## The run (`SyncCoordinator.Run`)

1. Sync off (no repository) → `Off`. The machine id is generated on first use (`SettingsStore.EnsureMachineId`, on the store thread).
2. `Prepare(url)`, `Pull()`, `ReadMachineFiles()`; a failure → `Failed`, stores and state untouched.
3. Read the files (`SyncRepositoryFiles`): this machine's own (only its revision matters) and the others, in machine-id order. A file that does not parse, breaks a rule, sits under another id, or has steps this build cannot read is skipped with a note (merging the last kind would delete those steps everywhere; the note says to update Augram here).
   - **A newer Augram** (see Format version): a file whose header is newer than this build, or this machine having published a newer format → `NeedsUpdate` with `NewerMachines`, before the join question; nothing merged, applied, saved or published.
4. **Join**: no local state yet and other machines' files exist → without a `SyncJoin`, `NeedsJoinChoice` with each machine's name and counts, nothing changed. `UseRemote` adopts the newest other machine's gestures and mapping (its base is its own content); `Merge` merges with empty bases (two machines that imported StrokesPlus.net separately get everything twice, renamed).
5. Merge each other file in turn against its base (`SyncPlanner`), the result of one feeding the next; then apply once: one `ReplaceAll` per store that changed (one undo step each; the config file's backup-before-write keeps the old file).
6. Save each machine's new state, then publish (`SyncPublisher`) when there is something new: different items, different `merged` entries (a newer acknowledged revision alone does not count, or two idle machines would acknowledge each other forever), the repo lacking our newest revision, or a failed publish last time. The revision is saved locally before the push. Commit message: `sync from <MachineName>`.
7. Report: status (`UpToDate` or `Applied`, or `Failed` when publishing failed), counts relative to this machine before the run, every pending conflict, repairs, notes. One log line, source `sync`: Info with the counts, or Warning with the error line (or, for `NeedsUpdate`, the newest format found and this build's); the URL appears only as its host.

## Format version

`SyncFile.CurrentFormatVersion` (the file's `formatVersion`, separate from the config `schemaVersion`) is the sync's own contract. **Raise it with every change to what a sync item holds or to which item kinds exist**, and add a line to its history on the constant. 1: every file before 2026-10-07 (no member). 2: F8 cross-platform commands (Use on, macOS executable names, a command's own steps as an item of their own). 3 (2026-10-08): "Use on" on a category (the category item's `useOn`); a format 2 build would read such a category as used everywhere and publish it back that way, so it pauses instead ("Paused: PC-WORK uses a newer Augram…"). 4 (2026-10-08): the step types TypeText, Run and Display (`typeText`, `run`, `displayMode`, `hdr`); a format 3 build cannot read a command holding one and would drop it. 5 (2026-10-09): Display mode's "highest available" refresh (`"refreshHz": "highest"`), which a format 4 build refuses to read. 6 (2026-10-09): trigger combinations (a trigger's `hold`, the click trigger `{ "click": true }`, an own-steps item's `trigger`); a format 5 build would read a combination as the plain trigger and publish it back that way (the config schema went to 2 at the same time, so such a build also refuses the file by its schema). 7 (2026-10-09): the step types Scroll and Clear clipboard (`scroll`, `clearClipboard`); a format 6 build cannot read a command holding one and would drop the step. 8 to 10: see the history on the constant. 11 (2026-10-10): hold remaps (the `holdRemap` item kind, a command item's `holdRemap`, the input trigger, the Remap step); a format 10 build would drop the hold remaps and publish the group without them, could not read an input trigger, and would keep a Remap step only as is (the config schema went to 4 at the same time). 12 (2026-10-10, plan 0004): a command's own drag distance (a trigger hold's `dragDistancePx`, in a command item and an own-steps item's trigger) and a Global command's `notIn`; a format 11 build would drop both and publish the commands without them (the config schema went to 5 at the same time). 13 (the same evening, plan 0004 revised): Ignored › Per command entries (an ignored app item's `"scope": "PerCommand"`), which a command item's `notIn` names instead of app groups; a format 12 build would read such an entry as an ignore of the whole app, switch Augram off over it and drop every "Not in" naming one (the config schema went to 6). 14 (the same night, plan 0005): button triggers (a command item's and an own-steps item's `{ "button": … }` trigger) and a command item's `alsoIn` (Exclusions › Global entries); a format 13 build could not read a file holding a button trigger, so it would skip that file, and it would drop every "Also in" and publish the commands without it (the config schema went to 7). Why: a build that reads a newer file drops what it cannot see, and its next publish deletes that on every machine (2026-10-07: the PC, still on an old build, had to be stopped by hand before it read the Mac's own steps).

- `SyncFileSerializer.ReadHeader` reads both versions and the machine name before anything else. A file newer in either goes to `SyncRepositoryFiles.Newer` unread, and the run answers `NeedsUpdate`. `Read` refuses such a file too.
- **Never downgrade.** The publisher records the format it publishes in `SyncBaseStore.PublishedFormatVersion` (before the push; only ever raised). A build that writes an older format answers `NeedsUpdate` naming this machine, as it does when this machine's own file in the repo is newer.
- **An older file still merges**: members it lacks read as their defaults. That is safe because a build with this guard never publishes after seeing a newer file, so an older file never acknowledges items it could not read. Builds from before the guard have no such protection; keep every machine on one that has it.

## Threading

`Run` and `Resolve` are for the **sync worker** (they take a lock; calling them on the store thread would deadlock on `onStoreThread`). `PendingConflicts()` is safe from any thread: a snapshot as of construction or the last run or resolution. The git calls, the merge and the state files run there. Every store mutation goes through the `onStoreThread` delegate the host passes (`Func<Func<SyncApplied>, SyncApplied>`: run the function on the stores' thread and return its result; tests pass `f => f()`). The worker merges against `GestureLibrary.All` and `MappingStore.Current` read as snapshots; the store-thread function applies only if both are still the same instances, else returns `SyncApplied.Moved` and the worker merges again (at most `MaxAttempts`), so an edit made meanwhile is never lost. The stores raise `Changed` there, so `ConfigSession` saves and the engine swaps as for any edit. After `Resolve`, the next `Run` publishes; the App's change-triggered sync does that.

## State files (`<configFolder>/sync/state/`)

- `machines/<id>.json`: one `SyncMachineState` (name, merged revision, applied acknowledgement, held keys, pending conflicts with both contents, base).
- `published/<sequence>-<revision>.json`: our newest 20 revisions (items and `merged`), named by a counter so age never depends on the clock.
- `publish-pending.txt`: present after a failed publish.
- `published-format.txt`: the newest sync format this machine has published (see Format version).

An unreadable state file is reported and treated as absent (no base: conflicts rather than guesses). All writes are temp file + atomic replace.

## Limits

- Contents are compared as text: a release that changes how an item is written makes items here look changed once (they are kept and published, not lost); a format change needs a schema version and a migration (`../Config/ConfigMigrations`), and any change to what an item holds raises the sync format version.
- The state belongs to one repository and Core does not know which. `SyncBaseStore.Clear()` forgets it (every machine's state, the published revisions, the pending-publish flag, the published format); the App calls it when the repository URL changes or is cleared, so the next run asks the join question again (`src/Augram.App/README.md`, Sync).

**May reference:** `Abstractions` (`ISyncRepository`, `IClock`, `IEventLog`), `Config` (settings, the sync file, item JSON), `Diagnostics`, `Gestures`, `Mapping`, `Steps`. **Referenced by:** the composition root, the Options › Sync view model, and `Transfer` (an import is this merge without a base: it reuses the items, `SyncConflict`, `SyncChoice`, `ConflictResolution.Effective`, `SyncDocumentBuilder` with its repairs, and `SyncCounts`; a change to any of them changes imports too, `../Transfer/README.md`).
