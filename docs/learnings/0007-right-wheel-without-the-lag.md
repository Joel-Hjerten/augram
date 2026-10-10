# Learnings 0007: Right + wheel without the lag (plan 0004)

Session 2026-10-10, PC. Joel's Global Zoom In/Out were Right + wheel, so every right press on the PC was held back. Right-drags lagged in Spine and Eyeris, which pan with Right. What came out of it, and what to read before reopening any of it. The other programs on the mouse, and whose dead zone it is, are in [learnings 0006](0006-other-programs-on-the-mouse.md).

## 1. Where the lag came from

An anchor (a button other than the stroke button held back for a trigger) was handed back at the **stroke's start distance**, 30 px, and after the **cancel delay**, 1 s. Neither was ever meant for it: an anchor never draws. On Windows a plain right-click costs almost nothing under hold-back. The click is replayed at the release, and Windows opens the menu on the release anyway. Drags and long presses paid the cost. The fix was a distance of its own: Options › Capture › Button drag distance (10 px), and a command's own distance. Over one window the largest distance wins, because the press is decided before the wheel says which command it is. Read the hand-back numbers first when an anchor feels slow.

## 2. Letting the press through: held off, and what reopening it takes

Joel's idea was to let Right's down reach the app at once, watch, and on a wheel tick fire the command and keep the release's menu away. He held it off (requirements F1). Nothing below was spiked; the facts are from the docs and from reasoning, unless they are also in learnings 0006 §3.

- **The release must always reach the app.** If the down passed, swallowing the up leaves the button down in the OS's state, and the app keeps the mouse capture it took at the press. Every later click then goes to that window: the "mouse taken away" class of 2026-10-06. So the only question is how to get rid of what the release causes.
- **Windows opens the menu on the release** (`DefWindowProc` turns `WM_RBUTTONUP` into `WM_CONTEXTMENU`; Chrome, Firefox and Explorer act on the up too). A cleanup would watch for a few hundred ms after the release and send Escape only when a menu appears:
  - Win32 menus raise `EVENT_SYSTEM_MENUPOPUPSTART`.
  - Chromium, WPF and Electron menus are popup windows of the same process. Whether Chromium raises the WinEvent without an assistive technology attached is unknown.
  - Menus a page draws itself (Google Docs, Figma) cannot be seen at all.
  - Never send Escape blind: it leaves full screen and closes dialogs.
  - The menu flashes before it closes.
- **macOS opens the menu on the press.** `NSMenu`'s tracking loop then takes the keyboard, so a zoom hotkey posted while the menu is open never reaches the app. The cleanup would be Escape at the first notch, wait until the app's pop-up menu window is gone, then the command. A pop-up menu window sits at layer 101 in `CGWindowListCopyWindowInfo`, owned by the app's pid. It looks like the cleaner platform; it was not tried.
- **Only Right has a side effect that can be undone.** What the release triggers for the other buttons cannot be taken back:
  - Left: the click lands.
  - Middle: a link opens in a tab, and Chrome starts autoscroll at the press.
  - X1 and X2: Back or Forward on the release.
- **Learnings 0006** adds a third reason: Eyeris works pass-first, and its fake release is what left Right stuck in Augram.
- **Before building it,** spike it in Chrome, Explorer, VS Code and Finder:
  - when the menu appears;
  - whether detection sees it;
  - whether Escape closes it cleanly with the zoom still landing;
  - how long the flash is.

  The spike installs a real hook, so only the lead runs it, after telling Joel.

## 3. Two format changes in one evening

"Not in" first named **app groups** (0.8.0: config schema 5, sync format 12). After seeing it, Joel wanted it on the ignore list instead (Ignored, now Exclusions, › Per command; 0.9.0: schema 6, format 13). Each change pauses sync on the other machine until it updates. The second change was required, not cosmetic: a 0.8.0 build would read a Per command entry as an ignore of the whole app and switch Augram off over it.

**Lesson:** before persisting a new list that points at apps, tell Joel in words where it lives and what it is picked from, or show the gallery page. Raise the version only after he has seen it. A choice of identity, such as app groups versus the ignore list, is his.

## 4. Check the named command before building

Joel asked for a per-command distance for "volume up and volume down". `augram.json` showed Volume is stroke button + wheel, where a per-command distance cannot apply (the stroke button's movement starts a gesture). Asking caught that he meant Zoom. When a request names a command, read its trigger in the config first. His words describe intent, and sometimes he names the wrong command.

## 5. Running agents in parallel

Plan 0004 ran as three rounds, each with the same split. It caused no merge conflicts.

- **The lead writes the shared model first:** Core types, rules, resolver, planner, the engine wiring and their tests.
- **Then two worktree agents run in parallel,** on folders that don't overlap:
  - persistence: `Core/Config`, `Core/Sync`, `Core/Transfer` and their tests;
  - the App: `src/Augram.App` and its tests.
- **When the model change breaks the other layers' tests, commit it locally and don't push.** Main stays green. Merge both agents' branches, run everything, then push once.
- **A worktree agent starts from origin's main, not from the local HEAD.** The step 6 agent's worktree began at the pushed `712dcaa`, while the model commit `a1f4237` existed only locally. Every prompt names the base commit and says: "check `git log`; if it is missing, `git merge --ff-only <sha>`". That line saved the round.
- **A detail Joel adds while an agent runs** (he wanted the Swap / Take it buttons inside the red note) reaches it through SendMessage. It does not need a new agent.
- **Clean up after each merge:** `dotnet build-server shutdown`, `git worktree remove --force`, `git worktree prune`, `git branch -d`. Leftover worktrees from older sessions stay until their owner removes them.
