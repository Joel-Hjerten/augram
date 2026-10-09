# Core/Gestures

The gesture model and its store. Templates are **raw point lists** (CLAUDE.md invariant 5), resampled only at match time so precision stays adjustable.

## Types

| Type | Role |
|---|---|
| `GesturePoint` | raw X/Y, screen pixels today (Y grows downwards) |
| `GestureSample` | one training sample, an immutable raw point list |
| `Gesture` | `Id`, `Name`, `IsActive`, `Samples`; immutable record |
| `GestureId` | strongly typed Guid, stable across renames; serialized as a Guid string |
| `GestureLibrary` | the store: the single mutable owner of the list for the running app |
| `GestureRules` | the business rules, called by the store (and nothing else re-implements them) |
| `GestureValidationException` | a rule was broken; the message is fit to show the user |
| `Cleanup/ShapeCleanup` | shape cleanup (F3, plan M2 step 10): corners by ShortStraw plus a 35° turn test on a smoothed copy, each piece fitted as Line / Arc / Circle / Smooth, a raw point list again in the drawn direction and angle; pure. Measurements: `docs/learnings/0004-shape-cleanup.md`. Not wired into the app yet (Gallery preview first) |
| `StarterGestures` | the fresh-install set (plan 0001 C3): 8 flicks, 4 out-and-backs, 4 L-shapes, C, S, Z, Circle at 100 px scale; ids derived from the names (SHA-256) so two installs agree |

## Store contract (`GestureLibrary`)

| Member | Behaviour |
|---|---|
| `All` | immutable snapshot (a new array after every change); `Version` increments with every change, undo and redo included; `Changed` is raised synchronously after each |
| `Find(id)` | null when absent |
| `Add(gesture)` | normalises (trim name; no samples → `IsActive` false), validates, appends; returns what was stored |
| `Update(gesture)` | replaces by id, keeping the position; `KeyNotFoundException` for an unknown id |
| `Rename(id, name)`, `SetActive(id, bool)` | `Update` shortcuts; activating a sample-less placeholder throws |
| `Remove(id)` | returns the removed gesture; `Undo()` puts it back with the same id in the same place |
| `ReplaceAll(gestures)` | import; the whole set is validated first; one undo step |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | linear history, 100 steps (`State/UndoStack`); a new change after an undo drops the redo branch |
| `ClearHistory()` | after a load that must not be undoable |

## Validation rules (`GestureRules`)

1. Name is trimmed; an empty name is rejected.
2. Names are unique, compared case-insensitively (`OrdinalIgnoreCase`) after trimming, on Add, Update, Rename and ReplaceAll.
3. Ids are unique.
4. A gesture with samples needs at least one sample with two distinct points. A gesture with **no** samples is allowed as a placeholder (created before training) but is stored inactive and cannot be activated until it has a sample.

The UI shows the exception message; it never re-checks these rules itself (ADR-0002 §5a). Persistence lives in `../Config/`; nothing here knows about storage.

## Threading

Single-writer: the UI thread mutates the library. Readers on other threads (the engine's recognizer) take `All` as a snapshot when told the version moved and never call a mutator. No locks inside.

**May reference:** `State`. **Referenced by:** `Recognition`, `Config`, the importer, view models.
