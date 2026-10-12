# Import

The StrokesPlus.net import flow (F8): the App's dialog over the reader and merge in `Augram.Import.StrokesPlus` ([its README](../../Augram.Import.StrokesPlus/README.md); the field-by-field mapping is [docs/reference/strokesplus-net-import-mapping.md](../../../docs/reference/strokesplus-net-import-mapping.md)).

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Pick, read and show the import | `IImportPresenter` / `ImportPresenter` | The shared open picker (`Transfer/ITransferFilePicker` with `ImportPresenter.OpenRequest`), `ImportPresenter.Read`, then the dialog. |
| The dialog | `ImportDialog` + `ImportView` | Over `ViewModels/ImportViewModel`: a `DialogWindow` (code-only, `Dialog.StrokesPlusImport` sizes) around the view (XAML) in the `DialogShell` (`Dialogs/README.md`). |
| One clashing gesture and its choice | `ImportConflictItem` | Keep mine / Take theirs / Keep both. |

## Import from StrokesPlus.net (F8, M1 step 10)

A one-time migration tool that works: it keeps its own merge (`GestureMerge`, `MappingImport` in `Augram.Import.StrokesPlus`) rather than the Augram file review (plan 0007, question 1). Reached from the Gestures toolbar and from Options › Sync › Export and import.

`ImportPresenter` asks the shared open picker (`ITransferFilePicker.PickOpenAsync` with `ImportPresenter.OpenRequest`: "Import from StrokesPlus.net", "StrokesPlus.net config (*.json)" and all files, `StrokesPlus.net.json` suggested, starting in `%APPDATA%\StrokesPlus.net\` when SP.net's live config exists there; it neither starts in nor changes the Augram dialogs' last folder). `ImportPresenter.Read` reads the whole file with `StrokesPlusImporter.ReadAll` (gestures, the Global and app groups with their commands, the excluded apps); a file that cannot be read or is not SP.net JSON becomes the dialog's error line and Apply stays off. Then `ImportDialog` over `ImportView` over `ImportViewModel`, modal to the main window: the file path, the stats line ("Found N gestures, G app groups, C commands (P steps as placeholders until their step types exist), E excluded apps."), the importer's warnings, the plan line, and one `ImportConflictItem` per gesture that clashes by name or matches an existing shape (`GestureMerge.Plan`: "same shape as 'X' (score 93)"), each with Keep mine / Take theirs / Keep both (default Keep mine) plus "Apply to all". The plan line follows the choices, because a Keep mine binds the imported commands to the existing gesture, which may already be bound in that group.

**Apply** merges the gestures (`GestureMerge.ApplyWithMap`, which also says which id each imported gesture ended up under), rebinds the imported commands to those ids (`MappingImport.Rebind`) and merges the mapping (`MappingImport.Merge`: groups and categories by name, a command whose name or trigger is taken in its group skipped, excluded apps by name) before either store changes, so a refusal leaves both untouched; then it writes two stores, `GestureLibrary.ReplaceAll` and `MappingStore.ReplaceAll` (one undo step each). It logs Info `import` / "Imported from StrokesPlus.net" with the counts (found, added, replaced, kept, groups and commands added and skipped, excluded apps, placeholder steps, warnings). Saved placeholder steps are upgraded at every start by `ConfigModule.UpgradeImportedSteps` (`PlaceholderUpgrade`). Tests (`tests/Augram.App.Tests/Import/`) read the synthetic fixtures through `ImportPresenter.Read`; none opens a picker or Joel's real SP.net file.
