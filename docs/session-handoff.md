# Agent handoff: Augram, refactor in progress (plan 0007), features paused (updated 2026-10-11, PC session)

This file holds the **current state only**: what is true now, what is open, and the rules that came from incidents. The story of how it got here is in git, the plans, and the frozen [archive/session-handoff-2026-10-11.md](archive/session-handoff-2026-10-11.md). When something here stops being true, change it here; when a session ends, replace its section rather than adding a dated one above it.

Joel works on a **Windows 11 PC** and a **Mac**; both run Augram and sync through `Joel-Hjerten/augram-sync` (private).

## 0. Right now

**[Plan 0007, the refactor](plans/0007-refactor.md), started 2026-10-11. Features are paused until it is done** (bug fixes Joel asks for still go in). Steps: fold the retired Wireframe theme into Default; a read-only inventory per area (Fable agents); one list Joel picks from; slices that keep behaviour exactly, each writing its folder's README.

**Versions.** Main is **0.11.14** (`Directory.Build.props`). The PC runs a 0.11.x build (Joel installs with `node scripts/package.mjs windows` and runs the Setup.exe himself). **The Mac still runs 0.7.0**: it cannot read the PC's config (schema 7, sync format 14), so its sync is paused ("uses a newer Augram") until it runs the current build. Nothing is lost meanwhile.

**Since 0.11.0 on the PC (Joel's feedback rounds, 2026-10-11):**
- 0.11.3–0.11.4: the step picker greys types a command cannot take (Core `StepOffer`); New command sits on the selected section's header.
- 0.11.5: a step's grip only drags and a header click opens or closes the step; **Change to…** in the step menu; a group renames only by a double click on its name.
- 0.11.6: **Uncategorized is an ordinary Global category** (`CategoryRules.Normalised` makes it, with a fixed id so two machines get one; app groups say "No category"). A Mac on an older build can show its old null bucket beside it until it updates. Joel's config was copied to `augram.before-0.11.6.json` first.
- 0.11.7–0.11.14: the step menu's pickers close on a click elsewhere; the header's Trigger / While holding rows wrap and the window has a minimum size (960 × 560); ⓘ on a gesture trigger, Category and Use on; Show other platforms has a tooltip and blinks twice when an edit hides the selected row (`PlatformFilterNudges`); steps reorder live while dragging (`StepRow.DragOffset`, one Reorder on release).

## 1. Waiting for the Mac (after the refactor)

The Mac goes straight from 0.7.0 to the current build. On the Mac: pull, build and test (`~/.dotnet/dotnet`), `node scripts/package.mjs mac --signed` (Joel quits the running Augram from the menu bar first; installing does not restart it), back up `~/Library/Application Support/Augram/augram.json`, Joel installs the `.pkg`. Then the "Done when" checks of [plan 0004](plans/0004-drag-distance-and-not-in.md) (step 4), [plan 0005](plans/0005-button-triggers.md) and [plan 0006](plans/0006-glass-look.md), which all name their Mac points. The ones most likely to need a decision:
- **The magnifier** (Global "magnifier": Button Left while holding Right, a Remap step Shift + Win + X, Also in Blender) syncs over with no Mac version. If the Mac's stroke button is Right, nothing is held back there; otherwise Right is held back on the Mac too. Its key output becomes Shift + Cmd + X: check what Eyeris's Mac shortcut is.
- **Spine and Eyeris** Per command entries were made from Windows windows and hold Windows names only; they match nothing on the Mac until macOS names are filled in (the magnifier on a Spine window). Ask Joel whether he uses them on the Mac.
- **Never seen on a real desktop yet:** the Add app… magnifier inside the modal Not in dialog, and a Used by link opening its command. The glass, title-bar drag, double-click and Options › Appearance live (plan 0006) on macOS.
- Read the log for `sync`, `ignore` and `hold` warnings: `~/Library/Application Support/Augram/logs/augram-<yyyyMMdd>.log`.

When Joel calls a plan done on both machines, move it to `docs/plans/completed/` and fix its links.

## 2. Open, awaiting Joel

- **The magnifier holds Right back everywhere at the Options value (10 px)**, so a right-drag in Spine and Eyeris waits 10 px. Two fixes offered (the magnifier's own drag distance 3, or Spine in its Not in); not chosen. Eyeris and Spine also drop the first pixels of a drag themselves ([learnings 0006](learnings/0006-other-programs-on-the-mouse.md)).
- **Start at login has never run on a real login** (off on the PC). Check lists: App README "Windows check list", Platform.MacOS README "Start at login on macOS".
- **Export/import (plan 0003, PROPOSED):** "Decided for Joel" and "Questions for Joel" wait for him; his check list is plan 0003 step 5 (throwaway config folders, `--config-folder … --no-engine`). Follow-ups: plan 0003 "Follow-ups from Joel's questions".
- **Sync conflicts show only in Options › Sync.** Proposed: "· 1 conflict" in the tray tooltip and Resolve… in the tray menu.
- **Augram stops while Task Manager has focus** (elevated windows); "later" (requirements §4 has the cause and two fixes).
- **Debug logging cannot be switched on** in any build (`ChannelEventLog.MinimumLevel` is set by nothing); Joel: not needed now.
- **Older questions:** may his 105 gestures become a test fixture in the public repo (never without his yes); Avalonia 12 / SharpHook 8 (deferred); Gestures tab Undo/Redo buttons keep or drop; Mac window extras (Center, snap halves, set size).
- **One app, defined once (Joel, 2026-10-11, backlog: after the refactor and the Mac).** Exclusions › Global and › Per command each hold their own app definitions, and names must be unique across both lists, so Joel's Per command "Blender" and "Spine" are stuck as "Blender 2" / "Spine 2" beside the Global ones. And re-registering Blender's exe in one place does not reach the others: the same app is defined up to three times (Global exclusion, Per command exclusion, the Apps app group), each with its own matcher. Options: (1) small: names unique per list, not across both (fixes the " 2" only); (2) proper, lead's recommendation: an app is defined once and exclusions and app groups refer to it, so one edit reaches every use. (2) changes the config schema and sync format, so it is its own plan once the Mac runs the current build.
- **Idea, not scheduled (Joel, 2026-10-11): one gesture that means player fullscreen over a YouTube video and window maximize elsewhere in Chrome.** Needs the UI element under the cursor via the accessibility tree (UIA / AX); an extension is ruled out. Findings and what to measure first (the cost to Chrome): [reference/element-under-cursor.md](reference/element-under-cursor.md).
- **After the refactor and the Mac:** the rest of plan 0001 M2 and M3; ask Joel for the order.

## 3. Read in this order

1. [CLAUDE.md](../CLAUDE.md): ground rules (including the turnaround rules and agent effort levels), the architecture invariants, the read-first table.
2. [plans/0007-refactor.md](plans/0007-refactor.md) while the refactor runs.
3. [requirements.md](requirements.md): everything DECIDED, the vocabulary (**app group › command › step**; the command-line step type is **Run**; never "action").
4. [adr/0002-code-and-repo-structure.md](adr/0002-code-and-repo-structure.md): where code goes and why, with the 2026-10-11 amendments (Wireframe retired, a README in every App folder).
5. The README in every project folder under `src/` and `tests/`, and in each Core subsystem; `src/Augram.Engine/README.md` has the thread table.
6. Reference and learnings docs only for the area you touch (CLAUDE.md's read-first table says which).

## 4. Running things

- **The app.** Only the lead launches it, after telling Joel. `.claude/settings.json` allows `dotnet run --project src/Augram.App/Augram.App.csproj -- --no-engine` and starting `src\Augram.App\bin\Debug\net10.0\Augram.exe`; Debug for feature work (gallery, inspector), Release only to measure latency. The dev build asks to take over the installed Augram (one Augram at a time across builds). **Stop the app before `dotnet build`**: it locks its DLLs, and `dotnet test --no-build` then runs stale binaries. To see the UI without launching anything, use `tools/UiShots` (headless PNGs; safe from agents).
- **Installers.** `node scripts/package.mjs windows|mac` builds one per platform ([release.md](release.md)); nothing is published. Joel runs the Setup.exe or the `.pkg` himself, never an agent. Back up `augram.json` before an install that changes the config schema.
- **Logs and config.** PC: `%APPDATA%\Augram\augram.json`, logs in `%APPDATA%\Augram\logs\augram-yyyyMMdd.log`. Mac: `~/Library/Application Support/Augram/`. Lead and agents may read and edit these (stop the app and back up first).
- **The Mac.** dotnet is `~/.dotnet/dotnet`. The agent never launches the app or uses the keychain: give Joel numbered steps and paste blocks; he runs `~/.dotnet/dotnet src/Augram.App/bin/Debug/net10.0/Augram.dll` after quitting the running Augram. zsh has noclobber and `rm -i` (use `>|`, `command rm -f`, fresh scratch names). Joel allows exactly `node scripts/package.mjs mac --signed`. A process launched from VS Code inherits VS Code's Accessibility grant.
- **CI.** `node scripts/ci-status.mjs --wait` in the background after every push; a red main is fixed before anything else.

## 5. How Joel works

- Short numbered answers. When he must act: numbered click-by-click steps with paste blocks.
- Owns UI and behaviour; defers on engine internals ("I trust you on this"). He thinks in StrokesPlus.net terms: map new concepts to the SP.net equivalent, and when he shows an SP.net screen, build that layout (no layout questionnaires).
- Reviews in the running app and sends feedback in small batches, often with screenshots (red rectangles mark what he means). Fix, rebuild, tell him what changed in two or three bullets.
- Commit and push to main as work progresses (authorised); the lead coordinates subagents.
- Reference material: `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\` (StrokesPlus classic source, GestureSign, StrokeIt SDK, Stroke, BetterMouse, SP.net install and settings). Joel's real StrokesPlus.net file never enters the repo (`sources/` is ignored).

## 6. Rules that came from incidents (do not relearn these)

- **Never launch Augram.App, a spike, or anything with a global hook or overlay from a subagent, or without telling Joel first.** Twice on 2026-10-06 an agent's test run showed a full-screen overlay that was not click-through and every left click on Joel's machine died until the process was killed.
- **The overlay is parked at 1×1 px between strokes, never hidden, and never shown unless `WS_EX_TRANSPARENT` reads back after `Show()`.** Hiding it made the previous stroke flash on the next one; parking fixed it. A watchdog hides a full-size idle window after 3 s.
- **A consumed press always gets a consumed release** (A19). The hook-side `SuppressionShadow` and the state machine are proven equal by a property test; keep it that way.
- **Logging never blocks the hook thread.** `IEventLog` enqueues; a worker writes.
- **Recognizer scoring is `Corrected` by default** (since 2026-10-09; `Legacy` stays selectable). `Corrected` changes what the 75 % threshold means.
- **The engine worker acts first and logs second.** A test that waits for the act must also wait for the log line (`EngineHarness.WaitForLog`), or it flakes on the two-core CI runner.
- **Test wait helpers poll every 10 ms, never spin**, with 30 s on the failure path (`EngineFixture`, `EngineHarness`, `SyncMachine`); App.Tests raises the thread-pool minimum at load (`TestThreadPool`).
- **A platform-bound step in a test runs converted on the other runner.** Author such steps where the test runs (`Mappings.CommandHere` in Engine.Tests).
- **Desktop-dependent tests skip when `GITHUB_ACTIONS` is set.** CI's console logger is `verbosity=minimal` (the detailed one crashed the Windows test host).
- **Raise `SyncFile.CurrentFormatVersion` with every change to what a sync item holds** (Core/Sync README): a build that finds a newer file pauses instead of misreading it. Both machines must update before sync resumes.
- **Tile layout must not move on selection:** the tile frame is a constant 2 px; only its colour changes.
- **Pull, rebuild, and only then let an app sync.**
- **Worktree agents on Windows:** the lead's shell must be on `C:\_myProjects\augram` with an uppercase drive letter before launching them, and they start from origin main, so name the base commit. `dotnet build-server shutdown` before removing a worktree, or the folder stays locked. Do not `git stash` while agents are writing; commit by path.
- **Shell:** no `rm -rf`, no long compound one-liners (permission prompts); PowerShell 5.1's `Set-Content` re-encodes files, so edit with node or the editor; `git commit -m` with embedded quotes splits on Windows, so write the message to a file and use `git commit -F`. Never put backticks inside a double-quoted shell string.

## 7. Tooling

- `.claude/settings.json` (committed): permission allows for the .NET workflow and the app launch on both OSes, an `ask` list for destructive git, and the memory-sync hooks. `.claude/settings.local.json` pins `autoMemoryDirectory` so PC and Mac share one memory store.
- The memory folder is a private git repo (`Joel-Hjerten/augram-claude-memory`) synced by `scripts/memory-sync.mjs`: SessionStart pulls memory and reports whether this checkout is behind origin; Stop commits and pushes memory; after a break of more than 10 minutes, a prompt hook pulls memory, fetches the checkout and notes what changed (act on that note first). Never hand-commit or force-push memory.
- Agent timing baseline (2026-10-11, before the effort and turnaround rules): 29 agent runs since 2026-10-09 averaged 24 minutes; 63 % of the time was the model thinking at `xhigh`, tests 6 %, build and format 4 %, edits about 4 s each. Plan 0007's close-out repeats the measurement.
