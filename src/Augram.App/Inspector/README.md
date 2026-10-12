# Inspector

The F1 layout inspector (ADR-0002 §5d), Debug builds only: hold F1 in the main window and every marked control is outlined, coloured by nesting depth; F1 + click copies what was declared there to the clipboard.

| Job | Use | Notes |
|---|---|---|
| Mark a control as a named region | `Region` (attached properties; `Region.Mark` in one call) | `SectionForm`, `ItemList` and `Shell` set them from their declarations, so no screen sets them by hand. |
| What a region says | `RegionInfo` | The declaration path (screen › section › field), its kind, where it was declared, the bound property. |
| Draw the outlines while F1 is held | `InspectorOverlay` | Composed into `Views/MainWindow` over the shell. The colours are the `Inspector.Depth0`…`Depth3` and `Inspector.Label` tokens (`Themes/Tokens.axaml`). |
| The clipboard text of F1 + click | `InspectorReport` | One line per item. |
