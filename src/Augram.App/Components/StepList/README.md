# StepList

The selected command's steps (F5a): `Steps` (`StepItem`s) in, one `StepListActionEventArgs` out. The selected row expands with its type's form (accordion); rows are reused while the count is unchanged and the open form is kept unless its step changed shape (`EnsureForm`).

| Type | Job |
|---|---|
| `StepList` (+ `.Picker`, `.FormContext`) | The list, New step… and Change to… through a `StepTypePicker` flyout (greyed types from `StepRefusals`/`ChangeRefusals`), and the `StepFormContext` the open form adapts to. |
| `StepRow` | A row on `ListEditing/ItemRow` (no rename), `:expanded`, `:dragging`. |
| `StepDragReorder` | Press tracking: a click on a header selects, a drag past the threshold on the header or grip reorders. |
| `StepListMenu` | The menu table over `ListEditing/ActionMenu`, keys from `EditKeymap`. |
| `StepItem`, `StepRowText` | Row data (built by `ViewModels/Commands/Projection/StepItemProjection`). |
| `StepPlatformMarker` | The F8 wording of step and command rows ("from Windows · converted"). |
