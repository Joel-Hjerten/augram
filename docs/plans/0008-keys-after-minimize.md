# Plan 0008: keys lost on the window that comes forward after a Minimize

Status: **PROPOSED, after the refactor** (Joel reported it 2026-10-11, during plan 0007; no code changed). A bug fix, not a feature. File and type names are the ones on main at `8978606`. The refactor may have moved them, so look them up again before starting.

## What Joel saw

PotPlayer was open at about a quarter of the screen, with Chrome (and later Explorer) beside it. Joel minimized Chrome with the `/Down` gesture, then drew `/Up` (PotPlayer › Fullscreen, a **hotkey step**) over PotPlayer. Nothing happened, gesture after gesture. It worked only after he clicked the desktop or inside the player first. In SP.net the same sequence works: the command lands on the window where the stroke began, whatever had focus.

## What the log shows (`%APPDATA%\Augram\logs\augram-20261011.log`, 23:24)

```
23:24:22.180 Trigger resolved  /Down → Global › Minimize   process=chrome.exe
23:24:22.915 Trigger resolved  /Up → PotPlayer › Fullscreen process=PotPlayerMini64.exe
23:24:22.915 Window activated  technique=not-needed focusMoved=false          ← key sent, nothing happened
… the same four more times (23.8, 24.6, 25.1, 26.4), all not-needed …
23:24:35.387 Trigger resolved  /Down → Global › Minimize   process=explorer.exe
23:24:36.011 Window activated  technique=not-needed focusMoved=false          ← nothing again
23:24:37.689 Window activated  technique=set-foreground focusMoved=true       ← after Joel clicked the desktop: works
```

The target was right every time: the window under the gesture start was PotPlayer (`CommandExecutor.Execute` → `WindowAt(request.Start)`). So Augram already does what SP.net does there. **What failed is the "is it already in front?" check.** Rule A20 (`ActivationPolicy.NeedsActivation`) compares only the *root owner* of `GetForegroundWindow` with the target's root owner. After the minimize, Windows had made a PotPlayer window the foreground, so the roots matched and Augram skipped activation. But PotPlayer did not have keyboard focus, so the injected key went nowhere. The click (or Augram's real `set-foreground`, once the desktop held the foreground) is what put focus inside PotPlayer.

## Why the foreground and the focus disagree (measure first)

When a window is minimized, Windows activates "the next window in the Z order". That activation does not come through PotPlayer's own input, and it leaves PotPlayer as the foreground window with **no focused window in its thread**, or with focus on a PotPlayer window that ignores keys. **It does not depend on how the window was minimized.** Joel retried with Chrome's own minimize button (2026-10-11, 23:29:53 and 23:31:43 in the log): same `not-needed`, same dead key. So the fix belongs in the in-front check, not in Augram's Minimize. Two variants fit the log, and step 1 tells them apart:
- (a) foreground = PotPlayer's main window, but `GUITHREADINFO.hwndFocus` is 0 or not a key-handling window;
- (b) foreground = another PotPlayer top-level window with the same root owner (a hidden owner, OSD or control bar), so the root comparison matches while the keys go to a window that drops them.

SP.net works in the same sequence although it has the same A20 rule. So either its root comparison differs (`GA_ROOT` vs Augram's `GA_ROOTOWNER`: under variant (b) a hidden PotPlayer owner would make Augram's roots match and SP.net's differ), or it does something after activation that Augram does not. Step 1's numbers decide which; no need to dig into SP.net beyond that.

## The fix (one measurement, one change)

1. **Measure (no behaviour change).** In the Windows activation path, when the policy says not-needed, log one Debug line with `GetGUIThreadInfo(foreground thread)`: `hwndActive`, `hwndFocus`, their classes and root owners, next to `GetForegroundWindow`. Reproduce Joel's sequence once and read which variant it is. (Debug logging can't be switched on in any build yet, see the handoff; either use Info for this one line during the check, or wire `MinimumLevel` first.)

2. **Fix the check, keeping A20.** Activation is skipped only when the foreground root matches the target root **and** keyboard focus is in it: `GetGUIThreadInfo(foregroundThread).hwndFocus` is non-zero and its root owner is the target root. Otherwise activate as for a different window. A20 still holds for its own cases: Notepad++'s find window or an XTranslate popup has focus *inside* the target's root, so activation is still skipped and the popup keeps focus.
   - If step 1 shows variant (a) and `SetForegroundWindow` on a window that is already foreground is a no-op, the activator needs one more technique for this case: `AttachThreadInput` to the target's thread and `SetFocus(target root)`, verified by reading `hwndFocus` again. `ForegroundActivator.SettledOn` checks the foreground root only and should also check focus, so "activated" means keys will land.
   - The A8 settle delay then applies, because focus moved.

Rejected: minimizing through `WM_SYSCOMMAND SC_MINIMIZE` like the minimize button. Joel tried the button itself and the bug stayed.

Order: 1, then 2. Use `effort: high` (Platform.Windows).

## Tests (sized to the change)

- `ActivationPolicy`: foreground root equals the target root, focus 0 → needs activation; focus in a popup owned by the target → not needed (A20 kept); focus in another root → needs activation.
- `ForegroundActivator`: "settled" requires the focus check (fake `IWin32Foreground` gains the focus read).

## Done when

Joel's sequence works on the PC with no click in between: PotPlayer at a quarter of the screen, Chrome beside it, `/Down` on Chrome (and once with Chrome's own minimize button), `/Up` on PotPlayer → fullscreen. The log shows a real activation technique, or not-needed with focus already in PotPlayer. Also check: Notepad++ Find dialog open, a hotkey gesture over Notepad++ still types into the Find box (A20).

## The Mac

Not seen there. After the Mac runs the current build, try the same sequence (minimize a window, then a hotkey gesture on the one that comes forward). macOS minimizes through `AXMinimized`. Whether the next app becomes key there is a separate question; record what the log says before changing anything.
