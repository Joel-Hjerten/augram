# Learnings 0008: building the glass look (plan 0006, 2026-10-11, PC session)

What the plan 0006 session learned while re-skinning the app with a lead and four worktree agents. Read it before touching a theme file.

## Seeing the UI without running Augram

- **`tools/UiShots`** renders the real main window headless (Avalonia headless with Skia drawing) to PNGs, one per tab key and theme. It runs from the composition root on a temporary config folder and never starts the desktop lifetime, so no engine, hook or overlay exists. It is safe for agents on Joel's machine. Read the PNGs with the file reader. `--gallery` renders every dev gallery page, and since step 6 each page shows dark and light side by side. Usage: `tools/UiShots/README.md`.
- **What it cannot show:**
  - the system's glass behind a real window (shots show the solid fallback);
  - the caption buttons and traffic lights;
  - states that need a pointer or a real capture, such as hover, an open dropdown or the hotkey box capturing.

  One agent covered hover and open states with a throwaway headless harness in its scratch folder (Skia, `Dispatcher.UIThread.RunJobs`, `AvaloniaHeadlessPlatform.ForceRenderTimerTick`, `CaptureRenderedFrame`). That is fine to repeat, but don't commit it.
- It caught real bugs at once. Example: tab labels invisible in light (next section).

## Theme layering (Default on top of Wireframe)

- **Resources:** a `Styles` object looks in its own `Resources` first, then in its children from last to first. So `Default.axaml`'s own resources, and an include added after the Wireframe include, win over Wireframe's.
- **Styles:** Avalonia has no selector specificity. Of two equal setters the later style wins. The include order is Wireframe, `Forms.axaml`, `Lists.axaml`, `Text.axaml`; Lists comes after Forms so its row metrics win, and the text roles come last.
- **Every theme file merges `Tokens.axaml` into its own resources.** A file's top-level setters resolve while it loads, before a sibling include exists, so a missing token is a load-time XAML error.
- **Wireframe's global `TextBlock { Foreground = {StaticResource Brush.Text} }` beats inherited colours.** A label inside a tab or pill ignored its parent's foreground and stayed light grey: invisible in the light theme. The Default theme overrides the global rule with `{DynamicResource Text.Primary}`. A template that must show its parent's colour binds it locally (`Foreground="{Binding $parent[TabItem].Foreground}"`), because a local value beats any style.
- **Text roles (`Text.axaml`, included last):** a button's, check box's or segment's label is an `AccessText` (its presenter recognises access keys), which a `TextBlock` type selector does not match, so it inherits its control's font size: Fluent's 14 px showed on every check box and segment until one rule gave those controls Body's size. And because the roles come last, a colour-only state (an inactive name, the log's columns) must come after them in that file, or the role's colour wins.
- **Fluent sets some values on its own template parts** (the dropdown frame, the arrow, the colour picker's tabs), and a plain style cannot override those. The Forms agent used selectors with an always-true condition while the part shows (`:dropdownopen`, `:not(:disabled)`, the flyout's `nopadding` class).

## Switching theme while the window is open

- **`StaticResource` is resolved once.** Only `DynamicResource` follows `ThemeDictionaries`, so every colour and corner in the Default theme is dynamic. Wireframe stays static and does not follow a theme switch, which is accepted: it is only a gallery option now.
- **Colours computed at run time** (the accent from the trail colour, the pictures' colours, the corners) go into `Application.Resources`, per variant into its `ThemeDictionaries` (`Themes/AppearanceLink`). Application resources win over the theme's tokens. The tokens are what a root without the link shows (tests, the previewer).
- **`ThemeVariantScope`** lets one window show both themes at once: the gallery's side-by-side pages (`DevGallery/ThemePair`).
- **The light theme needs its own accent rule** (Joel): darkening every fill to 3:1 against white turned yellow into mustard. Fills stay bright (1.6:1 with a deeper edge); only accent-coloured text takes the dark shade (`Themes/AccentPalette`).

## Glass

- **The system does the blur and keeps its strength to itself.** Avalonia takes a `TransparencyLevelHint` list and uses the first level the platform supports: Acrylic or Mica first, `None` last.
- **When `ActualTransparencyLevel` is `None`** (Solid, transparency effects off, battery saver, an unfocused Mica window), the window's tint must be painted opaque. Otherwise the window is see-through with nothing behind it (`Themes/WindowBackdrop`).
- **Dialogs get the backdrop** through a `Control.LoadedEvent` class handler on `Window`, only for windows with an owner, and never the trail overlay, which has no owner and is excluded by type as well (CLAUDE.md invariant 6).
- **Windows 11, Joel's first real-window run (2026-10-11):**
  - The frosted glass shows: the desktop is blurred behind the tint, and Windows draws its own caption buttons over the extended title bar (`PreferSystemChrome`).
  - Dragging by an empty part of the title bar works, and a double click maximizes and restores.
  - Snap layouts appear on the maximize button, and Wallpaper tint (Mica) works and looks good (Joel's second run).
  - macOS: not run yet.

## Clicks (Joel's first real-window run: no top-level tab could be clicked)

- **A `Border` whose corners differ (`10,10,0,0`) is not hit-testable** in Avalonia 11.3, even with a transparent background. Uniform corners are fine.
  - An unselected folder tab's only background was such a border, so presses fell through to the title bar.
  - The fix was `Background="Transparent"` on the template's root panel.
  - A headless check can't see this unless it renders a frame first: hit testing uses the last rendered frame, so call `Dispatcher.UIThread.RunJobs()` and `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` before `window.MouseDown`.
- **A drag area must leave its children's presses alone.** A TabControl selects in its own handler, which sits above the title bar in the bubble route. So `WindowDragArea` skips presses that land on a tab, button, text box or list item.
- **A test that sets `SelectedKey` from code proves nothing about clicking.** `ShellTitleBarTests` now clicks a real tab through the headless input.

## Smaller ones

- **`GlyphStroke` draws one segment at a time with round caps.** At 3 px, or with a translucent colour, the overlaps show as beads. Keep gesture strokes at 2 px and opaque (inactive glyphs use opaque greys).
- **Parallel agents share the session's temp and scratch folder.** One agent overwrote the lead's commit message file. Give scratch files names of your own.
- **Agents in worktrees start from the base the lead names.** Each restyle agent added one include line to `Default.axaml`, and the lead resolved the one-line conflicts at merge, as planned.
