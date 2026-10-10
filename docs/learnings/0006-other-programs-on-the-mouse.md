# Learnings 0006: other programs on the mouse (Eyeris's chord, foreign releases, drag dead zones)

2026-10-10/11, PC, plan 0005. What it took to find a stuck button another program caused, how Augram now copes with other programs' input, and how to tell Augram's right-drag lag from an app's own. Read before touching the hook's handling of simulated input, the hold-back of anchors, or when Joel reports a dead zone or a "held" button.

## 1. Reading a stuck button from the log

**Symptom (Joel's log, 2026-10-10 19:36 and 19:37):** after Eyeris's loupe chord (hold Right, click Left), every Middle stroke resolved as "Right + gesture '/Up'" and wheel triggers as "Stroke + Right + wheel up", with no command. One plain right-click cleared it.

**How it was read:**
- `hook` / `Button ignored {button=2 direction=up reason=simulated}` with **no** `direction=down … simulated` before it in the same 30 s window. A hand-back by Augram injects a down first, which would have been logged (each button, direction and reason logs at most once per 30 s, so a missing line inside that window is evidence). So Augram had not held Right back and handed it on.
- Strokes still started. A press stuck in Held or HandedBack would have stopped Middle strokes (another button hands back or passes). So the machine was Idle with Right in its held set: Right's down had **passed** and its release never arrived.
- Why it passed: Eyeris was a Per command entry in Zoom In/Out's Not in, so Right was no anchor over Eyeris's window. Read the live config's exclusions before trusting a theory (`%APPDATA%\Augram\augram.json`, `mapping.ignored`, a command's `notIn` / `alsoIn`).

The external write-up we were handed said "Augram holds Right back as an anchor press". That was wrong for the window in question. Check the plan the window actually had.

## 2. Other programs' low-level hooks

- **Order:** low-level hooks run newest first. A program that starts after Augram sees the input first and can swallow it; one that started before sees what Augram passes. Eyeris installs its hook (`eyeris-chord.exe`) whenever it starts, so either order happens.
- **The pattern that hurts:** swallow the real release, inject a synthetic one to keep the OS consistent. Augram dropped every simulated button event (so its own replays are not captured again), so it never saw the button go up.
- **No tags:** SharpHook 7.1.3 exposes no `dwExtraInfo` (searched `SharpHook.xml`), so another program's marker ('EYEC') cannot be read. Tell Augram's own injections apart instead: the simulator announces each release it posts (`OwnButtonInjections`, the same pattern as `OwnWheelInjections` for Scroll notches), the macOS remap wrapper too; a failed post withdraws; announcements expire after 1 s. An unclaimed simulated release is another program's (`RawInputKind.ButtonReleasedElsewhere`).
- **What a foreign release may change:** what Augram thinks is held, never what it owes. The button leaves `_down`; a press it owns ends quietly (no replay, no trigger, no second release after a hand-back); the owner stays owed, so a real release that still comes is swallowed and the OS never gets two (A19). The shadow ends the press on the hook thread, the machine on the worker.
- **Without the claim, a replay could end the next press:** Augram's replayed click's release arrives after the physical release; a quick second press could be owned by then. Hence the announce-and-claim, not "ignore releases while Idle".
- **Not covered:** a hold remap input whose release another program swallows (the button output would stay held). Plan 0005 "Not in this plan"; Eyeris's chord is off on Joel's machines.

## 3. Pass first, or hold back

Eyeris lets Right through at once and cleans up at the chord (a synthetic release; the real release swallowed). Augram holds an anchor back until it knows (replay at release, hand back on a drag or a long press). Seen on Joel's machines:
- Windows opens a context menu on the **release**, so pass-first looks free there.
- macOS opens it on the **press**: with Eyeris's own chord the menu was already open when Left came. Holding Right back fixes that.
- Pass-first needs the fake release that caused section 1.

Requirements F1 already holds pass-first off; this is the evidence from another app.

## 4. "Dead zone" reports: whose is it?

- **Is Augram's hook in at all?** "Closed" can mean "in the tray". Check the process list and the log: `hook` / `Hook stopped`, `app` / `App started`. On 2026-10-11 Joel's test with "both closed" really was without Augram (hook stopped 00:46:33–00:48:05), and the dead zone stayed.
- **Windows' own drag threshold** is `SM_CXDRAG` / `SM_CYDRAG`, 4 px by default (`HKCU\Control Panel\Desktop` `DragWidth` / `DragHeight`); many apps use it to tell a click from a drag.
- **Eyeris** waits `PAN_THRESHOLD_PX = 4` (`eyeris/src/renderer/viewport/input.js`) and drops the movement before it, so the image lags the cursor by about 5 px for the whole drag (a note went to its agent: pan by the distance from the press point on the crossing move).
- **Spine** has its own: Joel saw the same dead zone with Augram quit and on a **left**-drag with the H tool. Spine is built on libGDX, whose `DragListener` starts a drag past `tapSquareSize = 14` and drops the movement before it (inferred: the editor's source is closed; no setting changes it; Settings › Behavior › Middle mouse pans and the Pan Drag / Pan Move hotkeys avoid the right-click ambiguity).
- **Augram's part:** over a window, an anchor's press is handed back at the **largest** drag distance of the commands holding that button there, a command without its own counting as Options › Capture's value (10 px default). Adding a command that holds a button (the magnifier, Right + Left) brings the hold-back to every app it applies to, including apps an earlier command kept out with Not in (Zoom In/Out were Not in Spine and Eyeris). When adding such a command, check its Not in and drag distance; Joel settled on 3 px.

## 5. Tests

- **Random-sequence coverage:** a rare path (a button trigger fires only when an anchor is held back, the plan fires the next button and nothing hands it back first) fires a few dozen times where common paths fire hundreds. Assert that every state is reached, and set a count threshold to what the generator produces, with a comment saying why; widen the generator (a long drag distance in half the sequences) rather than weakening an invariant.
- **Engine tests over an excluded window:** the ignore watch's first pass, before the window is known, publishes Global's plan, which may already satisfy "Right + Left fires". Wait for `Host.IgnoredUnderPointer` to name the entry as well, or the first press is decided as over no excluded app.

## 6. Agent tooling on Windows

- PowerShell 5.1 splits a `git commit -m` argument at embedded double quotes ("Works when the app is excluded" became pathspecs). Write the message to a scratch file and `git commit -F <file>`.
- After a merge, `dotnet build-server shutdown` releases the locks a worktree's build left (an Avalonia `AVLN9999` "file in use" on `obj\…\Augram.App.dll` otherwise).
