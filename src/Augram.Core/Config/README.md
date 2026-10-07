# Core/Config

The on-disk configuration (requirements F8, checklist A3/A17) and the session that owns the three live stores behind the Options page, the gesture library and the Commands/Ignored tabs. Landed in M1 step 2; the mapping joined in M2 step 1.

## Types

| Type | Role |
|---|---|
| `ConfigDocument` | the whole file: `SchemaVersion` (const `CurrentSchemaVersion` = 1), `Settings`, `Gestures`, `Mapping` (`MappingDocument`, default `Empty`). `Default` = default settings + `StarterGestures` + an empty mapping |
| `Settings` | one record per subsystem: `General` (`GeneralSettings`), `Capture` (`CaptureThresholds`, reused), `Trail` (`TrailSettings`), `Recognition` (`RecognitionOptions`, reused), `NoMatch` (`NoMatchBehaviour`) |
| `GeneralSettings` | stroke button (`Capture.MouseButton`, default Right), `IgnoreKeys` flags, start at login, enabled |
| `TrailSettings`, `RgbColor` | width px 5, opacity 0.5, colour `#00FF40` |
| `ConfigSerializer` | `Write(document)` (indented, always the current schema version) and `Read(json)` / `Read(json, stepRegistry, notice)` (version check, migrate, deserialize, validate); every failure is a `ConfigFormatException` with a message fit for the log. The top-level envelope is written by hand so the `mapping` member can go through the two classes below |
| `ConfigMigrations` | `Migrate(JsonObject, fromVersion)`: forward-only steps on the JSON tree, one `case` per version bump; nothing to do while version 1 is current |
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
  "schemaVersion": 1,
  "settings": {
    "general": { "strokeButton": "Right", "ignoreKey": "None", "startAtLogin": false, "enabled": true },
    "capture": { "startDistancePx": 30, "minSegmentPx": 6, "cancelDelayMs": 1000, "resetCancelDelayOnMovement": true },
    "trail": { "widthPx": 5, "opacity": 0.5, "colour": "#00FF40" },
    "recognition": { "precision": 100, "threshold": 75, "scoringMode": "Legacy", "sampleAggregation": "Average" },
    "noMatch": "DoNothing"
  },
  "gestures": [
    { "id": "4f0c...-...", "name": "Up", "isActive": true, "samples": [ [[50,100],[50,90],[50,80]] ] }
  ],
  "mapping": {
    "groups": [
      { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "isActive": true, "suppressGlobals": false, "matcher": null,
        "categories": [ { "id": "9a1e...-...", "name": "Media" }, { "id": "c3d2...-...", "name": "Window" } ],
        "commands": [
          { "id": "…", "name": "Minimize", "trigger": { "gesture": "4f0c...-..." }, "isActive": true, "category": "c3d2...-...",
            "steps": [
              { "type": "windowOp", "authoredOn": "Windows", "isActive": true, "params": { "operation": "Minimize" },
                "overrides": { "macOS": { "operation": "Minimize" } } }
            ] },
          { "id": "…", "name": "Volume up", "trigger": { "wheel": "Up" }, "isActive": true, "steps": [ … ] },
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

Mapping specifics (`../Mapping/README.md` has the model): groups are written Global first then by name, commands by name. A trigger is `{ "gesture": "<id>" }`, `{ "wheel": "Up" | "Down" }` or `null` (not bound). A step's `params` is exactly what its type's `Write` returned and goes back through its `Read`; `overrides` holds a parameters object per platform (`windows`, `macOS`), always of the step's own type, and is omitted when empty; `note` is omitted when null. A group's `categories` (`{ "id", "name" }`, sorted by name) is omitted when empty and a command's `category` (the id of one of its group's categories) when the command is Uncategorized, so a file without categories looks as it did before they existed (schema version 1, additive). On read, a group or command needs `id` and `name`; everything else takes its default (`isActive` true, `suppressGlobals` false, `trigger` null, `steps` empty, `categories` empty, `category` null, `authoredOn` Windows, `matcher` null for a group and empty for an ignored app). A category without a Guid `id` or without a name is dropped with a notice; a `category` that is not a Guid string reads as Uncategorized with a notice; one that names no category of its group is cleared silently by `CategoryRules` when the store takes the document. A missing `mapping` member, or a missing `groups` array, is `MappingDocument.Empty` (just the Global group), which is why files written before M2 still load under schema version 1 without a migration.

## Store contract (`FileConfigStore`)

- **Save**: serialize first (a bad document never touches disk), copy the current file to `backup/augram-yyyyMMdd-HHmmss.json` (`-2`, `-3`... within one second), prune backups to the newest 20, write `augram.json.tmp`, then `File.Move(overwrite)` so readers see the old file or the new one, never a partial one.
- **Load** falls back in order: main file → newest backup that parses → `ConfigDocument.Default`. Each step reports one line through the `Action<string>? notice` callback (the host wires it to the event log). Load never throws for a missing or corrupt file.
- Schema: `Read` refuses a `schemaVersion` greater than `CurrentSchemaVersion` ("written by a newer Augram"), migrates a smaller one, and the store then treats the refusal like any other unreadable file (fall back to a backup).

## Stores, undo, live save

- `SettingsStore`, `GestureLibrary` (`../Gestures/`) and `MappingStore` (`../Mapping/`) are the only mutable owners of their aggregates (ADR-0002 §5a). Every mutation validates (`SettingsRules`, `GestureRules`, `MappingRules`), records the previous snapshot in an `UndoStack<T>` (`../State/`, cap 100), replaces the snapshot, increments `Version` and raises `Changed`. Undo/redo are changes too: they bump `Version`, raise `Changed` and therefore get saved. Each store has its own history; Ctrl+Z on the Options page undoes settings, on the Gestures page undoes gestures, on the Commands page undoes the mapping.
- Validation failures throw before anything is recorded; the store is unchanged.
- `ConfigSession` loads once in its constructor, builds the three stores and subscribes. A change marks the session dirty, raises `DocumentChanged` synchronously (the engine swaps to `Document` here), cancels any pending save and schedules a new one `SaveDelay` (250 ms) later through the injected `Func<Action, IDisposable>`; a burst of edits is one write and one backup. `Flush()` writes now; `Dispose()` flushes. A background save that fails with an I/O error is reported through the notice callback and stays pending for the next change or flush.
- On load, out-of-range settings fall back to `Settings.Default` with a notice; a gesture set that breaks a rule (duplicate names, say) is loaded one gesture at a time, skipping and reporting the offenders, and the undo history is cleared afterwards. A mapping that breaks a rule is loaded the same way, group by group and command by command (a second Global group, a duplicate name, a gesture bound twice in one group: each skipped with a notice naming it), and its history cleared. Categories load one at a time between the group and its commands, so a duplicate category costs only itself and its commands load Uncategorized. Steps that could not be read were already dropped by the serializer, with their own notices, before the session saw the document.

## Threading

Single-writer. The stores and `ConfigSession` are mutated from one thread (the UI thread in the app) and are not synchronised. The host's scheduler must run the save on that same thread (a `DispatcherTimer`). Other threads read `Current` / `All` as immutable snapshots after `DocumentChanged` (the engine resolves commands against `Mapping.Current` this way); they never call a mutator. Core has no timer of its own: the delay is the host's job.

**May reference:** `Gestures`, `Mapping`, `Steps`, `Capture`, `Recognition`, `State`. **Referenced by:** the composition root, view models, the engine (read-only snapshots), the importer (`ConfigDocument` for export).
