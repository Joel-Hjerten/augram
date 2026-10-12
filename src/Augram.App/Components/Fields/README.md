# Fields

One renderer per field kind of `Declarations/` (ADR-0002 §5c): `SectionForm` asks `FieldRendererRegistry` for the editor of a field and never names a kind. Adding a kind is adding a folder here; the steps are in the App README, "How to add… A field kind".

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Build the editor for a kind | an `IFieldRenderer` in `<Kind>/` (`Kind` matches the field's) | Found by assembly scan (`FieldRendererRegistry.Default`); parameterless constructor. `FieldKindCoverageTests` holds the naming convention. |
| Keep an editor in step with its binding | `BindingObserver.Attach(editor, binding, apply)` | Applies on entering the visual tree and on every `Changed` (through `Hosting/UiThread.RunOrPost`, so a change raised off the UI thread is posted), and unsubscribes on leaving the tree. |
| Look | the field classes in `Themes/Default/Forms.axaml` | A renderer names no colour, size or margin (Theme rule). |

The kinds: `ButtonRadio`, `Buttons` (buttons on a line, with a status, a hint or a dropdown), `CheckList`, `Color` (with `ColorEditor`), `CommittedText` (commit on leaving or Enter), `Custom`, `Dropdown`, `Links`, `Note`, `Number`, `Pattern`, `Slider`, `Text`, `Toggle`, `Toggles`.
