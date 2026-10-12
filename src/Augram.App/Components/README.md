# Components

Every shared control of the App: lookless (`TemplatedControl` with named `PART_*` parts and pseudo-classes, the template in `Themes/Default/*.axaml`), presentational (data in, events out; never a store or a service, ADR-0002 §5a), and shown in its states in the dev gallery (Gallery rule). One folder per component, each with its own README. The theme, text, help and row rules are in the App README, "Rules of the road".

## The five kinds

| Kind | Folders | What they are |
|---|---|---|
| Declarative renderers (ADR-0002 §5c) | `ScreenHost/`, `SectionForm/`, `Fields/`, `ItemList/`, `TextPanel/`, `Shell/`, `FormDialog/` | A `Screens/` declaration in, controls out. `ItemList` is the read-only table renderer (Log, Recognition, Used by); the editable lists below are bespoke controls, not `ListSpec`s, whatever ADR §5c foresaw. |
| Editable lists | `CommandTree/`, `CommandHeader/`, `CommandsWorkbench/`, `StepList/`, `StepTypePicker/`, `MasterDetail/`, `GestureGrid/`, `GesturePicker/`, `SyncConflictList/`, with the shared pieces in `ListEditing/` and `Parts/` | The contract below. |
| Gesture drawing | `GestureGlyph/`, `GestureDrawArea/` | A stroke as a glyph (normalised, gradient, arrowhead), and the training canvas (as drawn). |
| Input capture | `Capture/`, `WindowFinder/` | Controls that take keys, a mouse button or a window from the whole system through a port, and fall back to their own window without the engine. |
| Step forms | `Steps/<Name>/` | One `IStepForm` per step type, found by scan, each a declared `FormScreen` rendered by `SectionForm`. |

## The editable-list contract

- **Items in, one `ActionRequested` out.** The host (a view model) builds the items, plain records (`CommandItem`, `SectionItem`, `StepItem`, `MasterItem`, `GestureTileItem`); their `From` factories live in the view models (`ViewModels/Commands/Projection/`), the wording of kinds stays here (`TriggerKindExtensions`, `InputKindExtensions`, `StepPlatformMarker`). Every intent (menu entry, key, button, rename, drag) ends in one action enum value.
- **The host owns the selection**, by id; it survives a rebuild of the rows, and applying it never echoes back as a request (the `_applying`/`Quietly` guard in each list; S14b folds it into one `RowListHost`).
- **Right-click selects** before the menu opens; **a double click** renames (a row) or redraws (a tile).
- **Rename in place through `ListEditing/ItemRow`**, the one rename editor: Enter or leaving commits, Escape reverts, an unchanged name raises nothing, the commit after a leave is posted so a click on another row selects first.
- **One menu builder and one keymap**: each list's `…Menu` is a table over `ListEditing/ActionMenu<TAction>` and binds `ListEditing/EditKeymap` (F2 or Enter on Windows, Return on macOS, Ctrl/Cmd shortcuts). No list binds a key or builds a `MenuItem` of its own.
- **Keys stand down while editing**: a list's key bindings answer `canExecute` false while a name editor (or a step's form) has focus, so Delete and Ctrl+Z edit the text.
- **Controls only ask.** A dropdown or a box the host may refuse is put back on the item's value after every request (`Parts/AskingDropdown`; the header's Use on boxes), so a refused edit leaves it honest.

## Shared pieces (one home each)

| Job | Use | Not |
|---|---|---|
| In-place rename, the active box, `:editing`/`:inactive` | `ListEditing/ItemRow` (base of `SectionRow`, `CommandRow`, `StepRow`, `MasterRow`, `GestureTile`) | a `PART_NameEditor` handled in a row of its own |
| A list's context menu and its keys | `ListEditing/ActionMenu<TAction>` + `ListEditing/EditKeymap` | `new MenuItem`, `new KeyBinding`, or a `Key.F2`/`Key.Return` rename outside them |
| A dropdown that only asks | `Parts/AskingDropdown` (the header, the sync conflict row) | an `_applying` flag around a `ComboBox` |
| A template button's click | `Parts/TemplateParts.OnClick(e, "PART_X", action)` | `e.NameScope.Find<Button>(…).Click +=` |
| A button built in code | `Parts/ToolbarButton.Create(label, action)` (the `toolbar` class) | `new Button` + `Classes.Add("toolbar")` |
| A step form's value and its one edit path | `Steps/StepFormState<TStep>` | a local `state` and `Emit` |
| Key list, modifier toggle, held-key boxes | `Capture/HotkeyChoices` | a `KeyCode` choice list or `ModifierToggle` wiring in a form |

## How a component finds a port

Step forms and field renderers are built by assembly scan, without services, so the ports a component needs (`IKeyCapture`, `IButtonCapture`, `IWindowSystem`, `IDisplayModes`) are one application resource, `ComponentPorts` (`ComponentPorts.Near(control)`), which `Hosting/Modules/FormServices.Publish` puts there once the engine runs. A component's explicit property (`KeyCapture`, `Capture`, `ButtonCapture`, `WindowSystem`) wins: tests and the gallery set fakes. Without the engine the captures are null and each control falls back to its own window. Components never hold an engine adapter (`EngineKeyCapture`, `EngineButtonCapture` are in `Hosting/`).

## Templates

There is no `.axaml` here: each component's `ControlTheme` is in `Themes/Default/Lists.axaml` (lists, panels, workbench, header, pickers, dialogs) or `Forms.axaml` (forms, fields, the hotkey box). The `PART_*` names and pseudo-classes in a component's summary are the contract with those templates; search the theme files for them. Anything the F1 inspector should name gets `Region.Mark`.
