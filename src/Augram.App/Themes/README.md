# Themes

The look of every component (ADR-0002 §5b): tokens, the one theme, and the links from Options › Appearance into it. Default, the glass look (plan 0006), is the only theme since the Wireframe theme was retired (plan 0007, decision 2). The visual reference is `design/mockups/augram-glass-theme.html`; why the layers, and static versus dynamic, are in [learnings 0008](../../../docs/learnings/0008-the-glass-look.md). See the UI headless with `tools/UiShots` ([its README](../../../tools/UiShots/README.md)).

## What lives here

| File | Holds |
|---|---|
| `Tokens.axaml` | The semantic keys. Per theme variant (`ThemeDictionaries`, always used as `DynamicResource`): `Layer.*` (the glass layers), `Text.*`, `Accent.*`. Fixed: `Radius.*`, `TitleBar.Height`, `Tab.Height`, `Brush.Glyph` / `Brush.GlyphStart` (the gesture pictures' gradient, which `AppearanceLink` replaces), `Inspector.Depth*`, `Space.*`, `Pad.*`, `Border.Hairline`, `Row.*` (the Row rule), `Dialog.*` (the dialog windows' sizes, read by `Dialogs/DialogSize`), `Font.*`, `Form.LabelColumn`, `Editor.*`, `Swatch.*` (a colour field's preset swatches), `Workbench.TreeWidth`, `SyncConflict.*`. |
| `Default/Default.axaml` | The theme's root: the shell's title-bar tabs and sub-tab pills and `ScreenHost`; it includes the three files below, in this order. |
| `Default/Forms.axaml` | `SectionForm`, `SectionView`, `FieldRow`, `TextPanel`, `ColorEditor`, the form lines' classes, the standard controls app-wide (buttons, `primary`, text, dropdown and number fields, check box, the switch for `CheckBox.field-editor`, the segmented `RadioButton.button-radio`, slider, scroll bars, tooltips, menus, flyouts), the hotkey box and its modifier toggle. Its own per-variant tokens: `Layer.Popup`, `Slider.*`, `Swatch.*Brush`. |
| `Default/Lists.axaml` | Panels with their header inside, rows (`Border.row`, `Grid.row-line`), the Commands workbench and header, steps, pickers, gestures and the training canvas, dialogs, the sync conflict list, the window finder, transfer, the log. Its own per-variant tokens: `Lists.DangerSoft`, `Lists.GlyphStartMuted`, `Lists.GlyphMuted`, `Lists.Popup`, `Lists.Cleaned`, `Lists.PartnerFill`, `Lists.PartnerEdge`. |
| `Default/Text.axaml` | The text roles (heading, title, group, body, secondary, badge, mono, …), last so they win; the role table is in its header. |
| `AppearanceLink` | The one reader of Options › Appearance and the trail colour for the UI: theme, accent and picture colours per variant, corners, window backdrops. |
| `AccentPalette` | One colour in, each theme variant's accent shades out. |
| `WindowBackdrop` | The system glass and the tint over it (`Layer.Window` at the user's tint); on macOS also the blur view's material and unfocused state (`Platform.MacOS/MacWindowGlass`: Frosted glass stays glass unfocused). |
| `ThemeSelector` | The one line that picks the theme (`Active`), and loading it. |

## Rules

- A colour or a corner is a `DynamicResource` (only those follow a theme switch and the corner setting); a size or spacing may be a `StaticResource`. No literal colour under `Components/` (`Architecture/ComponentColorTests`); the theme files may use `Transparent`.
- A colour that differs between Dark and Light goes in a `ThemeDictionaries` block: `Tokens.axaml` when components share it, else the file that uses it.
- A text's size, weight and colour come from its role in `Text.axaml` (the Text rule, App README "Rules of the road").
- Check a change in both themes with UiShots; a gallery shot holds both side by side.
- A new theme: App README, "How to add… A theme".
