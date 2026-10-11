# Dialogs

How a dialog window is found an owner, shown, sized, closed and chromed: one implementation of each, used by every presenter in the App (plan 0007 S10). A presenter builds its window and calls in here; what the answer means stays with the presenter's caller.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| The main window (clipboard, a file picker's parent, "is the window up") | `DialogHost.MainWindow` | Null before the root hands it over and in headless tests. |
| The owner of a dialog | `DialogHost.Owner` | The main window while it is visible, else null ("show it alone"). |
| A modal dialog the caller awaits | `DialogHost.ShowModalAsync(window, activateWhenAlone)` | Modal to the owner; alone otherwise, activated only when the caller says so. |
| A modeless popup (training, "Used by…", the gesture picker) | `DialogHost.Show(window)` | Owned when there is an owner; always activated. |
| One popup of a kind at a time | `DialogSlot` | `Show` closes the one on show; `Close` first when the close must come before other work (training cancels its session on close). |
| A question before the main window exists (the take-over) | `DialogHost.ShowAloneAsync(window)` | Centred on the screen, activated. |
| A yes/no question or an OK message | `IConfirmPresenter` (`ConfirmAsync`, `InformAsync`) | `ConfirmPresenter` over the main window (what the tabs get); `ConfirmPresenter.Alone()` keyed `ConfirmPresenter.AloneKey` for the take-over. Tests use `FakeConfirmPresenter`. |
| A small form (create a group, an exclusion, export, sync's join) | `IFormDialogPresenter.ShowAsync(FormDialogRequest)` | The request carries title, confirm label, message, form, `CanConfirm`, `Width` (null: `Dialog.Form`), `ActivateWhenAlone`. `FormDialogPresenter.Build` gives the content without a window (gallery). The content is the lookless `Components/FormDialog`. |
| A dialog window | derive from `DialogWindow(DialogSize.X)` | Sizes from the `Dialog.X.*` tokens (`Themes/Tokens.axaml`), centred on the owner, Escape closes. |
| The chrome inside it | `DialogShell` | Lookless (`Themes/Default/Lists.axaml`): optional heading with ⓘ, the content, the buttons (cancel label first, the default button last; a null label leaves that button out), `Confirmed` / `Cancelled`. |

Who uses the shell: `FormDialog`'s template, `ConfirmDialog`, `UsedBy/UsedByWindow` (Close only), `Sync/SyncConflictWindow` (heading with ⓘ, Cancel / Apply), `Transfer/AugramImportView` (the import review, Cancel / Import). Not on the shell: the gesture picker (its own template with four buttons; `Components/GesturePicker`), the training window (XAML, `Training/`) and the StrokesPlus.net import dialog (`Import/ImportDialog`, XAML, still finds its owner through `DialogHost.MainWindow`).

## Kept rules (behaviour, not style: do not unify them away)

- **A question with no visible main window answers no** (`ConfirmPresenter.ConfirmAsync` over the main window): a destructive action never proceeds unasked, and no dialog is built. The take-over's presenter (`Alone()`) asks anyway, since nothing of that launch is up.
- **Whether a dialog shown alone is activated is decided per call site**, carried as `activateWhenAlone` / `FormDialogRequest.ActivateWhenAlone`:

  | Call site | Shown alone and activated? |
  |---|---|
  | `FormDialogPresenter` (the tabs' forms, export) | no (default of the request) |
  | `SyncJoinPresenter` (via the request) | yes: the start-up run usually finds the app in the tray |
  | `SyncConflictPresenter` | no |
  | `AugramImportWindows` (review; messages through `ConfirmPresenter.InformAsync`) | yes |
  | `TrainingPresenter`, `UsedByPresenter`, `GesturePickerPresenter` (`DialogHost.Show`) | yes, owned or not |
  | The take-over (`ShowAloneAsync`) | yes, centred on the screen |

- **Escape closes once**: `DialogWindow.OnKeyDown` closes and marks it handled, so a cancel button's own Escape (`IsCancel`) never fires as well. Closing is every dialog's cancel; the gesture picker and the training window keep their own (the picker's Cancel button, the training view's).
- **Sizes, titles, button order and labels** come from the tokens and the callers; the gallery reads the same tokens (`DialogSize.Width`).

## Known quirk, kept

The import review (`AugramImportView`) has no margin round its content, unlike every other dialog: before the shell it was a `DockPanel` subclass, which the theme's `DockPanel.dialog` style never matched. The shell keeps it at `Margin = 0` so the review looks as it did. Giving it the dialogs' margin is a visible change, Joel's call.
