# Agent handoff: Augram, refactor done (plan 0007), features resume (updated 2026-10-12, PC session)

This file holds the **current state only**: what is true now, what is open, and the rules that came from incidents. The story of how it got here is in git, the plans, and the frozen [archive/session-handoff-2026-10-11.md](archive/session-handoff-2026-10-11.md). When something here stops being true, change it here; when a session ends, replace its section rather than adding a dated one above it.

Joel works on a **Windows 11 PC** and a **Mac**; both run Augram and sync through `Joel-Hjerten/augram-sync` (private).

## 0. Right now

**[Plan 0007, the refactor](plans/completed/0007-refactor.md), done 2026-10-12.** Every App folder has a README (the App README is a map of them); 52 architecture tests fail CI when a second implementation of a folded job comes back ([reference/one-home-guards.md](reference/one-home-guards.md)); CLAUDE.md's "One home per job" rule. Features resume. **Main has visible changes since 0.11.14 that Joel has not installed:** every dialog on one base (the import review's margin, the picker's heading), the light theme's swatch ring and cleaned-stroke gold, Enter renames on the Gestures tab (Windows), one undo step for deleting or merging used gestures, command labels ("Global › Minimize", "Blender › Space › Orbit") in Used by, sync conflicts and the log, the sync conflict text right on the Mac. The next package should get a new version.

**0.12.1–0.12.3 (2026-10-12): a hotkey gesture over the window that came forward after a Minimize works again** (Joel: works; 0.12.2 cut the delay he noticed from 309 ms to 128 and leaves tooltips and flyouts alone; 0.12.3 trims the poll to ≈100 and logs why the first techniques fail). Windows had put PotPlayer's owned helper window in front; A20 now compares top-level windows, not root owners ([plans/completed/0008-keys-after-minimize.md](plans/completed/0008-keys-after-minimize.md), measured with a throwaway probe). Joel's check: PotPlayer and Chrome side by side, minimize Chrome (gesture or its button), `/Up` over PotPlayer → fullscreen with no click in between; the log shows a real technique instead of `not-needed`, and `elapsedMs` on `Window activated` is the delay (309 on 0.12.1 via `alt-tap`; ≈105 expected on 0.12.2). **Open:** inside Augram the plain `SetForegroundWindow` is refused on PotPlayer's helper case, yet the probe's identical call succeeded in 13 ms on the same state (06:57:22). Read `accepted`/`error`/`front` on the next `Activation attempt failed` lines from Joel's normal use (plan 0008 "Follow-up"). Also check a hotkey gesture over Notepad++ with its Find dialog focused still types into Find.

**Versions.** Main is **0.13.7** (`Directory.Build.props`; 0.13.1 to 0.13.7 are Joel's acceptance-round fixes of 2026-10-12: 0.13.4 the per-list excluded app names, 0.13.5 Enter leaves a single-line text box, 0.13.6 the rename key works again right after a rename, 0.13.7 dragging Tint no longer flickers the Mac window between glass and opaque, 0.13.8 an unfocused Mac window keeps the theme's glass instead of turning white); 0.13.6's Windows installer was built 2026-10-12 for Joel to install (0.13.7 and 0.13.8 change nothing on Windows). The PC runs a 0.11.x or 0.12.x build until he does (Joel installs with `node scripts/package.mjs windows` and runs the Setup.exe himself). **The Mac runs 0.13.6 since 2026-10-12 17:56** (it had been on 0.10.3, not 0.7.0; config already schema 7, sync format 14). Sync resumed on the first start (`Sync: Applied`, no warnings). **Since 18:19 it runs 0.13.8**: Joel confirmed the Tint drag no longer flickers and an unfocused window stays glass (Frosted glass) and is never white (`Platform.MacOS/MacWindowGlass`, learnings 0008). Next: the Mac checks below.

**Since 0.11.0 on the PC (Joel's feedback rounds, 2026-10-11):**
- 0.11.3–0.11.4: the step picker greys types a command cannot take (Core `StepOffer`); New command sits on the selected section's header.
- 0.11.5: a step's grip only drags and a header click opens or closes the step; **Change to…** in the step menu; a group renames only by a double click on its name.
- 0.11.6: **Uncategorized is an ordinary Global category** (`CategoryRules.Normalised` makes it, with a fixed id so two machines get one; app groups say "No category"). A Mac on an older build can show its old null bucket beside it until it updates. Joel's config was copied to `augram.before-0.11.6.json` first.
- 0.11.7–0.11.14: the step menu's pickers close on a click elsewhere; the header's Trigger / While holding rows wrap and the window has a minimum size (960 × 560); ⓘ on a gesture trigger, Category and Use on; Show other platforms has a tooltip and blinks twice when an edit hides the selected row (`PlatformFilterNudges`); steps reorder live while dragging (`StepRow.DragOffset`, one Reorder on release).

**[Plan 0009, search and filter on the lists](plans/0009-search-and-filter.md): merged as 0.13.0, awaiting Joel's acceptance** (the plan's "Joel's acceptance check (PC)": type "down" on the Gestures tab; on Commands › Apps a group's name then a command's name; select the group, clear, it stays selected and expanded; Ctrl+F from a selected row; Exclusions on a name and a process name; the picker with starts-with first; close and reopen the window, every box empty).

## 1. Mac session: start here (2026-10-12)

**Mac checks, Joel, 2026-10-12 on 0.13.9: passed.** The magnifier (latch, hold, no context menu on Right's release, nothing stuck), right-drag and Right + wheel, Options › Appearance live and after a restart, Reduce transparency, the title bar, the light theme, Add app… in Not in, Used by links, and the Spine/Eyeris Per command entries (Joel uses them on the Mac; they work). Plans 0004, 0005 and 0006 moved to `plans/completed/`. **Fixed after the check (2026-10-12):** 0.13.10 and 0.13.11: the Not in and Also in dialogs mark entries no other command names "unused" with an ✕ that deletes them on Save (one `ViewModels/Commands/RemovableEntries`, one `MappingDocument.UsersOf`). 0.13.12: the find key works from anywhere on a tab (the Shell binds it on its window; on the Mac nothing has focus until a click), and control characters never reach a text box (`Hosting/ControlCharacterInput`; Escape in an empty search box typed U+001B on the Mac). 0.13.13: Escape in a search box clears the text first, then (empty) leaves for the list's selected row. **The Mac runs 0.13.13 and Joel confirmed all of it ("works great", 2026-10-12).** Plan 0009 stays open only for its full PC acceptance check. The rest of this section is the history of the catch-up.

**Solved 2026-10-12:** cause 2 below. The `.pkg` had installed 0.13.6 over a 0.10.3 that had run since 2026-10-11; opening Augram from Applications only made the running 0.10.3 show its window (`Asked to show the window by a reopen` in the log). Quitting it and opening the app again started 0.13.6.

**Symptom (Joel, 2026-10-12):** he built the latest main on the Mac, but Augram shows the old UI and lacks most new sections. That means an old binary is on screen. The code is shared: `Augram.App` is the same Avalonia UI on both OSes, and no tab, section or glass look is Windows-only. Do not look for a Mac-specific UI bug until the steps below show that the 0.13.x build is the one running.

**Diagnose first, in this order** (read-only; the agent does not launch the app, Joel does):
1. **What is running?** The latest `App started` line in `~/Library/Application Support/Augram/logs/augram-<yyyyMMdd>.log` gives `version`, `commit` and `channel`. Main is **0.13.6** (`c93f5d9` or later). If it says 0.7.0, or a commit before `c93f5d9`, the old build is still the one on screen.
2. **Most likely cause: the old Augram never quit.** One Augram runs at a time. A launch of the same channel from the same path (the installed app from `/Applications`, or a dev build rebuilt in place) only asks the running copy to show its window, then exits (`src/Augram.App/Hosting/README.md`, single instance, "Same channel and same executable"). Installing a `.pkg` does not restart a running Augram. Fix: Joel quits Augram from the menu bar (or `pkill -f Augram`), checks with `pgrep -fl Augram` that nothing is left, then starts the new one.
3. **Is the checkout current?** Run `git status` and `git log -1 --oneline` (expect `main` at or after `c93f5d9`), and `git fetch` / `git pull` if it is behind. A detached HEAD or another branch from the 0.7.0 days would build the old UI.
4. **Did the build land where it is launched?** The dev build is `src/Augram.App/bin/Debug/net10.0/Augram.dll`. The installed app is `/Applications/Augram.app`, which only changes when a new `.pkg` is installed. Check the timestamps of whichever one Joel starts. If the `.pkg` was built, its version is in the `package.mjs` output ("Augram 0.13.6 from commit …").

**Then catch the Mac up:**
1. Back up `~/Library/Application Support/Augram/augram.json` (to `augram.before-0.13.json`). The new build migrates the config from schema 4 to 7 and the sync format to 15.
2. Pull. Then `~/.dotnet/dotnet build` and `~/.dotnet/dotnet test`. Fix anything red on macOS before going on.
3. Joel quits Augram. Run `node scripts/package.mjs mac --signed` (the only form he allows). Joel installs the `.pkg` and starts Augram from Applications.
4. Confirm the log's `App started` says 0.13.6. Sync then resumes on its own: the "uses a newer Augram" pause ends once the Mac reads sync format 15. Read the log for `sync`, `ignore` and `hold` warnings.
5. Joel runs the Mac checks below.

The Mac goes straight from 0.7.0 to the current build. After installing, run the "Done when" checks of [plan 0004](plans/completed/0004-drag-distance-and-not-in.md) (step 4), [plan 0005](plans/completed/0005-button-triggers.md) and [plan 0006](plans/completed/0006-glass-look.md); each names its Mac points. Plan [0009](plans/0009-search-and-filter.md) (search and filter) has no Mac-specific code, but give it a quick look on the Mac. The points most likely to need a decision:
- **The magnifier: done (Joel, 2026-10-12).** Joel made a Mac-only command for Eyeris's magnifier (`Global › Eyeris Magnifier › magnifier`, Right + Left); it fires on the Mac (log 18:23, about 40 ms). The note below is kept for the record.
  **The magnifier** (Global "magnifier": Button Left while holding Right, a Remap step Shift + Win + X, Also in Blender) syncs over with no Mac version. If the Mac's stroke button is Right, nothing is held back there; otherwise Right is held back on the Mac too. Its key output becomes Shift + Cmd + X: check what Eyeris's Mac shortcut is.
- **Spine and Eyeris** Per command entries were made from Windows windows and hold Windows names only; they match nothing on the Mac until macOS names are filled in (the magnifier on a Spine window). Ask Joel whether he uses them on the Mac.
- **Never seen on a real desktop yet:** the Add app… magnifier inside the modal Not in dialog, and a Used by link opening its command. The glass, title-bar drag, double-click and Options › Appearance live (plan 0006) on macOS.
- Read the log for `sync`, `ignore` and `hold` warnings: `~/Library/Application Support/Augram/logs/augram-<yyyyMMdd>.log`.

When Joel calls a plan done on both machines, move it to `docs/plans/completed/` and fix its links.

## 2. Open, awaiting Joel

- **[Plan 0010](plans/0010-platform-step-lists.md), PROPOSED 2026-10-12 (Mac session):** revised the same evening (Joel dropped Unified): a view-only "Steps for: Windows · macOS" switch; the platform you are on runs and is drawn so, the other side is a read-only peek; a side is its own or inferred (converted from the other); Copy from the other platform replaces the side in view; the other platform read-only on every "Fields for" switch. No format change. Waits for Joel's go-ahead and two small wording questions.

- **The magnifier holds Right back everywhere at the Options value (10 px)**, so a right-drag in Spine and Eyeris waits 10 px. Two fixes offered (the magnifier's own drag distance 3, or Spine in its Not in); not chosen. Eyeris and Spine also drop the first pixels of a drag themselves ([learnings 0006](learnings/0006-other-programs-on-the-mouse.md)).
- **Start at login has never run on a real login** (off on the PC). Check lists: [Hosting README](../src/Augram.App/Hosting/README.md) "Windows check list", Platform.MacOS README "Start at login on macOS".
- **Export/import (plan 0003, PROPOSED):** "Decided for Joel" and "Questions for Joel" wait for him; his check list is plan 0003 step 5 (throwaway config folders, `--config-folder … --no-engine`). Follow-ups: plan 0003 "Follow-ups from Joel's questions".
- **Sync conflicts show only in Options › Sync.** Proposed: "· 1 conflict" in the tray tooltip and Resolve… in the tray menu.
- **Augram stops while Task Manager has focus** (elevated windows); "later" (requirements §4 has the cause and two fixes).
- **Debug logging cannot be switched on** in any build (`ChannelEventLog.MinimumLevel` is set by nothing); Joel: not needed now.
- **Dialog headings sit right on their panel** (the gesture picker, sync conflicts; since plan 0007 S19 every dialog shares one shell). A gap is one setter in `Themes/Default/Lists.axaml` and changes every dialog with a heading; Joel has not said.
- **Is the refactor making small requests faster?** Measure the first few feature requests with `node scripts/agent-time.mjs` against plan 0007's close-out numbers.
- **Older questions:** may his 105 gestures become a test fixture in the public repo (never without his yes); Avalonia 12 / SharpHook 8 (deferred); Gestures tab Undo/Redo buttons keep or drop; Mac window extras (Center, snap halves, set size).
- **One app, defined once (Joel, 2026-10-11, backlog: after the refactor and the Mac).** Exclusions › Global and › Per command each hold their own app definitions, and names must be unique across both lists, so Joel's Per command "Blender" and "Spine" are stuck as "Blender 2" / "Spine 2" beside the Global ones. And re-registering Blender's exe in one place does not reach the others: the same app is defined up to three times (Global exclusion, Per command exclusion, the Apps app group), each with its own matcher. (1) **done as 0.13.4 (Joel, 2026-10-12: "the small fix is enough")**: names are unique per list, not across both, so the Per command "Blender 2" can be renamed "Blender" (sync format 15; a move between the lists takes the first free suffix when the name is taken there). (2) remains: an app is defined once and exclusions and app groups refer to it, so one edit reaches every use; it changes the config schema and sync format, so it is its own plan once the Mac runs the current build.
- **Idea, not scheduled (Joel, 2026-10-11): one gesture that means player fullscreen over a YouTube video and window maximize elsewhere in Chrome.** Needs the UI element under the cursor via the accessibility tree (UIA / AX); an extension is ruled out. Findings and what to measure first (the cost to Chrome): [reference/element-under-cursor.md](reference/element-under-cursor.md).
- **After the refactor and the Mac:** the rest of plan 0001 M2 and M3; ask Joel for the order.

## 3. Read in this order

1. [CLAUDE.md](../CLAUDE.md): ground rules (including the turnaround rules and agent effort levels), the architecture invariants, the read-first table.
2. [reference/one-home-guards.md](reference/one-home-guards.md): which type is the one home for which job; the folder READMEs say the same per folder.
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
