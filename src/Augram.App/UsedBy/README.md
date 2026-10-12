# UsedBy

The "Used by…" popup (F3, decision 2026-10-07): every command bound to one gesture, opened from a gesture tile on the Gestures tab. A click on a row jumps to the command on the Commands tab and closes the popup.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Open the popup for a gesture | `IUsedByPresenter` / `UsedByPresenter` | Owned by the main window, one at a time (`Dialogs/DialogSlot`); the jump goes through `Navigation/ICommandLocator`. |
| The rows | `UsedByRow.For(mapping, gestureId)` | Document order (Global first, then groups by name); `Label` is Core's `CommandNames.Label` ("Global › Media › Volume up", "Blender › Space › Orbit"); `CommandText` adds "(inactive)". |
| The window | `UsedByWindow` | A `Dialogs/DialogWindow` (`Dialog.UsedBy.*` sizes) over an `ItemList` and a declared `ListSpec`; an empty text when no command uses the gesture. |

The Exclusions tab's "Used by" and "Allowed for" links name commands the same way (`CommandNames.Label`) but are a `LinksField` on the form, not this window (`ViewModels/Ignored/README.md`).
