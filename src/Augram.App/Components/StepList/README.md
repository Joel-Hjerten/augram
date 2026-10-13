# StepList

The selected command's steps (F5a): `Steps` (`StepItem`s) in, one `StepListActionEventArgs` out. `IsReadOnly` (plan 0010: the other platform's steps) makes it `:readonly`, rows included: no New step…, menu, drag or open form, muted, and only Undo and Redo raised. `CardHeader` is one optional slot drawn as the card's first row, above the steps (null draws nothing); the list never looks inside it (the workbench's "Steps for" `CommandHeader/StepSideBar`, which draws its own row line). Every row in `OpenIndexes` expands with its type's form, any number of them (Joel, 2026-10-12); `SelectedIndex` is the selected row, which the keys and the menu act on, and selecting opens nothing. A header click raises `ToggleOpen` (open or close that step only); a drag closes the dragged step in the list only while it is dragged. Rows are reused while the count is unchanged and an open form is kept unless its step changed shape (`EnsureForm`). The host keeps the set and shifts it per action (`ViewModels/Commands/StepIndexShift`).

| Type | Job |
|---|---|
| `StepList` (+ `.Picker`, `.FormContext`) | The list, New step… and Change to… through a `StepTypePicker` flyout (greyed types from `StepRefusals`/`ChangeRefusals`), and the `StepFormContext` the open form adapts to. |
| `StepRow` | A row on `ListEditing/ItemRow` (no rename), `:expanded`, `:dragging`. |
| `StepDragReorder` | Press tracking: a click on a header opens or closes it, a drag past the threshold on the header or grip reorders; the drag's start and end are reported so the list closes the dragged step meanwhile. |
| `StepListMenu` | The menu table over `ListEditing/ActionMenu`, keys from `EditKeymap`. |
| `StepItem`, `StepRowText` | Row data (built by `ViewModels/Commands/Projection/StepItemProjection`); the summary is read once at projection, so a Hotkey step's per-layout summary (the active layout's shortcut, plan 0011) changes only when the Commands view model projects again, which it does on `KeyboardLayoutChanged` (Joel, 2026-10-13, third round). |
| `StepPlatformMarker` | The F8 wording of step and command rows ("from Windows · converted"). |
