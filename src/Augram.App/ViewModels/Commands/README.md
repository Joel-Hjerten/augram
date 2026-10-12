# ViewModels/Commands

The Commands tab's view models: `CommandsViewModel` (one per sub-tab, Global and Apps, over `MappingStore` and `GestureLibrary`; a singleton, so the expanded sections survive a window close), the edit view models its side panel and dialogs show, and the helpers they share. Rules are Core's (`MappingRules`, `CategoryRules`, `HoldRemapRules`, `StepOffer`); this folder finds the words and the store call.

## Where an intent goes

`Handle(CommandTreeActionEventArgs)` (the tree and the header) and `Handle(StepListActionEventArgs)` (the step list) clear the message and run the intent inside `Guard`. One partial per job:

| Intent (`CommandTreeAction`) | Partial | Store call |
|---|---|---|
| Select, ToggleExpanded, ToggleOtherPlatforms | `CommandsViewModel.cs`, `.Projection` | none (UI state); the projection rebuilds sections (`CommandSections`, the one place that cuts the mapping into sections) |
| NewSection, Rename/Delete/ToggleActive of a section | `.Sections` → `.Groups`, `.Categories`, `.HoldRemaps` | `AddGroup`/`UpdateGroup`/`RemoveGroup`; a category is an `UpdateGroup` of its group; `UpdateHoldRemap`/`RemoveHoldRemap`. Deleting asks first (`StoreViewModel.DeleteAsync`). |
| NewCommand, Rename/Delete/ToggleActive of a command, Copy, Paste, SetCategory | `.Commands` | `AddCommand`, `UpdateCommand`, `RemoveCommand`; Paste names through `FreeNames`/`NameScope` and carries the category with `CategoryRules.Carried` |
| NewHoldRemap, Copy of a hold remap | `.HoldRemaps` | `UpdateGroup` (the hold remap and its commands as one step) |
| SetTriggerKind/Hold/WheelDirection/Button, SetInputKind/Input, PickGesture | `.Trigger`, `.Input` → `.TriggerDraft` | `UpdateCommand` through `SaveTrigger`, the steps fitted by `HoldRemapRules.FittedTo` (`WithTriggerHere`) |
| SwapTrigger, TakeTrigger | `.TriggerConflict` | one `UpdateGroup` exchanging the two commands' triggers |
| SetUseOn, UseConvertedOriginal, MarkOwnVersionChecked | `CommandsViewModel.cs` | `UpdateCommand` |
| EditNotIn, EditAlsoIn | `.NotIn`, `.AlsoIn` (dialogs `NotInEditViewModel`, `AlsoInEditViewModel`) | `UpdateCommand`, or `UpdateCommandAddingIgnored` when Add app… made entries |
| Export | `.Export` | none (`StoreViewModel.ExportAsync`) |
| Undo, Redo | `CommandsViewModel.cs` | `Undo`/`Redo` |
| Step intents (`StepListAction`) | `.Steps` | `UpdateCommand` with `WithStepsFor`; new steps from `StepOffer.NewStep`; a refused edit hands the list the stored steps again |
| An edit in the side panel's form | `.GroupPanel`, `.HoldRemapPanel`, `.CategoryPanel` | `UpdateGroup`, `UpdateHoldRemap`, `UpdateGroup` of the category's group |

## The side panel

While a section row (not a command) is selected, the panel shows its form: an app group's (`GroupEditViewModel`), a hold remap's (`HoldRemapEditViewModel`) or a category's (`CategoryEditViewModel`), each through a `ViewModels/PanelEdit`. **The form is rebuilt only when another section is selected**, so typing never loses focus or caret; for the same section the stored values are synced in (undo, a rename in the tree), never the form's own edit. A refused name keeps the typed text; a refused value a box cannot keep (both Use on boxes unticked, a hold key already used) is put back with `PanelEdit.Sync`. The edit view models validate nothing; their "Use on" is a `ViewModels/UseOnEdit`.

## The trigger draft

A trigger edit the rules refuse waits as the selected command's draft (`.TriggerDraft`) instead of snapping back:

| State | How it gets there | What happens next |
|---|---|---|
| No draft | the start; any accepted save | an edit is saved at once (`TrySaveTrigger`: one store edit, none when nothing changes) |
| Draft kept | an edit the rules refuse (`KeepDraft`: the stored trigger and the drafted one) | the header shows it with its note (`RefusalOf`); the next edit starts from it (`DraftedTrigger`) |
| Draft with a conflict | refused only because another command of the group uses it here | the note offers Swap and Take it (`.TriggerConflict`) |
| Settled | Swap or Take it | one `UpdateGroup`; the draft is gone |
| Dropped | another selection; the command gone; its stored trigger changed (undo, sync); the rules now accept it (`WithDraft` saves nothing, it just stops showing) | back to no draft |

Only the header shows a draft; the row keeps the stored trigger. The header's item is a `CommandHeaderItem` (`Components/CommandHeader/`), built from the selected row by `HeaderItem` (`.Projection`): the draft, the step refusals, the Options drag distance and the stroke button live only there.

## Other files

| File | Job |
|---|---|
| `CommandSections` | sections per sub-tab, row summaries, platform notes (`AppMatcher.Summary`) |
| `Projection/CommandItemProjection`, `Projection/StepItemProjection` | the `From` factories of a command row (`CommandItem`: trigger words, step summary (`Summarise`), F8 lines and marker) and a step row (`StepItem`); the wording of kinds stays in Components (`TriggerKindExtensions`, `InputKindExtensions`, `StepPlatformMarker`) |
| `CommandClipboard` | the copied command or hold remap, shared by both sub-tabs |
| `CommandLocator` | `Navigation/ICommandLocator`: opens a command on its sub-tab |
| `CommandsScope` | Global or Apps |
| `HoldRemapCopy` | a copied hold remap with its commands |
| `NotInEditViewModel`, `AlsoInEditViewModel` | check-list dialogs (`IDialogForm`, ticks in a `CheckedSet`) |
