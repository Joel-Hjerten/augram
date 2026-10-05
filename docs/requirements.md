# Augram — v1 requirements (living document)

**Status: DRAFT — being worked out with Joel. Nothing here is frozen unless marked DECIDED.**

Each item is tagged:
- **DECIDED** — settled; changing it needs Joel's say-so
- **LEANING** — a preferred direction exists but is open to challenge
- **OPEN** — genuinely undecided; input wanted

Background research (prior art, the recognition algorithm, platform constraints) lives in [handoff.md](handoff.md). Joel's real StrokesPlus.net config, analysed as a usage-based spec, is in [reference/strokesplus-net-config.md](reference/strokesplus-net-config.md); notes on GestureSign (open-source .NET gesture app, GPL, reference only) are in [reference/gesturesign-notes.md](reference/gesturesign-notes.md); notes on StrokeIt (the 2001 original; MIT plugin SDK, closed engine) are in [reference/strokeit-notes.md](reference/strokeit-notes.md); notes on Stroke (minimal modern .NET engine, GPL) are in [reference/stroke-notes.md](reference/stroke-notes.md); notes on BetterMouse (500-line C++ direction-gesture tool, MIT) are in [reference/bettermouse-notes.md](reference/bettermouse-notes.md); the classic StrokesPlus C++ source, which the recognizer is ported from, is annotated in [reference/strokesplus-classic-source.md](reference/strokesplus-classic-source.md). Architecture decisions get their own ADRs in [adr/](adr/).

---

## 1. Product summary

A simplified, cross-platform (Windows + macOS) mouse gesture utility inspired by StrokesPlus.net: hold a chosen mouse button, draw a stroke, Augram recognizes it and fires an action (keystroke sequences, media/volume keys, window management). Tray-resident, tiny footprint, zero perceptible latency.

**Quality bars (DECIDED — from Joel, non-negotiable):**
1. Recognition quality on par with StrokesPlus.net
2. Zero perceptible latency while drawing
3. Trail renders on top of essentially all applications

**Scope stance (DECIDED — Joel, 2026-10-05):** StrokesPlus.net completeness is *not* a goal. Joel will likely never need most of it, and one-off needs can be custom-coded with agents cheaply. The minimum that must work end to end: connect user-defined gestures through a learn/capture window, then trigger actions — shortcuts, keystrokes, typed strings, command lines, media keys. Everything beyond that minimum is justified by evidence from Joel's real config ([reference/strokesplus-net-config.md](reference/strokesplus-net-config.md)), not by feature parity.

## 2. Functional requirements

### F1. Gesture capture — DECIDED (shape), OPEN (details)
- Any mouse button assignable as the stroke button.
- **Detect-to-assign (DECIDED):** assignment works by a "press the button you want" capture flow, not only a fixed dropdown. This handles buttons that vendor software (e.g. Logi Options+ on Joel's Logitech Anywhere 3S) doesn't expose by name. A standard dropdown of common buttons can exist alongside, but detect is the primary flow.
  - ⚠️ Known ceiling to document in-app: if vendor software consumes/remaps a button at driver level, the OS hook never sees the original button — Augram can only bind what actually arrives at the OS layer. (Detect flow makes this self-evident to the user: if pressing it shows nothing, Augram can't use it.)
- Suppress-then-replay loop per [handoff.md §5](handoff.md): motionless press → replay original click (context menus must still work); movement → capture stroke.
- **Cancelling a gesture in progress — DECIDED (Joel, 2026-10-05):** either **press another mouse button** while drawing, or **hold still for longer than a user-settable duration** (set on the Options page; default 1000 ms, reset on movement). A cancelled gesture fires nothing and replays nothing.
- LEANING (from Joel's SP.net settings, see reference doc §3): the remaining thresholds, user-tunable on the Options page with these defaults — gesture start distance **30 px** (less = plain click, replayed); **no** click replay when a stroke matches nothing. Both matter because the stroke button is Middle, where a stray replayed click opens links in new tabs.
- LEANING v1: **wheel while holding the stroke button** fires wheel-up/wheel-down commands (volume globally, zoom per app). Second most used feature in Joel's config (~73 k fires). Semantics follow StrokesPlus, not GestureSign: a wheel tick fires at any time while the button is held, every tick fires again (repeat volume), the trail stops at the first tick, and the eventual button-up fires nothing. See [reference/strokesplus-classic-source.md §2](reference/strokesplus-classic-source.md).
- LEANING defer: rocker gestures and modifier+button chords. Zero and one use respectively in five years of config.

### F2. Recognition — DECIDED
- Port the MIT-licensed StrokesPlus angle-sequence recognizer verbatim ([handoff.md §3](handoff.md)): resample to N segments, compare per-segment angles, scale/position-invariant, deliberately rotation-sensitive.
- Multiple training samples per gesture, scores averaged; threshold below which nothing fires.
- Templates stored as raw point lists (resampled at match time) so precision stays adjustable.

### F3. Gesture training / learning — DECIDED (feature), OPEN (UX)
- User can record a new gesture by drawing it (several samples), name it, and assign an action.
- **A learn/capture window is part of the minimum (Joel, 2026-10-05).**
- **Training happens only on the Gestures screen (DECIDED — Joel, 2026-10-05).** No ambient learning: an unrecognized stroke does nothing, silently. Augram never pops up "do you want to add this gesture?" (SP.net's Learning Mode prompt, which Joel keeps disabled).
- **Flow (DECIDED — Joel, 2026-10-05):** the Gestures screen lists all gestures as glyph tiles (like SP.net's grid, see reference §8) with an **"Add new gesture"** button. It opens a **semi-large popup window** with a canvas the user draws in, and just two buttons: **Cancel** and **Accept**. Releasing the button ends a stroke; drawing again **replaces** the previous stroke automatically, so the user redraws until happy. No averaging or multi-sample in the training flow for now (the model keeps multiple samples for imported gestures only). Accept saves the gesture (name entered in the same window).
- **Normalisation (DECIDED — Joel, 2026-10-05):** a gesture drawn tiny and one drawn huge are the same gesture. Recognition is scale-invariant by construction (arc-length resampling), and the stored glyph is normalised for display by scaling on the longer axis so the **width/height ratio is preserved** (the F4 recipe all predecessors use).
- **Inactive gestures (DECIDED — Joel, 2026-10-05):** a gesture can be set inactive; inactive gestures are skipped by the recognizer and drawn greyed in every list and tile. Same for commands and app groups (the Active toggle on the row).
- **Gestures are independent of commands (DECIDED — Joel, 2026-10-05).** A gesture is a named glyph in its own library. Commands reference gestures by name; the same gesture can be bound in many app groups to different commands (your `\Up` is close-window globally, close-tab in Chrome, nothing in Steam games). Deleting a gesture must warn about the commands that reference it.
- **Gesture picker from the command editor (DECIDED — Joel, 2026-10-05):** clicking a command's gesture tile opens a **Select Gesture** picker showing the same glyph grid, with **New Gesture…** (opens the same draw area; on Accept the new gesture is selected for the command), **No Gesture** (the command is trigger-only, e.g. wheel-up = volume), OK and Cancel. The grid, the draw area, and the training logic are one component and one service each, used from both the Gestures screen and the picker (ADR-0002 §5a).
- LEANING: in the draw area, show the closest existing gesture and its score live ("looks like *7*, 84%") so duplicates are caught before Accept. Adding a sample to an existing gesture is "Add sample" on that gesture's tile, reusing the same draw area; not the focus (3 of Joel's 90 gestures use it).
- Implementation note for the plan: the draw area must accept the stroke button, not just the left button, so what you train is what the hook sees. The engine's training mode routes a stroke that *starts over the draw area* to the panel instead of recognizing it; strokes elsewhere behave normally. Drawing with the left button also works, for convenience.

### F4. Gesture icons — DECIDED (feature)
- Every gesture must be displayable as an icon in the UI, generated from its template points (normalized polyline + direction arrowhead). No hand-drawn icon assets per gesture.
- Ships with a starter set of common gestures (up/down/left/right, L-shapes, Z, circle…) so the list isn't empty on first run.

### F5a. Mapping model and vocabulary — DECIDED (Joel, 2026-10-05)

Three levels, and these are the names used in code, config, and UI:

```
App
└─ Commands page                         one page for global and per-app commands
   └─ Command list                       expand/collapsible groups, one per app; "Global" is the first group
      └─ App group
         └─ Command                      tied to one gesture (or a wheel trigger); named
            └─ Step list                 one or more steps, played in order when the command triggers
               └─ Step
                  ├─ Category → type     System built-ins (minimize, maximize, volume, media playback…) ·
                  │                      Hotkey · String · Command-prompt execution · Delay
                  └─ Settings area       the step type's own parameter form (hotkey capture field, text, command line…)
```

(Hierarchy as stated by Joel, 2026-10-05.)

- SP.net calls the middle level "Action" and nests categories inside apps; Augram drops those categories and calls it a **Command**. A **Step** is one executable unit; a step's *type* defines its category, parameter form, conversion rule and executor (one folder per step type, per N3). The **category** in the step picker is metadata declared by the type, so the picker groups itself and adding a type never touches the picker.
- LEANING refinements:
  - The **Global group is pinned first and cannot be deleted**.
- **List editing — DECIDED (Joel, 2026-10-05):**
  - **New, Copy, Paste, Delete** apply to app groups, commands, and steps alike, reachable two ways: a **right-click context menu** on the list area and on items, and **keyboard shortcuts** (Ctrl+N new, Ctrl+C copy, Ctrl+V paste, Del delete) acting on the focused list and selection. Paste targets the focused level: a copied command pastes into whichever group is selected, a copied step into whichever command is selected.
  - **Steps are reordered by left-button drag and drop** within the step list; order is meaningful and user-controlled.
  - **App groups and commands sort alphabetically**, always; they are never reordered by hand (Global stays pinned first).
  - **Step placement (Joel, 2026-10-05):** **Ctrl+D** on a selected step duplicates it **directly below** the original; **Ctrl+V** with a copied step pastes it **at the bottom** of the step list. Both select the new step and leave it ready to drag into place.
  - **Delete (Joel, 2026-10-05):** deleting an app group or a command asks for confirmation; deleting a step does not. Undo (Ctrl+Z) covers all three after the fact.
  - **Rename (Joel, 2026-10-05):** app groups, commands, and gestures can be renamed via the right-click menu or by selecting the item and pressing the platform's rename key: **F2 on Windows, Return on macOS** (Finder convention). Rename edits in place in the list; Enter commits, Esc reverts. Keyboard bindings live in one per-platform keymap so this and the Ctrl/Cmd shortcuts above are defined once.
  - LEANING: Copy places the item as Augram JSON (the F8 export format) on the system clipboard, so a command or step can be pasted into another Augram instance, a chat, or a text file, and pasted back from there.
  - An **app group's header** carries its identity and the "edit app definition" affordance (the matcher form, F5), so the Commands page is the only place apps are managed. Ignored apps stay on their own page.
  - A **command row** shows: glyph (or a wheel/trigger icon when there is no gesture), name, a short step summary ("Ctrl+W", "3 steps"), an active toggle, and the platform marker from F8 when relevant. An "override to nothing" command shows an explicit "does nothing here" summary.
  - **Step editing is inline:** the selected step expands in place inside the step list to reveal its settings area (accordion), so the whole hierarchy stays visible on one screen. This is a template choice under ADR-0002 §5b/§5c, so switching to a side panel later is cheap if the wireframe proves it wrong.

### F5. Actions — DECIDED (initial set)
- **Minimum set (Joel, 2026-10-05):** keyboard shortcut; keystroke *sequences* (ordered steps with optional delays); typed string; run a command line; media/volume keys.
- LEANING add (each has real use in Joel's config): window actions (close, minimize, maximize/restore, always-on-top toggle, center, set size, snap to left/right half); mouse click at the gesture start point; run program with arguments (elevated/hidden flags).
- **Per-application scoping — DECIDED v1 (Joel, 2026-07-24):** the mapping model has global commands, app-specific command overrides, and an ignore/exclude list of apps where Augram stays out of the way entirely. The UI is organized around these sections (see F7).
- LEANING scoping details (all in daily use in Joel's config): an app can **override a gesture to nothing**; an app can **suppress all global actions**; the ignore list has two modes, "gestures don't fire over this app" and "disable Augram entirely while this app is focused".
- LEANING invariant: **the target is the window under the gesture start point, activated before the action runs.** Chrome actions land on the Chrome window you drew over, not the focused window. Both SP.net and GestureSign do this.
- **App identification (DECIDED — Joel, 2026-10-05):** every app group has an identification settings page, modelled on SP.net's App Definition (reference §8): **executable file name is the primary field** (what Joel uses for 14 of 19 groups and what "add app" fills in by default), then full path, process name, window title, each with a Use-Regex toggle, plus a "pick a window" crosshair that fills the fields. Platform-specific escape hatch (Windows: window class chain, needed to match the desktop). On macOS the same fields map to bundle id / path / title. **One group may match several executables** (SP.net users do this with regex alternation, e.g. all Chromium browsers in one group); Augram's matcher takes a list of process names rather than requiring regex for that.
- Not needed by evidence: open URL, paste text, scripting.
- **Hotkey capture field — DECIDED (Joel, 2026-10-05):** while a hotkey field is capturing, the engine's keyboard hook **suppresses every key event system-wide** and routes it to the field, so combinations such as Win+L, Esc, PrintScreen, Alt+Tab or Ctrl+Alt+Del-adjacent keys are recorded instead of acted on (SP.net behaviour; GestureSign cannot do this because it uses ordinary window key events). The field shows the combination as modifiers + one key. Commit is **mouse-only** by design, since every key is being captured: an **Accept** button beside the field, and **clicking anywhere outside the field** also commits and returns control. A **Clear** button resets. Model: modifier set + one key; chords are expressed as a sequence of hotkey steps.
  - Safety invariant: suppression is held only while the field is actively capturing. It is released on commit, on click-outside, on window deactivate, on the capture control being unloaded, and by a watchdog after a configurable idle time (default 10 s) so a hung UI can never leave the keyboard dead. Mouse input is never suppressed during hotkey capture.

### F6. Trail overlay — DECIDED (feature)
- Visible stroke trail over all apps while drawing; transparent, click-through, per-platform native window; points batched per frame (handoff §7).
- LEANING: hardcoded-nice first with Joel's SP.net look as the default (width 5 px, 50% opacity, green 0/255/64); color/width become options later. Pen width must scale with the DPI of the monitor the stroke starts on (GestureSign lesson).

### F7. Tray presence + settings app — DECIDED
- Tray icon (menu bar extra on macOS): open settings, enable/disable, quit.
- **Tray behaviour (DECIDED — Joel, 2026-10-05):** **single click toggles Augram on/off** (icon shows the state unmistakably), **double click opens the app window**, right-click menu has Open, Enable/Disable, Start at login, Quit. macOS menu bar extras open their menu on single click by convention; how the toggle maps there is decided with the macOS port.
- **Start at login (DECIDED — Joel, 2026-10-05):** a setting, exposed both on the Options page and in the tray menu.
- **Stroke button (DECIDED — Joel, 2026-10-05):** user-selectable on the Options page (detect-to-assign per F1); default for a fresh install is Right, like SP.net. Development happens on Right while SP.net still owns Joel's Middle.
- Settings window is where gestures/actions/training/options live. Closed = app keeps running in tray.
- **The UI is a real workbench, not an afterthought (Joel, 2026-07-24):** it is used heavily while setting up, testing, and configuring gestures. Required capabilities:
  - Gesture list showing each user-defined gesture as its auto-generated icon (F4)
  - **Commands view (Joel, 2026-10-05):** one list area of app groups → commands (glyph + name) → a step panel for the selected command (F5a). Global is the first group, not a separate section.
  - **Navigation is tabs with sub-tab pages (DECIDED — Joel, 2026-10-05):** top-level tabs for Gestures · Commands · Ignored · Options; each tab may hold sub-tabs (e.g. Options › General / Trail / Shortcuts / Diagnostics). Tabs and sub-tabs are declared in the navigation registry (ADR-0002 §5c), so adding a page is adding an entry.
  - Standard desktop controls done properly: tabs, sectioned lists, checkboxes, radio groups, etc.
  - **Single UI implementation for both Windows and macOS** — the framework choice must not force writing the UI twice (constraint feeds ADR-0001)
- **Single-source UI components (DECIDED — Joel, 2026-10-05):** every UI element (button, list row, section header, toggle, gesture tile, dialog chrome…) is implemented once and shared app-wide. No per-screen one-offs.
- **Declarative, rearrangeable screens (DECIDED — Joel, 2026-10-05):** screens are declared as section/field trees (toggles, dropdowns, text, numbers, hotkeys, notes, custom components) and lists as specs, rendered by generic components. Adding a subsection or moving a checkbox is a one-place edit. See ADR-0002 §5c.
- **Layout inspector (good-to-have — Joel, 2026-10-05):** in dev/debug builds, holding **F1** outlines and labels every UI region so the structure is visible on the running app. See ADR-0002 §5d.
- **Dev-mode component gallery (DECIDED — Joel, 2026-10-05):** a panel available only in dev builds that shows every shared component in its states, so Joel and agents have a single place to see, reference, and visually verify the UI vocabulary. New components are added to the gallery in the same change that introduces them.
- OPEN: start-on-login default? First-run onboarding (macOS permissions especially)?

### F8. Configuration and portability — LEANING (JSON; Joel inclined, 2026-10-05)
- Human-readable config + gesture library on disk as **JSON**, safe to hand-edit and diff, with a `schemaVersion` field from day one and forward-only migrations in Core. (SP.net's binary `.spexport` is .NET `BinaryFormatter`, chosen for convenience not size; it is unreadable without the app, version-locked, and the API is removed from modern .NET. Not a model to follow.)
- **Export is the same format as the on-disk file**, never a second format. Export scopes: everything · gestures only · selected app groups (with the gestures they reference). Import merges rather than replaces, with a per-item conflict choice (keep mine / take theirs / keep both renamed).
- **Stable ids, not names, for references.** Gestures and commands get a generated id; commands reference gestures by id and display the name. Renaming never breaks a binding. (SP.net binds by name; two of Joel's commands already point at gestures that no longer exist.)
- Size is a non-issue: raw template points for 90 gestures are well under 100 KB minified. Write indented for diffability; optional `.augram.gz` later only if ever needed.
- **Cross-platform steps — LEANING (Joel's proposal, 2026-10-05; agent agrees):**
  - A step is authored once, on whichever platform the user is on, and stored with the platform it was authored on.
  - On the other platform, Augram applies an **automatic conversion** at execution time (Ctrl ↔ Cmd, Alt ↔ Option, Win ↔ Cmd where sensible, media keys unchanged, typed text unchanged). The command is **visibly marked** in the UI as auto-converted: a colored marker on the row and a text line under the step, e.g. "auto-converted from Windows: Ctrl+W → Cmd+W".
  - If the conversion is wrong or impossible (run-command paths, app-specific shortcuts), the user edits the step on that platform and Augram stores it as a **per-platform override** alongside the original. Resolution order: override for the current platform → auto-conversion of the authored step. The original is never modified, so the export still works on both platforms.
  - Steps with no sensible conversion (run-command, launch app) are marked "needs a macOS version" instead of being auto-converted, and are skipped at execution until one exists.
  - Model consequence for F5a: `Step { authoredOn, params, overrides: { windows?, macos? } }`. The step type owns both its conversion rule and its "convertible?" answer, so adding a step type stays one folder (N3).
- LEANING: back up the previous file before every write; fall back to backup, then shipped defaults, on load failure (GestureSign behaviour).
- LEANING (D9): **import from StrokesPlus.net's live JSON** (`%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`) as a v1 feature: 90 gestures, 19 apps, 212 actions migrate in one step. Script-only actions import disabled with the script kept as a note.
- OPEN: file location conventions per platform; single file vs split (settings vs gesture library).

## 3. Non-functional requirements

### N1. Cross-platform — DECIDED (goal), see ADR-0001 (means)
- Windows first (Joel has no Mac to test on yet), but every layer boundary chosen so macOS is a port of thin platform adapters, not a rewrite. No Windows types above the platform-adapter layer.

### N2. Performance — DECIDED
- Hook callback does near-zero work (append point, decide forward/consume, return).
- Recognition on button-up only.
- Tray-resident 24/7 ⇒ memory footprint matters. Target: tens of MB, not hundreds (this constrains the shell choice — see ADR-0001).

### N3. Extensibility & agent-friendliness — DECIDED
This is an explicit requirement, not an aspiration:
- **Extensible structure:** actions, gesture sources, and platform integrations behind small interfaces/registries, so "add an action type" is additive — a new file registering itself, never edits scattered across the codebase.
- **Agent-oriented docs:** CLAUDE.md conventions + docs taxonomy modeled on the Chimera repo (see [docs/README.md](README.md)) — read-first table, ADRs for closed decisions so future agents don't relitigate or reimplement, learnings/ for hard-won insight, plans/ with completed/ archive.
- **Comments at load-bearing spots:** invariants documented where an agent would otherwise "fix" them (e.g. *why* rotation sensitivity must be preserved, *why* handlers must stay synchronous).

## 4. Out of scope for v1 (DECIDED — from handoff)

- No scripting engine, no text expansion, no floaters, no window-automation API, no plugin system (extensible ≠ pluggable-by-users).
- Elevated/admin windows on Windows and secure input fields on macOS are accepted ceilings — document, don't chase.

## 5. Open decisions index

| # | Decision | Status |
|---|----------|--------|
| D1 | App shell / language ([ADR-0001](adr/0001-app-shell-and-language.md)) | **DECIDED — .NET 10 + SharpHook + Avalonia** (2026-10-05) |
| D2 | UI design (layout, look, training flow) | OPEN — Joel wants to think this through; required capabilities in F7; SP.net's structure and the resulting component vocabulary are in [reference/strokesplus-net-config.md §8](reference/strokesplus-net-config.md) |
| D3 | Per-app gesture overrides in v1? | **DECIDED — yes**, incl. ignore-list (2026-07-24, see F5) |
| D4 | Gesture timeout + modifier/rocker behaviors | LEANING — SP.net thresholds as defaults, wheel-while-holding in v1, rocker/chords deferred (see F1) |
| D5 | Config format & location | LEANING JSON |
| D6 | macOS "maximize" semantics (zoom/fullscreen/tile) | OPEN — deferrable until macOS port |
| D7 | Distribution ambitions (personal vs public; signing/notarization) | OPEN |
| D8 | Start-on-login, onboarding | OPEN |
| D9 | Import from StrokesPlus.net JSON in v1 | LEANING yes (see F8) |
| D11 | Pre-plan checklist (§6) | OPEN — walk through with Joel, then write the plan |
| D10 | Initial implementation shape (project layout, build order, first milestone) | **DECIDED** — layout in [ADR-0002](adr/0002-code-and-repo-structure.md), build order in [plans/0001-first-version.md](plans/0001-first-version.md) |

## 6. Pre-plan checklist (2026-10-05)

Items to walk through before the first implementation plan is written. Each row has the agent's recommendation; Joel decides. Mark the Status column as rows close (DECIDED / DEFERRED), then fold the decision into the section it belongs to and delete the row.

### A. Decisions

| # | Item | Recommendation | Status |
|---|---|---|---|
| A1 | Accept [ADR-0001](adr/0001-app-shell-and-language.md) (.NET + SharpHook + Avalonia), bumped to .NET 10 LTS | Accept | DECIDED |
| A2 | Accept [ADR-0002](adr/0002-code-and-repo-structure.md) (projects, ports, step slices, declarative UI, wireframe theme, F1 inspector + F1-click copies region identity) | Accept | DECIDED |
| A3 | Settings model: explicit Apply/OK/Cancel (SP.net: edits do nothing until Apply) or live save (every change takes effect immediately, undo to revert) | **Live save with undo** (Joel: "let's try that for now"); the engine swaps to the new config atomically. Revisit after M2 if it feels wrong | DECIDED (trial) |
| A4 | Name of the command-line step type ("Command" already means the gesture mapping) | Call it **Run** | DECIDED |
| A5 | Definition of "first version done" | **M1 (Joel):** app window (wireframe), tray icon, and basic gesture recognition testable end to end: chosen button captured, trail drawn, stroke matched against trained/imported gestures, result shown in the recognition log. Commands and steps are M2 | DECIDED |
| A6 | Gestures drawn over Augram's own windows | Augram's windows behave like any other app: gestures over them work (Joel minimises SP.net with its own gesture), and Augram is just another app group if overrides are wanted. The only exception is the training popup, where the stroke goes to the draw area instead of the recognizer | DECIDED |
| A7 | Same gesture bound twice in one group; two library gestures that match each other above threshold | Prevent the first at save; show a "likely to be confused" diagnostic on the Gestures page for the second. Plus inactive gestures greyed (F3) | DECIDED |
| A8 | Keystrokes lost right after window activation (Joel's config has 24 manual delay steps for this) | **Requirement (Joel):** release → command fires instantly, e.g. a quick diagonal minimises the window the stroke started over with no noticeable delay, and it must be reliable. Implementation: no delay before firing; window operations never wait; a settle delay applies only between activating a target window that did *not* have focus and the first injected keystroke (default ~30 ms, configurable, 0 allowed), because keys injected before focus has moved are dropped. Reliability is the point of the delay, not latency | DECIDED |
| A9 | Delete confirmation | Confirm for app groups and commands; none for steps; undo (Ctrl+Z) for all three | DECIDED |
| A10 | Start at login, single instance, tray clicks | Start-at-login is a setting (Options page + tray menu); single instance (relaunch opens the window); tray single-click toggles on/off, double-click opens | DECIDED |
| A11 | Stroke button while SP.net still runs (two suppressing hooks on Middle will fight) | Develop on Right; the button is user-selectable in settings regardless | DECIDED |
| A12 | Cancelling a gesture in progress | Another button cancels; hold-still past a user-settable duration cancels (folded into F1) | DECIDED |
| A13 | Hold still after press: cancel (SP.net) or turn into a real drag for the app underneath (GestureSign) | Cancel. The drag variant stays a possible later option | DECIDED |
| A14 | App command fall-through to global for the same gesture (StrokeIt `SP_NEXT_APP`) | No for v1; override-only matches Joel's config | DEFER |
| A15 | Recognition log panel: last stroke, top 3 matches with scores, matched app, fired command or why nothing fired | v1 (part of M1); it is the main setup tool | DECIDED |
| A16 | "Test" button on a command that runs its steps now | v1; trivial once steps execute | DECIDED |
| A17 | Config file location | Default per-platform app-data folder; an option to point at another folder (Joel syncs backups to Drive) | DECIDED |
| A18 | Win-key hotkeys on macOS (no equivalent for Win+Left snapping) | Treat Win-combos as unconvertible; overrides handle them. Decide when the Hotkey step is built | DEFER |
| A19 | Stuck-button safety (SP.net's most recurring bug class, see reference §9) | Invariant: every consumed down is paired with a consumed up for the same button; stale modifier state causes a relay, never a swallow; only the engine worker injects input, never the hook thread | DECIDED (Joel: trusts the recommendation) |
| A20 | Activation rule refinement (SP.net learned this the hard way) | Activate the root owner under the gesture start **only if it differs from the foreground root and is not the Desktop**; otherwise leave focus alone (keeps popup/find windows working) | DECIDED (as above) |
| A21 | Full-screen detection for games | Window rect equals its monitor rect, desktop excluded; expose "ignore if full screen" per group and globally like SP.net | DECIDED (as above) |
| A22 | Scoring mode for multi-pattern gestures | Classic source averages, Rob says best-wins; ship both, default Average, verify on Joel's three multi-pattern gestures. Stroke's "blend 10% of each new drawing into one template" is a possible third mode later | DECIDED (as above) |

### B. Risks to spike before the shell (spike 2, a few days, throwaway code)

| # | Risk | What to verify | Status |
|---|---|---|---|
| B1 | Activating another app's window from a background process is restricted on Windows | `AttachThreadInput` + `SetForegroundWindow` (StrokeIt and GestureSign both use it) works from the hook worker for Chrome, Explorer, a borderless game | OPEN — probe built; Joel to run `activate` ([learnings 0001](learnings/0001-spike2.md)) |
| B2 | Avalonia transparent click-through overlay | Pre-created window appears within one frame of stroke start, over borderless-fullscreen games, across monitors with different DPI, and never takes focus. Fallback: native layered window (GestureSign technique) | PARTIAL — 3–11 ms to first segment, never takes focus, on one monitor; game + multi-DPI pending; Show() resets ex-style (fix known) |
| B3 | Hook resilience over days of uptime | Health check + reinstall after sleep, lock, session switch, and after Windows drops a slow hook | PARTIAL — watchdog + reinstall built and runs; sleep/lock hours pending |
| B4 | SharpHook keyboard suppression (incl. Win key) for hotkey capture; media key simulation; text entry into a game console | Each works via SharpHook on Windows, or we know which needs a Platform call | PARTIAL — media keys and text entry verified; Win+L suppression and game-console typing pending |

### C. To be written in the plan

| # | Item |
|---|---|
| C1 | Import mapping table: SP.net step methods → Augram step types (SendHotKey, SendVKey, SendKeys, Delay, Run, window ops, MouseClick, SendAltDown/Up, scripts → disabled with note). Note: SP.net `SendKeys` strings use .NET SendKeys syntax (`^` Ctrl, `+` Shift, `%` Alt, `{ENTER}`, `{F5}`…; classic S+ also had `@` Win, `{DELAY n}`, `{VKEY n}`); the importer must parse those into Hotkey / Type-text / Delay steps rather than storing the raw string |
| C2 | Build order and milestones (engine first, then wireframe shell, then overlay polish and per-app details) |
| C3 | Starter gesture set for first run (direction flicks and the common letters) |
