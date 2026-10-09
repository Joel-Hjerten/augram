# Core/Config

The on-disk configuration (requirements F8, checklist A3/A17) and the session that owns the three live stores behind the Options page, the gesture library and the Commands/Ignored tabs. Landed in M1 step 2; the mapping joined in M2 step 1; the sync settings and the sync file format (F8 sync) on 2026-10-07.

## Types

| Type | Role |
|---|---|
| `ConfigDocument` | the whole file: `SchemaVersion` (const `CurrentSchemaVersion` = 2 since 2026-10-09, trigger combinations), `Settings`, `Gestures`, `Mapping` (`MappingDocument`, default `Empty`). `Default` = default settings + `StarterGestures` + an empty mapping |
| `Settings` | one record per subsystem: `General` (`GeneralSettings`), `Capture` (`CaptureThresholds`, reused), `Trail` (`TrailSettings`), `Recognition` (`RecognitionOptions`, reused), `NoMatch` (`NoMatchBehaviour`), `Sync` (`SyncSettings`). All of it stays on this machine: sync carries gestures and the mapping only |
| `SyncSettings`, `SyncSettingsRules` | F8 sync, local: `RepositoryUrl` (null = off), `MachineId` (empty until `SettingsStore.EnsureMachineId` generates it once; that clears the settings undo history so no undo can bring the empty id back), `MachineName` (default `Environment.MachineName`), `AutoSync` (default true). Rules: the name is not blank; the URL is `https://…`, `git@host:path`, a full local path or `file://`, never with a user name or token (`UserInfoProblem` says why: git's credential helper signs in). `Host(url)` is all the log may show. `SettingsStore.SetSync` trims, turns a blank URL into null and keeps the machine id |
| `SyncFile`, `SyncAcknowledgement`, `SyncFileHeader`, `SyncFileSerializer` | one machine's file in the sync repo, see "Sync file" below. `TryRead` never throws: a broken file is an error line |
| `SyncItemJson` (internal) | the canonical text of one sync item (`../Sync/README.md`), written and read back by the writers and readers above |
| `GeneralSettings` | stroke button (`Capture.MouseButton`, default Right), `IgnoreKeys` flags, start at login, enabled |
| `TrailSettings`, `RgbColor` | width px 5, opacity 0.5, colour `#00FF40` |
| `ConfigSerializer` | `Write(document)` (indented, always the current schema version) and `Read(json)` / `Read(json, stepRegistry, notice)` (version check, migrate, deserialize, validate); every failure is a `ConfigFormatException` with a message fit for the log. The top-level envelope is written by hand so the `mapping` member can go through the two classes below |
| `ConfigMigrations` | `Migrate(JsonObject, fromVersion)`: forward-only steps on the JSON tree, one `case` per version bump. 1 → 2 changes no node (every new member is optional and absent means what version 1 meant); the bump exists so a version 1 build refuses a version 2 file instead of dropping its combinations (below, "Older builds") |
| `ConfigJsonContext` + `*JsonConverter` | source-generated `JsonSerializerContext` (trimming/AOT safe) for settings and gestures; converters give gesture ids as Guid strings, samples as one-line `[[x,y],...]`, trail colour as hex |
| `MappingJsonWriter`, `MappingJsonReader`, `CommandStepJsonReader`, `JsonMembers` | the `mapping` subtree, by hand over `Utf8JsonWriter` / `JsonObject` because a step's `params` are whatever its `IStepType.Write` returns and are read back through the `StepRegistry`. A step whose type the registry lacks, or whose parameters its type refuses (`StepFormatException`), is **dropped with one notice** and the rest of the file loads; a broken override is dropped alone. Structural faults (no `id`, a `trigger` of the wrong shape) are `ConfigFormatException`s like any other |
| `IConfigStore` | the port: `Location`, `Load()`, `Save(document)`. Lives here, not in `Abstractions/`, because only Core implements it |
| `FileConfigStore`, `ConfigBackups` | `<folder>/augram.json`; see below. Takes the `StepRegistry` to read with (`BuiltIn` by default; tests pass their own) |
| `DefaultConfigFolder` | `%APPDATA%\Augram`, `~/Library/Application Support/Augram`, `$XDG_CONFIG_HOME/Augram`; the folder is a constructor argument everywhere else (A17) |
| `SettingsStore`, `SettingsRules`, `SettingsValidationException` | the settings aggregate store with undo |
| `ConfigSession` | wires store + the three aggregates (`Settings`, `Gestures`, `Mapping`) and does live save |

## File shape

```json
{
  "schemaVersion": 2,
  "settings": {
    "general": { "strokeButton": "Right", "ignoreKey": "None", "startAtLogin": false, "enabled": true },
    "capture": { "startDistancePx": 30, "minSegmentPx": 6, "cancelDelayMs": 1000, "resetCancelDelayOnMovement": true },
    "trail": { "widthPx": 5, "opacity": 0.5, "colour": "#00FF40" },
    "recognition": { "precision": 100, "threshold": 75, "scoringMode": "Legacy", "sampleAggregation": "Average" },
    "noMatch": "DoNothing",
    "sync": { "repositoryUrl": null, "machineId": "00000000-0000-0000-0000-000000000000", "machineName": "PC-HOME", "autoSync": true }
  },
  "gestures": [
    { "id": "4f0c...-...", "name": "Up", "isActive": true, "samples": [ [[50,100],[50,90],[50,80]] ] }
  ],
  "mapping": {
    "groups": [
      { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "isActive": true, "suppressGlobals": false, "matcher": null,
        "categories": [ { "id": "9a1e...-...", "name": "Media" }, { "id": "5b70...-...", "name": "PC tools", "useOn": ["windows"] }, { "id": "c3d2...-...", "name": "Window" } ],
        "commands": [
          { "id": "…", "name": "Minimize", "trigger": { "gesture": "4f0c...-..." }, "isActive": true, "category": "c3d2...-...",
            "steps": [
              { "type": "windowOp", "authoredOn": "Windows", "isActive": true, "params": { "operation": "Minimize" } }
            ] },
          { "id": "…", "name": "Delete word", "trigger": { "gesture": "…" }, "isActive": true,
            "steps": [ { "type": "hotkey", "authoredOn": "Windows", "isActive": true, "params": { "modifiers": "Control", "key": "Backspace" } } ],
            "ownVersion": { "platform": "MacOS", "basedOn": "3f9a0c1d2e4b5a69", "changedAt": "2026-10-07T21:40:00.000Z",
              "steps": [ { "type": "hotkey", "authoredOn": "MacOS", "isActive": true, "params": { "modifiers": "Alt", "key": "Backspace" } } ] } },
          { "id": "…", "name": "Volume up", "trigger": { "wheel": "Up" }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Zoom in", "trigger": { "wheel": "Up", "hold": { "buttons": "Right" } }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Reopen tab", "trigger": { "gesture": "…", "hold": { "buttons": "Stroke", "keys": "Shift", "capture": "Before" } }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Alt click", "trigger": { "click": true, "hold": { "buttons": "Stroke", "keys": "Alt" } }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Not bound yet", "trigger": null, "isActive": false, "note": "Imported from StrokesPlus.net: script-only action …", "steps": [] }
        ] },
      { "id": "…", "name": "Chrome", "isActive": true, "suppressGlobals": false,
        "matcher": { "processNames": ["chrome.exe"], "processPath": null, "processPathIsRegex": false, "title": null, "titleIsRegex": false, "classChain": [], "ignoreWhenFullScreen": false },
        "commands": [ { "id": "…", "name": "Close", "trigger": { "gesture": "…" }, "isActive": true, "steps": [] } ] }
    ],
    "ignored": [
      { "id": "…", "name": "Game", "isActive": true, "matcher": { … }, "disableEntirely": true }
    ]
  }
}
```

camelCase names, enums as strings (flags comma-separated), comments and trailing commas tolerated on read. A missing settings section or member takes its default; a gesture must have `id`, `name` and `samples`. Everything else in the file is written in full by Augram, so a diff after an edit shows only the edit.

Mapping specifics (`../Mapping/README.md` has the model): groups are written Global first then by name, commands by name. A matcher's `processNames` are the Windows names (the key predates the macOS list); `macProcessNames` is written only when it has names, and a group's `useOn` (`["windows"]`, `["macos"]`; target names, so specific machines can join later) only when the group is not on every platform, so files and sync items without them read and write as before. A trigger is `{ "gesture": "<id>" }`, `{ "wheel": "Up" | "Down" }`, `{ "click": true }` (schema 2) or `null` (not bound), with a `hold` (schema 2, F1 combinations) only when it holds more than the stroke button alone: `buttons` (`HeldButtons` names, comma-separated: `Stroke`, `Left`, `Middle`, `Right`, `X1`, `X2`; missing is `Stroke`), `keys` (`KeyModifiers` names: `Control`, `Alt`, `Shift`, `Meta`; omitted when none) and `capture` (`Before` or `After`; omitted for `Either`). A plain trigger therefore writes exactly as in schema 1; a name or bit the enum lacks is a format error naming the member. An own version's `trigger` (schema 2) is that platform's own trigger, `null` when it is unbound there, and is omitted while the platform uses the original's, converted. A step's `params` is exactly what its type's `Write` returned and goes back through its `Read`; a command's `ownVersion` (F8, 2026-10-07) holds the steps of the platform it was not authored on with `platform`, `basedOn` (the fingerprint of the original it was made from or last checked against) and `changedAt` (UTC, display only), and is omitted while that platform runs the converted original; a step-level `overrides` member from the earlier design is passed over on read; `note` is omitted when null. A group's `categories` (`{ "id", "name" }`, sorted by name; a category's `useOn` (F8, 2026-10-08) is written like a group's and a command's, only when it is not every platform) is omitted when empty and a command's `category` (the id of one of its group's categories) when the command is Uncategorized, so a file without categories looks as it did before they existed (schema version 1, additive; a category without `useOn` is on every platform). On read, a group or command needs `id` and `name`; everything else takes its default (`isActive` true, `suppressGlobals` false, `trigger` null, `steps` empty, `categories` empty, `category` null, `authoredOn` Windows, `matcher` null for a group and empty for an ignored app). A category without a Guid `id` or without a name is dropped with a notice; a category `useOn` that is not a list of strings reads as every platform with a notice (a group's or command's is a format error, as before); a `category` that is not a Guid string reads as Uncategorized with a notice; one that names no category of its group is cleared silently by `CategoryRules` when the store takes the document. A missing `mapping` member, or a missing `groups` array, is `MappingDocument.Empty` (just the Global group), which is why files written before M2 still load under schema version 1 without a migration.

The `sync` section (schema version 1, additive): missing, null or partial takes the defaults above (`repositoryUrl` null, `machineId` empty, `machineName` the OS machine name, `autoSync` true).

## Sync file (`SyncFileSerializer`)

`machines/<machineId>.json` in the sync repo (F8 sync; the merge is `../Sync/README.md`): the config file's `gestures` and `mapping` members, written and read by exactly the same code, under a machine header. Settings never travel.

```json
{
  "schemaVersion": 2,
  "formatVersion": 7,
  "machine": { "id": "<machine id>", "name": "PC-HOME", "writtenAt": "2026-10-07T18:30:00+00:00", "revision": "<new Guid per publish>" },
  "merged": [
    { "machineId": "<other machine>", "revision": "<its revision merged here>", "except": ["command:<id>"], "pending": ["command:<id>"] }
  ],
  "gestures": [ … ],
  "mapping": { "groups": [ … ], "ignored": [ … ] }
}
```

`merged` has one entry per other machine this one has merged: the revision of its file, the item keys of that revision not taken (`except`: pending conflicts on either side) and the keys this machine has a conflict on with it, waiting for the user (`pending`; the other machine leaves those alone). The schema version is the config file's: newer is refused, older is migrated by `ConfigMigrations` (same members, same steps). `formatVersion` is the sync's own (`SyncFile.CurrentFormatVersion`; missing = 1): newer is refused, older reads with defaults. `ReadHeader` reads both and the machine name alone, so the sync can pause on a file a newer Augram wrote without reading the rest (`../Sync/README.md`, Format version; `SyncFileHeader`: `IsNewer` for this build, `IsNewerThan(schema, format)` for what an older build answers). Reading validates with `GestureRules` and `MappingRules`; a file that breaks a rule is an error like a malformed one. Steps of an unknown type are dropped with a notice, as on load; the sync then skips such a file rather than merge it lossily.

## Store contract (`FileConfigStore`)

- **Save**: serialize first (a bad document never touches disk), copy the current file to `backup/augram-yyyyMMdd-HHmmss.json` (`-2`, `-3`... within one second), prune backups to the newest 20, write `augram.json.tmp`, then `File.Move(overwrite)` so readers see the old file or the new one, never a partial one.
- **Load** falls back in order: main file → newest backup that parses → `ConfigDocument.Default`. Each step reports one line through the `Action<string>? notice` callback (the host wires it to the event log). Load never throws for a missing or corrupt file.
- Schema: `Read` refuses a `schemaVersion` greater than `CurrentSchemaVersion` ("written by a newer Augram"), migrates a smaller one, and the store then treats the refusal like any other unreadable file (fall back to a backup).

### Older builds (2026-10-09, schema 2)

An older build sharing the config folder (Joel's installed 0.4.0 beside a dev build) reads schema 1 only. Had combinations been added under schema 1, it would have read `{ "gesture": …, "hold": … }` as the plain gesture and saved it back without the hold at its next change (silently dropping the combination), and refused the whole file at the first `{ "click": true }`. With schema 2 it refuses the file instead ("written by a newer Augram"), loads the newest backup it can read, and runs on that; it cannot misread a combination. It does save over the newer file at its first change (copying the newer one into `backup/` first): a build already released cannot be changed. So that this never goes unnoticed, `Load` here reports a main file older than the newest backup ("saved by an older Augram (schema 1) over a newer one (schema 2); what only the newer one could hold, such as trigger combinations, is in backup/…"). Nothing is restored automatically, since that would drop what was edited in the older build. Keep every machine's Augram on a build that reads schema 2 once combinations are used; the sync format version (6 for combinations, 7 since the Scroll and Clear clipboard steps) pauses an older build's sync the same way.

A **new step type** has so far gone without a schema change (Type text, Run and Display; Scroll and Clear clipboard): an older build reading a config that holds one drops that step with a notice (`Step 1 of command 'Zoom In' in 'Windows Explorer' dropped: unknown step type 'scroll'.`, an Info line from `config`) and loads the rest; the command stays, without the step. That is quiet, not a refusal: if the older build then saves (at its next change), the step is gone from the main file, and only the backups taken before (the newest 20 are kept) still hold it. The sync format version is raised instead (4, then 7), so an older build pauses its sync rather than publish the loss to every machine.

## Stores, undo, live save

- `SettingsStore`, `GestureLibrary` (`../Gestures/`) and `MappingStore` (`../Mapping/`) are the only mutable owners of their aggregates (ADR-0002 §5a). Every mutation validates (`SettingsRules`, `GestureRules`, `MappingRules`), records the previous snapshot in an `UndoStack<T>` (`../State/`, cap 100), replaces the snapshot, increments `Version` and raises `Changed`. Undo/redo are changes too: they bump `Version`, raise `Changed` and therefore get saved. Each store has its own history; Ctrl+Z on the Options page undoes settings, on the Gestures page undoes gestures, on the Commands page undoes the mapping.
- Validation failures throw before anything is recorded; the store is unchanged.
- `ConfigSession` loads once in its constructor, builds the three stores and subscribes. A change marks the session dirty, raises `DocumentChanged` synchronously (the engine swaps to `Document` here), cancels any pending save and schedules a new one `SaveDelay` (250 ms) later through the injected `Func<Action, IDisposable>`; a burst of edits is one write and one backup. `Flush()` writes now; `Dispose()` flushes. A background save that fails with an I/O error is reported through the notice callback and stays pending for the next change or flush.
- On load, out-of-range settings fall back to `Settings.Default` with a notice; a gesture set that breaks a rule (duplicate names, say) is loaded one gesture at a time, skipping and reporting the offenders, and the undo history is cleared afterwards. A mapping that breaks a rule is loaded the same way, group by group and command by command (a second Global group, a duplicate name, a gesture bound twice in one group: each skipped with a notice naming it), and its history cleared. Categories load one at a time between the group and its commands, so a duplicate category costs only itself and its commands load Uncategorized. Steps that could not be read were already dropped by the serializer, with their own notices, before the session saw the document.

## Threading

Single-writer. The stores and `ConfigSession` are mutated from one thread (the UI thread in the app) and are not synchronised. The host's scheduler must run the save on that same thread (a `DispatcherTimer`). Other threads read `Current` / `All` as immutable snapshots after `DocumentChanged` (the engine resolves commands against `Mapping.Current` this way); they never call a mutator. Core has no timer of its own: the delay is the host's job.

**May reference:** `Gestures`, `Mapping`, `Steps`, `Capture`, `Recognition`, `State`. **Referenced by:** the composition root, view models, the engine (read-only snapshots), the importer (`ConfigDocument` for export), `Sync` (settings, the sync file, the item JSON).
