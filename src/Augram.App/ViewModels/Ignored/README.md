# ViewModels/Ignored

The Exclusions tab's view models ("Exclusions" in the UI since plan 0005, "Ignored" in the code and keys): `IgnoredViewModel`, one per sub-tab (Global, Per command; singletons over the one `MappingStore`, sharing its history with the Commands tab), its form `IgnoredEditViewModel`, the Allowed for dialog `AllowedForEditViewModel`, and `IgnoredModes` (the two modes' words). The rules are Core's (`IgnoreList`, `MappingRules`); the list's row summary is `AppMatcher.Summary(platform).Text`.

## Where an intent goes

`Handle(MasterDetailActionEventArgs)` clears the message and runs the intent inside `Guard`:

| Intent (`MasterDetailAction`) | Partial | Store call |
|---|---|---|
| Select | `IgnoredViewModel.cs` → `.Panel` | none; the side panel shows the entry's form |
| New | `IgnoredViewModel.cs` (`IgnoredEditViewModel` as an `IDialogForm`) | `AddIgnored` |
| Rename, ToggleActive | `IgnoredViewModel.cs` | `UpdateIgnored` |
| Move (to the other list) | `.PerCommand` (asks first when commands name the entry) | `UpdateIgnored`; Core's normalisation drops it from their Not in / Also in in the same step |
| Delete | `IgnoredViewModel.cs` (`StoreViewModel.DeleteAsync`) | `RemoveIgnored` |
| Undo, Redo | `IgnoredViewModel.cs` | `Undo`/`Redo` |
| An edit in the side panel's form | `.Panel` | `UpdateIgnored`; leaving Also in lists says so (`.AllowedFor`'s `LeftAlsoIn`) |
| Allowed for's Change… | `.AllowedFor` (`AllowedForEditViewModel`, an `IDialogForm` with a `CheckedSet`) | `SetAllowedFor`, one step, nothing when nothing changed |

## The side panel

The selected entry's form lives in a `ViewModels/PanelEdit`: rebuilt only when another entry is selected, so typing keeps focus; for the same entry the stored values are synced in, and its "Used by" (Per command: the commands naming it in their Not in) and "Allowed for" (Global: the commands naming it in their Also in) are re-read on every store change. Those two lists and the identification's guess are the host's, so they are never taken for an edit. The links read `CommandNames.Label` ("Global › Media › Zoom in") and open the command through `Navigation/ICommandLocator` when there is one.
