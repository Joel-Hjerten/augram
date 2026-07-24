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

**Planning phase.** Requirements are being worked out in [docs/requirements.md](docs/requirements.md); documentation conventions in [docs/README.md](docs/README.md). See [docs/handoff.md](docs/handoff.md) for the original research handoff (prior art, recognition algorithm, platform notes, build order).

- `src/Augram.Spike` — risk spike: capture a chosen mouse button globally, suppress it, collect stroke points, and replay the original click when the stroke isn't a gesture. Also records strokes to JSON for recognizer development.

## Credits

Gesture recognition approach descends from **StrokesPlus** by Rob Larkin (MIT), itself crediting **HighSign** by Dylan Vester. See [docs/handoff.md](docs/handoff.md) §3.
