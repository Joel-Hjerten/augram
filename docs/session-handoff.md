# Agent handoff — Augram, M1 accepted, M2 under way, macOS port started (updated 2026-10-07 17:00)

You are picking up a project whose first runnable milestone is built and accepted by Joel. Everything decided is in the docs; this file tells you the state, the order to read things, what to do next, and the rules that came from mistakes. Joel works on his **Windows 11 PC** and, since 2026-10-07, on his **Mac** too (sections 8 and 8a).

## 1. Read in this order (30 minutes)

1. [CLAUDE.md](../CLAUDE.md): ground rules, the six architecture invariants, read-first table. Invariant 6 and the "never launch the app from a subagent" rule exist because of two incidents on 2026-10-06 (section 6 below).
2. [requirements.md](requirements.md): everything DECIDED, the F5a vocabulary (**app group › command › step**; the command-line step type is called **Run**), and the section 6 checklist with Joel's answers. The 2026-10-07 decisions are marked with the date inline (F3 redraw, active-per-command, A3 no undo on Options, A7 duplicates).
3. [adr/0002-code-and-repo-structure.md](adr/0002-code-and-repo-structure.md): where code goes and why. Dependencies point inward to Core; step types are one folder each; state lives in stores; screens are declared; components are lookless; F1 inspector.
4. [plans/0001-first-version.md](plans/0001-first-version.md): milestones. M0 and M1 done and accepted; **M2 is next** (section 4 below).
5. [learnings/0001-spike2.md](learnings/0001-spike2.md): measured facts about the hook, overlay and recognizer on Joel's machine, and the overlay flash story.
6. The README.md in every project folder under `src/` and `tests/` (purpose, allowed references, threading). `src/Augram.Engine/README.md` has the thread table.
7. Reference docs under [reference/](reference/) only when touching that area: `strokesplus-net-config.md` (Joel's real usage is the spec), `strokesplus-classic-source.md` (recognizer and capture semantics), the GestureSign/StrokeIt/Stroke/BetterMouse notes.

## 2. State of the code (main, after tag `m1` plus the 2026-10-07 acceptance fixes)

Solution `Augram.slnx`, .NET 10, Avalonia 11.3.22, SharpHook 7.1.3. `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` are all green on Windows and macOS (1078 tests on 2026-10-07: Core 714, App 228, Engine 117, Platform.MacOS 19, plus Platform.Windows on Windows only); CI (`.github/workflows/ci.yml`, windows-latest and macos-latest) runs on every push and `node scripts/ci-status.mjs` prints recent runs with failing test names.

| Project | What is there | Tests |
|---|---|---|
| Augram.Core | Capture state machine; recognizer ported from StrokesPlus (Legacy/Corrected scoring, Average/Best aggregation, ConfusionCheck with `DuplicateCutOff` 90); Gesture model + GestureLibrary with undo; Config (JSON document, FileConfigStore with backups, SettingsStore, ConfigSession live save); Diagnostics (IEventLog port, RecognitionLog whose entries carry the drawn stroke and matched gesture id, HealthRegistry); Abstractions (all ports) | 262 |
| Augram.Engine | ChannelEventLog + rolling file / in-memory sinks; SharpHook input source and simulator; HookHealthMonitor; EngineHost / InputGate / EngineWorker (hook thread decides suppression from a shadow, worker runs the machine, ticks, recognition) | 76 |
| Augram.Platform.Windows | Win32 window identity (class chain, UWP, desktop, full-screen), ForegroundActivator (A20 rule), cursor probe, system events, overlay style apply/verify/place, Run-key startup | 47 |
| Augram.Import.StrokesPlus | Gestures-only reader for SP.net JSON, warnings, stats, GestureMerge | (in Core.Tests) |
| Augram.App | Dark wireframe theme; declarative screens (SectionForm, ItemList with text or component cells, field kinds); Shell with tabs Gestures / Commands (placeholder) / Ignored (placeholder) / Options / Diagnostics (Health, Log, Recognition with Drawn and Matched glyph columns); tray; single instance; F1 inspector; Debug gallery; Gestures tab (glyph grid, training popup, import dialog, duplicates outlined in red with partner highlight and score on select, "Keep this, delete its duplicates"); Options bound to SettingsStore with Detect button, no undo row; TrailOverlayWindow (parked at 1×1 between strokes); EngineModule and GesturesModule composed in CompositionRoot | 102 |

Runtime facts from Joel's machine: hook installs in ~50 ms; hook handler worst case 9–18 µs after JIT; recognition 0.04–0.4 ms over 20 gestures; trail first frame 8–24 ms; config at `%APPDATA%\Augram\augram.json`, logs at `%APPDATA%\Augram\logs\augram-yyyyMMdd.log`. Joel's config now holds his **105 imported StrokesPlus.net gestures** (stroke button Right); the starters are gone. His set contains real shape duplicates under two names (Down and ↕ Down, Up and ↕ Up, Circle and O, …) that he curates by hand on the Gestures tab.

## 3. M1 acceptance: done (Joel, 2026-10-07)

Import of his SP.net gestures, recognition of his daily gestures (checked in Diagnostics › Recognition with the drawn-vs-matched glyphs), training a new gesture, Redraw, Options, Detect and F1 + click all verified by Joel. His feedback produced the 2026-10-07 commits (Recognition glyphs, Redraw replaces, no Toggle active / Import in the tile menu, duplicate outlines at cut-off 90 with partner highlight and "Keep this", no diagnostic text line, no Undo/Redo on Options). Nothing from the pass is open.

## 4. M2 scope (plan 0001, section "M2")

Commands and steps: Core/Mapping (AppGroup, Command, CommandResolver with override-to-nothing and global fallback, MappingStore with undo); step types as vertical slices (Hotkey, TypeText, Run, MediaKey, Delay, WindowOp); Engine executor (serialized, activation rule A20, settle delay A8 only when focus moved); Commands tab (groups › commands › inline step list with drag reorder, step type picker by category, gesture picker popup, context menu + keymap, confirmations for group/command delete, Test button); app identification page and Ignored tab; hotkey capture field with system-wide suppression and watchdog (F5); full SP.net import per the C1 table, **matching incoming gestures by shape so a re-import reuses an existing gesture** (A7, no alias memory); Augram JSON export/import. Also in M2: the **Used by…** popup on a gesture tile (every app group › command using it; click a row to jump there; same list backs the delete warning); "Keep this, delete its duplicates" retargets the commands that used the deleted copies; a gesture no command uses is drawn greyed (active is per command, not per gesture). Keep each step type one folder (N3) and run `dotnet test` after every slice. Suggested order: command model + Commands tab with the Hotkey step first (most of Joel's SP.net actions), then per-app groups, then the full import, then the remaining step types.

## 5. How Joel works

- Short numbered answers; wants one-liners back when he asks "what do you need from me". Explain a choice in a few sentences when he says he does not know enough, then let him decide.
- Defers to the agent on engine internals ("I trust you on this"), owns anything UI/behaviour. He thinks in StrokesPlus.net terms; map new concepts to the SP.net equivalent.
- He reviews in the running app and sends feedback in small batches, often with a screenshot; fix, rebuild, relaunch, commit, and tell him what changed in two or three bullets.
- He authorised committing and pushing to main as work progresses, and lead-dev coordination of subagents. Commit at coherent checkpoints with the Claude co-author line; push; check CI.
- Reference material lives under `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\` (StrokesPlus classic source, GestureSign, StrokeIt SDK, Stroke, BetterMouse, SP.net install and settings).

## 6. Rules that came from incidents (do not relearn these)

- **Never launch Augram.App, a spike, or anything with a global hook or overlay from a subagent, or without telling Joel first.** Twice on 2026-10-06 an agent's test run showed a full-screen overlay that was not click-through and every left click on Joel's machine died until the process was killed. Subagents build, test (fakes only) and format; the lead runs the app.
- **Launching the app:** `.claude/settings.json` allows `Start-Process -FilePath src\Augram.App\bin\Debug\net10.0\Augram.App.exe` (relative path, from the repo root) and `Stop-Process`. Use the **Debug** build for feature work (dev gallery, inspector); Release only to measure latency. **Stop the app before `dotnet build`**: a running app locks its DLLs and the build fails with copy errors, and then `dotnet test --no-build` silently runs stale test binaries. A stale Debug exe from before a DI change crashes at startup with an unresolved-service exception in the Windows Application event log; rebuild fixes it.
- **The overlay is parked at 1×1 px between strokes, never hidden, and never shown unless `WS_EX_TRANSPARENT` reads back after `Show()`.** Hiding it made the previous stroke flash on the next one (retained surface); a deferred hide did not fix it; parking did. A watchdog hides a full-size idle window after 3 s.
- **A consumed press always gets a consumed release** (A19). The hook-side `SuppressionShadow` and the state machine are proven equal by a property test; keep it that way.
- **Logging never blocks the hook thread.** `IEventLog` enqueues; a worker writes.
- **Recognizer scoring stays `Legacy` by default** (divide by precision, as the original does). `Corrected` changes what the 75% threshold means; a 45°-off stroke scores 75.25 Legacy vs 75.0 Corrected.
- **The engine worker acts first and logs second.** A test that waited for the act (click injected, event raised, listening flag cleared) must also wait for the log line or status text (`EngineHarness.WaitForLog`), or it flakes on the two-core CI runner (it did, twice).
- **Vocabulary is fixed:** app group › command › step; the command-line step type is `Run`; never "action".
- **Tile layout must not move on selection:** the tile frame is a constant 2 px and only its colour changes; the partner score overlays the glyph. A 1 px border change shifted the whole grid and Joel noticed.
- **CI is red → fix before moving on.** `node scripts/ci-status.mjs` names the failing tests. Runner-specific traps so far: desktop-dependent tests (skip when `GITHUB_ACTIONS` is set) and the act-then-log ordering above.
- **Watch item (2026-10-07):** `EngineKeyCaptureTests.ARunningEngineCapturesAndEveryEventIsMarshalled` timed out once ("timed out waiting for the marshalled key", 5 s) on commit 167f031; the next two runs with the same code were green and the capture path has no ordering race (arming is synchronous under a lock; the key goes to the worker's channel as a non-critical write). Which runner (Windows or macOS) failed was not established. If it recurs, check whether the worker channel dropped the non-critical `KeyCaptured` write (`InputGate.Post` counts drops) before raising the timeout.

## 7. Working with subagents (what worked)

Give each agent disjoint folder ownership, the exact docs to read, the verification commands, and "do not commit". Have them report file lists with line counts and deviations. Verify a subagent's work yourself (`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`), commit by path so parallel agents' in-progress files stay out, and check CI after the push. When several agents run, an isolated `git worktree` at HEAD is the safe way to verify a commit (`dotnet build-server shutdown` before removing it, or the folder stays locked). Do not `git stash` while agents are writing. For small UI fixes driven by Joel's feedback, the lead does them directly; it is faster than briefing an agent.

## 8. Tooling set up on 2026-10-07

- `.claude/settings.json` (committed): permission allows for the .NET workflow and the app launch on both OSes, an `ask` list for destructive git, and the memory-sync hooks. `.claude/settings.local.json` (committed) pins `autoMemoryDirectory` to `~/.claude/projects/c---myProjects-augram/memory` so PC and Mac share one store.
- The memory folder is a private git repo, `Joel-Hjerten/augram-claude-memory`, synced by `scripts/memory-sync.mjs`: SessionStart pulls memory and reports if this checkout is behind or ahead of origin; Stop commits and pushes memory when it changed. Never hand-commit or force-push memory. An already-open idle chat never re-runs SessionStart; if Joel says he switched machines, pull memory and fetch by hand.
- Mac, one time: pull Augram, clone the memory repo into that folder (moving aside anything a Mac session wrote), start a new chat.

## 8a. macOS port (started 2026-10-07, on Joel's Mac)

The whole solution builds and tests green on macOS (arm64; .NET SDK installed per user in `~/.dotnet`). `Augram.Platform.MacOS` now has the window system (window under the point from the window server's list, focus and activation through the Accessibility API), window operations Close / Minimize / MaximizeOrRestore, a verified click-through overlay style (`ignoresMouseEvents`) and the cursor probe; `EngineModule` registers them on macOS. The overlay covers the display under the stroke start on macOS (one window cannot span displays while "Displays have separate Spaces" is on) and treats capture coordinates as points. SharpHook's KeyTyped is off on macOS (it would make every key wait for the UI thread). CI runs on windows-latest and macos-latest. Details, coordinates and what is next: [src/Augram.Platform.MacOS/README.md](../src/Augram.Platform.MacOS/README.md). **Verified by Joel on the Mac (2026-10-07):** app launches with the engine (hook installs in ~10 ms, handler worst case 6–80 µs), trail on both displays, Detect (middle button via Logitech Options+), and gestures firing Minimize and Maximize/restore over TextEdit and over Augram's own window (5–34 ms per command). Close is built but not yet tried.

macOS traps found that day (each fixed, with a code comment where it lives):
- **AX calls on Augram's own windows run in-process on the calling thread**, and AppKit off the main thread hangs. Every AX call on an own element and every `NSScreen` read goes through `Interop/MainThread` (main queue, 2 s, withdrawn if not started).
- **The Dock draws in a transparent display-sized window at layer 20**; hit-testing skips display-sized windows above the normal layer, or every point on that display resolves to Dock.
- **`Local\` mutexes are per terminal session on Unix**, and every macOS launch is its own session: the single-instance mutex is per user there (`NamedWaitHandleOptions`).
- Each Bash call from an agent is its own session too, and a process launched from VS Code inherits VS Code's Accessibility grant.

Open: D6 (maximize = fill the visible frame, working choice; Joel has used it), the placement operations, an app bundle (Accessibility is granted to VS Code while launched from it), start at login, system events.

## 8b. Commands tab changes from Joel's Mac session (2026-10-07)

All recorded in requirements.md (F5a lines) and `src/Augram.App/README.md`; listed here so a new agent does not undo them:
- Selecting an app group shows its form (name, active, suppress globals, executable names, title) in the side panel; edits apply at once, one undo step each. The right-click "Edit app definition…" is gone; New group… still uses the dialog.
- A click anywhere on a section header toggles it; a double click on any row renames it (a header's double click puts its toggle back); F2 or Enter renames on Windows, Return on macOS; a rebuild keeps keyboard focus on the selected row.
- A click outside a focused text box accepts it and takes the focus, in every window (`Views/ClickAwayFocus`); in-place renames commit on leaving, Escape reverts.
- No Undo/Redo buttons on the Global and Apps lists (deletes of groups, categories and commands ask first); Ctrl/Cmd+Z still undoes. The Gestures tab still has its buttons.
- Hotkeys read with the platform's key names (Cmd, Opt, Ctrl, Shift on macOS; words, not symbols); display only.
- Executable names on macOS are the executable's file name (`Google Chrome`); a per-app command overrides the global one on the same gesture without "Suppress global commands" (verified by Joel with Chrome's Close Tab over the global Close).

## 9. Open questions for Joel

- May Joel's 105 gestures be copied into the public repo as a test fixture? Not asked yet; do not do it without his yes.
- Avalonia 12 / SharpHook 8 upgrade: deferred to the start of M2 or later; nothing blocks on it.
- **Tray click on macOS** (asked 2026-10-07, no answer yet): 1) Windows behaviour via a native NSStatusItem (single click toggles, double opens, right-click menu; recommended), 2) Mac convention (click opens the menu, first item toggles), 3) click opens the menu, Option-click toggles. Today Avalonia's tray on macOS opens the menu on every click.
- **F8 conversion of Windows-authored hotkeys** (proposed, not decided): Ctrl → Cmd, Alt → Opt, Shift stays, Win combos marked "needs a Mac version" (A18). Matters as soon as Joel's imported SP.net commands run on the Mac; app groups' `.exe` names need Mac names the same way.
- **D6** maximize = fill the visible frame and restore (working choice; Joel has used it without objecting).
- Gestures tab Undo/Redo buttons: keep or drop like the Commands lists? Keyboard undo on the Commands lists: kept; Joel may want it gone.
- Next Mac slices offered: Close (built, untested), Center / snap halves / set size, an `Augram.app` bundle (own Accessibility grant, start at login).
