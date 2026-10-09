# Learnings 0003: trigger modifiers and mouse-button chords (how StrokesPlus.net handles them)

**Status: RESEARCHED 2026-10-09** (PC session, worktree agent), before building F1's trigger modifiers. Joel's decision is "match what StrokesPlus.net does" (requirements F1); this doc pins down exactly what that is. The same day Joel widened the scope to mouse-button chords. His design: a trigger is a gesture, a wheel tick (up or down) or none, plus a "while holding" set. That set holds mouse buttons (the stroke button, Left, Right, Middle, X1, X2) and keys (Ctrl, Alt, Shift, Win). For a wheel trigger the stroke button is ticked by default and can be unticked, e.g. "Right + wheel up = zoom in". §2.7 and §4 cover it. No code was changed and nothing was launched.

**Sources, strongest first.**

- **SP.net 0.5.8.0, the version Joel runs** (`C:\Program Files\StrokesPlus.net`, FileVersion 0.5.8.0; the same build is the portable zip in the official archive). It is a .NET assembly without obfuscation. It was decompiled with ILSpy into a temp folder outside the repo and read; nothing was copied. Cited as `net:Type.Member`, e.g. `net:ApplicationMatching.ActionMatched`. Where the code and a forum post disagree, the code wins. To repeat it: `dotnet tool install ilspycmd --tool-path <temp>\tools`, then `<temp>\tools\ilspycmd -p -o <temp>\out StrokesPlus.net.exe`. Keep the output out of the repo: SP.net's licence reserves all rights beyond use and redistribution.
- **Classic StrokesPlus C++ source** (MIT), `StrokesPlusHook/StrokesPlusHook.cpp`, cited as `cpp:<line>`. This is SP.net's ancestor. §2.9 lists where the two differ.
- **Forum archive and change logs** from strokesplus.net (`roblarky/roblarky.github.io`): SP.net forum topics `[N]` (`data.json`), classic forum topics `[cN]` (`cdata.json`), SP.net `ChangeLog.txt` as `net x.y.z.w`, the classic change log as `classic x.y.z`.
- **SP.net settings UI** (`HTML/js/strokesplus-net-actioneditor.js`, 0.5.6.5 and 0.5.8.0) and the factory config `Default_StrokesPlus.net.json` (0.5.6.5 portable).
- **Joel's live config**, `%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`, read on 2026-10-09. Only counts and setting values were taken from it.

## 1. Short answers

| # | Question | SP.net behaviour | Confidence | Source |
|---|---|---|---|---|
| 1 | Matching | **Exact set match.** An action with Control fires only when Control was registered. An action with no modifiers fires only when *no* Ctrl, Alt or Shift was registered, so a plain gesture drawn with Shift held fires nothing unless a Shift action exists. For a plain/Shift pair on one gesture: with Shift the Shift action fires, without it the plain one. | Verified in SP.net code; forum agrees | `net:ApplicationMatching.ActionMatched`; [7091], [8420] |
| 2a | Before / After / Either | **Before** means the action's keys were held when the stroke button went down and *nothing* was pressed after. **After** means they were pressed while the stroke button was held and *nothing* was held at button-down. **Either** means the union of the two equals the action's set. The JSON stores `Capture`: 0 Before, 1 After, 2 Either. A new action defaults to Either. | Verified in SP.net code | `net:ApplicationMatching.ActionMatched`, `net:ModifierCapture`, `net:ActionList` (add action) |
| 2b | Is an After key consumed? | **Yes.** While the stroke button is held, every Ctrl/Alt/Shift key-down is consumed and recorded, auto-repeats included. The key-up is consumed while the stroke button is still held. The app never sees an After key. A Before key is **never consumed**: the app received its key-down before capture began. Only its auto-repeats during the hold are eaten, and its key-up passes through. | Verified in SP.net code; forum and classic agree | `net:KeyboardHook` (hook callback); [c302]; classic 2.0.13; [8252], [8422] |
| 2c | `CaptureModifiersOnMouseDown` | The switch that samples the Before set from the key state at stroke-button-down. When off, keys held before the press are invisible and only After presses count. Default is on (Joel: on). | Verified in SP.net code | `net:MouseHook` (stroke-button down), `net:Settings`; classic help |
| 2d | Modifiers for a wheel action | The Before set comes from button-down; After presses count until the **first tick**. From the first tick on the set is frozen: every later tick fires with it, and later key presses are neither recorded nor consumed (they reach the app). | Verified in SP.net code | `net:MouseHook` (wheel branch), `net:KeyboardHook`, `net:HookState.FireGestureThread` |
| 3 | Ignore key vs action modifier | An ignore key held at stroke-button-down passes the button, and every mouse event until the key is released, straight through. The same key pressed *after* the button went down is an ordinary After modifier and is consumed. So one key can be both, but only as an After (or Either-via-After) modifier. A Before action using an ignore key can never fire. | Verified in SP.net code; forum agrees | `net:KeyboardHook`, `net:MouseHook`, `net:ActionEditor.cboCaptureModifiers_SelectedIndexChanged`; [8390], [8338] |
| 4 | Fallbacks | **None.** There is no fallback to an action with fewer modifiers. The search order is the first matching app's actions in list order, then Global's actions in list order, unless the app has No Global Actions. The exact-set rule applies at both levels, so a plain `\Up` override in Chrome does not shadow a Shift+`\Up` in Global. | Verified in SP.net code | `net:ApplicationMatching.ActionMatched`, `net:ApplicationMatching.ApplicationMatched` |
| 5 | Modifier-only trigger | Fires on **stroke-button release** when the pointer stayed inside the start distance, no wheel tick happened and at least one modifier was registered. It matches actions with no gesture and the exact set. If nothing matches, **the click is swallowed**: SP.net's relay skips strokes of one point. That is why the factory config ships the "Shift+Right Click" action, which re-sends the click. | Verified in SP.net code; forum agrees | `net:MouseHook` (stroke-button up), `net:ActionFunctions.RelayGesture`; [8319], [8390], [c1280] |
| 6a | A non-stroke button + wheel | Only **while the stroke button is held**. Left/Middle/Right/X1/X2 are flags in the same set as the keys, under the same Before/After/exact rule. A button pressed after the stroke button has its down consumed. Its up is consumed only while the capture is still in its pre-wheel phase. After the first wheel tick, or once the stroke button is up, the up passes to the app as an unpaired up. A Right button pressed *before* the stroke button reaches the app in full, so its menu opens [8639]. The only way to own Right and keep its menu away is to make it a **(secondary) stroke button**. Then its down is held back, a quick click is replayed at release, and the up is swallowed when something fired. Joel's own "Right + wheel = Zoom" commands were built exactly that way (§2.7). | Verified in SP.net code; forum reports | `net:MouseHook`; [8639], [8701], [3010], net 0.3.2.2 |
| 6b | Keys + wheel, no button (Ctrl + wheel) | **Not an action trigger.** Every action needs a held stroke button (primary or secondary). The only alternatives are touch, a script's manual capture, or the single global "Wheel Tick" script, which Rob called slow [7117]. | Verified in SP.net code | `net:MouseHook` (wheel, `MouseWheelScript`), `net:HookState.FireWheelTick` |
| 6c | Two buttons + wheel; order | Order counts only relative to the **stroke button** (Before vs After). The order of two After buttons is not recorded. With primary and secondary stroke buttons, the one that goes down first owns the press and picks the action set; the other becomes an After button. | Verified in SP.net code | `net:MouseHook`, `net:ApplicationMatching.ActionMatched`; net 0.3.2.2 |
| 6d | Gesture + extra held button | **Yes.** The gesture must match and the button flag must match exactly, e.g. "Up + Left held". | Verified in SP.net code; forum | `net:ApplicationMatching.ActionMatched`; [3015], [7228], [8252] |
| 7 | macOS | No SP.net evidence: SP.net is Windows-only and has **no Win modifier on actions** (Joel's design adds Win). Recommendation: physical keys, named as HotkeyText names them: Ctrl → Control, Alt → Option, Shift → Shift, Win → Command. No Ctrl ↔ Cmd conversion (§3.7). | Inferred | `src/Augram.Core/Steps/Hotkey/HotkeyText.cs`, requirements F1 (ignore keys) |

## 2. Details

### 2.1 What SP.net records per press

Each press keeps two sets over Ctrl, Alt, Shift and the five mouse buttons. Left and right Ctrl/Alt/Shift count as the same key. The Win key is not tracked.

- **B (Before):** sampled once, at stroke-button-down, from the key state, and only when `CaptureModifiersOnMouseDown` is on (`net:MouseHook`; `cpp:7990`).
- **A (After):** a key is added on its key-down while the stroke button is held and wheel firing has not started, unless the key is already in B (`net:KeyboardHook`; `cpp:8320`). The B check is the 0.2.0.2 fix: "a modifier After capture variable [became] true during key repeat … your Before action not being recognized" (net 0.2.0.2). A is **sticky**: releasing the key before the button-up does not remove it ("modifiers do not have to be held for the duration of the sequence, only performed once after the Stroke button is pressed", classic help).
- The wheel-up and wheel-down flags, and the stroke's point count. The count stays at 1 until the pointer moves beyond `MinGestureStartDistance` (Joel: 30 px).

Both sets are cleared at the end of every gesture. They are *not* cleared between wheel ticks or rocker clicks, which is why the wheel set stays frozen (`net:HookState.ClearCaptureVars`, `net:HookState.FireGestureThread`).

### 2.2 The match (`net:ApplicationMatching.ActionMatched`)

```
gestureKey = (pointCount <= 1) ? "" : recognisedName   // an unrecognised stroke matches no action at all
for each active action, in list order, with the same secondary-button flag (and region, if used):
    require action.GestureName == gestureKey
    require action.WheelUp == wheelUp and action.WheelDown == wheelDown
    Either: action.Keys == B ∪ A
    Before: action.Keys == B  and  A is empty
    After:  action.Keys == A  and  B is empty
    the first action that passes wins
```

`Keys` covers Ctrl, Alt, Shift and the mouse buttons. The search order runs first over the actions of **the first active application whose matchers all match**, in list order (`net:ApplicationMatching.ApplicationMatched`). If none matches there and the app has `NoGlobalActions` set, nothing fires. Otherwise Global's actions are searched with the same rule. If that finds nothing, the no-match path runs: sound, then `RelayGestureOnNoMatch`. Joel has both off.

Worked cases. Each assumes `\Up` in Chrome = close tab and Global `\Up` = close window.

| Held / pressed | Commands | What fires |
|---|---|---|
| nothing | Chrome plain `\Up` | Chrome's close tab |
| Shift before the press | Chrome plain `\Up` only | nothing: no Shift+`\Up` exists anywhere |
| Shift before the press | Chrome plain `\Up`; Global Shift+`\Up` (Either) | Global's Shift+`\Up`: Chrome's plain override does not shadow it |
| Shift before the press | Global Shift+`\Up` (After) | nothing: After requires B to be empty |
| Shift before, Ctrl after | Global Ctrl+Shift+`\Up` (Either) | it fires (B ∪ A = {Ctrl, Shift}) |
| Shift before, Ctrl after | Global Shift+`\Up` (Before) or Ctrl+`\Up` (After) | neither: Before needs A empty, After needs B empty |

Rob described the same rule while explaining why a "hold a key to un-ignore an app" feature would be ambiguous [7091]. With a global "Control modifier and L gesture" and a plain L, holding Control "would always fire the Control+L action … unless you made the action Capture Modifiers as After. But even then, you could have another global action which had Control+L and Before or Either for Capture Modifiers, and that one would win every time." And [8420]: "if you were holding a key like Control/Alt down, S+ would see the button being held which would then consider it as a modifier button being pressed, so you'd need to make a custom set of actions for the app that all had that key as a modifier."

The settings editor uses the same notion of "same trigger" for its conflict warning (`net:ApplicationMatching.ActionExists`). Two actions conflict when the gesture, the flag set and the secondary-button flag are equal and the capture modes overlap. Either overlaps everything, Before overlaps Before, After overlaps After, and the mode is ignored when no modifier is set. SP.net only warns; at runtime the first in list order wins.

### 2.3 Which key events are consumed (`net:KeyboardHook`, `net:MouseHook`)

| Event | When | SP.net does |
|---|---|---|
| Ctrl/Alt/Shift/Win key-down | stroke button not held | Passes. If the key is in the ignore list (and SP.net's own window is not in front), ignore mode turns on. The key is never consumed. |
| stroke-button down | ignore mode on | Passes, as does every other mouse event, until ignore mode ends |
| stroke-button down | otherwise | Consumed. Capture starts and B is sampled. |
| Ctrl/Alt/Shift key-down, left or right | held, no wheel tick yet | **Consumed**, auto-repeats included. Added to A unless already in B. Restarts the hold-still timer when "Reset on Movement or Modifiers" is on (Joel: on). Auto-repeats restart it too. |
| Ctrl/Alt/Shift key-up | held, no wheel tick yet | Consumed if the key is not in B; passes if it is in B |
| any key | after the first wheel tick | Passes and is not recorded |
| Win key, any other key | held, no wheel tick yet | Passes when Fire on Mouse Wheel is on (default, Joel). With it off, SP.net eats *every* key-down while the stroke button is held: a leftover. |
| keys SP.net sends itself | any | Pass and are never recorded (`SendKeysExecuting`) |
| any key | after the stroke-button up | Passes. An After key released after the button therefore reaches the app as an unpaired key-up, which is harmless. |

What the target app sees as a result. A **Before** key is down in the app while the action runs, so keystrokes the action sends arrive combined with it: Shift + gesture → "F5" reached Excel as Shift+F5. Rob's advice was to send the key-up first [7146]. An **After** key is invisible to the app. Classic said the same in 2012: "S+ is only recognizing the modifiers, NOT consuming them" (Before, [c302]). It also said that "S+ does not consume a modifier if the stroke button isn't down (otherwise you wouldn't be able to do anything with the mouse!)" (classic 2.0.13). For buttons, see [8252] (After: "the Left/Middle button down event is captured and consumed by S+, so the app doesn't see it") and [8422] (Before: "the app will still receive the left button down").

### 2.4 Wheel

The first tick while the stroke button is held sets wheel-firing mode (`net:MouseHook`). It copies the stroke's points so far, and stops the trail, the hold-still timer and modifier recording. Every tick then runs the match with:

- the wheel direction of this tick, the opposite flag cleared;
- the gesture key: "" if the pointer never left the start distance, otherwise the gesture recognised from the points before the first tick;
- the frozen B and A.

So the modifiers of a wheel action are "held at button-down, or pressed before the first tick". Classic stated it the same way in 1.7.7: actions fire on "the criteria at the time the mouse wheel was scrolled". Joel's volume commands (Global, no gesture, no modifiers) therefore do **not** fire when Shift is held. His inactive PotPlayer "Speed Up/Down" were Shift + wheel (Either).

### 2.5 Ignore key

- `IgnoreKeyList` holds any of Control, Alt, Shift, Win (several since net 0.2.8.2; Win since 0.3.3.0). The factory config and Joel's both have `[Control]`.
- Ignore mode ends on the key-up of the listed key that turned it on, at the end of a capture, and when the foreground changes while no listed key is held (`net:WinEventHook`; net 0.2.4.9, the "stuck in ignore mode" fix).
- Ignore mode is only turned on while the stroke button is *not* held. Pressed after the button went down, an ignore key that is Ctrl, Alt or Shift is an After modifier and is consumed; Win passes. Rob [8390]: "I had Control as my ignore key, so I didn't notice that was why Control+Right click was working fine". The workaround he offers is the ignore key or a passthrough action. Also [8338], about a scripted left-button ignore: "this would still allow you to use Left as a modifier, but only as an After modifier".
- The old WinForms action editor unticked and disabled the ignore keys' check boxes when Capture was Before (`net:ActionEditor.cboCaptureModifiers_SelectedIndexChanged`). The WebView2 editor (0.5.6.5, 0.5.8.0) has no such check, so such an action can be saved but never fires.

### 2.6 Modifier-only trigger, and Joel's "Shift+Right Click"

At stroke-button release (`net:MouseHook`; `cpp:8099`):

- If the pointer is still inside the start distance, no wheel tick happened and B ∪ A is empty, the click is replayed.
- If B ∪ A is **not** empty, the click is **not** replayed. The match runs with gesture key "".

If no action matches, `RelayGestureOnNoMatch` calls `net:ActionFunctions.RelayGesture`, which returns at once for one point. The click is gone either way. Hence [8319] (2021): "When I Shift-RightClick a file in Windows Explorer … nothing happens". The same happened in classic [c1280]. Rob's fix: "Make a global action, with Shift as the modifier, no gesture, and Capture Modifiers: Before … this action simply sends Shift+Right click directly". That action is in the factory config (0.5.6.5) and in Joel's: Global, active, no gesture, Shift, Capture 0 (Before), 87 fires. Its steps are ConsumePhysicalInput(true), MouseClick(start point, Right, down and up), ConsumePhysicalInput(false). It works because Shift is still physically held when the injected click arrives. Today Alt + click and Alt+Shift + click are swallowed silently on Joel's machine. Any click with Ctrl held passes, because Ctrl is his ignore key.

The same rule hit stale state: a stuck X1 "Before" flag made SP.net "absorb the RMB" on every right click [7081].

### 2.7 Mouse-button modifiers and chords, in depth

**Only a stroke button starts anything.** Every path to an action match requires the stroke button to be held. That covers the stroke-button-up decision, the wheel branch (which needs `StrokeButtonDown`) and the rocker branch (`net:MouseHook`). The only other callers are the touch floater and a script's `sp.StartManualCapture` (`net:HookState.GestureComplete` callers). SP.net has two stroke buttons: the primary, and an optional **secondary** with its own action set (`UseSecondaryStrokeButton`, drawn in the secondary pen colour). Both are captured identically (`net:MouseHook.IsStrokeButton(…, checkSecondary)`). Whichever of the two goes down first owns the press. If the other goes down during the press, it becomes an ordinary After button flag. This was fixed in net 0.3.2.2 ("not allowing the secondary stroke button to be a modifier as long as the capture dropdown is set to After").

**(a) A non-stroke button while the stroke button is held** (`net:MouseHook`, the block guarded by `StrokeButtonDown && !Cancel && !MouseWheelFiring`; classic `cpp:7849` is the same).

| Event | SP.net |
|---|---|
| Left/Middle/Right/X1/X2 down, pre-wheel phase | **Consumed**; its After flag is set; the hold-still timer restarts |
| its up, pre-wheel phase | **Consumed** if its After flag is set (classic 1.7.7: "only consume the mouse BUTTONUP event if the matching BUTTONDOWN event was also consumed"). One exception: a Left or Right up passes when the press belongs to the secondary stroke button, a side effect of the net 0.2.5.9 rocker fix. |
| its up, **after the first wheel tick** | **Passes**: the block is skipped during wheel firing. The app gets an up without a down. |
| its up, after the stroke button was released or the press was cancelled | **Passes**, again an up without a down |
| a button already held at stroke-button-down (Before) | Its down already reached the app, and its up passes as well. The app sees a complete click, and for Right that means its context menu ([8422]: "the app will still receive the left button down"). |
| a new button down after the first wheel tick | Passes; not recorded |
| Rocker Support (default on, Joel on): Left or Right **up**, no movement, no other button/key/wheel | Consumed, and fires the no-gesture match **at once** (`InRockerMode`). It repeats on each further click until the stroke button is released. Middle, X1 and X2 never fire early. Rob [8521]: Rocker Support "just adds some extra functionality to better emulate the rocker style gestures from Opera". Button-flag actions still work with it off. |

How SP.net avoids **stuck buttons**: it never consumes an up whose down it let through (classic 1.7.7; before that, classic 1.6.2 still consumed it). The remaining asymmetry is the unpaired *up* in the rows above. That cannot leave Windows holding a button, because the down never reached it. It can still reach a window: Windows' default window procedure turns a right-button up into `WM_CONTEXTMENU` (Microsoft Learn, WM_CONTEXTMENU). That fits [8639] (2023, unanswered): with a modifier plus right button, "the right click triggering the menu". Stuck *state inside SP.net* is a separate class:

- rocker races that left SP.net "stuck in rocker/stroke button down state" (net 0.2.4.6, 0.2.5.8; [3010]: "clicking the left and right buttons at nearly the same time");
- a crash race in rockers (net 0.3.2.4, [6065]);
- [8701] (2024, Windows 11, unanswered): after "Right mouse button and mouse wheel up/down", "it seems as if the right mouse button is stuck in Strokesplus". Context menus misbehave and plain wheel scrolling runs the action, until Right or Left is clicked a few times. A missed stroke-button up during wheel firing leaves the capture open. A button whose down the hook ate never shows in the OS key state, so nothing can query it.

**How SP.net keeps Right's context menu away.** Only by owning the button *as a stroke button*:

- the down is held back;
- released with nothing done, down and up are replayed together at release (`net:MouseHook.EmitStrokeClick` / `EmitSecondaryStrokeClick`; [3010]: the down is "not fired when the right down button is pressed but only when released - then all 3 mouse events are fired");
- released after something fired, the up is swallowed;
- held past the cancel delay, the down is replayed at the start point and the app takes over. The exception is `DisableRelayOnGestureTimeout` on with the pointer already moved: then SP.net swallows the next stroke-button up instead (`net:HookState.cancelTimerCallback`, [3010]).

AutoHotkey's custom combinations work the same way. A prefix key such as `RButton & WheelUp` "loses its native function". A hotkey on the prefix alone "is fired upon release, and is not fired at all if another key is pressed". The tilde keeps the native function, menu included (autohotkey.com, Hotkeys, "Custom Combinations").

**Joel's own chord is SP.net's secondary stroke button.** His config has four active secondary-button actions: Global "Zoom In" / "Zoom Out" (no gesture, wheel up / down; 9,724 and 7,611 past fires) and Windows Explorer "Zoom In" / "Zoom Out" (11,394 and 790). That is "Right (or another button) + wheel = zoom" as SP.net can express it. Today `SecondaryStrokeButton` is 0 (none), so they are dead. A held secondary button starts its own capture, so it can also draw gestures.

**(b) Keys + wheel with no button.** Not an action trigger. A wheel tick without a held stroke button goes to `MouseWheelScript`. When the global "Wheel Tick" Mouse Event is enabled (`MouseWheelTick`; Joel: off), that runs one global script per tick (`net:HookState.FireWheelTick`). The script receives the key and button state and decides itself; it is not a per-app action. With "consume" on, every tick is swallowed and the script must re-send the scroll. Rob [7117]: "mouse wheel scripts … can have poor performance due to a script having to execute for each mouse wheel tick … still scrolling after you've stopped"; also [7109]. A Ctrl + wheel *action* was always "hold the stroke button, press Ctrl, scroll" ([4019] shows scripts that *send* Ctrl + wheel).

**(c) Two buttons + wheel, and order.** The match sees one flag per button, Before or After relative to the stroke button (`net:ApplicationMatching.ActionMatched`). The relative order of two After buttons, or of two Before buttons, is lost. Example: Left held, then the stroke button, then Right, then a tick gives B = {Left}, A = {Right}. Only an Either action with {Left, Right} + wheel matches. With two *stroke* buttons, order picks the action set, as above.

**(d) Gesture + extra held button.** Supported: the gesture key and the flags must both match. Forum use: [3015] "an actual gesture (e.g. 'Up' + Left btn hold)"; [7228] ("assign a gesture to an action to use the modifier correctly and … perform the gesture using both keys"); [8252] ("3. (optional draw gesture)"). Drawing continues while the extra button is held. With Before, the app received that button's down, so the stroke also drags or selects in the app [8422].

Joel's five Left-flag actions are all inactive. The factory rocker pair is Back (no gesture, Left, After: hold right, click left) and Forward (no gesture, Left, Before: hold left, click right).

### 2.8 Joel's config and the factory default (read 2026-10-09)

| | Joel (264 actions) | Factory 0.5.6.5 (58) |
|---|---|---|
| Capture field, all actions | 250 Either, 11 Before, 3 After | 45 Either, 11 Before, 2 After |
| Control / Alt | 0 / 0 | 0 / 0 |
| Shift | 4: "Shift+Right Click" (active, no gesture, Before); "Plug In Test" (P, Either, inactive stock); PotPlayer "Speed Up/Down" (Shift + wheel, Either, inactive, 99 / 109 past fires) | 2 (the first two) |
| Mouse-button flags | Left on 5, all inactive | Left on 2 (Back/Forward rocker, active) |
| Secondary stroke button | 4 active wheel actions ("Zoom In/Out", Global and Explorer; 29,519 past fires in all), dead because `SecondaryStrokeButton` = 0 | none |
| Mouse Events (Wheel Tick, Left/Right/… Click, Release) | all disabled | all disabled |
| Settings | CaptureModifiersOnMouseDown true, IgnoreKeyList [Control], FireOnMouseWheel true, RockerSupport true, ResetCancelDelayOnMovement true, RelayGestureOnNoMatch false | same, but RelayGestureOnNoMatch true |

Aside: today's live file has `StrokeButton` = 0x200000 (**Right**). The copy Joel saved on 2026-10-07 (`…\StrokesPlus.net\Settings\AppData-Roaming-StrokesPlusnet\`) still has Middle, as reference §3 records.

### 2.9 Where classic (C++) differs, for anyone reading it

| | Classic | SP.net (wins) |
|---|---|---|
| Capture enum | `StrokeState` 0 Either, 1 Before, 2 After | `Capture` 0 Before, 1 After, 2 Either |
| Before match | action set equals B; A ignored (`cpp:6534`) | also requires A empty |
| App search | every matching app, then Global (`cpp:6284`) | the first matching app only, then Global |
| After key-up | passes: the consume block is unreachable (`cpp:8483`) | consumed while the stroke button is held |
| Ignore key | one key from a drop-down, default Alt | several, check boxes, Win added |

Both versions use exact matching, take the Before set at button-down, fire "no gesture" actions for a one-point stroke (`cpp:6935`) and freeze the set at the first wheel tick.

## 3. What Augram should implement

### 3.1 Model

- A trigger is Joel's design: a gesture, a wheel direction or none, plus a **while-holding set**. The set has **Keys** ⊆ {Ctrl, Alt, Shift, Win}, stored as `KeyModifiers`, and **Buttons** ⊆ {stroke button, Left, Right, Middle, X1, X2} (§4). It also has a **Capture mode**: Before, After or Either, default Either.
- SP.net has only Ctrl/Alt/Shift; Win is Joel's addition (consequences in §3.2). When the set holds nothing but the anchor button (§4.1), the mode is meaningless; store Either.
- "None" with a non-empty set is SP.net's no-gesture action, fired at release (a **click** trigger). "None" with nothing beyond the stroke button stays unbound, as today: that press is a plain click and is replayed.
- Editor, as in SP.net's WebView2 editor: one check box per key (Ctrl / Opt / Shift / Cmd on a Mac) and per button. A "Capture modifiers: Either / Before / After" choice appears only when something besides the anchor is ticked. Before = held when you press the stroke button; After = pressed while holding it; Either = any.
- No `CaptureModifiersOnMouseDown` option: fixed on (Joel's value and SP.net's default).

### 3.2 Sampling and consumption (hook thread, O(1), invariant 1)

| Event | Engine state | Rule |
|---|---|---|
| stroke-button down | Idle | Ignore key held (existing per-press check) → pass, nothing recorded. Otherwise suppress and set **B = event modifiers ∩ {Ctrl, Alt, Shift, Win}**, the same mask the ignore check reads, plus the buttons already held (§4.2, row 2). A = ∅. |
| Ctrl/Alt/Shift/Win key-down (left or right) | Held or Drawing | **Suppress**, auto-repeats included. If the key is not in B, add it to A (sticky). Push the hold-still deadline on the first down only (see "Not copied"). SP.net does this for Ctrl/Alt/Shift; Win joins per Joel's design. |
| the same key-up | any | Suppress if and only if this key's down was suppressed during this press, i.e. the key was not in B. Use the key-suppression shadow (pairing, A19). SP.net lets an up through after button-up; pairing is stricter and harmless. |
| the same key-down | WheelFiring, Cancelled, Idle | Pass; not recorded |
| every other key | any | Pass (unless the hotkey capture field is active, which is a separate feature) |
| any key Augram injects itself | any | Pass; never recorded |

**Win and Alt held *before* the press** pass through as Before keys. When they are released, Windows can see a lone Win (which opens Start) or a lone Alt (which focuses the app's menu bar), because the stroke-button events between were swallowed. SP.net never met this for Win, since Win was not an action modifier. For its unregistered hotkeys it masks exactly this: a Ctrl tap before the Win/Alt key-up passes (`net:KeyboardHook`, `PreventWinAltKeyUp` → `SendControlDown/Up`; net 0.3.9.6: "handle the Win/Alt the KEY_UP events … prevent Start Menu from popping up"). Augram should do the same once per press: a capture that held Win or Alt in B taps a masking key before that key's up passes. This guards the Before-Alt case too, which SP.net left open.

Not copied:

- SP.net's "eat every key while held when Fire on Mouse Wheel is off": Augram always has wheel firing.
- The hold-still timer reset by Windows auto-repeat of a held Before key. That keeps a hold-still cancel from ever firing while Shift is down. macOS sends no modifier repeats, so it cannot be matched there anyway. Push the deadline on a press only.

### 3.3 Decisions at release and at wheel ticks (worker)

| Situation | Rule |
|---|---|
| Release, Held (inside start distance), no tick, B ∪ A = ∅ | Replay the click (unchanged) |
| Release, Held, no tick, B ∪ A ≠ ∅ | Do **not** replay. Resolve a **click** trigger with (B, A). No match → nothing: SP.net swallows the click. Open 1 asks whether to relay instead. |
| Release, Drawing | Recognise, then resolve the gesture trigger with (B, A). No match → existing no-match handling (Joel: nothing). |
| First wheel tick | Freeze B and A for the rest of the press. Resolve the wheel trigger with them. |
| Later ticks | Same frozen sets |
| Release, WheelFiring or Cancelled | Nothing (unchanged) |

§4.2 extends these rows to held mouse buttons and to anchors other than the stroke button.

### 3.4 Match predicate and precedence

A command matches when its kind matches (gesture id, wheel direction, or click), its anchor button matches (§4.1), and its set S (keys and the non-anchor buttons) passes:

| Mode | Matches when |
|---|---|
| Either | S == B ∪ A |
| Before | S == B and A = ∅ |
| After | S == A and B = ∅ |

B and A hold keys and buttons alike, as in SP.net. `CommandResolver`'s order is already SP.net's: ignored app → first matching app group → its commands → stop if it suppresses globals → Global's commands → none. Keep it. The only change is that the lookup key now includes (anchor, S, mode) under the predicate above. There is **no fallback** to the same trigger with fewer or no keys, at either level. An app "override to nothing" for plain `\Up` does not block a Global Shift+`\Up`.

### 3.5 Uniqueness (A7)

Two commands in one group use "the same trigger" when kind and gesture/wheel are equal, the anchor and S are equal, and the modes overlap (Either overlaps all; Before–Before; After–After). This is SP.net's conflict rule. SP.net only warns and lets the first win; Augram keeps one bound, as A7 does today.

### 3.6 Ignore key interaction

- An ignore key held at button-down passes the press. Nothing is recorded, as today: read per press.
- An ignore key pressed after button-down is an ordinary After modifier and is suppressed. In SP.net that covers Ctrl/Alt/Shift; Win passes. Win joins them here because Joel's design makes it a trigger key.
- In the editor, a key that is also an ignore key can never be a Before key. Do what SP.net's WinForms editor did: disable that box while Before is chosen, with the reason in its tooltip. Either stays allowed because it works through After.

### 3.7 macOS mapping

| SP.net (Windows) | Stored `KeyModifiers` | Windows name | macOS key | macOS name |
|---|---|---|---|---|
| Control | Control | Ctrl | Control ⌃ | Ctrl |
| Alt | Alt | Alt | Option ⌥ | Opt |
| Shift | Shift | Shift | Shift ⇧ | Shift |
| (none: Win is only an ignore key in SP.net; Joel's design adds it) | Meta | Win | Command ⌘ | Cmd |

These are the stored values and names `HotkeyText` already uses, and the ignore keys already store them this way. Recommendation: **physical identity, no conversion.** A Ctrl trigger needs the Control key on both machines. The Ctrl ↔ Cmd best guess belongs to hotkey *steps*, which talk to apps; a trigger only describes the user's hand. Open 3 asks Joel.

To verify on the Mac:

- modifier presses arrive as `flagsChanged` events and do not repeat;
- whether dropping a `flagsChanged` event in the tap also hides the key from the flags on the next mouse events the app sees;
- Control+click is macOS's secondary click, which matters only if Left is the stroke button.

### 3.8 Import mapping (`src/Augram.Import.StrokesPlus/ActionReader.cs`)

| SP.net action | Augram |
|---|---|
| `GestureName` set | gesture trigger (as today) |
| no gesture, exactly one of `WheelUp` / `WheelDown` | wheel trigger (as today) |
| no gesture, no wheel, any of `Control` / `Alt` / `Shift` | **click** trigger (new) |
| no gesture, no wheel, no key | unbound (as today) |
| `Control`, `Alt`, `Shift` | Keys Ctrl, Alt, Shift (no longer "inactive with a note") |
| `Capture` 0 / 1 / 2 | Before / After / Either. This is SP.net's order; classic XML's `StrokeState` is ordered differently and is never imported. |
| `Capture` with no key set | Either |
| `Left` / `Middle` / `Right` / `X1` / `X2` | buttons in S, with the action's capture mode; the anchor is the stroke button |
| `UseSecondaryStrokeButton` with `SecondaryStrokeButton` set | anchor = that button, without the stroke button (§4.1). This is how SP.net expressed "Right + wheel". |
| `UseSecondaryStrokeButton` with no secondary button (Joel's file) | inactive, with a note: "was a secondary-stroke-button command in StrokesPlus.net; choose the button to hold" |
| two actions in one group with the same trigger under §3.5 | the existing A7 warning and rule |

On Joel's file:

- "Shift+Right Click" becomes an active click trigger, Shift, Before. Its steps are ConsumePhysicalInput, which imports disabled per plan C1, and MouseClick, which has no Augram step yet and becomes a placeholder. So until Open 1 is answered or a click step exists, Shift+right-click would be swallowed after import.
- PotPlayer "Speed Up/Down" import as wheel + Shift (Either), inactive as in the source. "Plug In Test" imports as `P` + Shift (Either), inactive.
- The four "Zoom In/Out" commands import inactive with the secondary-button note: they are the obvious first users of a Right + wheel chord.
- The five Left-flag actions import with Left in S, inactive as in the source.

## 4. Mouse-button chords: what Augram should implement

### 4.1 Anchors

- Every press that can fire a command starts with an **anchor** button whose down Augram suppresses, as SP.net does with its stroke buttons.
- The anchors are the stroke button, plus every button that some active command uses *without* the stroke button, e.g. Right in "Right + wheel up". That second kind is SP.net's secondary stroke button, generalised.
- The worker recomputes the anchor set whenever the mapping changes. The hook reads it as one volatile bitmask (invariant 1). It cannot resolve app groups per press, so anchors are global. Over a window where no command uses the anchor, the press is simply replayed (§4.2, row 8). Ignored apps and ignore keys pass anchors untouched, as they pass the stroke button.
- A non-stroke anchor serves **wheel triggers only**. Gestures need the stroke button (Joel's design ticks it for gestures). A "none" trigger on a non-stroke anchor would just replace that button's click.
- The first anchor down of a press owns the press. A button pressed later is an After member of the set; a button already held at that moment is a Before member, and its down reached the app. This is SP.net's rule, primary/secondary order included (§2.7 c).

### 4.2 Event by event

The hook decides pass or suppress synchronously from volatiles; the worker resolves and runs commands.

| # | Event | State | Hook | Then |
|---|---|---|---|---|
| 1 | anchor down | Idle; ignore key held, ignored app, or capture not allowed | **pass** | its up passes too |
| 2 | anchor down | Idle | **suppress** | Held(anchor). B = keys and buttons physically held now, from Augram's own down/up tracking. A = ∅. Start point and hold-still deadline as today. |
| 3 | another button down | Held or Drawing, and some command with this anchor has that button in S (a precomputed mask per anchor, like the anchor set) | **suppress** | add to A; push the deadline |
| 4 | another button down | Held or Drawing, no command uses it with this anchor | **pass** | today's A12: the press is cancelled |
| 5 | another button down | WheelFiring, Cancelled | **pass** | not recorded (SP.net) |
| 6 | a non-anchor button up | any state, including after the anchor's release | **suppress if and only if its down was suppressed**, else pass | SP.net lets these ups through after the first tick or the anchor's release, and the app's context menu follows ([8639]). Augram pairs them. |
| 7 | wheel tick | Held or Drawing | **suppress** | WheelFiring; freeze B and A; resolve the wheel trigger (anchor, S, mode). Every later tick: suppress, fire again. |
| 8 | anchor up | Held, nothing registered, no tick | **suppress** | replay down and up together at release (today's ReplayClick). A non-stroke anchor's click thus arrives at release, as SP.net's secondary stroke button's did [3010]. |
| 9 | anchor up | Held, something registered, no tick | **suppress** | Stroke anchor: click trigger (§3.3; Open 1 for no match). Non-stroke anchor: no command can match (wheel only), so replay the click. Shift + Right then reaches the app as a native Shift+right-click. |
| 10 | anchor up | Drawing (stroke anchor) | **suppress** | recognise, then resolve the gesture trigger with (B, A) |
| 11 | pointer leaves the start distance | Held, non-stroke anchor, no tick | n/a | **hand back** (Open 2): inject the anchor's down at the start point, move to the current point, go Idle. The physical up then passes and pairs with the injected down, so a right-drag in Explorer still works. This mirrors SP.net's cancel relay (`net:HookState.cancelTimerCallback`). |
| 12 | hold-still deadline | Held, non-stroke anchor, no tick | n/a | hand back as in 11, so a long press (press-and-hold, games) still reaches the app. A stroke anchor keeps today's rule (Joel: cancel, no relay). |
| 13 | anchor up | WheelFiring or Cancelled | **suppress** | nothing. Down and up were both swallowed, so no context menu. |
| 14 | anchor down | not Idle (an up was missed) | **suppress** | today's restart rule, for every anchor |
| 15 | wheel tick | Idle | **pass** | no keys-only wheel triggers: SP.net has none (§2.7 b). Adding them would mean suppressing ticks on a guess and replaying the rest, the lag Rob reported [7117]. |
| 16 | Left/Right up | Held (stroke anchor), no movement, nothing else registered | **suppress** | Only if rockers are wanted (SP.net's Rocker Support, on in Joel's file, used by none of his active commands): fire the "none" trigger with S = {that button} at once, and repeat per click. Otherwise "none" + button fires at the anchor's release, as SP.net does with Rocker Support off. |
| 17 | any event Augram injects (replay, hand-back, steps) | any | **pass** | never recorded or captured (SP.net: `HookIgnore`, `dwExtraInfo` 654321; classic 123456) |

### 4.3 A19: stuck-button safety

1. **Pair per button, across every state change**: anchor released first, wheel firing started, cancel, stroke button changed in Options. A suppressed down gets a suppressed up; a passed down gets a passed up. Extend the existing pairing invariant and `PairingInvariantTests` from the stroke button to every button and to the modifier keys. SP.net breaks the first half in three places (§2.7 a).
2. **Never suppress an up whose down passed** (classic 1.7.7). Before members are the everyday case: their up always passes.
3. **Decide at the down, never afterwards.** A button whose down passed can only ever be a Before member. A suppressed anchor that turns out unused is *replayed* (rows 8, 9) or *handed back* (rows 11, 12); it is never "un-suppressed".
4. **A hand-back is the one deliberate unpaired injection**: an injected down whose up is the physical one. The machine records the hand-back so the pairing shadow passes that up.
5. **The OS cannot be asked about a suppressed button.** It never appears in `GetAsyncKeyState`. Before membership and "is the anchor still down" come from Augram's own tracking, reset on hook reinstall, lock/unlock and resume (reference §9). A missed anchor up must not leave wheel ticks firing commands, as in [8701]. Row 14 repairs it on the next press of that button. A watchdog cannot tell a held button from a lost one, so do not add one.
6. **Stale state → relay.** If a press's only registered member is a button Augram wrongly believes is held, and nothing matches, the click must be replayed, not eaten (Open 1's recommendation). Otherwise one stale flag eats every click, as SP.net's stale X1 did [7081].
7. **Unpaired ups are not harmless for Right.** "DefWindowProc generates the WM_CONTEXTMENU message when it processes the WM_RBUTTONUP" message (Microsoft Learn, WM_CONTEXTMENU), and it does not check whether the window saw the down. Many apps open their menu on the up themselves. [8639] reports menus in exactly this situation. Row 6 exists for this.

## 5. Open for Joel

1. **A click with Shift (or Alt) held and nothing bound to it.** SP.net swallows any stroke-button click made while Ctrl, Alt or Shift is held, unless a "no gesture" command exists for exactly those keys. That is why your "Shift+Right Click" command exists, and why Alt + right-click does nothing today. Copy that, and give Augram a "click at the start point" step so your command can re-send the click? Or pass such a click straight through when no command matches, which makes that command unnecessary? *Recommendation: pass it through.* You get the same result for Shift, Alt + click starts working, and it follows A19 ("relay, never swallow") and the stuck-X1 lesson [7081].
2. **Right + wheel without the stroke button.** SP.net could only do this by making Right a second stroke button, which is how your old "Zoom In/Out" commands worked. Augram must then hold back every Right press until it knows whether a wheel tick follows. A right-click happens when you let go, not when you press. A right-drag goes back to the app as soon as you move. A long press goes back after the hold-still time. Is that acceptable? Should Left be allowed the same way? Every left click and drag would then wait for release. *Recommendation: yes for Right, Middle, X1 and X2, for wheel commands only; not Left.*
3. **A synced "Ctrl + gesture" command on the Mac.** Should it need the Control key (the same physical key, as ignore keys work), or Command (converted, as hotkeys are)? *Recommendation: Control, no conversion.*

## 6. Side findings outside modifiers (for the lead)

- **SP.net scores like Augram's `Corrected` mode, not `Legacy`.** `net:GestureRecognition.GetGestureName` resamples to `MatchPrecision` points, compares the P−1 headings and divides the delta sum by the number of deltas it actually filled, not by P. It **averages** across a gesture's patterns. Reference §1 says SP.net's choice of average vs best is unknown, and the recognizer's NOTICE says the threshold of 75 was "tuned against" the divide-by-P quirk. Joel's 75 was in fact tuned against SP.net, which does not have the quirk. Augram's default (`ScoringMode.Legacy`) therefore scores 0.25 points higher than SP.net at a 45° error (§1's example), and accepts strokes SP.net rejects. Worth a decision on the recognizer side; nothing was changed here.
- **Wheel after drawing.** In SP.net the wheel match uses the gesture recognised from the points before the first tick. A "no gesture" wheel command (Joel's volume) fires only if the pointer stayed inside the start distance. Augram's worker fires `Trigger.ForWheel` whatever was drawn (`EngineWorker`, `CaptureOutcome.WheelTrigger`), which differs from SP.net when Joel moves more than 30 px before scrolling.
