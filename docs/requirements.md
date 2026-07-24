# Augram — v1 requirements (living document)

**Status: DRAFT — being worked out with Joel. Nothing here is frozen unless marked DECIDED.**

Each item is tagged:
- **DECIDED** — settled; changing it needs Joel's say-so
- **LEANING** — a preferred direction exists but is open to challenge
- **OPEN** — genuinely undecided; input wanted

Background research (prior art, the recognition algorithm, platform constraints) lives in [handoff.md](handoff.md). Architecture decisions get their own ADRs in [decisions/](decisions/).

---

## 1. Product summary

A simplified, cross-platform (Windows + macOS) mouse gesture utility inspired by StrokesPlus.net: hold a chosen mouse button, draw a stroke, Augram recognizes it and fires an action (keystroke sequences, media/volume keys, window management). Tray-resident, tiny footprint, zero perceptible latency.

**Quality bars (DECIDED — from Joel, non-negotiable):**
1. Recognition quality on par with StrokesPlus.net
2. Zero perceptible latency while drawing
3. Trail renders on top of essentially all applications

## 2. Functional requirements

### F1. Gesture capture — DECIDED (shape), OPEN (details)
- Any mouse button assignable as the stroke button.
- **Detect-to-assign (DECIDED):** assignment works by a "press the button you want" capture flow, not only a fixed dropdown. This handles buttons that vendor software (e.g. Logi Options+ on Joel's Logitech Anywhere 3S) doesn't expose by name. A standard dropdown of common buttons can exist alongside, but detect is the primary flow.
  - ⚠️ Known ceiling to document in-app: if vendor software consumes/remaps a button at driver level, the OS hook never sees the original button — Augram can only bind what actually arrives at the OS layer. (Detect flow makes this self-evident to the user: if pressing it shows nothing, Augram can't use it.)
- Suppress-then-replay loop per [handoff.md §5](handoff.md): motionless press → replay original click (context menus must still work); movement → capture stroke.
- OPEN: timeout behavior (StrokesPlus cancels a gesture held too long — adopt? value?)
- OPEN: modifier keys / rocker gestures / scroll-during-gesture — v1 or later? (StrokesPlus has them; they add real capture complexity.)

### F2. Recognition — DECIDED
- Port the MIT-licensed StrokesPlus angle-sequence recognizer verbatim ([handoff.md §3](handoff.md)): resample to N segments, compare per-segment angles, scale/position-invariant, deliberately rotation-sensitive.
- Multiple training samples per gesture, scores averaged; threshold below which nothing fires.
- Templates stored as raw point lists (resampled at match time) so precision stays adjustable.

### F3. Gesture training / learning — DECIDED (feature), OPEN (UX)
- User can record a new gesture by drawing it (several samples), name it, and assign an action.
- OPEN: the exact flow — draw-first-then-name vs name-first-then-draw; how re-training/adding samples to an existing gesture works; live feedback showing match score while practicing.

### F4. Gesture icons — DECIDED (feature)
- Every gesture must be displayable as an icon in the UI, generated from its template points (normalized polyline + direction arrowhead). No hand-drawn icon assets per gesture.
- Ships with a starter set of common gestures (up/down/left/right, L-shapes, Z, circle…) so the list isn't empty on first run.

### F5. Actions — DECIDED (initial set)
- Keystroke shortcut and *sequences* of shortcuts (close tab, new tab, min/max window, arbitrary chains).
- System volume up/down and media keys.
- OPEN: does v1 need per-application gesture→action overrides (StrokesPlus's killer feature for power users), or is one global mapping enough to start? Architecture should allow adding it either way.
- OPEN: other action types worth having day one? (launch app, open URL, paste text?)

### F6. Trail overlay — DECIDED (feature)
- Visible stroke trail over all apps while drawing; transparent, click-through, per-platform native window; points batched per frame (handoff §7).
- OPEN: trail styling (color/width/fade) configurable in v1 or hardcoded-nice first?

### F7. Tray presence + settings app — DECIDED
- Tray icon (menu bar extra on macOS): open settings, enable/disable, quit.
- Settings window is where gestures/actions/training/options live. Closed = app keeps running in tray.
- OPEN: start-on-login default? First-run onboarding (macOS permissions especially)?

### F8. Configuration — LEANING
- Human-readable config + gesture library on disk (LEANING: JSON; raw template points per handoff §3), safe to hand-edit and diff, versioned schema from day one.
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
| D1 | App shell / language ([ADR-0001](decisions/0001-app-shell-and-language.md)) | LEANING .NET+Avalonia — awaiting Joel |
| D2 | UI design (layout, look, training flow) | OPEN — Joel wants to think this through |
| D3 | Per-app gesture overrides in v1? | OPEN |
| D4 | Gesture timeout + modifier/rocker behaviors | OPEN |
| D5 | Config format & location | LEANING JSON |
| D6 | macOS "maximize" semantics (zoom/fullscreen/tile) | OPEN — deferrable until macOS port |
| D7 | Distribution ambitions (personal vs public; signing/notarization) | OPEN |
| D8 | Start-on-login, onboarding | OPEN |
