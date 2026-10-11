# ViewModels

Projections over the Core and Engine stores (ADR-0002 §5a): a view model reads a store, exposes what a screen shows, and turns the screen's intents into store calls. It holds UI-only state (selection, expanded sections, a draft), so deleting one loses nothing. Rules live in Core; only their messages show here. The App README's "Folders" row lists every view model and the tab sections ("Commands tab", "Gestures tab", "Exclusions tab", "How settings flow") say what each does.

## What lives here

- `StoreViewModel`: the base of the tabs that edit a store (below).
- The tab view models: `Commands/CommandsViewModel` (one per Commands sub-tab; partial files per level, the trigger and its draft, the side panel's forms) with its edit view models, `Ignored/IgnoredViewModel` (one per Exclusions sub-tab; partials `.Panel`, `.PerCommand`, `.AllowedFor`) with its edit view models, `GesturesViewModel`.
- The other screens' and dialogs' view models: `AppSettingsViewModel`, `AppMatcherEditViewModel`, `TrainingViewModel`, `ImportViewModel`, `ExportViewModel`, `AugramImportViewModel`, `ConfigurationViewModel`, `SyncViewModel`, `SyncJoinViewModel`, `SyncConflictsViewModel`, `HealthViewModel`, `LogViewModel`, `RecognitionViewModel`, `MainWindowViewModel`.
- Small helpers: `DraftText` (a text field's commit/revert draft), `ExportItem`, `ExportKind`.

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
| A text field committed on Enter or focus loss, reverted on Escape | `DraftText` | `SyncViewModel`'s address and machine name. |

## Rules

- No `Dispatcher.UIThread` in a view model: store changes come through `OnStoreChanged`, a marshal through `Hosting/UiThread.Post`. Timers that belong to the UI (the Gestures tab's 200 ms duplicate check) may be a `DispatcherTimer`.
- The message texts, which exceptions each tab catches, and the order of UI updates are behaviour: a refactor keeps them.
