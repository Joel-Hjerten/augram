# Core/Sync

Machine-to-machine sync (requirements F8, "Sync between machines", Joel 2026-10-07): gestures and the mapping (groups, categories, commands, ignored apps) travel between Joel's machines through a private git repo; settings never do. Each machine writes only its own file, `machines/<machineId>.json` (format: `../Config/README.md`, sync file), so git never merges content. Augram merges, item by item, here. The git side is the `ISyncRepository` port (`../Abstractions/`), implemented by `Augram.Sync.Git`.

Everything in this folder is pure except `SyncBaseStore` (local files) and `SyncCoordinator` (calls the port and the stores).

## Types

| Type | Role |
|---|---|
| `SyncItemKind`, `SyncItemKey` | the five kinds and an item's identity; text form `gesture:<id>`, `group:<id>`, `category:<groupId>/<id>`, `command:<id>`, `ignored:<id>` |
| `SyncItem` (+ nested `GestureItem`, `GroupItem`, `CategoryItem`, `CommandItem`, `IgnoredItem`) | one item: key, name, model value and canonical `Content` |
| `SyncItemSet` | a document as items, in document order; `From(gestures, mapping)`, `Contents()`, `SameAs` |
| `ThreeWayMerge` | `Merge(base, local, remote[, held])` → `SyncMergeResult` (gestures, mapping, conflicts, repairs, counts, items) |
| `SyncDocumentBuilder`, `CommandPlacement`, `SyncNames` (internal) | rebuild merged items into a valid document, repairing (below) |
| `SyncConflict`, `SyncRepair` + `SyncRepairKind`, `SyncCounts` + `SyncKindCounts` | what a merge reports |
| `SyncMachineState`, `SyncPublished`, `SyncBaseStore` (+ internal `SyncStateJson`) | the local state under `<configFolder>/sync/state/` |
| `SyncPlanner` (internal) | one run's merges, pure |
| `ConflictResolution` (internal) | what a resolution does to the document, pure |
| `SyncCoordinator` (+ internal `SyncStoreWriter`, `SyncPublisher`, `SyncRepositoryFiles`) | a run and a resolution, end to end |
| `SyncReport`, `SyncStatus`, `SyncJoin`, `SyncChoice`, `SyncApplied`, `SyncMachineSummary` | the App's side of it |

## Items

A document splits into items keyed by kind + id. Each item's `Content` is its JSON as the config file writes it (`Config/SyncItemJson`, which calls the same writers), so equal text is equal item and nothing is compared field by field.

| Kind | Key | Content |
|---|---|---|
| gesture | `GestureId` | the gesture as in `gestures` |
| group | `GroupId` | the group header: name, active, suppress globals, matcher; no categories, no commands |
| category | group id + `CategoryId` (category ids are unique per group only) | `{ "group", "id", "name" }` |
| command | `CommandId` | `{ "group": <id>, "command": { … } }`: moving a command to another group is a change of the command |
| ignored app | its id | the ignored app as in `ignored` |

Moving a category to another group is a delete in one group and an add in the other. Gesture order is not synced (it is not an item property).

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

- name clash among gestures, among groups, among one group's categories, among one group's commands → the incoming one takes the first free " (2)", " (3)"… (Global is never renamed);
- a trigger bound twice in one group (A7) → the incoming command is unbound (`Trigger.None`);
- a command whose group is gone → into Global, uncategorized;
- a command whose gesture is gone → unbound, when the merge removed that gesture or the command is incoming (a reference that was already dangling here is left alone);
- a command whose category is gone → uncategorized;
- a category whose group is gone → dropped.

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
4. **Join**: no local state yet and other machines' files exist → without a `SyncJoin`, `NeedsJoinChoice` with each machine's name and counts, nothing changed. `UseRemote` adopts the newest other machine's gestures and mapping (its base is its own content); `Merge` merges with empty bases (two machines that imported StrokesPlus.net separately get everything twice, renamed).
5. Merge each other file in turn against its base (`SyncPlanner`), the result of one feeding the next; then apply once: one `ReplaceAll` per store that changed (one undo step each; the config file's backup-before-write keeps the old file).
6. Save each machine's new state, then publish (`SyncPublisher`) when there is something new: different items, different `merged` entries (a newer acknowledged revision alone does not count, or two idle machines would acknowledge each other forever), the repo lacking our newest revision, or a failed publish last time. The revision is saved locally before the push. Commit message: `sync from <MachineName>`.
7. Report: status (`UpToDate` or `Applied`, or `Failed` when publishing failed), counts relative to this machine before the run, every pending conflict, repairs, notes. One log line, source `sync`: Info with the counts, or Warning with the error line; the URL appears only as its host.

## Threading

`Run` and `Resolve` are for the **sync worker** (they take a lock; calling them on the store thread would deadlock on `onStoreThread`). `PendingConflicts()` is safe from any thread: a snapshot as of construction or the last run or resolution. The git calls, the merge and the state files run there. Every store mutation goes through the `onStoreThread` delegate the host passes (`Func<Func<SyncApplied>, SyncApplied>`: run the function on the stores' thread and return its result; tests pass `f => f()`). The worker merges against `GestureLibrary.All` and `MappingStore.Current` read as snapshots; the store-thread function applies only if both are still the same instances, else returns `SyncApplied.Moved` and the worker merges again (at most `MaxAttempts`), so an edit made meanwhile is never lost. The stores raise `Changed` there, so `ConfigSession` saves and the engine swaps as for any edit. After `Resolve`, the next `Run` publishes; the App's change-triggered sync does that.

## State files (`<configFolder>/sync/state/`)

- `machines/<id>.json`: one `SyncMachineState` (name, merged revision, applied acknowledgement, held keys, pending conflicts with both contents, base).
- `published/<sequence>-<revision>.json`: our newest 20 revisions (items and `merged`), named by a counter so age never depends on the clock.
- `publish-pending.txt`: present after a failed publish.

An unreadable state file is reported and treated as absent (no base: conflicts rather than guesses). All writes are temp file + atomic replace.

## Limits

- Contents are compared as text: a release that changes how an item is written makes items here look changed once (they are kept and published, not lost); a format change needs a schema version and a migration (`../Config/ConfigMigrations`).
- The state belongs to one repository and Core does not know which. `SyncBaseStore.Clear()` forgets it (every machine's state, the published revisions, the pending-publish flag); the App calls it when the repository URL changes or is cleared, so the next run asks the join question again (`src/Augram.App/README.md`, Sync).

**May reference:** `Abstractions` (`ISyncRepository`, `IClock`, `IEventLog`), `Config` (settings, the sync file, item JSON), `Diagnostics`, `Gestures`, `Mapping`, `Steps`. **Referenced by:** the composition root and the Options › Sync view model.
