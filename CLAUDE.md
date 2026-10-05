# Augram

Cross-platform (Windows + macOS) mouse gesture utility: hold a chosen mouse button, draw a stroke, fire an action. Replaces StrokesPlus.net (abandoned). Tray-resident, tiny footprint, zero perceptible latency.

## Current phase: BUILDING — plan 0001, milestone M0

Requirements are in [docs/requirements.md](docs/requirements.md); both ADRs are ACCEPTED; the build order is [docs/plans/0001-first-version.md](docs/plans/0001-first-version.md). Feature code follows that plan and [ADR-0002](docs/adr/0002-code-and-repo-structure.md)'s structure rules, nothing else. `src/Augram.Spike` is the first throwaway spike (capture/suppress/replay via SharpHook): learn from it, do not build on it. Session-only context (how Joel works, easy-to-get-wrong items, what is in flight) is in [docs/session-handoff.md](docs/session-handoff.md).

## Ground rules for agents

- **Don't reimplement — extend.** Check [docs/requirements.md](docs/requirements.md), the read-first table below, and `docs/reference/` before writing. If a pattern exists, add to it. If it's missing, add the pattern *and its doc* in the same change.
- **Closed decisions live in [docs/adr/](docs/adr/) as ADRs.** Don't relitigate an ACCEPTED ADR unless Joel reopens it. Current: [ADR-0001 app shell/language](docs/adr/0001-app-shell-and-language.md) (ACCEPTED 2026-10-05: .NET 10 + SharpHook + Avalonia) and [ADR-0002 code/repo structure](docs/adr/0002-code-and-repo-structure.md) (ACCEPTED 2026-10-05).
- **Docs taxonomy** (what goes where, lifecycle): [docs/README.md](docs/README.md).
- Keep this file lean — link out, don't inline. When a subsystem gains invariants, it gets a doc and a read-first row, not a paragraph here.
- Platform: Windows 11 dev machine, PowerShell. No Mac available for testing yet — macOS code paths are design-for, not test-on.
- **Never launch Augram.App, a spike, or anything that installs a global hook or a full-screen overlay on this machine from a subagent or without telling Joel first.** A bug there takes his mouse or keyboard away while he works (it happened: a click-opaque overlay ate every left click on 2026-10-06). Only the lead session runs the app, after saying so, and kills it afterwards. Tests use fakes, never real hooks.
- **CI runs on every push** (`.github/workflows/ci.yml`: format, build, test on a clean Windows runner). After pushing, check it: `node scripts/ci-status.mjs` prints recent runs and the failing test names (no token needed). A red main is yours to fix before moving on.

## Architecture invariants (already established)

These come from research + the spike; they are quality-bar-critical, not preferences:

1. **Hook handlers do near-zero work** — append a point, set suppress, return. Anything heavier goes through a channel to a worker. (macOS will kill slow event taps; Windows hooks lag the whole pointer.)
2. **SharpHook: only `SimpleGlobalHook` (synchronous handlers) supports `SuppressEvent`;** `IsEventSimulated` distinguishes our replayed clicks from real input. Verified 2026-07-24 with SharpHook 7.1.3.
3. **Recognition is rotation-SENSITIVE by design** (up-flick ≠ down-flick). Never "fix" this by normalizing rotation. See [docs/handoff.md §3](docs/handoff.md).
4. **Recognition runs on button-up only**, never during capture.
5. Gesture templates are stored as **raw point lists**, resampled at match time — precision must stay adjustable.
6. **The trail overlay is never visible while idle and never shown unless its click-through style has been verified** (Windows: `WS_EX_TRANSPARENT` read back after every `Show()`). A watchdog hides it after 3 s without a stroke. An unverified full-screen window swallows every left click on the machine (2026-10-06, twice). `--no-engine` / `AUGRAM_NO_ENGINE=1` keep the app from installing hooks or overlays at all.

## Read-first table

| Editing… | Read first |
|---|---|
| Anything | [docs/requirements.md](docs/requirements.md), then [docs/plans/0001-first-version.md](docs/plans/0001-first-version.md) for what is in scope now |
| Project layout, where a new file goes, UI components, state | [docs/adr/0002-code-and-repo-structure.md](docs/adr/0002-code-and-repo-structure.md) |
| Recognition math | [docs/reference/strokesplus-classic-source.md](docs/reference/strokesplus-classic-source.md) §1 (exact algorithm incl. the divide-by-P quirk) + [docs/handoff.md](docs/handoff.md) §3 (MIT attribution requirements) |
| Hook / capture / replay | [docs/reference/strokesplus-classic-source.md](docs/reference/strokesplus-classic-source.md) §2 (the state machine table) + [docs/handoff.md](docs/handoff.md) §5–7 + `src/Augram.Spike/Program.cs` (annotated spike) |
| Actions, app matching, capture thresholds, SP.net import | [docs/reference/strokesplus-net-config.md](docs/reference/strokesplus-net-config.md) (Joel's real usage = the spec) + [docs/reference/gesturesign-notes.md](docs/reference/gesturesign-notes.md) (GPL, read-only reference) + [docs/reference/strokeit-notes.md](docs/reference/strokeit-notes.md) (cancel-by-click, activation trick) + [docs/reference/stroke-notes.md](docs/reference/stroke-notes.md) (GPL; minimal engine, overlay hidden when idle) + [docs/reference/bettermouse-notes.md](docs/reference/bettermouse-notes.md) (MIT; hold-modifier-across-wheel pattern, anti-patterns) |
