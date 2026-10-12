# Screens

Each file turns a view model into a `ScreenDeclaration` (`Declarations/`): declare, never lay out. A screen that needs something the vocabulary lacks gets a field kind (`Declarations/README.md`), not a hand-built control.

| Screen | Shape | Notes |
|---|---|---|
| `CommandsScreen`, `IgnoredScreen`, `GesturesScreen` | `ComponentScreen` | The workbench components (`CommandsWorkbench`, `MasterDetail`, `GestureGrid`) bound to their view models. |
| `OptionsScreen` | `FormScreen` per sub-tab | One `Declare…` per sub-tab (General, Strokes, Appearance, Sync, About); moving a field is moving a line. The sub-tab's title is the F1 path's first part. |
| `OptionsSyncSection`, `OptionsConfigurationSection` | `Section`s of Options › Sync | Sync (address and machine name as `CommittedTextField`s over `DraftText`, Sync now and Resolve… as `ButtonsField`s with a status); Export and import (`ButtonsField`s). |
| `HealthScreen` | `FormScreen` | Notes. |
| `LogScreen`, `RecognitionScreen` | `ListScreen` | Live lists with filters. |
| `AugramImportScreen` | `FormScreen` | Only the top half of `Transfer/AugramImportView`; its "Apply to all" is a `ButtonsField` with a `Choice`. |

The Detect… row (Options › General) is a `ButtonsField` with a `Hint`. No screen here builds a control or uses a `CustomField` (plan 0007 S13); a `CustomField` is for a component inside a form (capture boxes in the edit forms).
