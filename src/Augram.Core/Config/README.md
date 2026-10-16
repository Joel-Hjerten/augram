# Core/Config

The on-disk configuration (requirements F8, checklist A3/A17) and the session that owns the three live stores behind the Options page, the gesture library and the Commands/Ignored tabs. Landed in M1 step 2; the mapping joined in M2 step 1; the sync settings and the sync file format (F8 sync) on 2026-10-07.

## Types

| Type | Role |
|---|---|
| `ConfigDocument` | the whole file: `SchemaVersion` (const `CurrentSchemaVersion`, 7; each bump is a row of "Schema history" below), `Settings`, `Gestures`, `Mapping` (`MappingDocument`, default `Empty`). `Default` = default settings + `StarterGestures` + an empty mapping |
| `Settings` | one record per subsystem: `General` (`GeneralSettings`), `Capture` (`CaptureThresholds`, reused), `Trail` (`TrailSettings`), `Recognition` (`RecognitionOptions`, reused), `NoMatch` (`NoMatchBehaviour`), `Sync` (`SyncSettings`), `Appearance` (`AppearanceSettings`). All of it stays on this machine: sync carries gestures and the mapping only, and an export (`../Transfer/`) the options without `sync` and `appearance` |
| `AppearanceSettings`, `AppTheme`, `WindowBackground` | plan 0006, Options › Appearance › Theme: `Theme` Dark (or Light, System = follow the OS), `WindowBackground` FrostedGlass (or WallpaperTint, Solid), `TintPercent` 62 (0–100), `CornerRadiusPx` 12 (4–18), `AccentFollowsTrail` true, `Accent` `#F5C542` (used only when the accent does not follow the trail). `SettingsStore.SetAppearance` |
| `SyncSettings`, `SyncSettingsRules` | F8 sync, local: `RepositoryUrl` (null = off), `MachineId` (empty until `SettingsStore.EnsureMachineId` generates it once; that clears the settings undo history so no undo can bring the empty id back), `MachineName` (default `Environment.MachineName`), `AutoSync` (default true). Rules: the name is not blank; the URL is `https://…`, `git@host:path`, a full local path or `file://`, never with a user name or token (`UserInfoProblem` says why: git's credential helper signs in). `Host(url)` is all the log may show. `SettingsStore.SetSync` trims, turns a blank URL into null and keeps the machine id |
| `GeneralSettings`, `IgnoreKeyJsonConverter` (internal) | stroke button (`Capture.MouseButton`, default Right), the ignore keys (`IgnoreKey`, `Abstractions/KeyModifiers`: the engine's mask as it is; plan 0007 folded the setting's own enum into it, and the converter keeps the file's names, `"Control, Win"`, byte for byte), start at login, enabled, the macOS colour menu bar icon |
| `TrailSettings`, `RgbColor` | width px 5, opacity 0.5, colour `#F5C542` (plan 0006 decision 7, 2026-10-11; StrokesPlus.net's `#00FF40` before, and a file with a colour keeps it) |
| `ConfigSerializer` | `Write(document)` (indented, always the current schema version) and `Read(json)` / `Read(json, stepRegistry, notice)` (version check, migrate, deserialize, validate); every failure is a `ConfigFormatException` with a message fit for the log. The top-level envelope is written by hand so the `mapping` member can go through the two classes below. The internal `Read(root, steps, notice, what)` takes a tree already parsed by `ParseObject` and the subject of its messages: the export file reader (`../Transfer/TransferSerializer`) comes in there to see which members a file has and to say "The file was written by a newer Augram" |
| `ConfigMigrations` | `Migrate(JsonObject, fromVersion)`: forward-only steps on the JSON tree, one `case` per version bump. No bump so far changes a node (every new member is optional and absent means what the older version meant; "Schema history" below); each exists so an older build refuses the newer file instead of dropping or misreading what it cannot read ("Older builds") |
| `ConfigJsonContext` + `*JsonConverter` | source-generated `JsonSerializerContext` (trimming/AOT safe) for settings and gestures; converters give gesture ids as Guid strings, samples as one-line `[[x,y],...]`, the trail and appearance sections by hand (a missing member keeps its default, colours as hex), and the ignore keys by their file names |
| `MappingJsonWriter`, `MappingJsonReader` (each with a `.HoldRemaps.cs` and a `.Matcher.cs` part; the reader's triggers in `.Triggers.cs`), `CommandStepJsonReader`, `JsonMembers` | the `mapping` subtree, by hand over `Utf8JsonWriter` / `JsonObject` because a step's `params` are whatever its `IStepType.Write` returns and are read back through the `StepRegistry`. Every member is read through `JsonMembers`, this file's front for `../Json/JsonMember` (one reader with the steps' `StepParameters`; enums by name only). A step whose type the registry lacks, or whose parameters its type refuses (`StepFormatException`), is **kept as is with one notice** (`../Steps/Unknown/`, "Older builds" below) and the rest of the file loads; a broken override is dropped alone. Structural faults (no `id`, a `trigger` of the wrong shape) are `ConfigFormatException`s like any other; "The mapping's members" below says, member by member, what is a format error, what is dropped with a notice and what is normalised silently |
| `IConfigStore` | the port: `Location`, `Load()`, `Save(document)`. Lives here, not in `Abstractions/`, because only Core implements it |
| `FileConfigStore`, `ConfigBackups` | `<folder>/augram.json`; see below. Takes the `StepRegistry` to read with (`BuiltIn` by default; tests pass their own) |
| `DefaultConfigFolder` | `%APPDATA%\Augram`, `~/Library/Application Support/Augram`, `$XDG_CONFIG_HOME/Augram`; the folder is a constructor argument everywhere else (A17) |
| `SettingsStore`, `SettingsRules`, `SettingsValidationException` | the settings aggregate store with undo |
| `ConfigSession` | wires store + the three aggregates (`Settings`, `Gestures`, `Mapping`) and does live save |
| `ConfigRecovery` (internal) | what survives a loaded file that breaks a rule (`ValidSettings`, `ValidLibrary`, `ValidMapping`, each with the notice callback): strict first, then item by item; the load order is under "Stores, undo, live save". `ConfigSession` asks it once, at load; the sync and the imports refuse a whole file instead |

## File shape

```json
{
  "schemaVersion": 8,
  "settings": {
    "general": { "strokeButton": "Right", "ignoreKey": "None", "startAtLogin": false, "enabled": true },
    "capture": { "startDistancePx": 30, "minSegmentPx": 6, "cancelDelayMs": 1000, "resetCancelDelayOnMovement": true, "buttonDragDistancePx": 10 },
    "trail": { "widthPx": 5, "opacity": 0.5, "colour": "#F5C542" },
    "recognition": { "precision": 100, "threshold": 75, "scoringMode": "Legacy", "sampleAggregation": "Average" },
    "noMatch": "DoNothing",
    "sync": { "repositoryUrl": null, "machineId": "00000000-0000-0000-0000-000000000000", "machineName": "PC-HOME", "autoSync": true },
    "appearance": { "theme": "Dark", "windowBackground": "FrostedGlass", "tintPercent": 62, "cornerRadiusPx": 12, "accentFollowsTrail": true, "accent": "#F5C542" }
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
          { "id": "…", "name": "Zoom in", "trigger": { "wheel": "Up", "hold": { "buttons": "Right", "dragDistancePx": 3 } }, "isActive": true,
            "notIn": [ "2f6b…", "8c41…" ], "steps": [ … ] },
          { "id": "…", "name": "Reopen tab", "trigger": { "gesture": "…", "hold": { "buttons": "Stroke", "keys": "Shift", "capture": "Before" } }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Alt click", "trigger": { "click": true, "hold": { "buttons": "Stroke", "keys": "Alt" } }, "isActive": true, "steps": [ … ] },
          { "id": "…", "name": "Magnifier", "trigger": { "button": "Left", "hold": { "buttons": "Right" } }, "isActive": true, "alsoIn": [ "5e07…" ],
            "steps": [ { "type": "remap", "authoredOn": "Windows", "isActive": true, "params": { "output": "Key", "key": "X", "modifiers": "Shift, Meta" } } ] },
          { "id": "…", "name": "Not bound yet", "trigger": null, "isActive": false, "note": "Imported from StrokesPlus.net: script-only action …", "steps": [] }
        ] },
      { "id": "…", "name": "Chrome", "isActive": true, "suppressGlobals": false,
        "matcher": { "processNames": ["chrome.exe"], "processPath": null, "processPathIsRegex": false, "title": null, "titleIsRegex": false, "classChain": [], "ignoreWhenFullScreen": false },
        "commands": [ { "id": "…", "name": "Close", "trigger": { "gesture": "…" }, "isActive": true, "steps": [] } ] },
      { "id": "…", "name": "Blender", "isActive": true, "suppressGlobals": false, "matcher": { "processNames": ["blender.exe"], … },
        "holdRemaps": [ { "id": "a1…", "name": "Space", "holdKey": "Space", "tapTimeMs": 180, "isActive": true } ],
        "commands": [
          { "id": "…", "name": "Orbit", "trigger": { "input": { "buttons": "Left" } }, "isActive": true, "holdRemap": "a1…",
            "steps": [ { "type": "remap", "authoredOn": "Windows", "isActive": true, "params": { "output": "Button", "button": "Middle" } } ] },
          { "id": "…", "name": "Zoom both", "trigger": { "input": { "buttons": "Left, Right" } }, "isActive": true, "holdRemap": "a1…", "steps": [ … ] },
          { "id": "…", "name": "Grab", "trigger": { "input": { "key": "W" } }, "isActive": true, "holdRemap": "a1…",
            "steps": [ { "type": "remap", "authoredOn": "Windows", "isActive": true, "params": { "output": "Key", "key": "G" } } ] }
        ] }
    ],
    "ignored": [
      { "id": "…", "name": "Game", "isActive": true, "matcher": { … }, "disableEntirely": true },
      { "id": "5e07…", "name": "Blender", "isActive": true, "matcher": { "processNames": ["blender.exe"], … }, "disableEntirely": false },
      { "id": "2f6b…", "name": "Spine", "scope": "PerCommand", "isActive": true, "matcher": { "processNames": ["Spine.exe"], … }, "disableEntirely": false },
      { "id": "8c41…", "name": "Eyeris", "scope": "PerCommand", "isActive": true, "matcher": { … }, "disableEntirely": false }
    ]
  }
}
```

camelCase names, enums as strings (flags comma-separated), comments and trailing commas tolerated on read. A missing settings section or member takes its default; a gesture must have `id`, `name` and `samples`. Everything else in the file is written in full by Augram, so a diff after an edit shows only the edit.

### The mapping's members

`../Mapping/README.md` has the model. Groups are written Global first then by name, commands, categories and hold remaps by name; an optional member is written only when it differs from what its absence means, so a file without a newer member reads and writes as before. On read, a group or command needs `id` (a Guid string) and `name`; everything else takes the value under "Absent means". Every member goes through `JsonMembers` (`../Json/README.md`): enums by name only, case-insensitive. Unless a row says otherwise, a member of the wrong shape, or a name or bit its enum lacks, is a **format error naming the member** (the whole file is refused; the store falls back to a backup). "Dropped" is dropped with a notice naming it while the rest of the file loads; "the rules" is `MappingRules`, `CategoryRules` or `HoldRemapRules` normalising silently when the store takes the document.

| Member | Shape, and when it is written | Absent means | Otherwise | Schema |
|---|---|---|---|---|
| `mapping`, its `groups` | an object; an array of groups | `MappingDocument.Empty` (just the Global group), which is why files written before M2 still load under schema 1 without a migration | | 1 |
| group `id`, `name` | a Guid string; a string | required | | 1 |
| group `isActive`, `suppressGlobals` | `true` / `false` | `true`; `false` | | 1 |
| group `matcher` | an object (the matcher rows below) | `null` for a group, empty for an ignored app | | 1 |
| group, command `useOn` | `["windows"]`, `["macos"]` (target names, so specific machines can join later); only when not every platform | every platform | | 1 (F8) |
| matcher `processNames`, `processPath` | the Windows names and the Windows path (the keys predate the macOS ones) | not consulted | | 1 |
| matcher `macProcessNames` | only when it has names | none | | 1 (F8) |
| matcher `processNamesAreRegex`, `macProcessNamesAreRegex`, `macProcessPath`, `macTitle`, `rootTitle`, `parentTitle`, `controlTitle`, `ownerClass`, `rootClass`, `parentClass`, `controlClass` | each text with its `…IsRegex`; only when set, each toggle only when on | not consulted, so a matcher without them reads and writes as in schema 2 | | 3 |
| group `categories` | `[{ "id", "name", "useOn" }]`, sorted by name, omitted when empty; a category's `useOn` (F8, 2026-10-08) as a group's, only when not every platform | none; a category without `useOn` is on every platform | a category without a Guid `id` or without a name: dropped; a category `useOn` that is not a list of strings: every platform, with a notice | 1 (additive) |
| command `category` | the id of one of its group's categories; omitted when the command has none (an app group's command: on Global every command has one, Joel 2026-10-11) | none | not a Guid string: none, with a notice (`The category of command 'Close' in 'Global' dropped: 'category' must be a Guid string.`); naming no category of its group: cleared by the rules. On Global either way it goes into Uncategorized: a file whose Global commands have no category (written before 2026-10-11, or by hand) loads migrated, without a notice or a schema change (`../Mapping/README.md`, rule 8), and the next save writes it | 1 |
| group `holdRemaps` | `[{ "id", "name", "holdKey", "tapTimeMs", "isActive" }]` (F9), sorted by name, plus `useOn` when not every platform; omitted when empty | no hold remaps | an entry that is not an object, has no Guid `id`, or has a member of the wrong shape (an unknown `holdKey`, a `tapTimeMs` that is not an integer): dropped. A missing `name` is the hold key's name, `tapTimeMs` 180, `isActive` true, `useOn` every platform | 4 |
| command `holdRemap` | the id of one of its group's hold remaps; omitted for an ordinary command | an ordinary command | not a Guid string: none, with a notice; naming no hold remap of its group: an ordinary command without its input (`HoldRemapRules`, silently) | 4 |
| command `trigger` | `{ "gesture": "<id>" }`, `{ "wheel": "Up" \| "Down" }`, `{ "click": true }` (schema 2), `{ "input": … }` (schema 4), `{ "button": … }` (schema 7), or `null` (not bound) | not bound | | 1 |
| trigger `hold` | F1 combinations: only when it holds more than the stroke button alone, so a plain trigger writes exactly as in schema 1. `buttons` (`HeldButtons` names, comma-separated: `Stroke`, `Left`, `Middle`, `Right`, `X1`, `X2`; missing is `Stroke`), `keys` (`KeyModifiers` names: `Control`, `Alt`, `Shift`, `Meta`; omitted when none), `capture` (`Before` or `After`; omitted for `Either`) | the stroke button alone | | 2 |
| hold `dragDistancePx` | plan 0004: the command's own button drag distance, 1 to 200 px; only when set, which the rules allow only on a set without the stroke button (`TriggerHold.HandsBackDrags`, Right in Right + wheel); in a command's trigger and an own version's alike | the Options › Strokes › Capture value | not an integer: format error. Outside 1 to 200: dropped (`'dragDistancePx' of 'hold' of 'trigger' of command 'Zoom In' in 'Global' dropped: 0 is not between 1 and 200; the Options value is used.`) and the command keeps its trigger, as an out-of-range setting falls back to its default (left to the store, the value would cost the whole command on load, and refuse a whole sync or export file). On a set holding the stroke button: dropped by the rules | 5 |
| input trigger | the trigger of a command under a hold remap: `{ "input": { "buttons": "Left, Right" } }` (`HeldButtons` names), `{ "input": { "wheel": "Up" } }` or `{ "input": { "key": "W" } }` (a `KeyCode` name), never with a `hold`; its Remap step's `params` are `../Steps/Remap/README.md`'s | | | 4 |
| button trigger | plan 0005: `{ "button": "Left", "hold": { "buttons": "Right" } }`, the pressed button by its `MouseButton` name (`Left`, `Middle`, `Right`, `X1`, `X2`) and the set in `hold` as for a wheel trigger, `dragDistancePx` included; in a command's trigger and an own version's alike. Its `hold` is always written, since the set holds another button and not the stroke button. Its Remap step's `params` are a key output's | | without a `hold` it holds the stroke button, which the rules refuse. A name the enum lacks: `'button' of 'trigger' of command 'Magnifier' in 'Global' must be one of Left, Middle, Right, X1, X2.` | 7 |
| command `isActive` | `true` / `false` | `true` | | 1 |
| command `steps` | `[{ "type", "authoredOn", "isActive", "params" }]`; `params` exactly what the type's `Write` returned, read back through its `Read` | empty (an override to nothing) | an unknown `type`, or `params` its type refuses: kept as is, with a notice ("Older builds" below); no `type`, or `params` not an object: format error; `authoredOn` absent is Windows; a step-level `overrides` member from the earlier design is passed over | 1 |
| command `ownVersion` | F8, 2026-10-07: the steps of the platform the command was not authored on, with `platform`, `basedOn` (the fingerprint of the original it was made from or last checked against), `changedAt` (UTC, display only) and, schema 2, `trigger` (that platform's own trigger, `null` when it is unbound there, omitted while the platform uses the original's, converted); omitted while that platform runs the converted original | the converted original | a broken override is dropped alone | 1 |
| command `note` | a string; omitted when null | none | | 1 |
| command `notIn` | plan 0004: the ids of the Ignored › Per command entries the command is not used over (named app groups in schema 5), sorted by id, on any command not under a hold remap; only when it names one | no "Not in" | not an array of strings: format error; an entry that is not a Guid string: dropped; one naming no Per command entry (deleted, an Ignored › Global entry, an app group's id from a schema 5 file, unknown): dropped by `MappingRules`, as is the whole list under a hold remap | 5 (6) |
| command `alsoIn` | plan 0005: the ids of the Exclusions › Global entries without the disable-while-focused mode the command still works over, sorted by id, on a command not under a hold remap whose trigger holds no stroke button; written after `notIn`, only when it names one | no "Also in" | not an array of strings: format error; an entry that is not a Guid string: dropped (`An entry of 'alsoIn' of command 'Magnifier' in 'Global' dropped: "Blender" is not a Guid string.`); one naming no such entry (deleted, disable-while-focused, a Per command entry, unknown): dropped by `MappingRules`, as is the whole list under a hold remap and on a command whose trigger holds the stroke button on both platforms | 7 |
| ignored app `scope` | plan 0004 revised: `"PerCommand"` for an Ignored › Per command entry, which does nothing on its own and only stops the commands naming it (its `disableEntirely` is always false); plan 0012: `"WhileHolding"` for an Exclusions › While holding entry (schema 9; its `disableEntirely` is always false too); written only for those | `Global` (Ignored › Global), so a file without Per command entries reads and writes as in schema 5 | another value: `'scope' of ignored app 'Spine' must be one of Global, PerCommand, WhileHolding.` | 6 (9) |
| ignored app `holding`, `ownHolding` | plan 0012: a While holding entry's inputs, written after `disableEntirely`. `holding` is the original, `[{ "key": "S", "authoredOn": "Windows" }, { "modifier": "Alt", … }, { "button": "Middle", … }]` (a `KeyCode` name that is no modifier key, one `KeyModifiers` name: `Control`, `Alt`, `Shift`, `Meta`, or a `MouseButton` name), written when the entry has inputs on some side; `ownHolding` is the own version for the other platform, `{ "platform": "MacOS", "inputs": [ … ] }`, only when there is one | no inputs; the other platform inferred | an input with none of the three members, or a name its enum lacks: format error; `authoredOn` absent is Windows. On a Global or Per command entry: dropped by the rules. A modifier key as `key` (`"LeftAlt"`) becomes its modifier, an input with no key, and one repeated on a side, are dropped by the rules | 9 |

The `sync` section (schema version 1, additive): missing, null or partial takes the defaults above (`repositoryUrl` null, `machineId` empty, `machineName` the OS machine name, `autoSync` true).

The `appearance` section (plan 0006, 2026-10-11) is additive the same way, under schema 7 without a bump: missing, null or partial takes the defaults above; a name its enums lack, or a member of the wrong shape, is a format error naming it (`'theme' must be one of Dark, Light, System.`); out of range is the rules' (reset on load with a notice, like any setting). No bump, because an older build only passes the section over and, at its next save, writes the file back without it: that machine's look returns to the defaults, nothing else is lost, whereas a bump would make it refuse the whole file.

## Sync file

The sync repo's machine file (`SyncFile`, `SyncFileSerializer`) and a sync item's text (`SyncItemJson`) live in `../Sync/` (`Files/` and beside `SyncItem`) since plan 0007; they write and read through the writers and readers here, so the two formats never drift (`../Sync/README.md`, Sync file).

## Store contract (`FileConfigStore`)

- **Save**: serialize first (a bad document never touches disk), copy the current file to `backup/augram-yyyyMMdd-HHmmss.json` (`-2`, `-3`... within one second), prune backups to the newest 20, write `augram.json.tmp`, then `File.Move(overwrite)` so readers see the old file or the new one, never a partial one.
- **Load** falls back in order: main file → newest backup that parses → `ConfigDocument.Default`. Each step reports one line through the `Action<string>? notice` callback (the host wires it to the event log). Load never throws for a missing or corrupt file.
- Schema: `Read` refuses a `schemaVersion` greater than `CurrentSchemaVersion` ("written by a newer Augram"), migrates a smaller one, and the store then treats the refusal like any other unreadable file (fall back to a backup).

### Older builds

An older build sharing the config folder (Joel's installed 0.4.0 beside a dev build) refuses a file whose schema is newer than its own ("written by a newer Augram"), loads the newest backup it can read, and runs on that: it cannot misread what it does not know. It does save over the newer file at its first change (copying the newer one into `backup/` first): a build already released cannot be changed. So that this never goes unnoticed, `Load` here reports a main file older than the newest backup ("saved by an older Augram (schema 6) over a newer one (schema 7); what only the newer one could hold is in backup/…"). Nothing is restored automatically, since that would drop what was edited in the older build. Keep every machine's Augram on a build that reads the newest schema in use; the sync format, raised with each bump, pauses an older build's sync the same way (`../Sync/README.md`, Format version).

**Raising the schema** is three edits: `ConfigDocument.CurrentSchemaVersion`, one `case` in `ConfigMigrations` (a comment, when no node changes), and a row here. Raise it whenever an older build would misread a new member or save the file back without it; a member an older build only passes over and loses nothing by (the appearance section) needs none.

### Schema history

| Schema | Date | What changed | Why an older build must refuse the file | Sync format with it |
|---|---|---|---|---|
| 1 | up to 0.4.0 | everything until then. Added later without a bump, since an older build only passes them over: categories and their `useOn`, F8 "Use on", own versions and the `sync` section (all under schema 1), the `appearance` section (plan 0006, under schema 7) | | |
| 2 | 2026-10-09 | trigger combinations: a trigger's `hold`, the click trigger, an own version's `trigger` | a schema 1 build would read a combination as the plain trigger and save it back that way (silently dropping the combination), or refuse the whole file at the first `{ "click": true }` | 6 |
| 3 | 2026-10-09 | the StrokesPlus.net app definition's matcher fields | a schema 2 build would ignore a root title or a class and match too many windows | 8 |
| 4 | 2026-10-10 | hold remaps (F9): a group's `holdRemaps`, a command's `holdRemap`, the input trigger | a schema 3 build would read the commands under a hold remap as ordinary ones and save the file back without its hold remaps, or refuse the whole file at the first input | 11 |
| 5 | 2026-10-10 (plan 0004) | a command's own drag distance (a hold's `dragDistancePx`) and a Global command's "Not in" (`notIn`) | a schema 4 build would ignore both and save the file back without them | 12 |
| 6 | 2026-10-10 evening (plan 0004 revised) | Ignored › Per command entries (an ignored app's `"scope": "PerCommand"`); a command's `notIn` names them instead of app groups. No node changes: a schema 5 `notIn` names no Per command entry, so the rules drop it on load (plan 0004 decision 6: the 0.8.0 ticks are dropped; Joel had made none) | a schema 5 build (0.8.0) would read a Per command entry as an ignore of the whole app and switch Augram off over it, and its "Not in" ids would name no app group | 13 |
| 7 | 2026-10-10 night (plan 0005) | button triggers (`{ "button": … }`, a command's and an own version's) and a command's "Also in" (`alsoIn`) | a schema 6 build would refuse the whole file at its first button trigger, and would ignore `alsoIn` and save the file back without it | 14 |
| 8 | 2026-10-13 (plan 0011) | a Hotkey step's `layouts` (one shortcut per keyboard layout, keyed by the platform's layout id, inside the step's `params`). No node changes | a schema 7 build reads a step's `params` and passes an unknown member over, so it would read the step without its layout shortcuts and save the file back without them | 16 |
| 9 | 2026-10-16 (plan 0012) | Exclusions › While holding entries (an ignored app's `"scope": "WhileHolding"`) and their inputs per platform (`holding`, `ownHolding`). No node changes: a schema 8 file has no such entry | a schema 8 build would refuse the whole file at the first `"WhileHolding"` scope it cannot name; had it read the entry, it would save it back without its inputs | 17 |

A **new step type** has so far gone without a schema change (Type text, Run and Display; Scroll and Clear clipboard). Since 2026-10-09 (Joel) an older build keeps such a step as is (`../Steps/Unknown/`: the type key and `params` it read, written back unchanged; `Step 1 of command 'Zoom In' in 'Windows Explorer' kept as is: unknown step type 'scroll'.`, an Info line from `config`), shows it read-only and skips it when the command runs; the same goes for parameters its type refuses (a value a newer build added). Builds before that one dropped the step instead, and lost it from the main file at their next save (the backups, newest 20, still held it). The sync format version is still raised with a new type (4, then 7), so an older build pauses its sync.

## Stores, undo, live save

- `SettingsStore`, `GestureLibrary` (`../Gestures/`) and `MappingStore` (`../Mapping/`) are the only mutable owners of their aggregates (ADR-0002 §5a). Every mutation validates (`SettingsRules`, `GestureRules`, `MappingRules`), records the previous snapshot in an `UndoStack<T>` (`../State/`, cap 100), replaces the snapshot and raises `Changed` (each counts its changes in `Version`, internal on the settings and mapping stores, for the tests). Undo/redo are changes too: they raise `Changed` and therefore get saved. Each store has its own history; Ctrl+Z on the Options page undoes settings, on the Gestures page undoes gestures, on the Commands page undoes the mapping.
- Validation failures throw before anything is recorded; the store is unchanged.
- `ConfigSession` loads once in its constructor, builds the three stores and subscribes. A change marks the session dirty, raises `DocumentChanged` synchronously (the engine swaps to `Document` here), cancels any pending save and schedules a new one `SaveDelay` (250 ms) later through the injected `Func<Action, IDisposable>`; a burst of edits is one write and one backup. `Flush()` writes now; `Dispose()` flushes. A background save that fails with an I/O error is reported through the notice callback and stays pending for the next change or flush.
- On load (`ConfigRecovery`; hold remaps: between the categories and the commands, one at a time, so a bad one costs only itself and its commands load as ordinary commands without their input), out-of-range settings fall back to `Settings.Default` with a notice; a gesture set that breaks a rule (duplicate names, say) is loaded one gesture at a time, skipping and reporting the offenders, and the undo history is cleared afterwards. A mapping that breaks a rule is loaded the same way, group by group and command by command (a second Global group, a duplicate name, a gesture bound twice in one group: each skipped with a notice naming it), and its history cleared. Categories load one at a time between the group and its commands, so a duplicate category costs only itself and its commands load without it (on Global, in Uncategorized). The ignored apps load one at a time after every group, so the Ignored › Per command entries a command's "Not in" names (plan 0004) and the Exclusions › Global entries its "Also in" names (plan 0005) are not there when the commands load; once they are in, each loaded command, in any group, gets both lists back (an entry that did not load stays out). Steps the serializer could not read were already kept as is (`../Steps/Unknown/`), with their own notices, before the session saw the document.

## Threading

Single-writer. The stores and `ConfigSession` are mutated from one thread (the UI thread in the app) and are not synchronised. The host's scheduler must run the save on that same thread (a `DispatcherTimer`). Other threads read `Current` / `All` as immutable snapshots after `DocumentChanged` (the engine resolves commands against `Mapping.Current` this way); they never call a mutator. Core has no timer of its own: the delay is the host's job.

**May reference:** `Gestures`, `Mapping`, `Steps`, `Capture`, `Recognition`, `State`. **Referenced by:** the composition root, view models, the engine (read-only snapshots), the importer (`ConfigDocument` for export), `Sync` (settings; its machine file and item JSON go through the writers, readers and migrations here), `Transfer` (an export file is this file with members left out: the same writers, `ConfigSerializer`'s reader and migrations).
