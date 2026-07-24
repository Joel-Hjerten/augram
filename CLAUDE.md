# Augram

Cross-platform (Windows + macOS) mouse gesture utility: hold a chosen mouse button, draw a stroke, fire an action. Replaces StrokesPlus.net (abandoned). Tray-resident, tiny footprint, zero perceptible latency.

## Current phase: PLANNING

Requirements are being worked out in [docs/requirements.md](docs/requirements.md) — read it before proposing or writing any code. **Do not write feature code yet.** The only code that exists is `src/Augram.Spike`, a throwaway risk spike (capture/suppress/replay via SharpHook) — learn from it, don't build on it without a plan saying so.

## Ground rules for agents

- **Don't reimplement — extend.** Check [docs/requirements.md](docs/requirements.md), the read-first table below, and `docs/reference/` before writing. If a pattern exists, add to it. If it's missing, add the pattern *and its doc* in the same change.
- **Closed decisions live in [docs/decisions/](docs/decisions/) as ADRs.** Don't relitigate an ACCEPTED ADR unless Joel reopens it. Current: [ADR-0001 app shell/language](docs/decisions/0001-app-shell-and-language.md) — status PROPOSED, awaiting Joel.
- **Docs taxonomy** (what goes where, lifecycle): [docs/README.md](docs/README.md).
- Keep this file lean — link out, don't inline. When a subsystem gains invariants, it gets a doc and a read-first row, not a paragraph here.
- Platform: Windows 11 dev machine, PowerShell. No Mac available for testing yet — macOS code paths are design-for, not test-on.

## Architecture invariants (already established)

These come from research + the spike; they are quality-bar-critical, not preferences:

1. **Hook handlers do near-zero work** — append a point, set suppress, return. Anything heavier goes through a channel to a worker. (macOS will kill slow event taps; Windows hooks lag the whole pointer.)
2. **SharpHook: only `SimpleGlobalHook` (synchronous handlers) supports `SuppressEvent`;** `IsEventSimulated` distinguishes our replayed clicks from real input. Verified 2026-07-24 with SharpHook 7.1.3.
3. **Recognition is rotation-SENSITIVE by design** (up-flick ≠ down-flick). Never "fix" this by normalizing rotation. See [docs/handoff.md §3](docs/handoff.md).
4. **Recognition runs on button-up only**, never during capture.
5. Gesture templates are stored as **raw point lists**, resampled at match time — precision must stay adjustable.

## Read-first table

| Editing… | Read first |
|---|---|
| Anything (this phase) | [docs/requirements.md](docs/requirements.md) |
| Recognition math | [docs/handoff.md](docs/handoff.md) §3 (algorithm + MIT attribution requirements) |
| Hook / capture / replay | [docs/handoff.md](docs/handoff.md) §5–7 + `src/Augram.Spike/Program.cs` (annotated spike) |
