# Core/State

Infrastructure shared by the stores (ADR-0002 §5a, §6): `UndoStack<T>`, a linear undo/redo history over immutable snapshots, capped at 100 entries by default. A store calls `Record(current)` before every mutation, `Undo(current)` / `Redo(current)` to swap states, and `Clear()` after a load. Recording after an undo discards the redo branch.

Landed in M1 step 2 for `GestureLibrary` and `SettingsStore`; `MappingStore` (M2) uses it too.

**May reference:** nothing. **Referenced by:** every store. Single-writer, like the stores that own it.
