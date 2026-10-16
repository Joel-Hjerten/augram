# HeldInputList

Exclusions › While holding's "Ignore gestures while holding" card (plan 0012): an entry's inputs, laid out like a command's step list. `Items` (`HeldInputItem`), `SelectedIndex`, `IsReadOnly`, `Side` (a `Parts/PlatformSide`), `CopyToText` and `Platform` in; one `HeldInputListActionEventArgs` out. The host is `ViewModels/Ignored/IgnoredViewModel.WhileHolding`; `Screens/IgnoredScreen` puts the card in the `MasterDetail`'s `DetailFooter`.

| Type | Job |
|---|---|
| `HeldInputList` | The card: title line with New…, then the "Ignore for: Windows · macOS" row (`PART_Side`, a `Parts/PlatformSideBar`, as "Steps for"), the recorder line while adding, the rows. Reorder by a left drag (`ListEditing/RowDragReorder`), menu and keys `HeldInputListMenu` (New…, Copy, Paste at bottom, Delete, Undo, Redo). `:readonly` on the other platform's side: no New…, menu or drag; only the switch, Copy from, a row's Copy to, Undo and Redo are raised. Its keys stand down while the recorder listens or has focus. |
| `HeldInputRow` | One input on `ListEditing/ItemRow` (no rename, no active box): grip and number, name, the hold-key warning under it (`warn`), the F8 marker, and on the other side `PART_CopyTo` ("Copy to Windows", a link). |
| `HeldInputPicker` | The recorder line New… opens: `PART_Key`, the hotkey capture field in one-key mode taking a modifier alone (`Capture/HotkeyCaptureBox.AcceptsModifier`); `PART_Button`, the next mouse button press (`Capture/ButtonDetector`); `PART_Pick`, the buttons and modifiers (`Choices`, `Parts/AskingDropdown`); `PART_Cancel`. A recorded modifier key folds into its modifier (`KeyOrButton.FromKey`). |
| `HeldInputItem`, `HeldInputListAction`, `HeldInputListActionEventArgs`, `HeldInputListMenu` | Row data (name and marker from `StepList/StepPlatformMarker.For(WhileHoldingInput, …)`), the intents, the menu table. |

Gallery: "Exclusions while holding" (Blender on Windows, a hold-key warning) and "…, macOS side" (After Effects' own macOS list, read-only, Copy to Windows).
