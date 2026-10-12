# Plan 0008: keys lost on the window that comes forward after a Minimize

Status: **DONE on the PC, 0.12.1 (2026-10-12)**; Joel's acceptance check is in the handoff. Reported by Joel 2026-10-11 during plan 0007; measured and fixed 2026-10-12. The Mac is untouched (its `Activate` asks the Accessibility API whether the app is focused, a different rule).

## What Joel saw

PotPlayer was open at about a quarter of the screen, with Chrome (and later Explorer) beside it. Joel minimized Chrome with the `/Down` gesture, then drew `/Up` (PotPlayer › Fullscreen, a **hotkey step**) over PotPlayer. Nothing happened, gesture after gesture. It worked only after he clicked the desktop or inside the player first. Minimizing Chrome with its own button instead of the gesture made no difference. In SP.net the same sequence works.

## What the log showed (`%APPDATA%\Augram\logs\augram-20261011.log`, 23:24)

```
23:24:22.180 Trigger resolved  /Down → Global › Minimize   process=chrome.exe
23:24:22.915 Trigger resolved  /Up → PotPlayer › Fullscreen process=PotPlayerMini64.exe
23:24:22.915 Window activated  technique=not-needed focusMoved=false          ← key sent, nothing happened
… the same four more times, all not-needed …
23:24:37.689 Window activated  technique=set-foreground focusMoved=true       ← after Joel clicked the desktop: works
```

The target was right every time (`CommandExecutor.Execute` → `WindowAt(request.Start)` gave PotPlayer). What failed was the "is it already in front?" check: rule A20 (`ActivationPolicy.NeedsActivation`) compared the *root owner* of `GetForegroundWindow` with the target's root owner, found them equal, and skipped activation.

## What the measurement showed

A throwaway console probe (no hook, no overlay; deleted, not committed) read `GetForegroundWindow`, its `GA_ROOT` and `GA_ROOTOWNER`, and `GetGUIThreadInfo` (`hwndActive`, `hwndFocus`) four times a second while Joel repeated the sequence with Chrome's own minimize button:

```
05:38:00.958 fg=3BF1046[Chrome_WidgetWin_1|'…Google Chrome'|chrome]  focus=3BF1046            ← Chrome focused
05:38:03.585 fg=C710CC[Afx:00007FFB…|''|PotPlayerMini64|vis] fgRootOwner=5717D4[PotPlayer64|'24.S02E10…']
             active=C710CC focus=C710CC                                                        ← after the minimize
05:38:10.405 Augram: /Up → Fullscreen, Window activated technique=not-needed                  ← key to C710CC: nothing
05:38:14.616 fg=20250[Progman]                                                                 ← Joel clicked the desktop
```

After the minimize, Windows gave the foreground to **the next top-level window in the stacking order**: PotPlayer's title-less `Afx:…` helper window (control bar or OSD), *owned by* the main window and therefore above it. Foreground, active and focus were all on the helper. Its root owner is the main window, so the root-owner comparison called the main window "in front" while every injected key went to the helper. This is the same shape as the find dialog A20 protects (an owned popup of the target has focus), with one difference that tells them apart: the gesture was drawn over the **main window**, not over the popup.

## The fix (`Augram.Platform.Windows`, 0.12.1)

- **A20 compares top-level windows (`GA_ROOT`), not root owners.** `Win32WindowSystem.Activate` takes the top-level window under the gesture start (`GA_ROOT` of `target.Handle`, `RootHandle` when the handle is gone) and the top-level window in front, and skips activation only when they are the same window, when the target is the desktop, or when the target is disabled (`IsWindowEnabled` false: one of its own modal dialogs holds the input, and `SetForegroundWindow` on the owner would only bounce back to it; the old rule handled this case silently and the new one must too).
  - Gesture over the focused find dialog: both are the dialog → not needed. A20 kept.
  - Gesture over PotPlayer's main window while its helper is in front: different windows → activate the main window. The bug.
  - Gesture over an owned dialog that is not focused: the dialog is brought forward, not its owner, so the keys go where the stroke began.
- **`ForegroundActivator.Activate(target, window)`** brings that top-level window forward and counts as settled only when `GA_ROOT` of the foreground is `window` itself, not a window it owns (the blind spot), with the modal exception above. `Activate(target)` alone keeps bringing `RootHandle` forward for the Open app step, whose window is an unowned top-level one.
- `IWin32Windows.IsEnabled`, `WindowHandles.RootOrSelf`; `FakeWin32.Disabled`.
- Tests: `ActivationPolicyTests` (seven cases incl. the helper, the focused find dialog and the disabled target), `Win32WindowSystemTests` (the PotPlayer tree, the owned dialog, the modal case), `ForegroundActivatorTests` (an owned helper in front is not settled; a modal dialog of a disabled target is).

Rejected: minimizing through `WM_SYSCOMMAND SC_MINIMIZE` like the minimize button (Joel tried the button itself and the bug stayed); a focus-aware check through `GetGUIThreadInfo` at root-owner level (the measurement showed focus *was* in the root owner, on the helper, so it would not have helped).

Why SP.net works with the same rule in words: its "foreground root" is most likely the top-level window, which is what Augram compares now.

## Follow-up, 0.12.2 (2026-10-12): the delay Joel noticed

Joel: "it works, but is there a fraction of a delay?" The log said yes: `Window activated technique=alt-tap elapsedMs=309`. Two causes, two changes:

1. **Each failed technique took 153 ms, not 50.** `SettledOn` counted ten 5 ms sleeps as 50 ms, but a Windows sleep rounds up to the 15.6 ms timer tick. The budget is now measured with a `TimeProvider` clock (`ForegroundActivator` takes one; tests use a fake the fake sleep advances). Worst case is now the 150 ms the README always claimed; Joel's case drops to ≈105 ms.
2. **Owned tool-window and no-activate popups stand for their owner** (`Win32WindowSystem.ActivatableTopLevel`). The self-triggering probe (acts when the top-level under the pointer differs from the one in front while both share a root owner and a process) fired twice on the wrong thing and both runs taught the same lesson: a Chrome tab hover card (`WS_EX_TOOLWINDOW`, owned) and an Avalonia flyout (tool window, topmost, owned) under the pointer while their owner was in front, and **no technique could bring either forward** in 100 ms (`SetForegroundWindow` returned true and moved nothing; `SetActiveWindow`, `BringWindowToTop`, `SwitchToThisWindow`, the Alt tap: nothing). Without this rule, every gesture over a tooltip would have cost 150 ms and a Warning. The keys drawn over such a popup are meant for its owner.

Also seen: the same owned-popup-in-front shape in Chrome (a title-less `Chrome_WidgetWin_1` owned by the main window held the front while the pointer was over the page), where the plain `SetForegroundWindow` settled in 0.9 ms, and the focus followed only ~300 ms later. So the shape is general, not PotPlayer's alone.

**Open:** which technique brings PotPlayer's main window forward *fast* when its helper is in front. On 0.12.1 `set-foreground` and `attach-thread-input` both failed there and only `alt-tap` worked (in 3 ms). The probe's last run was limited to PotPlayer (`auto` mode with the techniques `setfg,attach-setfg,attach-active,bring-top,switch,alt-setfg`) but timed out before Joel ran the sequence. If the remaining ≈105 ms bothers him, rebuild the probe (its design is above; no hook, no overlay), get one PotPlayer run, and order or add a technique for the same-process case from what it shows.

## Done when

Joel's sequence works on the PC with no click in between: PotPlayer at a quarter of the screen, Chrome beside it, `/Down` on Chrome (and once with Chrome's own minimize button), `/Up` on PotPlayer → fullscreen. The log shows a real activation technique instead of `not-needed`. Also check: Notepad++ Find dialog open and focused, a hotkey gesture over the Find dialog still types into it (A20).

## The Mac

Not seen there. `MacWindowSystem.Activate` asks whether the target app is focused through the Accessibility API, which does not have this blind spot in the same form. After the Mac runs the current build, try the same sequence once (minimize a window, then a hotkey gesture on the one that comes forward) and read what the log says before changing anything.
