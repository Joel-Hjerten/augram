# Agent handoff — Augram, after M1 (written 2026-10-06 01:40)

You are picking up a project that reached its first runnable milestone a few hours ago. Everything decided is in the docs; this file tells you the state, the order to read things, what to do next, and the rules that came from mistakes. Joel works on his **Windows 11 PC** (not the Mac) until the app runs well here.

## 1. Read in this order (30 minutes)

1. [CLAUDE.md](../CLAUDE.md): ground rules, the six architecture invariants, read-first table. Invariant 6 and the "never launch the app from a subagent" rule exist because of two incidents last night (section 6 below).
2. [requirements.md](requirements.md): everything DECIDED, the F5a vocabulary (**app group › command › step**; the command-line step type is called **Run**), and the section 6 checklist with Joel's answers.
3. [adr/0002-code-and-repo-structure.md](adr/0002-code-and-repo-structure.md): where code goes and why. Dependencies point inward to Core; step types are one folder each; state lives in stores; screens are declared; components are lookless; F1 inspector.
4. [plans/0001-first-version.md](plans/0001-first-version.md): milestones. M0 and M1 done; **M2 is next** (section 4 below).
5. [learnings/0001-spike2.md](learnings/0001-spike2.md): measured facts about the hook, overlay and recognizer on Joel's machine, and the overlay flash story.
6. The README.md in every project folder under `src/` and `tests/` (purpose, allowed references, threading). `src/Augram.Engine/README.md` has the thread table.
7. Reference docs under [reference/](reference/) only when touching that area: `strokesplus-net-config.md` (Joel's real usage is the spec), `strokesplus-classic-source.md` (recognizer and capture semantics), the GestureSign/StrokeIt/Stroke/BetterMouse notes.

## 2. State of the code (main = tag `m1` + a few doc commits)

Solution `Augram.slnx`, .NET 10, Avalonia 11.3.22, SharpHook 7.1.3. `dotnet build -c Release`, `dotnet test -c Release`, `dotnet format --verify-no-changes` are all green; CI (`.github/workflows/ci.yml`, windows-latest) runs on every push and `node scripts/ci-status.mjs` prints recent runs with failing test names.

| Project | What is there | Tests |
|---|---|---|
| Augram.Core | Capture state machine; recognizer ported from StrokesPlus (Legacy/Corrected scoring, Average/Best aggregation, ConfusionCheck); Gesture model + GestureLibrary with undo; Config (JSON document, FileConfigStore with backups, SettingsStore, ConfigSession live save); Diagnostics (IEventLog port, RecognitionLog, HealthRegistry); Abstractions (all ports) | 261 |
| Augram.Engine | ChannelEventLog + rolling file / in-memory sinks; SharpHook input source and simulator; HookHealthMonitor; EngineHost / InputGate / EngineWorker (hook thread decides suppression from a shadow, worker runs the machine, ticks, recognition) | 76 |
| Augram.Platform.Windows | Win32 window identity (class chain, UWP, desktop, full-screen), ForegroundActivator (A20 rule), cursor probe, system events, overlay style apply/verify/place, Run-key startup | 47 |
| Augram.Import.StrokesPlus | Gestures-only reader for SP.net JSON, warnings, stats, GestureMerge | (in Core.Tests) |
| Augram.App | Dark wireframe theme; declarative screens (SectionForm, ItemList, field kinds); Shell with tabs Gestures / Commands (placeholder) / Ignored (placeholder) / Options / Diagnostics (Health, Log, Recognition); tray; single instance; F1 inspector; Debug gallery; Gestures tab with glyph grid, training popup, import dialog; Options bound to SettingsStore with undo and Detect-button; TrailOverlayWindow (parked at 1×1 between strokes); EngineModule and GesturesModule composed in CompositionRoot | 95 |
| Augram.Spike, Augram.Spike2 | Throwaway probes. **Delete both at the start of M2** (add nothing to them). | |

Runtime facts from Joel's machine: hook installs in ~50 ms; hook handler worst case 9–18 µs after JIT; recognition 0.04–0.4 ms over 20 gestures; trail first frame 8–24 ms; config at `%APPDATA%\Augram\augram.json` (20 starter gestures, stroke button **Right**), logs at `%APPDATA%\Augram\logs\augram-yyyyMMdd.log`.

## 3. Joel's pending acceptance pass (do this first, with him)

Only the lead session runs the app, and only after telling Joel. Start it with `src\Augram.App\bin\Release\net10.0\Augram.App.exe` (or `dotnet run --project src/Augram.App -c Release`), kill it afterwards, read the log. `--no-engine` or `AUGRAM_NO_ENGINE=1` runs the UI without hooks or overlay.

1. Gestures tab › Import: pick `%APPDATA%\StrokesPlus.net\StrokesPlus.net.json` (90 gestures). Expect a few name clashes with starters; KeepMine/TakeTheirs/KeepBoth per row.
2. Draw his daily gestures (reference §4 lists the top ones) and read Diagnostics › Recognition. Misrecognitions here are the first M2 input.
3. Add new gesture (training popup: canvas, Cancel, Accept, redraw replaces).
4. Options: change a value, Undo; Detect button for the stroke button.
5. Hold F1 and click a region: the clipboard gets the declaration path and source location.
6. Note everything Joel says is off; that list opens M2.

## 4. M2 scope (plan 0001, section "M2")

Commands and steps: Core/Mapping (AppGroup, Command, CommandResolver with override-to-nothing and global fallback, MappingStore with undo); step types as vertical slices (Hotkey, TypeText, Run, MediaKey, Delay, WindowOp); Engine executor (serialized, activation rule A20, settle delay A8 only when focus moved); Commands tab (groups › commands › inline step list with drag reorder, step type picker by category, gesture picker popup, context menu + keymap, confirmations for group/command delete, Test button); app identification page and Ignored tab; hotkey capture field with system-wide suppression and watchdog (F5); full SP.net import per the C1 table; Augram JSON export/import. Keep each step type one folder (N3) and run `dotnet test` after every slice.

## 5. How Joel works

- Short numbered answers; wants one-liners back when he asks "what do you need from me". Explain a choice in a few sentences when he says he does not know enough, then let him decide.
- Defers to the agent on engine internals ("I trust you on this"), owns anything UI/behaviour. He thinks in StrokesPlus.net terms; map new concepts to the SP.net equivalent.
- He authorised committing and pushing to main as work progresses, and lead-dev coordination of subagents. Commit at coherent checkpoints with the Claude co-author line; push; check CI.
- Reference material lives under `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\` (StrokesPlus classic source, GestureSign, StrokeIt SDK, Stroke, BetterMouse, SP.net install and settings).

## 6. Rules that came from incidents (do not relearn these)

- **Never launch Augram.App, a spike, or anything with a global hook or overlay from a subagent, or without telling Joel first.** Twice last night an agent's test run showed a full-screen overlay that was not click-through and every left click on Joel's machine died until the process was killed. Subagents build, test (fakes only) and format; the lead runs the app.
- **The overlay is parked at 1×1 px between strokes, never hidden, and never shown unless `WS_EX_TRANSPARENT` reads back after `Show()`.** Hiding it made the previous stroke flash on the next one (retained surface); a deferred hide did not fix it; parking did. A watchdog hides a full-size idle window after 3 s.
- **A consumed press always gets a consumed release** (A19). The hook-side `SuppressionShadow` and the state machine are proven equal by a property test; keep it that way.
- **Logging never blocks the hook thread.** `IEventLog` enqueues; a worker writes.
- **Recognizer scoring stays `Legacy` by default** (divide by precision, as the original does). `Corrected` changes what the 75% threshold means; a 45°-off stroke scores 75.25 Legacy vs 75.0 Corrected.
- **Vocabulary is fixed:** app group › command › step; the command-line step type is `Run`; never "action".
- **CI is red → fix before moving on.** `node scripts/ci-status.mjs` names the failing tests. The only runner-specific traps so far: desktop-dependent tests (skip when `GITHUB_ACTIONS` is set) and timing assertions on a 2-core runner.

## 7. Working with subagents (what worked)

Give each agent disjoint folder ownership, the exact docs to read, the verification commands, and "do not commit". Have them report file lists with line counts and deviations. Verify a subagent's work yourself (`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`), commit by path so parallel agents' in-progress files stay out, and check CI after the push. When several agents run, an isolated `git worktree` at HEAD is the safe way to verify a commit (`dotnet build-server shutdown` before removing it, or the folder stays locked). Do not `git stash` while agents are writing.

## 8. Scratch that is gone

The session scratchpad held clones of the SP.net forum archive (`roblarky/roblarky.github.io`), `doozan/si_plugins`, `xkonglong/strokesplus.net` and node scripts that parsed Joel's live config into the usage profile. All findings are in the reference docs; re-clone from the URLs in `reference/strokesplus-net-config.md §1` if more mining is needed.
