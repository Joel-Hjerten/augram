# Declarations

The vocabulary screens are written in (ADR-0002 §5c): records that say what a screen holds, never how it looks. `Components/` renders them; a screen never lays out a control itself (the Declaration rule, App README).

## Screen shapes

| Shape | Rendered by | Used by |
|---|---|---|
| `FormScreen` (sections of fields) | `Components/SectionForm` | Options' sub-tabs, Health, every edit form and dialog form, the import review's top |
| `ListScreen` (a `ListSpec`) | `Components/ItemList` | Log, Recognition |
| `ComponentScreen` (one component built by hand) | the component | the Commands, Exclusions and Gestures workbenches |
| `TextScreen` | `Components/ScreenHost/TextScreenRenderer` | a module's fallback when its tab is not registered |

## Field kinds

Each kind is a record here and a renderer in `Components/Fields/<Kind>/` (found by assembly scan; `Architecture/FieldKindCoverageTests` lists them). Before adding a `CustomField`, look for the kind below.

| Kind | Record | For |
|---|---|---|
| Toggle | `ToggleField` | one bool |
| Toggles | `TogglesField` + `ToggleOption` | a few bools on one row ("Use on ☑ Windows ☑ macOS"; the edit forms take it from `ViewModels/UseOnEdit.Declare`) |
| CheckList | `CheckListField` + `CheckListItem` (`LiveItems` for a list that changes; an item's optional `Remove`, a `CheckListRemove`: while its `Removable` reads true the caption is muted, its note ("unused") shows and an ✕ removes it) | bools one under the other with a detail (the Not in, Also in, Allowed for and export dialogs; their ticks are a `ViewModels/CheckedSet`; the Not in and Also in dialogs set `Remove` through `ViewModels/Commands/RemovableEntries`) |
| Dropdown, ButtonRadio | `DropdownField<T>`, `ButtonRadioField<T>` + `Choice<T>` | one of a few values; `ChoiceBinding<T>` maps a typed binding to indexes; a `Choice` may be `Marked` (a button-radio segment outlined in the accent line whether selected or not: this computer's platform on Fields for, 0.14.1) |
| Text | `TextField` (`Multiline`) | a value stored on every keystroke |
| CommittedText | `CommittedTextField` (`Wide`) | a value committed on leaving or Enter, reverted on Escape, its problem under it (Options › Sync's address and machine name over a `DraftText`) |
| Number, Slider | `NumberField`, `SliderField` | a number in a range |
| Color | `ColorField` (`Presets`) | a colour |
| Pattern | `PatternField` | text + Use Regex + magnifier on one line (the identification form) |
| Note | `NoteField` (`Warns`: in the warning colour) | read-only text, live or fixed |
| Links | `LinksField` + `LinkItem` | names that open something ("Used by") |
| Buttons | `ButtonsField` + `FieldButton` (`Status`, `Hint`, `Choice`) | buttons on a form line: alone (Export…, Import…), with a wrapping status line (Sync now, Resolve…), with a hint (Detect…), after a dropdown (Apply to all) |
| Custom | `CustomField` | a component with no kind of its own (a capture box, the window finder) |

Any kind can carry `Visible`, `Enabled` and `Accessory` (`Field`); `StretchesEditor` makes the editor take the row.

## Bindings

| Job | Use | Notes |
|---|---|---|
| Bind a field to a property | `DelegateBinding<T>(get, set?, owner?, propertyName?)` | The property name is read from the getter (`() => vm.Name` → `Name`); pass `propertyName:` when the getter is not a property, `null` to refresh on any change. An owner raising the **empty name** refreshes every binding on it (the one "everything changed" convention). |
| One owner over several observables | `FormChanges(sources…)` | Raises a nameless change when any source changes (the identification form and its Windows/macOS view). |
| A field on a choice | `ChoiceBinding<T>` | Labels and a selected index over `IValueBinding<T>`. |
| F1 source location | `SourceLocation` (from `[CallerFilePath]`/`[CallerLineNumber]`) | A helper that builds a field passes its caller's file and line through (`UseOnEdit.Declare`). |

## Lists

`ListSpec`: title, columns, source, toolbar, filters, a `ListSearch` (plan 0009: the box's text as a `IValueBinding<string>`, the row's searchable text, and the rows' word for "No lines match 'xyz'"), auto-scroll, row component. A list with a menu, keys or drag-reordering is a component (step list, gesture grid), not a declared list.
