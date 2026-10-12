# ListEditing

The pieces every editable list shares (the contract is in `../README.md`). One home each; S14b adds `RowListHost` here.

| Type | Job |
|---|---|
| `ItemRow` | Base of every list row (`SectionRow`, `CommandRow`, `StepRow`, `MasterRow`, `GestureTile`): the one in-place rename editor (`PART_NameEditor`; `BeginEdit`, Enter or leaving commits, Escape reverts, an unchanged name raises nothing, a commit on leave is posted so a click elsewhere selects first), the active box (`PART_Active`, raised only for a user click), `:editing` / `:inactive`, and the double-click rename rule (`IsRenameClick`, `RenamesAt`). A row without `PART_Active` (a tile) simply has no box; `CanRename` false turns rename off. |
| `ActionMenu<TAction>` | A list's context menu from its entry table (`Build`, each entry's `Tag` is its action; `Entries` for the list's `Refresh` rules) and its key bindings (`BindKeys`: a null key is skipped; `canExecute` must say no while a name editor or a form has focus). |
| `EditKeymap` | The one per-platform keymap (`Current`; tests build the other): rename F2, and Enter as a second key, on Windows; Return on macOS (Q11, 2026-10-11, the Gestures tab included); New, Copy, Paste, Duplicate, Delete, Undo, Redo with Ctrl or Cmd. View models name its keys in their messages. |

Each list keeps its own `…Menu` (table and `Allows`/`Refresh` rules) and action enum. Do not bind a key, build a `MenuItem` or handle a rename key outside these types.
