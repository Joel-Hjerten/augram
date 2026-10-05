# Core/Config

The on-disk configuration (requirements F8, checklist A3/A17) and the two stores that own the live state behind the Options page and the gesture library. Landed in M1 step 2.

## Types

| Type | Role |
|---|---|
| `ConfigDocument` | the whole file: `SchemaVersion` (const `CurrentSchemaVersion` = 1), `Settings`, `Gestures`. `Default` = default settings + `StarterGestures` |
| `Settings` | one record per subsystem: `General` (`GeneralSettings`), `Capture` (`CaptureThresholds`, reused), `Trail` (`TrailSettings`), `Recognition` (`RecognitionOptions`, reused), `NoMatch` (`NoMatchBehaviour`) |
| `GeneralSettings` | stroke button (`Capture.MouseButton`, default Right), `IgnoreKeys` flags, start at login, enabled |
| `TrailSettings`, `RgbColor` | width px 5, opacity 0.5, colour `#00FF40` |
| `ConfigSerializer` | `Write(document)` (indented, always the current schema version) and `Read(json)` (version check, migrate, deserialize, validate); every failure is a `ConfigFormatException` with a message fit for the log |
| `ConfigMigrations` | `Migrate(JsonObject, fromVersion)`: forward-only steps on the JSON tree, one `case` per version bump; nothing to do while version 1 is current |
| `ConfigJsonContext` + `*JsonConverter` | source-generated `JsonSerializerContext` (trimming/AOT safe); converters give gesture ids as Guid strings, samples as one-line `[[x,y],...]`, trail colour as hex |
| `IConfigStore` | the port: `Location`, `Load()`, `Save(document)`. Lives here, not in `Abstractions/`, because only Core implements it |
| `FileConfigStore`, `ConfigBackups` | `<folder>/augram.json`; see below |
| `DefaultConfigFolder` | `%APPDATA%\Augram`, `~/Library/Application Support/Augram`, `$XDG_CONFIG_HOME/Augram`; the folder is a constructor argument everywhere else (A17) |
| `SettingsStore`, `SettingsRules`, `SettingsValidationException` | the settings aggregate store with undo |
| `ConfigSession` | wires store + both aggregates and does live save |

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
  ]
}
```

camelCase names, enums as strings (flags comma-separated), comments and trailing commas tolerated on read. A missing settings section or member takes its default; a gesture must have `id`, `name` and `samples`. Everything else in the file is written in full by Augram, so a diff after an edit shows only the edit.

## Store contract (`FileConfigStore`)

- **Save**: serialize first (a bad document never touches disk), copy the current file to `backup/augram-yyyyMMdd-HHmmss.json` (`-2`, `-3`... within one second), prune backups to the newest 20, write `augram.json.tmp`, then `File.Move(overwrite)` so readers see the old file or the new one, never a partial one.
- **Load** falls back in order: main file → newest backup that parses → `ConfigDocument.Default`. Each step reports one line through the `Action<string>? notice` callback (the host wires it to the event log). Load never throws for a missing or corrupt file.
- Schema: `Read` refuses a `schemaVersion` greater than `CurrentSchemaVersion` ("written by a newer Augram"), migrates a smaller one, and the store then treats the refusal like any other unreadable file (fall back to a backup).

## Stores, undo, live save

- `SettingsStore` and `GestureLibrary` (`../Gestures/`) are the only mutable owners of their aggregates (ADR-0002 §5a). Every mutation validates (`SettingsRules`), records the previous snapshot in an `UndoStack<T>` (`../State/`, cap 100), replaces the snapshot, increments `Version` and raises `Changed`. Undo/redo are changes too: they bump `Version`, raise `Changed` and therefore get saved. Each store has its own history; Ctrl+Z on the Options page undoes settings, on the Gestures page undoes gestures.
- Validation failures throw before anything is recorded; the store is unchanged.
- `ConfigSession` loads once in its constructor, builds both stores and subscribes. A change marks the session dirty, raises `DocumentChanged` synchronously (the engine swaps to `Document` here), cancels any pending save and schedules a new one `SaveDelay` (250 ms) later through the injected `Func<Action, IDisposable>`; a burst of edits is one write and one backup. `Flush()` writes now; `Dispose()` flushes. A background save that fails with an I/O error is reported through the notice callback and stays pending for the next change or flush.
- On load, out-of-range settings fall back to `Settings.Default` with a notice; a gesture set that breaks a rule (duplicate names, say) is loaded one gesture at a time, skipping and reporting the offenders, and the undo history is cleared afterwards.

## Threading

Single-writer. The stores and `ConfigSession` are mutated from one thread (the UI thread in the app) and are not synchronised. The host's scheduler must run the save on that same thread (a `DispatcherTimer`). Other threads read `Current` / `All` as immutable snapshots after `DocumentChanged`; they never call a mutator. Core has no timer of its own: the delay is the host's job.

**May reference:** `Gestures`, `Capture`, `Recognition`, `State`. **Referenced by:** the composition root, view models, the engine (read-only snapshots), the importer (`ConfigDocument` for export).
