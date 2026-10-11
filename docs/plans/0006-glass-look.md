# Plan 0006: The glass look (D2): frosted, layered, dark by default, light on request

**Status: BUILT on main (2026-10-11, PC session); Joel's real-window check (steps 1 and 7) next.** Decided by Joel the same day on the mockup. What was built:
- **Steps 2–6, with tests:** `Themes/AppearanceLink`, `AccentPalette` and `WindowBackdrop`; the appearance settings and the yellow trail default; Options sub-tabs with Appearance › Theme, `SliderField` and colour swatches; the Default theme (`Default.axaml` shell, `Forms.axaml`, `Lists.axaml`); the gallery in both themes; dialogs' backdrop.
- **Step 1's code:** the title-bar tabs, `WindowDragArea` and the backdrop. Its check needs Joel's eyes on a real window: the glass, dragging, snap layouts, and the same on the Mac.
- **Gotchas found on the way:** [learnings 0008](../learnings/0008-the-glass-look.md).

Joel: the app is "in a pretty decent state", time to make it more attractive. He doesn't like the white outlines and dividers, and wants depth: "background layers are darker, and when you add the tab and the sub tab, they stack on top of each other and move towards the user", with tabs that "go up, a bit rounded, in front of inactive tabs" like Chrome or VS Code. He likes frosted glass and rounded corners. Dark is preferred, light should be possible. References he collected: [docs/reference/ui-inspiration/](../reference/ui-inspiration/README.md) (JetBrains Islands, Windows 11 Mica, SukiUI, Raycast). The design was settled on an interactive mockup: [design/mockups/augram-glass-theme.html](../../design/mockups/augram-glass-theme.html) (also published privately for Joel at https://claude.ai/artifact/FMAG9FhhMRnHqHB3zmf8rH). Open the HTML file in a browser: it is the visual reference for every decision below. ADR-0002 §5b already reserved `Themes/Default/` for "the real design" once D2 was decided; this plan fills it. The Wireframe theme stays, as a gallery option.

## Done when

On the PC and on the Mac (Joel):
- Augram opens dark, with a frosted window background, by default. Options › Appearance changes theme, window background, tint, corner rounding and accent while the window is open, and the choices are still there after a restart. Each machine keeps its own.
- With transparency effects off in Windows Settings › Personalization › Colors, the window is solid and still looks right.
- The top-level tabs sit in the title bar, and the selected one is joined to its page. Sub-tabs are pills inside the page. Options has five sub-tabs.
- No white outlines or white dividers anywhere. Panels are told apart by being a step lighter, with a faint light edge.
- Light theme: every screen and dialog is readable. The dev gallery shows every component in dark and light side by side.
- A fresh install's trail and accent are yellow. Trail and accent colours can be picked from the swatches or with Custom….
- The trail overlay, the hooks and the engine are untouched: nothing in this plan goes near them.

## Decisions (Joel, 2026-10-11, and the lead's calls he can overrule)

1. **Depth from layers, not lines.** Four layers, each a step lighter in dark (and a step whiter in light): the window background, the selected tab's page, the panels (cards) on it, and the controls in them. Every layer is a translucent fill over the window background, so the same tokens work over glass and over solid. Outlines go: a panel gets only a faint light edge (an inset 1 px highlight). Dividers survive only between rows inside a card, and very faint. Values to start from are in the mockup's `[data-mode]` blocks.
2. **Window background: Frosted glass, Wallpaper tint or Solid** (Joel: "I've always liked the frosted look"). Frosted glass is Windows 11's Acrylic and the macOS system blur: it blurs whatever is behind the window. Wallpaper tint is Windows 11's Mica: it picks up only the wallpaper's colour. On macOS it is the system blur with a stronger tint (lead's call; step 1 confirms what looks right). Solid has no glass. Default: Frosted glass. The operating system does the blur, so Augram never captures the screen. Windows shows the backdrop solid by itself when transparency effects are off, in battery saver, and for Mica while the window is unfocused, so every screen must look right solid.
3. **Blur strength is the system's.** Neither Windows nor macOS lets an app set it (Joel asked, 2026-10-11; "it's fine"). What the user controls is the **Tint**: 0–100 %, default 62 %, the opacity of the theme colour over the glass. More tint keeps text readable over a bright or busy desktop. Disabled under Solid.
4. **Corner rounding is a user setting:** 4–18 px, **default 12 px** (Joel). It sets panels, tabs, buttons and fields together (`--r` in the mockup; controls use half of it). The window's own corners stay the system's (8 px on Windows 11).
5. **Tabs.** The top-level tabs move into the title bar. The selected one is a folder tab in the page's colour, rounded at the top, flaring into the page at the bottom, with no line between tab and page (Chrome, and Windows 11's tabbed title bar, Joel's Mica Alt screenshot). Sub-tabs are pills inside the page: the selected one has an accent-tinted fill and a faint accent outline (JetBrains Islands). Two different shapes, so the two levels never look alike. **Overflow (Joel, 2026-10-11):** sub-tab pills wrap onto a second row, or more, when they don't fit, so none is ever hidden (the dev Gallery has dozens). The title-bar tabs stay on one row; in a window too narrow for them they scroll sideways with the mouse wheel and by dragging. The title bar keeps the build's name and version ("Augram (Dev) 0.10.3", requirements F7). On Windows the system caption buttons stay; on macOS the traffic lights stay.
6. **The accent follows the trail colour** (Joel: "a great idea"), with **Options › Appearance › Accent colour: "Same as the trail colour"**, a switch, on by default. Turned off, a colour field appears and the accent is that colour instead ("some options are always good"). From the one colour, each theme derives its own shades in code. **Dark:** fills at least 3:1 against the panel, link text at least 5:1. **Light** (Joel, 2026-10-11: yellow fills darkened to 3:1 looked "too muted and dark"; "yellow on white needs care"): fills stay bright: the primary button, switch, check box, slider and the selection tint. They are darkened only until they stand out from white at 1.6:1, and get a thin deeper edge for definition. Only text written in the accent colour (links, the New badge) takes the darker readable shade, at least 4.5:1. Text on a fill is whichever of near-black or white contrasts more, so yellow gets dark text. The gesture pictures already follow the trail colour (`Themes/GlyphColourLink`, `GlyphColours.StartFor`). In the light theme they use a darker shade of it, only as dark as a thin line on a white tile needs (2:1), so a yellow trail stays visible without turning mustard (lead's call, eased after Joel saw the span dip at yellow).
7. **A fresh install's trail colour is yellow, `#F5C542` (245, 197, 66)** (Joel: "yellow, as that I think will be the theme for Augram, as I've made the icon that color"). It is the mockup's shade, not the icon's exact fill (`#EECE59`); Joel: the mockup's colours "look good". It replaces StrokesPlus.net's green 0/255/64 (requirements F6, 2026-10-05). Only the default changes: a config that has a colour keeps it.
8. **Colours are picked from swatches or Custom…** (Joel: "we definitely need to give the user an ability to pick colors themselves"; the swatches "look very pretty"). The Trail colour, and the Accent colour when it doesn't follow the trail, are shown as a row of round preset swatches in rainbow order (Joel): Red `#FF5A4F`, Orange `#FF8A3D`, Yellow `#F5C542`, Green `#00FF40` (StrokesPlus.net's, so existing configs match it), Cyan `#36D6E7`, Blue `#5B9BFF`, Purple `#A78BFA`. Joel expects yellow, green, blue, cyan and red to be picked most; purple is hard to see as a trail but stays for whoever wants it. After them comes **Custom…**, which opens the colour picker the Colour field has today. The selected colour has a ring; a custom colour shows as one more swatch at the end, selected.
9. **Options gets sub-tabs** (Joel: "you get a faster overview and you get where you want to more quickly; if we add more settings we don't run out of space"):
   - **General**: stroke button, detect, ignore keys, start at login, the macOS menu-bar icon, config folder
   - **Strokes**: Capture, Recognition
   - **Appearance**: Theme (new), Trail
   - **Sync**: Sync, Export and import
   - **About**
10. **Appearance › Theme** holds, in order:
   - **Theme**: Dark / Light / Follow system. Default Dark (requirements F7, dark from the start).
   - **Window background**: Frosted glass / Wallpaper tint / Solid.
   - **Tint**: a slider.
   - **Corner rounding**: a slider.
   - **Accent colour**: as decision 6.

   These are settings, so they stay per machine like the trail and the stroke button. Settings don't sync (requirements F8; lead's call).
11. **Switch or check box** (Joel agreed, 2026-10-11). A single on/off setting on its own row is a **switch**: Start at login, an excluded app's Active, "Same as the trail colour". A **check box** is for:
    - one per list row (command, app group, excluded app)
    - several side by side (Ignore keys, Use on)
    - check lists, and dialogs with an OK button (the export selection)

    In the code, `ToggleField`'s editor becomes a switch; `TogglesField`, `CheckListField` and the row actives stay check boxes. This is a theme job: a `CheckBox` ControlTheme drawn as a switch for the toggle field's class.
12. **Section titles: above the card in Options, inside everywhere else** (Joel: titles outside give the sections "a little bit of space between them naturally"; "it's only Options that uses this; everything else has basically the title inside each group"). A `SectionForm` puts each section's title on the page above its card. Panels with their own buttons (`ItemList`, `CommandTree`, `MasterDetail`, the gesture grid, the log, dialogs) keep the title in their header row, beside the buttons.
13. **Unchanged:** the system font (Segoe UI Variable on Windows, SF on macOS); the ⓘ help rule; every screen's layout and declaration (the Commands workbench stays tree beside panel); the Row rule's one line height; the theme rule (components name tokens, never colours).
14. **Dialogs get the same look.** The training window, form dialogs, sync conflicts and the import review get the same window background setting and layers as the main window (lead's call).

## Steps

1. **The glass test (lead, PC then Mac; Joel runs it).** Turn the window background on in the real main window, behind a setting with Solid as the fallback: `TransparencyLevelHint` from the setting (`AcrylicBlur`, `Mica`, `None`), and `ExtendClientAreaToDecorationsHint` for the title-bar tabs. Check on Windows 11 and macOS:
   - which backdrop Avalonia 11.3 actually picks, and how its blur compares with the mockup's
   - the fallbacks: transparency effects off, battery saver, an unfocused window
   - dragging the window by its title bar, double-click to maximize, Windows 11's snap layouts on the maximize button
   - the F1 inspector still works
   - a dev build behaves the same as the installed one

   It is a normal window, not the overlay, so invariant 6 is untouched. Still, per CLAUDE.md, only the lead runs it, after telling Joel. Record what was found in a learnings note.
2. **Tokens and theme switching (lead).**
   - `Tokens.axaml` gains the layer ladder (`Layer.Window`, `Layer.Page`, `Layer.Panel`, `Layer.Control`), `Edge`, `EdgeTop`, `Divider`, the accent family (`Accent`, `AccentText`, `AccentSoft`, `AccentLine`, `OnAccent`) and radius tokens tied to the corner setting.
   - Dark and Light values go in `ThemeDictionaries`. The Default theme uses `DynamicResource`, so a change applies at once; the Wireframe theme keeps its `StaticResource`s.
   - `App.RequestedThemeVariant` follows the setting: Dark, Light, or Default for "Follow system".
   - The accent shades are computed by a pure, tested class, as `GlyphColours` is. They are written into the resources by **extending `Themes/GlyphColourLink`**, which already follows the trail colour, rather than adding a second link.
3. **Settings (agent).**
   - An `appearance` section in the config: theme, window background, tint, corner rounding, accent follows trail, accent colour. It is additive: missing or partial means the defaults, as the `sync` section does (Config README).
   - Its rules: tint 0–100, rounding 4–18.
   - `SettingsStore.SetAppearance`.
   - `TrailSettings`' default colour becomes `#F5C542`, and its comment and the requirements reference change with it.
   - Tests: round trip, defaults, and an older file without the section.
4. **The Default theme (agent, from the mockup).**
   - A ControlTheme in `Themes/Default/` for every component, replacing the Wireframe include: `Shell` (title-bar tabs, folder tab, page; the tab strip a horizontal scroller whose wheel scrolls sideways and which can be dragged), sub-tab pills in a wrapping panel, `SectionView` (title above the card), `FieldRow`, `ItemList`, `CommandTree` rows, `MasterDetail`, `GestureGrid` tiles, `StepList`, `WindowFinder`, `SyncConflictList`, `FormDialog`.
   - The standard editors: button, primary button, dropdown, text and number fields, the segmented `ButtonRadioField`, the switch, the check box, the colour field, the slider. In dark: a solid accent thumb on an accent fill, with a lighter track (the browser's own slider in the mockup; Joel: "the old one was fine"). In light: an accent fill on a light grey track and a white-ringed thumb, never a dark track.
   - The Row rule's metrics stay as they are.
5. **Options sub-tabs and Appearance (agent).**
   - `OptionsScreen` becomes five declarations under one `NavEntry` with sub-entries, as Diagnostics has: keys `options.general`, `options.strokes`, `options.appearance`, `options.sync`, `options.about`. Anything that sends the user to "Options › Sync" (Resolve…, notes) targets the sub-tab key.
   - A new field kind, **`SliderField`** (a declaration, a renderer, a gallery entry; App README "A field kind").
   - The Colour field gains preset swatches and Custom… (decision 8), for the trail and the accent. The accent's colour field uses `Visible` so it shows only while the switch is off.
   - Texts that say "Options › Trail" become "Options › Appearance › Trail".
6. **Gallery (agent).** Every component page shows it in dark and light side by side, and Wireframe stays selectable (ADR-0002 §5b).
7. **Joel's check, PC then Mac.** He installs the build and goes through "Done when". His machines keep their saved trail colour (green) until he picks the yellow in Options › Appearance › Trail.

## Not in this plan

- **User-set blur strength**: the system's (decision 3).
- **A Frost slider adding grain and haze over the blur**: offered, and Joel didn't need it.
- Named themes or a theme editor; custom fonts; icons on tabs; animations beyond hover and press.
- New layouts for any screen.
- The training flow (the "training flow" part of D2's original wording stays as built).
