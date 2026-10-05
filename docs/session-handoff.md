# Session handoff (2026-10-05): what is not in the other docs

For an agent (or a compacted context) picking up where this planning session left off. Everything decided is in [requirements.md](requirements.md), the two ADRs, and [plans/0001-first-version.md](plans/0001-first-version.md). This note only holds context that lives nowhere else.

## State at handoff (updated 2026-10-06 01:30)

- Phase: BUILDING, plan 0001. **M1 reached and tagged `m1`** (commit 187818f): tray, dark wireframe window with Gestures / Commands (placeholder) / Ignored (placeholder) / Options / Diagnostics tabs, training popup, SP.net gesture import, settings with live save and undo, SharpHook engine with hook health monitor, recognizer, parked trail overlay. 448+ tests green; check CI with `node scripts/ci-status.mjs`.
- Verified on Joel's machine: strokes recognised from the starter set, no left-button lockout, no stroke flash. **Pending Joel's acceptance pass:** import his SP.net gestures via the Gestures tab, train one gesture, exercise Options (undo, Detect button) and the F1 inspector. Then M2 (commands, steps, per-app scoping, hotkey capture, full import) per plan 0001.
- Two incidents on 2026-10-06 shaped hard rules: agents must never launch the app or anything with a hook or overlay (CLAUDE.md ground rule); the overlay is parked at 1×1 between strokes, never hidden, and never shown unverified (invariant 6). `--no-engine` / `AUGRAM_NO_ENGINE=1` is the safe way to run the UI. Only the lead runs the app, after telling Joel, and kills it afterwards.
- Toolchain on Joel's machine: .NET SDK 10.0.201 (also 8.0 and 9.0), node 25, no Python, `gh` not installed, Git Bash available. Repo remote: github.com/Joel-Hjerten/augram.
- Config on Joel's machine: `%APPDATA%\Augram\augram.json` (20 starter gestures, stroke button Right); logs in `%APPDATA%\Augram\logs`.
- `src/Augram.Spike` and `src/Augram.Spike2` are still in the solution; delete them at the start of M2 (their findings are in `docs/learnings/0001-spike2.md`).

## How Joel works (observed)

- Answers in short numbered lines; wants one-liners back when he asks "what do you need from me". Explain a choice in a few sentences when he says he does not know enough to choose, then let him decide.
- Decides fast once the trade-off is clear; defers to the agent on engine internals ("I trust you on this") but owns anything UI/behaviour. Record such items as "DECIDED (trusts the recommendation)".
- Thinks in terms of StrokesPlus.net, which he has used daily for years. When unsure what a term means, map it to the SP.net equivalent (e.g. SP.net's Apply button vs live save).
- Keeps reference material under `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\` and adds to it mid-conversation; paths in the reference docs are current as of 2026-10-05.
- Commit only when he says so, or when he has explicitly delegated ("act as lead dev"); he did the latter for M0.

## Things easy to get wrong

- **Vocabulary is fixed:** app group › command › step. "Command" is the gesture mapping; the command-line step type is called **Run**. Do not say "action".
- **Stroke button for development is Right.** SP.net owns Middle on Joel's machine until he retires it (M3). Two suppressing hooks on the same button fight.
- **Training flow is deliberately minimal:** popup canvas, Cancel, Accept, redraw replaces. No averaging, no multi-sample UI. The model still stores multiple samples because imported SP.net gestures have them.
- **Recognizer port keeps the divide-by-precision quirk** as the default scoring mode; see [reference/strokesplus-classic-source.md §1](reference/strokesplus-classic-source.md). Changing it silently would make the imported threshold mean something else.
- **Wheel-while-holding follows SP.net semantics** (fires any time during the hold, every tick, up fires nothing), not GestureSign's.
- **Settings are live-save with undo** (trial); no Apply button. Group and command deletes confirm; step deletes do not; undo covers all.
- Handoff.md (no date) is the original July research document and is frozen; this file is the session handoff. The old docs/decisions folder is now docs/adr.

## Scratchpad artefacts that will disappear

The session scratchpad (`%TEMP%\claude\c---myProjects-augram\<session>\scratchpad\`) held clones of `roblarky/roblarky.github.io` (the SP.net forum/changelog archive, with `search.js`/`topic.js` helpers written by a subagent), `doozan/si_plugins`, `xkonglong/strokesplus.net`, and node scripts (`sp.js`, `sp2.js`, `sp3.js`, `sp4.js`) that parse Joel's live `StrokesPlus.net.json` into the usage profile in reference §4. All findings are already in the reference docs; re-clone from the URLs in [reference/strokesplus-net-config.md §1](reference/strokesplus-net-config.md) if more mining is needed. The parse scripts are trivial to rewrite: load the JSON (strip BOM), walk `Gestures`, `GlobalApplication.Actions`, `Applications[].Actions`, `IgnoredApplications`.

## Immediate next steps

1. Collect the two subagent reports, review, commit M0 scaffold and Spike2 separately.
2. Ask Joel to run the four Spike2 modes; write `docs/learnings/0001-spike2.md`; decide B2's overlay implementation (Avalonia vs native layered window) and update plan 0001 and ADR-0002 §2 if the fallback is taken.
3. Delete `src/Augram.Spike` and `src/Augram.Spike2` when M1 starts, after their findings are in `docs/learnings/`.
4. Start M1 step 1 (recognizer port + fixtures). Joel's 90 gestures: ask him before copying his config into the repo as a test fixture.
