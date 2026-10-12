# ViewModels

Projections over the Core and Engine stores (ADR-0002 §5a): a view model reads a store, exposes what a screen shows, and turns the screen's intents into store calls. It holds UI-only state (selection, expanded sections, a draft), so deleting one loses nothing. Rules live in Core; only their messages show here. The App README's "Folders" row lists every view model and the tab sections ("Commands tab", "Gestures tab", "Exclusions tab", "How settings flow") say what each does.

## What lives here

- `StoreViewModel`: the base of the tabs that edit a store (below).
- The tab view models: `Commands/CommandsViewModel` (one per Commands sub-tab; partial files per level, the trigger and its draft, the side panel's forms) with its edit view models, `Ignored/IgnoredViewModel` (one per Exclusions sub-tab; partials `.Panel`, `.PerCommand`, `.AllowedFor`) with its edit view models, `GesturesViewModel`.
- The other screens' and dialogs' view models: `AppSettingsViewModel`, `AppMatcherEditViewModel`, `TrainingViewModel`, `ImportViewModel`, `ExportViewModel`, `AugramImportViewModel`, `ConfigurationViewModel`, `SyncViewModel`, `SyncJoinViewModel`, `SyncConflictsViewModel`, `HealthViewModel`, `LogViewModel`, `RecognitionViewModel`, `MainWindowViewModel`.
- Shared parts (below): `PanelEdit`, `UseOnEdit`, `CheckedSet`, `DraftText`; and `ExportItem`, `ExportKind`.
- The Commands and Exclusions folders have their own READMEs: which partial holds which intent, the side panel, the trigger draft.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| A tab that projects a store and edits it | derive from `StoreViewModel` | Gives `Message` (the message line), `Guard`, `DeleteAsync`, `ExportAsync`, `Deleted(name, undo)` and `OnStoreChanged`. `CommandsViewModel`, `IgnoredViewModel`, `GesturesViewModel`. |
| Run an intent and show a rule's refusal | `Guard(action)` | Catches only the exceptions the view model named in its constructor: `StoreViewModel.MappingRefusals` (`MappingValidationException`, `KeyNotFoundException`; Commands, Exclusions) or `GestureRefusals` (`GestureValidationException`, `MappingValidationException`; Gestures). Anything else throws. False when refused. |
| Delete after asking | `DeleteAsync(title, question, remove)` | Asks with a Delete button through the `IConfirmPresenter`; on no nothing happens; on yes `remove` runs inside `Guard` and the line it returns is the message (usually `Deleted(name, undoKey)`: "Deleted 'X'. Ctrl+Z undoes it."). A null question deletes without asking (an unused gesture). |
| The export dialog from a tab | `ExportAsync(scope)` | Without an export presenter (tests, the gallery) says "Export is not available here."; Cancel leaves the message alone. |
| Follow a store from any thread | subscribe `OnStoreChanged`, override `OnStoreChangedOnUi` | Runs at once on the UI thread, posted from elsewhere (`Hosting/UiThread.RunOrPost`). The view model subscribes in its constructor and unsubscribes in `Dispose`. |
| Follow part of the settings | `Hosting/SettingsFollower<T>` | `CommandsViewModel` follows the stroke button and the drag distance this way (given the `SettingsStore`; null in tests and the gallery). |
| Tell every binding to read again | `OnPropertyChanged(string.Empty)` | One convention: `TrainingViewModel`, `AppSettingsViewModel`, `AugramImportViewModel`, `ExportViewModel`. |
| A text field committed on Enter or focus loss, reverted on Escape | `DraftText` | `SyncViewModel`'s address and machine name, declared as a `CommittedTextField`. |
| The side panel's form for the selected item | `PanelEdit<TEdit, TId>` | One lifecycle for the four panels (app group, hold remap, category, excluded app). `Show(id, create, sync, refresh?)` builds a form **only when another item is selected** (typing keeps focus and caret), else syncs the stored values in (not while the form's own edit is applied); the host's `edited(form, id, property)` runs inside the applying window; `Sync` puts back a value the store refused; properties the host fills are passed as ignored. Never rebuild a panel form yourself. |
| An edit form's "Use on ☑ Windows ☑ macOS" | `UseOnEdit` | `Value` (a `PlatformSet`), `SyncFrom`, `Declare(help)`; the form exposes it as `UseOn` and raises its changes as its own (`UseOnWindows`, `UseOnMac`). |
| A check-list dialog's ticks | `CheckedSet<TId>` | Owns the ticked ids, calls back on a real change, builds each `CheckListItem` (`Item(caption, id, owner, propertyName, detail)`). Not in, Also in, Allowed for, export. |
| A form shown in the form dialog | implement `Dialogs/IDialogForm` | `DialogTitle`, `DialogConfirmLabel`, `Declare()`; show with `IFormDialogPresenter.ShowAsync(form)` (`Dialogs/FormDialogs`). The new group and exclusion forms, Not in, Also in, Allowed for, export. |
| What a matcher matches here, in words | `Core/Mapping/AppMatcher.Summary(platform)` | The Exclusions row, the Apps tab's "no macOS name", the identification form's guess. |
| A command's "where", in words | `Core/Mapping/CommandNames.Label(group, command)` | "Global › Media › Volume up", "Blender › Space › Orbit": every list, link, message and log line. |

## Rules

- No `Dispatcher.UIThread` in a view model: store changes come through `OnStoreChanged`, a marshal through `Hosting/UiThread.Post`. Timers that belong to the UI (the Gestures tab's 200 ms duplicate check) may be a `DispatcherTimer`.
- The message texts, which exceptions each tab catches, and the order of UI updates are behaviour: a refactor keeps them.
- Edit view models (`From`, `SyncFrom`, `Apply`/`ToX`, `Declare`) validate nothing: the store's rules answer when the dialog confirms or the panel applies. A rule (Global is never renamed, a gesture's commands are unbound in one step) goes in Core, never in a view-model branch.
