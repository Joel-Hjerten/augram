# Agent handoff — Augram: M2 under way, sync live on both machines, signed Mac installer works (updated 2026-10-08 afternoon, Mac session)

You are picking up a project whose first runnable milestone is built and accepted by Joel. Everything decided is in the docs; this file tells you the state, the order to read things, what to do next, and the rules that came from mistakes. Joel works on his **Windows 11 PC** and his **Mac** (sections 8, 8a–8c); both run Augram and sync through `Joel-Hjerten/augram-sync`.

## 0. Right now

**Mac session, 2026-10-08 (all on main, CI green):**
- **Mac packaging works end to end.** `node scripts/package.mjs mac` with Joel's Developer ID builds `Augram.app`, notarized and stapled (`spctl`: "Notarized Developer ID"), and a signed `Augram-osx-Setup.pkg` since the Developer ID Installer certificate exists. Commands and this Mac's values (sign by SHA-1 fingerprint: two Application certificates share one name now; notary profile `eyeris-notary`): [release.md](release.md). The script finds dotnet in `~/.dotnet` and restores vpk itself. **Nothing is published (Joel): each machine builds and installs its own installer;** no tags, no GitHub releases. Joel runs signed builds himself (the agent may not use the keychain or notary credentials).
- **Single-instance fix on macOS:** the pipe dropped the request after a Hello (domain-socket queue closed with the last server stream); macOS CI had been red since 046c0ea. App README "Tray, version and channel, single instance".
- **Dock (Joel):** Dock icon only while the window is open; close or minimize hides to the menu bar (`MacDockPresence`, `MacDock`); Windows unchanged. Tried by Joel on the dev build.
- **Icons:** Joel's gold art and a hand-drawn menu-bar master (`design/app-icon/exports/app-icon-mac-tray.png`); **Options › General › Colour menu-bar icon** (macOS only, per machine, as in Eyeris) switches the template for the colour icon.
- **Planned (Joel):** gesture shape cleanup, plan 0001 M2 step 10 (keep the original, restorable, opt-in).
- Not yet confirmed by Joel: the colour menu-bar switch in the running app; the installed `.pkg` (Accessibility grant for the signed bundle).

**PC session, 2026-10-08:**

**Merged 2026-10-08 (main, both below are done):** the next step is building the 0.2.0 Windows installer (`node scripts/package.mjs windows`) and installing it with Joel; any build from before this merge must be quit by hand once (it cannot be asked to quit). The text below describes what was built. **Previously in flight: two worktree agents** (launched by the PC lead; their reports arrive in that session). When they report: merge each branch (`git merge` the `worktree-agent-*` branch), stop the app, build, `dotnet test`, `dotnet format --verify-no-changes`, push, remove the worktree (`dotnet build-server shutdown`, `git worktree remove --force`, `git worktree prune`, `git branch -d`). Both touch `src/Augram.App/Program.cs` (one adds only `VelopackApp.Build().Run();` at the top of `Main`): resolve that by hand.
1. **Version + Dev label + one instance:** `<Version>0.2.0</Version>` in `Directory.Build.props` with the commit; `AugramChannel` (`Dev` default, `Release` when packaged); `AppInfo`; window title and tray tooltip "Augram (Dev)" for dev builds (Joel: "Dev within parentheses"); About on Options; dev builds never write or remove start-at-login; **one Augram at a time across dev and installed** (Joel: "definitely only one"): a newcomer of another build offers "Quit it and start this one".
2. **Packaging:** Velopack (per-user Windows Setup.exe, macOS .app signed and notarized with Joel's Developer ID from his Keychain, auto-update later), `scripts/package.mjs windows|mac`, `.github/workflows/release.yml` (tag `vX.Y.Z` → Windows installer on a GitHub Release), `docs/release.md`. Modelled on Joel's Eyeris repo.
**After both merge:** build the Windows installer, install it with Joel on the PC (never install or launch from an agent), make the installed Augram his daily driver; then the Mac half (`node scripts/package.mjs mac` with `AUGRAM_SIGN_IDENTITY` and `AUGRAM_NOTARY_PROFILE`) on the Mac.

**Done 2026-10-07/08 (all on main, CI green):** window ops, Hotkey (with system-wide capture field and right-hand modifiers RAlt/RCtrl kept), MediaKey, Delay, Imported placeholders; Commands tab with Global (categories) and Apps sub-tabs; full SP.net import (categories, hotkeys); **sync between machines** (git, per-machine files, three-way merge with acknowledgements, join and conflict dialogs, format guard); F8 "Use on" per app group, per category and per command, plus best-guess cross-platform step conversion (Mac session); one set of list row metrics (`Row.*` tokens, `RowMetricsTests`); the app icon (`design/app-icon`, `tools/IconTool`, multi-size tray .ico on Windows, template icon on macOS); the resync hook for open chats.

**Next after the release pipeline:** the remaining step types, TypeText and Run (most of Joel's still-"not supported" SP.net commands); then Augram JSON export/import (F8) and the Ignored tab.

**Rules from 2026-10-07/08 (also in section 6):** pull → rebuild → only then let an app sync (Joel; the format guard now pauses a stale build); stop the app before `dotnet build`; agents run in isolated worktrees and commit on their branch, the lead merges; on Windows run `Set-Location C:\_myProjects\augram` (uppercase drive) before launching worktree agents; no `rm -rf`, no long compound shell lines (settings `.claude/settings.json` allow list); settings and hooks are Joel's to paste; Joel's real StrokesPlus.net file never enters the repo (`sources/` is ignored).

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

**Version, channel, one Augram (2026-10-08):** version 0.2.0 lives in `Directory.Build.props`; every local and CI build is channel Dev ("Augram (Dev)" in the title and tray tooltip; it never writes or removes start at login), `-p:AugramChannel=Release` builds the installed one; only one Augram runs at a time across both, and a launch of a different build asks "Quit it and start this one?" before anything of it starts (App README "Tray, version and channel, single instance").

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
- **CI is red → fix before moving on.** `node scripts/ci-status.mjs --wait` waits for HEAD's run and names the failing tests per runner (`build (windows-latest)`, `build (macos-latest)`); exit 2 means unknown (rate limited), not done. Runner-specific traps so far: desktop-dependent tests (skip when `GITHUB_ACTIONS` is set) and the act-then-log ordering above.
- **Test wait helpers poll, never spin (2026-10-07).** Two Windows CI runs timed out at 5 s (`EngineKeyCaptureTests`, `StrokeButtonDetectionTests.TimesOutWhenNothingIsPressed`): busy-spinning `SpinWait.SpinUntil` helpers running in parallel starved the engine worker and thread-pool timer they waited on. `EngineFixture`, `EngineHarness` and `SyncMachine` now poll every 10 ms with 30 s on the failure path; write new wait helpers the same way. A zero-delay timer then still missed 5 s once (`TimerSaveSchedulerTests`): App.Tests raises the thread pool minimum at load (`TestThreadPool`), and fixed waits there allow 30 s.

## 7. Working with subagents (what worked)

Give each agent disjoint folder ownership, the exact docs to read, the verification commands, and "do not commit". Have them report file lists with line counts and deviations. Verify a subagent's work yourself (`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`), commit by path so parallel agents' in-progress files stay out, and check CI after the push. When several agents run, an isolated `git worktree` at HEAD is the safe way to verify a commit (`dotnet build-server shutdown` before removing it, or the folder stays locked). Do not `git stash` while agents are writing. For small UI fixes driven by Joel's feedback, the lead does them directly; it is faster than briefing an agent.

## 8. Tooling set up on 2026-10-07

- `.claude/settings.json` (committed): permission allows for the .NET workflow and the app launch on both OSes, an `ask` list for destructive git, and the memory-sync hooks. `.claude/settings.local.json` (committed) pins `autoMemoryDirectory` to `~/.claude/projects/c---myProjects-augram/memory` so PC and Mac share one store.
- The memory folder is a private git repo, `Joel-Hjerten/augram-claude-memory`, synced by `scripts/memory-sync.mjs`: SessionStart pulls memory and reports if this checkout is behind or ahead of origin; Stop commits and pushes memory when it changed. Never hand-commit or force-push memory. An open chat resyncs through the UserPromptSubmit hook (`memory-sync.mjs prompt`): after a break of more than 10 minutes between Joel's messages it pulls memory and fetches the checkout, and when something changed it adds a note to his message (changed memories and MEMORY.md lines, commits pushed from the other machine); act on that note first (re-read memory, pull, rebuild, restart the app). It never pulls code itself.
- Mac, one time: pull Augram, clone the memory repo into that folder (moving aside anything a Mac session wrote), start a new chat.
- **Releases (2026-10-08):** Velopack installer per platform, `node scripts/package.mjs windows|mac` (vpk is a repo-local tool: `dotnet tool restore`); a pushed tag `vX.Y.Z` makes `.github/workflows/release.yml` build the Windows Setup.exe and create the GitHub release, and the Mac adds its signed, notarized package to it. Everything, including the Mac commands and traps: [release.md](release.md). Never run the Setup.exe from an agent: it installs Augram and starts it.

## 8a. macOS port (started 2026-10-07, on Joel's Mac)

The whole solution builds and tests green on macOS (arm64; .NET SDK installed per user in `~/.dotnet`). `Augram.Platform.MacOS` now has the window system (window under the point from the window server's list, focus and activation through the Accessibility API), window operations Close / Minimize / MaximizeOrRestore, a verified click-through overlay style (`ignoresMouseEvents`) and the cursor probe; `EngineModule` registers them on macOS. The overlay covers the display under the stroke start on macOS (one window cannot span displays while "Displays have separate Spaces" is on) and treats capture coordinates as points. SharpHook's KeyTyped is off on macOS (it would make every key wait for the UI thread). CI runs on windows-latest and macos-latest. Details, coordinates and what is next: [src/Augram.Platform.MacOS/README.md](../src/Augram.Platform.MacOS/README.md). **Verified by Joel on the Mac (2026-10-07):** app launches with the engine (hook installs in ~10 ms, handler worst case 6–80 µs), trail on both displays, Detect (middle button via Logitech Options+), and gestures firing Minimize and Maximize/restore over TextEdit and over Augram's own window (5–34 ms per command), and Close (it closed Chrome when the global Close fired over it, before Chrome's own group existed).

macOS traps found that day (each fixed, with a code comment where it lives):
- **AX calls on Augram's own windows run in-process on the calling thread**, and AppKit off the main thread hangs. Every AX call on an own element and every `NSScreen` read goes through `Interop/MainThread` (main queue, 2 s, withdrawn if not started).
- **The Dock draws in a transparent display-sized window at layer 20**; hit-testing skips display-sized windows above the normal layer, or every point on that display resolves to Dock.
- **`Local\` mutexes are per terminal session on Unix**, and every macOS launch is its own session: the single-instance mutex is per user there (`NamedWaitHandleOptions`).
- Each Bash call from an agent is its own session too, and a process launched from VS Code inherits VS Code's Accessibility grant.

Open: D6 (maximize = fill the visible frame, working choice; Joel has used it), the placement operations, start at login, system events. The app bundle exists since 2026-10-08 (signed `.pkg`, release.md); a dev build launched from VS Code still uses VS Code's Accessibility grant.

## 8b. Commands tab changes from Joel's Mac session (2026-10-07)

All recorded in requirements.md (F5a lines) and `src/Augram.App/README.md`; listed here so a new agent does not undo them:
- Selecting an app group shows its form (name, active, suppress globals, executable names, title) in the side panel; edits apply at once, one undo step each. The right-click "Edit app definition…" is gone; New group… still uses the dialog.
- A click anywhere on a section header toggles it; a double click on any row renames it (a header's double click puts its toggle back); F2 or Enter renames on Windows, Return on macOS; a rebuild keeps keyboard focus on the selected row.
- A click outside a focused text box accepts it and takes the focus, in every window (`Views/ClickAwayFocus`); in-place renames commit on leaving, Escape reverts.
- No Undo/Redo buttons on the Global and Apps lists (deletes of groups, categories and commands ask first); Ctrl/Cmd+Z still undoes. The Gestures tab still has its buttons.
- Hotkeys read with the platform's key names (Cmd, Opt, Ctrl, Shift on macOS; words, not symbols); display only.
- Executable names on macOS are the executable's file name (`Google Chrome`); a per-app command overrides the global one on the same gesture without "Suppress global commands" (verified by Joel with Chrome's Close Tab over the global Close).

## 8c. Sync between machines (built 2026-10-07, ahead of the remaining M2 step types)

Design and rules: requirements F8 "Sync between machines". Code: `Core/Sync/` (README: items, three-way merge, the acknowledgement design that replaced a naive "last merge" base, join, conflicts), `Augram.Sync.Git` (README: process and credential rules), App `Hosting/SyncService` + `SyncModule`, Options › Sync, `Sync/` dialogs (App README "Sync between machines"). Joel's repo: `https://github.com/Joel-Hjerten/augram-sync.git` (private, created empty). Gestures and the mapping sync; settings stay per machine. Augram stores no credentials (git's credential helper; `GIT_TERMINAL_PROMPT=0`; network calls capped at 30 s). The PC publishes first; the Mac joins with "Use the synced settings". **Verified by Joel (2026-10-07), Mac ↔ PC on the own-steps build (ff0e6ba and later):** a group made on the Mac arrived on the PC after a sync there. The status line's time is this machine's last run (at launch, then every 5 minutes while automatic sync is on), not the other machine's last publish; an old time on a running machine means automatic sync is off or the runs are failing. **Rule: raise `SyncFile.CurrentFormatVersion` with every change to what a sync item holds or which item kinds exist** (Core/Sync README, Format version): a build that finds a newer file pauses sync ("Paused: Mac uses a newer Augram…") instead of misreading it, and never publishes below what its machine already published. Open: a longer timeout for a first sign-in if one ever needs it.

## 8d. Cross-platform commands (F8, built 2026-10-07)

Decided design: requirements F8 "Cross-platform commands — DECIDED" and "Use on". Built in four slices: step conversion both ways (`IStepType.Convert`, `Steps/Hotkey/HotkeyConversion` with its exception table), a command's own steps for the other platform made on the first edit there (`Mapping/Command` `OwnVersion`, `PlanFor`, a fingerprint that flags a changed original), own steps as a sync item of their own (`version:<commandId>`), and "Use on" Windows/macOS per app group and per command with "Show other platforms" on both lists. App groups hold Windows and macOS executable names separately, with a known-apps guess for an empty list. Run with the new build on both machines before making own steps: an older build drops `ownVersion` when it saves.

## 9. Open questions for Joel

- May Joel's 105 gestures be copied into the public repo as a test fixture? Not asked yet; do not do it without his yes.
- Avalonia 12 / SharpHook 8 upgrade: deferred to the start of M2 or later; nothing blocks on it.
- **Tray click on macOS** (asked 2026-10-07, no answer yet): 1) Windows behaviour via a native NSStatusItem (single click toggles, double opens, right-click menu; recommended), 2) Mac convention (click opens the menu, first item toggles), 3) click opens the menu, Option-click toggles. Today Avalonia's tray on macOS opens the menu on every click.
- **D6** maximize = fill the visible frame and restore (working choice; Joel has used it without objecting).
- Gestures tab Undo/Redo buttons: keep or drop like the Commands lists? Keyboard undo on the Commands lists: kept; Joel may want it gone.
- Next Mac slices offered: Center / snap halves / set size, start at login for the installed bundle.
- Shape cleanup: on by default ever, or opt-in for good? (opt-in until the replay measurement, requirements F3)
