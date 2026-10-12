# Augram

**Augur** (the Roman diviner who read intent from flight trajectories) + **gram** (Greek *grámma*, a drawn character) — a cross-platform mouse gesture app: draw a sign, have it divined, fire an action.

Sibling of [Chimera] (Spine skeleton tool) and [Eyeris] (image viewer/annotation).

## What it does (v1 scope)

- Any mouse button selectable as the gesture-drawing button
- Draw gesture → recognize → fire action
- Actions: keystroke shortcuts and sequences, system volume/media keys
- Visible gesture trail drawn over other applications
- Windows + macOS

Inspired by [StrokesPlus.net](https://www.strokesplus.net/) (Windows-only, abandoned). Deliberately simplified: no scripting engine, no text expansion, no plugin system.

## Status

**Building.** The current phase and where to start are in [CLAUDE.md](CLAUDE.md) and [docs/session-handoff.md](docs/session-handoff.md); requirements in [docs/requirements.md](docs/requirements.md); documentation conventions in [docs/README.md](docs/README.md). The code is under `src/` (one README per project and folder; the layout is [ADR-0002](docs/adr/0002-code-and-repo-structure.md)). See [docs/handoff.md](docs/handoff.md) for the original research handoff (prior art, recognition algorithm, platform notes, build order).

## Credits

Gesture recognition approach descends from **StrokesPlus** by Rob Larkin (MIT), itself crediting **HighSign** by Dylan Vester. See [docs/handoff.md](docs/handoff.md) §3.
