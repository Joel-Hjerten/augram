# Core/Sync

Machine-to-machine sync (requirements F8, "Sync between machines", Joel 2026-10-07): gestures and the mapping (groups, categories, commands, ignored apps) travel between Joel's machines through a private git repo; settings never do. Each machine writes only its own file, `machines/<machineId>.json` (format: "Sync file" below), so git never merges content. Augram merges, item by item, here. The git side is the `ISyncRepository` port (`../Abstractions/`), implemented by `Augram.Sync.Git`.

Everything in this folder is pure except `SyncBaseStore` and `SyncFolders` (local files) and `SyncCoordinator` (calls the port and the stores).

**Where the one merge decision is:** `ThreeWayMerge.Decide`, the merge table below. The Augram import (`../Transfer/`) is that table with no base: `ImportPlan`'s New / Same / Different are its rows with the base column empty (added there, the same, both added differently), and the import's choices are applied by the sync's own `ConflictResolution`.

## Types

| Type | Role |
|---|---|
| `SyncItemKind`, `SyncItemKey` | the kinds and an item's identity; text form `gesture:<id>`, `group:<id>`, `category:<groupId>/<id>`, `holdRemap:<groupId>/<id>`, `command:<id>`, `version:<commandId>`, `ignored:<id>` |
| `SyncItem` (+ nested `GestureItem`, `GroupItem`, `CategoryItem`, `HoldRemapItem`, `CommandItem`, `VersionItem`, `IgnoredItem`) | one item: key, name, model value and canonical `Content` |
| `SyncItemJson` (internal) | an item's canonical text, written and read back by the config file's writers and readers (`../Config/`) |
| `SyncItemSet` | a document as items, in document order; `From(gestures, mapping)`, `Contents()`, `SameAs` |
| `ThreeWayMerge` | `Merge(base, local, remote[, held])` → `SyncMergeResult` (gestures, mapping, conflicts, repairs, counts, items) |
| `SyncDocumentBuilder` (+ `.HoldRemaps.cs`), `CommandPlacement` | rebuild merged items into a valid document, repairing (below); every rename goes through `SyncDocumentBuilder.Claimed` (one sentence shape for all six kinds) |
| `SyncConflict`, `SyncRepair` + `SyncRepairKind`, `SyncCounts` + `SyncKindCounts` | what a merge reports; `SyncCounts.Phrase(pick)` is "1 gesture, 2 app groups and 8 commands" for Options › Sync and the import summary |
| `SyncItemNouns` | the one noun table for the kinds: `Count(kind, n)` ("2 app groups", "1 command's own steps"), `Heading` ("App group", "Own steps": a conflict's and a match line's kind), `LogPlural` ("groups", for `SyncCounts.ToString`), `ReadingOrder`; also used by `Transfer/TransferContents` and the import review |
| `SyncMachineState`, `SyncPublished`, `SyncBaseStore` (+ internal `SyncStateJson`) | the local state under `<configFolder>/sync/state/` |
| `SyncFolders`, `SyncFolderReset` | which repository the clone and the state belong to: `sync/repository-url.txt`; `Prepare(url, bases)` resets both when the URL changes or is cleared (below, Limits) |
| `SyncPlanner` (internal) | one run's merges, pure |
| `SyncChoiceRules` | which choices a conflict offers (`Allowed`: Keep both only for a gesture or command on both sides; the conflict dialog and the import review show it) and the choice actually applied (`Effective`, the nearest one with a note) |
| `ConflictResolution` (internal) | what answering conflicts does to the document, pure: a list of answers (theirs, or null for a deletion, with an effective choice), then `SyncDocumentBuilder.Build`. The sync passes one conflict; the Augram import passes every item of the file it adds or changes, with its two extras (arriving commands bound to the copy of a gesture kept both; own steps following their command) |
| `SyncCoordinator` (+ internal `SyncStoreWriter`, `SyncPublisher`, `SyncRepositoryFiles`) | a run and a resolution, end to end; `SyncStoreWriter.StillHolds` and `Replace` are the store-thread apply the Augram import uses too |
| `Files/`: `SyncFile`, `SyncAcknowledgement`, `SyncFileHeader`, `SyncFileSerializer` | one machine's file in the sync repo ("Sync file" below); `TryRead` never throws: a broken file is an error line |
| `SyncReport`, `SyncStatus`, `SyncJoin`, `SyncChoice`, `SyncResolution`, `SyncApplied`, `SyncMachineSummary`, `SyncNewerMachine` | the App's side of it |

## Items

A document splits into items keyed by kind + id. Each item's `Content` is its JSON as the config file writes it (`SyncItemJson`, which calls the same writers), so equal text is equal item and nothing is compared field by field.

| Kind | Key | Content |
|---|---|---|
| gesture | `GestureId` | the gesture as in `gestures` |
| group | `GroupId` | the group header: name, active, suppress globals, matcher; no categories, hold remaps or commands |
| category | group id + `CategoryId` (category ids are unique per group only) | `{ "group", "id", "name" }`, plus `useOn` when the category is not on every platform (format 3, 2026-10-08), so a category on every platform has the same content as before |
| hold remap (`holdRemap:`) | group id + `HoldRemapId` (unique per group only, like a category's) | `{ "group", "id", "name", "holdKey", "tapTimeMs", "isActive" }`, plus `useOn` when not every platform (F9, format 11, 2026-10-10): the header only; the commands under it are command items |
| command | `CommandId` | `{ "group": <id>, "command": { … } }`: moving a command to another group is a change of the command; without its own version; a command under a hold remap carries `holdRemap` and its `input` trigger, so moving it in or out of a hold remap is a change of the command; a command's own drag distance (its trigger hold's `dragDistancePx`) and its `notIn` (format 12, plan 0004; since format 13 the ids of Ignored › Per command entries, on any command) travel in it like any member, so changing either is a change of the command; so do a button trigger (`{ "button": "Left", "hold": … }`) and a command's `alsoIn` (format 14, plan 0005: the ids of Exclusions › Global entries) |
| own steps (`version:`) | the command's `CommandId` | `{ "command": <id>, "version": { "platform", "basedOn", "changedAt", "trigger"?, "steps" } }` (F8, 2026-10-07; the own `trigger` since format 6, 2026-10-09: a trigger changed on that platform is its own, and travels here rather than with the command; a button trigger there since format 14): a command's own steps for the platform it was not authored on, apart from the command so the original changing on one machine and the own steps on the other merge without a conflict; the rebuild puts them back on their command and drops them, with a repair line (`OwnStepsDropped`), when the command is gone; counted as a command change in the log |
| ignored app | its id | the ignored app as in `ignored`, with `"scope": "PerCommand"` for an Ignored › Per command entry (format 13, plan 0004; omitted for Ignored › Global, so a Global entry has the same content as before): both lists are one item kind, each with its own name space since format 15 (a Global and a Per command "Blender"; Joel, 2026-10-12), and moving an entry between them is a change of the item |

Moving a category or a hold remap to another group is a delete in one group and an add in the other. Gesture order is not synced (it is not an item property).

**Uncategorized on two machines** (Joel, 2026-10-11): every Global command is in a category, and the rules make "Uncategorized" for one left without (`../Mapping/README.md`, rule 8) with a fixed id (`…c00000000001`, or the next fixed one when a renamed one holds it). So two machines that each make it, migrating on load or after deleting different categories, publish the same item, which merges as added once; a renamed one keeps its id and the next one each machine makes is again the same. A file from a build without the rule (Global commands with no category) is normalised as it is read, so it carries the same Uncategorized and its commands read as already in it: no conflict, no format bump. Tests: `SyncUncategorizedTests`.

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

- name clash among gestures, among groups, among the ignored apps of one list (Global or Per command, format 15; "Incoming excluded app 'Blender' on Per command renamed 'Blender (2)': the name is taken."), among one group's categories, among the commands of one parent (one hold remap's commands, or one group's ordinary commands: `Mapping/CommandNames`, Joel 2026-10-10; an "Orbit" under Space and one under S, or one under Space and an ordinary one, do not clash) → the incoming one takes the first free " (2)", " (3)"… (Global is never renamed); the repair line says where ("Incoming command 'Orbit' under 'Space' in 'Blender' renamed 'Orbit (2)': the name is taken.");
- a trigger bound twice in one group (A7: triggers that overlap on Windows or macOS where both commands are used, combinations and conversions included, `MappingRules.Overlap`) → the incoming command is unbound (`Trigger.None`);
- an own version's trigger (format 6) naming a gesture that is gone, or overlapping another command of its group → that platform's trigger is unbound (`TriggerCleared`);
- a command whose group is gone → into Global, in Uncategorized;
- a command whose gesture is gone → unbound, when the merge removed that gesture or the command is incoming (a reference that was already dangling here is left alone);
- a command whose category is gone → in Uncategorized on Global ("is in Uncategorized now"), none in an app group;
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

**Conflicts show on one machine.** The machine whose merge finds a conflict keeps its own version, lists the conflict and publishes the key as `pending`; the other machine holds the key (keeps its own, no conflict) until the resolution arrives. Keep mine: our version stays, and since their version is now our base the other machine takes ours on its next sync. Take theirs: we take theirs; both now agree. Keep both (gestures and commands present on both sides): ours stays and theirs is added with a new id, renamed on a name clash and unbound if its trigger is taken; for other kinds, or when one side deleted the item, the nearest choice is applied and the report's note says so (`SyncChoiceRules`). Two machines that found the same conflict at the same moment both list it; the last resolution wins.

## The run (`SyncCoordinator.Run`)

0. With the `SyncFolders` (the App passes them): the folders are made to belong to the URL in the settings (Limits, below). Then the job's resolutions (`Run(join, resolutions)`, the conflict dialog's answers) are applied in order, as `Resolve` does, stopping at the first that fails; their notes lead the report.
1. Sync off (no repository) → `Off`. The machine id is generated on first use (`SettingsStore.EnsureMachineId`, on the store thread).
2. `Prepare(url)`, `Pull()`, `ReadMachineFiles()`; a failure → `Failed`, stores and state untouched.
3. Read the files (`SyncRepositoryFiles`): this machine's own (only its revision matters) and the others, in machine-id order. A file that does not parse, breaks a rule, sits under another id, or has steps this build cannot read is skipped with a note (merging the last kind would delete those steps everywhere; the note says to update Augram here).
   - **A newer Augram** (see Format version): a file whose header is newer than this build, or this machine having published a newer format → `NeedsUpdate` with `NewerMachines`, before the join question; nothing merged, applied, saved or published.
4. **Join**: no local state yet and other machines' files exist → without a `SyncJoin`, `NeedsJoinChoice` with each machine's name and counts, nothing changed. `UseRemote` adopts the newest other machine's gestures and mapping (its base is its own content); `Merge` merges with empty bases (two machines that imported StrokesPlus.net separately get everything twice, renamed).
5. Merge each other file in turn against its base (`SyncPlanner`), the result of one feeding the next; then apply once: one `ReplaceAll` per store that changed (one undo step each; the config file's backup-before-write keeps the old file).
6. Save each machine's new state, then publish (`SyncPublisher`) when there is something new: different items, different `merged` entries (a newer acknowledged revision alone does not count, or two idle machines would acknowledge each other forever), the repo lacking our newest revision, or a failed publish last time. The revision is saved locally before the push. Commit message: `sync from <MachineName>`.
7. Report: status (`UpToDate` or `Applied`, or `Failed` when publishing failed), counts relative to this machine before the run, every pending conflict, repairs, notes. One log line, source `sync`: Info with the counts, or Warning with the error line (or, for `NeedsUpdate`, the newest format found and this build's); the URL appears only as its host.

## Format version

`SyncFile.CurrentFormatVersion` (the file's `formatVersion`, separate from the config `schemaVersion`) is the sync's own contract. **Raise it with every change to what a sync item holds or to which item kinds exist**, and add a row here. Why: a build that reads a newer file drops what it cannot see, and its next publish deletes that on every machine (2026-10-07: the PC, still on an old build, had to be stopped by hand before it read the Mac's own steps). The last column is what a build one format older would have done, and why it pauses instead ("Paused: PC-WORK uses a newer Augram…").

| Format | When | What changed | A build one format older |
|---|---|---|---|
| 1 | before 2026-10-07 | every file before the member existed (no `formatVersion`) | |
| 2 | 2026-10-07 | F8 cross-platform commands: Use on, macOS executable names, a command's own steps as an item of their own | |
| 3 | 2026-10-08 | "Use on" on a category (the category item's `useOn`) | reads such a category as used everywhere and publishes it back that way |
| 4 | 2026-10-08 | the step types TypeText, Run and Display (`typeText`, `run`, `displayMode`, `hdr`) | cannot read a command holding one and drops it |
| 5 | 2026-10-09 | Display mode's "highest available" refresh (`"refreshHz": "highest"`) | refuses to read it |
| 6 | 2026-10-09 | trigger combinations (a trigger's `hold`, the click trigger `{ "click": true }`, an own-steps item's `trigger`); config schema 2 | reads a combination as the plain trigger and publishes it back that way (such a build also refuses the file by its schema) |
| 7 | 2026-10-09 | the step types Scroll and Clear clipboard (`scroll`, `clearClipboard`) | cannot read a command holding one and drops the step |
| 8 | 2026-10-09 | the StrokesPlus.net app definition's fields (regex executable names; a macOS executable path and window title; root, parent and control titles; owner, root, parent and control classes); config schema 3 | ignores them, matches too many windows and publishes the matcher back without them |
| 9 | 2026-10-09 | the Open app step (`openApp`) | keeps it as is but cannot run it |
| 10 | 2026-10-09 | a gesture's `originalSamples` (shape cleanup) | drops them and publishes the gesture without its original |
| 11 | 2026-10-10 | hold remaps (F9: the `holdRemap` item kind, a command item's `holdRemap`, the input trigger, the Remap step); config schema 4 | drops the hold remaps and publishes the group without them, cannot read an input trigger, keeps a Remap step only as is |
| 12 | 2026-10-10, plan 0004 | a command's own drag distance (a trigger hold's `dragDistancePx`, in a command item and an own-steps item's trigger) and a Global command's `notIn`; config schema 5 | drops both and publishes the commands without them |
| 13 | the same evening, plan 0004 revised | Ignored › Per command entries (an ignored app item's `"scope": "PerCommand"`), which a command item's `notIn` names instead of app groups; config schema 6 | reads such an entry as an ignore of the whole app, switches Augram off over it, and drops every "Not in" naming one |
| 14 | the same night, plan 0005 | button triggers (a command item's and an own-steps item's `{ "button": "Left", "hold": … }` trigger) and a command item's `alsoIn` (Exclusions › Global entries); config schema 7 | cannot read a file holding a button trigger, so skips that file; drops every "Also in" and publishes the commands without it |
| 15 | 2026-10-12 | nothing new in an item: ignored app names are unique per list (Global, Per command), no longer across both, so a file may hold a Global and a Per command "Blender" | renames the second one "Blender (2)" on merge and publishes the rename back; the version raise pauses it until it runs this build |
| 16 | 2026-10-13, plan 0011 | a Hotkey step's `layouts` (one shortcut per keyboard layout), inside the step wherever steps travel: a command item's `steps` and an own-steps item's; config schema 8 | reads the Hotkey step without its layout shortcuts (a step's parameter reader passes an unknown member over) and publishes the command without them on every machine |

- `SyncFileSerializer.ReadHeader` reads both versions and the machine name before anything else. A file newer in either goes to `SyncRepositoryFiles.Newer` unread, and the run answers `NeedsUpdate`. `Read` refuses such a file too.
- **Never downgrade.** The publisher records the format it publishes in `SyncBaseStore.PublishedFormatVersion` (before the push; only ever raised). A build that writes an older format answers `NeedsUpdate` naming this machine, as it does when this machine's own file in the repo is newer.
- **An older file still merges**: members it lacks read as their defaults. That is safe because a build with this guard never publishes after seeing a newer file, so an older file never acknowledges items it could not read. Builds from before the guard have no such protection; keep every machine on one that has it.

## Sync file (`Files/SyncFileSerializer`)

`machines/<machineId>.json` in the sync repo (F8 sync; the merge is above): the config file's `gestures` and `mapping` members, written and read by exactly the same code, under a machine header. Settings never travel.

```json
{
  "schemaVersion": 8,
  "formatVersion": 16,
  "machine": { "id": "<machine id>", "name": "PC-HOME", "writtenAt": "2026-10-07T18:30:00+00:00", "revision": "<new Guid per publish>" },
  "merged": [
    { "machineId": "<other machine>", "revision": "<its revision merged here>", "except": ["command:<id>"], "pending": ["command:<id>"] }
  ],
  "gestures": [ … ],
  "mapping": { "groups": [ … ], "ignored": [ … ] }
}
```

`merged` has one entry per other machine this one has merged: the revision of its file, the item keys of that revision not taken (`except`: pending conflicts on either side) and the keys this machine has a conflict on with it, waiting for the user (`pending`; the other machine leaves those alone). The schema version is the config file's: newer is refused, older is migrated by `../Config/ConfigMigrations` (same members, same steps). `formatVersion` is the sync's own (`SyncFile.CurrentFormatVersion`; missing = 1): newer is refused, older reads with defaults. `ReadHeader` reads both and the machine name alone, so the sync can pause on a file a newer Augram wrote without reading the rest (Format version below; `SyncFileHeader`: `IsNewer` for this build, `IsNewerThan(schema, format)` for what an older build answers). Reading validates with `GestureRules` and `MappingRules`; a file that breaks a rule is an error like a malformed one. Steps of an unknown type are kept as is with a notice, as on load (`../Steps/Unknown/`); the sync then skips such a file rather than merge what it cannot read.

## Threading

`Run` and `Resolve` are for the **sync worker** (they take a lock; calling them on the store thread would deadlock on `onStoreThread`). `PendingConflicts()` is safe from any thread: a snapshot as of construction, the last run or resolution, or a folder reset (which empties it). The git calls, the merge and the state files run there. Every store mutation goes through the `onStoreThread` delegate the host passes (`Func<Func<SyncApplied>, SyncApplied>`: run the function on the stores' thread and return its result; tests pass `f => f()`). The worker merges against `GestureLibrary.All` and `MappingStore.Current` read as snapshots; the store-thread function applies only if both are still the same instances, else returns `SyncApplied.Moved` and the worker merges again (at most `MaxAttempts`), so an edit made meanwhile is never lost. The stores raise `Changed` there, so `ConfigSession` saves and the engine swaps as for any edit. After `Resolve`, the next `Run` publishes; the App's change-triggered sync does that.

## State files (`<configFolder>/sync/state/`)

- `machines/<id>.json`: one `SyncMachineState` (name, merged revision, applied acknowledgement, held keys, pending conflicts with both contents, base).
- `published/<sequence>-<revision>.json`: our newest 20 revisions (items and `merged`), named by a counter so age never depends on the clock.
- `publish-pending.txt`: present after a failed publish.
- `published-format.txt`: the newest sync format this machine has published (see Format version).

An unreadable state file is reported and treated as absent (no base: conflicts rather than guesses). All writes are temp file + atomic replace.

## Limits

- Contents are compared as text: a release that changes how an item is written makes items here look changed once (they are kept and published, not lost); a format change needs a schema version and a migration (`../Config/ConfigMigrations`), and any change to what an item holds raises the sync format version.
- The state belongs to one repository, the one `SyncFolders` remembers in `sync/repository-url.txt` (a URL string and a folder; Core still knows nothing of git). Before every run the coordinator calls `SyncFolders.Prepare(url)`: no marker yet → remember the URL (an existing clone is taken as this URL's); a different URL or none → move `sync/repo` aside to `sync/repo-old-<yyyyMMdd-HHmmss>` (never deleted; the git adapter refuses a clone of another URL), `SyncBaseStore.Clear()` (every machine's state, the published revisions, the pending-publish flag, the published format), remember the new URL (or delete the marker), forget the pending conflicts with the state, and log "Sync state cleared: the repository changed" (host only). The next run asks the join question again; a URL changed just before quitting is reset at the next start. A clone that cannot be moved (a file open in it) fails the run ("The old sync folder could not be moved aside: …", logged as "Sync folder could not be prepared") and nothing changes.

**May reference:** `Abstractions` (`ISyncRepository`, `IClock`, `IEventLog`), `Config` (settings; the config file's writers, readers and migrations, which the sync file and item JSON go through), `Diagnostics`, `Gestures`, `Mapping`, `Steps`. **Referenced by:** the App's sync: `Hosting/Modules/SyncModule` and `SyncModuleOptions` (composition); the runtime and dialogs in `App/Sync/` (`SyncService` over `SyncWorker` and `SyncScheduler`, `SyncStoreThread`, `SyncSnapshot`/`SyncPhase`/`SyncStatusText`, `SyncPrompter`, the join and conflict presenters, `SyncConflictSideText`; map in `../../Augram.App/Sync/README.md`); the view models `SyncViewModel`, `SyncConflictsViewModel`, `SyncJoinViewModel`, `AugramImportViewModel`; `Screens/AugramImportScreen`; `Components/SyncConflictList`; `DevGallery/SyncGalleryPages`. `Tray/SyncTrayItem` and `SyncHealthContributor` read it only through `SyncService`. Also `Transfer` (an import is this merge without a base: it reuses the items, `SyncConflict`, `SyncChoice`, `SyncChoiceRules`, `ConflictResolution` with its two extras, `SyncDocumentBuilder` with its repairs, `SyncStoreWriter`'s store-thread apply, and `SyncCounts`; a change to any of them changes imports too, `../Transfer/README.md`).
