# Core/Transfer

Export and import of Augram JSON (requirements F8: "Export is the same format as the on-disk file … Import merges rather than replaces, with a per-item conflict choice"; [plan 0003](../../../docs/plans/0003-export-import.md), whose "Decided for Joel" list is the source of the rules below). A file is the config file with some members left out; an import is the sync's merge without a base. Pure, except `ImportResult.ApplyTo`, which calls the stores.

Why a folder of its own: it sits on `Config` (the format: reader, writer, migrations) and `Sync` (the item merge, its conflicts and repairs). Config may not reference Sync, and Sync is about machines.

## Types

| Type | Role |
|---|---|
| `TransferFile` | an Augram JSON file: `Gestures`, `Mapping` (null = none in the file), `Settings` (null = none; never with the sync section), `SchemaVersion` as written, `Notices` from reading; `Extension` = `.augram.json` |
| `TransferSerializer` | `Write(file)`: `schemaVersion`, then `settings` (only when there are some, **never its `sync` member**), `gestures` (always, maybe empty), `mapping` (only when there is one), each through the config file's own writers. `Read` / `TryRead(json, steps)`: `ConfigSerializer.Read(root, …, "The file")` (the config file's version check, migrations and deserialisation), then `GestureRules`, `MappingRules`, `SettingsRules`; the sync section of a file that has one is dropped |
| `ExportScope` | `Everything`, `GesturesOnly`, `Of(groups, ignored apps)` (a `Selection`) |
| `Exporter` | `Export(scope, ConfigDocument)` → `TransferFile`; `SuggestedFileName(scope, mapping, date)` ("Augram Blender 2026-10-10.augram.json") |
| `TransferContents` | what a file holds, for both dialogs; `PrivateTextSteps` counts the steps whose type says `MayHoldPrivateText` (Type text, Run, imported placeholders), own versions included; `IsShell(group)` |
| `ImportMatcher` (internal, + `.Mapping.cs`) | lines a file up with this configuration (below) |
| `ImportPlan`, `ImportEntry`, `ImportStatus`, `ImportMatch`, `ImportMatchKind` | `ImportPlan.Create(file, current, options)`: one entry per file item, New / Same / Different, with how it matched when not by id; the differing ones as `SyncConflict`s (`MachineName` = `ImportOptions.SourceName`); `HasSettings`, `SettingsDiffer`, `IsEmpty`, `Preview`; `Resolve(choices)` |
| `ImportOptions`, `ImportChoices` | the source's name and whether to match gestures by shape (the App passes the recognition options); per-key `SyncChoice`s, a default for the rest, and whether to take the file's options |
| `ImportResolution` (internal) | the choices applied to the items, then `SyncDocumentBuilder.Build` (below) |
| `ImportResult` | gestures, mapping, options, `SyncCounts`, `SyncRepair`s, notes; `ApplyTo(settings, gestures, mapping)` |

## Export

- **Everything:** the options without `sync`, every gesture, the whole mapping. **Gestures only:** the library. **A selection:** its groups whole (header, categories, hold remaps, commands with their own versions; Global is selectable), its ignored apps, and the gestures its commands' triggers name (original and own version), in library order.
- **Every file has a Global group**, because the format requires one: when Global was not selected it is written as an empty shell (its header only). So an export always reads as a valid config file too.
- Ids, names, active flags and everything else are written exactly as on disk. Nothing is stripped from steps: the dialog warns with `TransferContents.PrivateTextSteps`.

## Import

**Reading:** a newer schema is refused ("The file was written by a newer Augram (schema version 5); this build reads up to version 4."), an older one migrated, a file breaking a rule refused whole ("The file breaks a rule: …"), a step of a type this build lacks kept as is with a notice (`../Steps/Unknown/`). Any Augram JSON reads: an export, `augram.json`, a file from `backup/`, a sync machine file (its machine header is passed over).

**Matching (`ImportMatcher`).** Ids are kept: a file item whose id is here is that item. A file item whose id is unknown here takes the id of the local item it matches, and the file's references to it follow (a command's gesture, original and own trigger; its category; its hold remap):

| Kind | By, in order | Within |
|---|---|---|
| gesture | id, name, shape (first sample ≥ `ConfusionCheck.DuplicateCutOff`, only with `ImportOptions.MatchShapes`) | the library |
| app group | id (Global is always Global), name | the mapping |
| category | id, name | the matched group |
| hold remap | id, hold key, name | the matched group |
| command | id, name among its siblings (same hold remap, or the group's ordinary commands) | the matched group |
| ignored app | id, name | the ignore list |

A local item matched once (by id, or by an earlier file item) is never matched again, so no two file items share a key; a file group with no counterpart keeps its ids, its children too. A command matched by id may sit in another group here: the merge reads that as a change (a move).

**The plan.** The matched file is split into sync items (`SyncItemSet.From`), minus the Global header when the file's Global is a shell (it imports nothing, not even its active flag). Each item against this configuration, by key: absent here → **New**; the same content → **Same**; other content → **Different**, a `SyncConflict` keeping mine until chosen. That is the sync's merge table with an empty base (`../Sync/README.md`); no row deletes, so an import never removes what the file lacks. A command's own steps (`version:` items) are an entry of their own unless their command is Different; then they follow its choice and are no entry.

**Resolving (`ImportResolution`).** The local items in order; a New file item appended; a Same one skipped; a Different one by its effective choice (`ConflictResolution.Effective`, the sync's: Keep both only for gestures and commands, else Take theirs with a note):

| Choice | Gesture | Command | Anything else |
|---|---|---|---|
| Keep mine | mine stays; the file's commands bind to it | mine stays, and the file's own steps for it are left out | mine stays |
| Take theirs | the file's in place of mine, under my id: every command using it gets the file's shape | the file's in place of mine, with the file's own steps | the file's in place of mine |
| Keep both | the file's added under a new id; **the file's commands that arrive (added, taken or copied) bind to it**, mine keep mine | the file's added under a new id with the file's own steps | (Take theirs, with the note) |

Then `SyncDocumentBuilder.Build(items, arriving, local)`, exactly as a sync merge: what was here keeps its name and trigger, an arriving item that clashes is renamed " (2)" or unbound (A7), dangling references are cleared, the hold remap rules are checked, and the result passes `GestureRules` and `MappingRules`. With every choice Keep mine the result equals `ThreeWayMerge.Merge(SyncItemSet.Empty, mine, file)` (a test holds that).

**Options** (`ImportChoices.TakeSettings`, off by default): capture, trail, recognition and no-match whole, and General's stroke button and ignore keys; never the sync section, start at login, enabled or the menu-bar icon (`ImportResolution.WithOptionsFrom`, the one place that decides).

**Applying.** `ImportResult.ApplyTo` replaces what changed, one `ReplaceAll` / `Apply` per store (one undo step each, like a sync apply; the config file's backup before every write keeps the previous file). Make the plan from the stores' snapshots (`ConfigSession.Document`): `ApplyTo` refuses (false, nothing applied) when a store no longer holds them (a sync applied while the review was open), and the caller plans again with the same choices, which are keyed by item and so carry over.

## Threading

Everything but `ApplyTo` is pure and may run anywhere. `ApplyTo` runs on the stores' thread (the UI thread in the app), like every store mutation.

**May reference:** `Config`, `Gestures`, `Mapping`, `HoldRemaps`, `Recognition`, `Steps`, `Sync`, `Abstractions`. **Referenced by:** the App's export and import (`src/Augram.App/Transfer/`, the view models `ExportViewModel` and `AugramImportViewModel`; plan 0003 steps 3–4, built 2026-10-10; App README "Export and import of Augram files").
