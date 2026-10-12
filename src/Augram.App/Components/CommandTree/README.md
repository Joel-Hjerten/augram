# CommandTree

The tree of a Commands sub-tab (F5a) and the vocabulary its rows and the header share. Presentational: `Sections` in, one `CommandTreeActionEventArgs` out; the host owns the selection (`SelectedSectionId`, `SelectedCommandId`) and the expanded sections. It never asks which tab it is on: each `SectionItem` says what its header offers.

| Type | Job |
|---|---|
| `CommandTree`, `CommandTreeRows` | The list (`PART_Rows`, through `ListEditing/RowListHost`: the rebuild that keeps selection and focus, right-click select, double-click rename), what a double click on a header does (puts its first click's toggle back), the platform filter toggle, the search box (`PART_Search`, a `SearchBox/SearchBox` on the title line, stretching with the tree down to `Editor.SearchFloorWidth` (Joel, 2026-10-12), two-way bound to `SearchText`, which the host narrows `Sections` by; plan 0009) with `EmptyText` ("No commands match 'xyz'") while nothing is left. The find key (`CommandTreeAction.Find`) focuses the box, and `IsEditing` is true while the box is typed in, so the edit keys stand down there. |
| `SectionRow`, `CommandRow` | Rows, on `ListEditing/ItemRow`. A section header toggles on a tap, renames on a double click on its name. |
| `CommandTreeMenu` | The menu table and `Allows` rules over `ListEditing/ActionMenu`, keys from `ListEditing/EditKeymap`. |
| `CommandTreeAction`, `CommandTreeActionEventArgs` | Every intent of the tree and the header. |
| `SectionId` | A section's identity: an app group, a hold remap in it (`Parent` is the group), or a Global category; stable across renames. |
| `SectionItem` | One section: name, active, expanded, its commands, and the flags that unlock menu entries (`CanEditDefinition`, `CanToggleActive`, `CanCopy`, `CanAddHoldRemap`, `CanExport`). |
| `CommandItem` | What a command row shows (plain data; `ViewModels/Commands/Projection/CommandItemProjection.From` builds it). The header's extra fields are on `CommandHeader/CommandHeaderItem`. |
| `CategoryChoice` | One entry of the header's Category dropdown. |
| `TriggerKind` (+ `TriggerKindExtensions`), `InputKind` (+ extensions) | The kinds and their wording (labels, hints, anchor warnings): the Commands tab's words stay with the components. |
