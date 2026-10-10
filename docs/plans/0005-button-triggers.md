# Plan 0005: Button triggers ("hold Right, press Left"), Eyeris's magnifier chord in Augram

**Status: PLANNED (2026-10-10, PC session), nothing built.** Decision 7 was revised the same evening with Joel. Origin: Eyeris, Joel's image viewer, has its own mouse chord for its loupe: hold Right, click Left. It runs its own `WH_MOUSE_LL` hook. When Eyeris's hook is the newer one, it swallows the physical Right release and sends a synthetic one instead. Augram drops every simulated button event, so it never sees Right go up. Right then stays in the machine's held set (`CaptureStateMachine._down`), and every later trigger reads "Right + …" until a real right-click clears it. Log 2026-10-10 19:36:13 and 19:37:17: a simulated Right up, no hand-back before it, then "Stroke + Right + wheel up" and "Right + gesture '/Up'" with no command. Over Eyeris, Right is not an anchor (Eyeris is in Zoom In/Out's Not in), so its down passed through and was recorded as a Before button. Joel moves the chord into Augram (2026-10-10). Eyeris keeps its chord as a setting for people without Augram, off on Joel's machines.

## Done when

On the PC (Joel), with Eyeris's chord off and a Global "Magnifier" command: a **Button** trigger, Left while holding Right, with one Remap step whose output is the key Win+Shift+X:
- Hold Right and tap Left: Eyeris latches the loupe. Right's release opens no context menu. With the loupe up, clicks, right-click menus, drags and text selection work as usual. Hold Right and tap Left again: the loupe exits.
- Hold Right + Left, move, release either button: the loupe shows while both are held and hides on release.
- A plain right-click, a right-drag and Right + wheel (Zoom In/Out) behave as before.
- In Blender with Space held, Left + Right still zoom (the hold remap), and the magnifier does not fire. Blender stays on Exclusions › Global, so the magnifier does not fire there without Space either (decision 7).
- The tab reads **Exclusions**. Blender's Space hold remap shows **Works when the app is excluded** ticked, and still works. Unticking it stops the hold remap in Blender.
- Nothing is ever left held. No Win, Shift or X is stuck after any order of releases, a focus change, an engine reset or an engine stop.
- A gesture never reads "Right + …" again after Eyeris's own chord is used (step 1, tried with the chord switched back on).
- The Mac is unchanged until Joel wants the magnifier there too.

## Decisions (Joel, 2026-10-10, and the lead's calls he can overrule)

1. **A trigger, not a hold remap** (Joel: "let's try the trigger route"). A new trigger kind, **Button**, sits beside Gesture, Wheel and No trigger: one button, the *pressed* button, with the usual "while holding" set. Left while holding Right reads "Right + Left", the way Zoom In reads "Right + wheel up". A hold remap would be wrong for this, for two reasons:
   - It claims its input buttons outright and never replays a click, so one armed on Right all the time would swallow every right-click.
   - It belongs to one app group and a hold key, and is never Global.

   Gesture capture already holds Right back, replays a plain click at release, and hands a drag back.
2. **The held set has a button other than the stroke button, and not the stroke button** (lead's call). These are the sets that already hold back and hand back (`TriggerHold.HandsBackDrags`), like Right + wheel. Keys may join the set with the usual Before / After / Either. The pressed button is the trigger itself, never a member of the set, and never one of the held buttons. The stroke button is kept out on purpose. A button joining a stroke press already means a click trigger at release ("Stroke + Right click") or a cancel (A12). Joel's case doesn't need it; it can come later.
3. **When it fires and when it ends** (Joel: "Augram just sends the command"):
   - The trigger fires at the pressed button's down while the set is held.
   - A **Remap** step's output is held exactly while both buttons are down. It is released at the first release of either, so a tap of Left is a short press of the shortcut. Pressing Left again while Right is still held fires again.
   - **Ordinary steps** run once at the press, as for a wheel trigger. That gives the classic rocker for free (Right + Left = Back).
   - Tap and hold logic belongs to the app that receives the shortcut. Eyeris: a short press latches, a long press shows the loupe only while held, and another short press exits. Augram never latches a held key. Holding Win and Shift while Joel works would change every click and key, and an Augram that stopped mid-latch would leave them stuck.
4. **After it fires, the press is frozen** (lead's call), as a wheel tick freezes one: there is no hand-back on the hold-still time or on movement. Moves pass, so the pointer moves the loupe. Wheel ticks pass to the app too, so a loupe can use the wheel; they never fire a wheel command during the chord. The held button's release is swallowed (no context menu), as today when an After button took part.
5. **Order and timing.** The held button goes down first, then the pressed one. The pressed one must arrive while the held button's press is still held back: within the hold-still time (1 s by default) and before it moves its drag distance. After that, Right has been handed to the app and Left is an ordinary click. Pressing Left first and then Right does nothing special: Left is never held back for this, since that would delay every click and drag.
6. **Hold remaps come first, and nothing needs building for that.** An engaged hold remap is asked before gesture capture, and whatever it takes never reaches it (`InputGate`), so Space + Left + Right in Blender stays "Zoom both". Edge, accepted: Right pressed first, then Space, then Left. Left is a hold remap input, so the hold remap takes it and the magnifier does not fire.
7. **Exclusions stay deny-only; hold remaps get a toggle** (Joel, 2026-10-10, revising the first draft's "Also over ignored apps" switch).
   - **The model stays deny-only.** Everything works everywhere, and Exclusions take things away: Global takes everything over an app, Per command takes the commands that name the entry. Joel weighed local permissions over a Global entry and dropped them: allowing anything that uses the stroke button would bring capture back into the app, which is what the entry exists to stop.
   - **The tab is renamed Exclusions** (Joel asked for a fitting name), with sub-tabs Global and Per command as today.
   - **Hold remaps get a toggle, Works when the app is excluded** (`HoldRemap.WorksWhenExcluded`, default true). Today a hold remap works over a Global-ignored app as a rule built into the code (plan 0002: Blender is ignored so Middle stays its own, and must keep its hold remap). That rule becomes this visible choice. A hold remap never uses the stroke button, so ticked costs the app nothing. Unticked, the exclusion covers the hold remap too. The default keeps today's behaviour, so no migration is needed.
   - **The toggle only applies to plain Global entries.** An entry that disables Augram while focused (VMware) stops every hold remap, as it does today.
   - **The magnifier does not fire in Blender** (Blender stays on Exclusions › Global). If Joel misses it there, there are two ways: a Space input under Blender's hold remap with a Remap key output Win+Shift+X, which needs nothing new; or, later, the same toggle on commands without the stroke button. That would hold back every Right press in that app.
8. **The Remap step on a Button trigger takes a key output only** (lead's call). A button output would need the macOS drag re-posting that only the hold remap shadow does today (learnings 0005), and a wheel output needs a wheel input (HoldRemaps rule 7). The step stays a command's only step.
9. **The worker plays the output, not the executor** (lead's call). The executor's queue drops its oldest entry when full (8), so a dropped release would leave Win+Shift+X held. The bindings for the window under the pointer are worked out off the hook thread with the anchor plan and carried by the press, as its drag distances are. The worker presses and releases the output itself, in order with the input, through the hold remaps' output injection (`EngineWorker.HoldRemaps.cs`). It releases at the end of the chord, at a reset and at engine stop (A19). A Button trigger with ordinary steps goes to the executor like a wheel trigger.
10. **A synthetic release takes a passed-through button off the held set** (lead's call, step 1). The trigger route doesn't need it, but anyone running Eyeris's own chord next to Augram hits the stuck "Right + …". A simulated up of a button whose down Augram let through, and still counts as down, means the OS now has it up. The machine takes it off `_down`. No decision changes: the event stays unsuppressed and is still logged as "Button ignored". It never touches an owed button or the owner of a press in progress, so Augram's own replays and hand-backs, whose ups arrive after the physical one, change nothing.
11. **Formats:** config schema 7 and sync format 14, raised together (step 4). An older build would drop the trigger, or read it as unbound, and publish the command without it. As with 0.7.0 and 0.9.0, both machines update before sync resumes. Version **0.10.0**.

## Steps

1. **The synthetic-release guard (lead).** `SharpHookInputSource` posts a simulated button up as one new message instead of dropping it silently; it costs one `TryWrite` and is never suppressed. `CaptureEvent.ButtonReleasedElsewhere(button)` lets the machine drop the button from `_down` when it is neither owed nor the owner. Tests: a scripted Eyeris sequence (passed Right down, simulated Right up, Middle stroke: no Right in the hold), and the pairing tests with random foreign ups. Capture README table row; Engine README input row.
2. **Core (lead).**
   - `Trigger.ButtonTrigger(MouseButton)` and `Trigger.ForButton`. `NeedsStroke` is false. `KindPhrase` names the button ("Left"), so `Describe` reads "Right + Left".
   - `PressedTrigger`; the resolver; `MappingRules`: decision 2's set, the pressed button outside it, A7 overlap on the same pressed button and an overlapping set, and the Remap step allowed on a Button trigger with a key output (decision 8; `RemapStepType.HoldRemapsOnly` becomes a rule both places share).
   - `AnchorPlanner`: the pressed button is an extra of each anchor in the set, plus a new **fires** mask per physical anchor slot. It takes 25 bits, which fit in `AnchorPlan`'s 29 free bits, so the hook still reads one `long`.
   - Bindings per window: per (anchor, pressed button), the Remap output or "runs steps", immutable, published beside the plan (decision 9).
   - The machine: on a claimed down that the plan marks as firing, the outcome `ButtonTrigger(pressed, start, hold)` and a new state `ButtonFiring` (frozen, decision 4). On the pressed button's up, or the owner's up while it is down, `ButtonTriggerEnded(pressed)`. On a new down of the pressed button, fire again.
   - The pairing tests and `ChordPairingTests` with random Button triggers in the plans.
3. **Engine (lead).** `SuppressionShadow` mirrors the frozen press, so wheel ticks pass (decision 4). The worker handles the outcomes:
   - Remap output: pressed and released as in decision 9, with a cross-check that it never ends a chord holding nothing.
   - Ordinary steps: an `ExecutionRequest`.
   - Logs: `capture` "Button trigger" at Info, `exec` "Trigger resolved", and an output pressed / released line at Debug.

   Tests: engine tests through the fake source, including focus change and reset mid-chord. Engine README.
4. **Config schema 7, sync format 14, export/import (agent).** The trigger as `{ "button": "Left", "hold": { "buttons": "Right" } }`, and the Remap step on it. Migrations with their comment lines; round trip, format guard and merge tests; Config, Sync and Transfer READMEs.
5. **App (agent).**
   - **Trigger dropdown:** Gesture / Wheel / Button / No trigger (lead's order; Joel may reorder). For Button, a button choice beside the dropdown, as the wheel's direction is.
   - **"While holding":** it must hold a button other than the stroke button, and not the stroke button; the header's note says so on a draft. The Drag distance row shows, since the set hands drags back.
   - **Step picker:** offers Remap for a Button trigger, with a key output only.
   - Swap / Take it works for the new kind. Gallery entries; App README.
6. **The hold remap toggle and the Exclusions name (decision 7; lead for Core and engine, agent for config and App).**
   - **Core:** `HoldRemap.WorksWhenExcluded`. `HoldRemapPlan.For(mapping, foreground window, platform)` leaves out a hold remap with it off when the window matches an active Global entry (`IgnoreList`, Global scope). `WatchesFocus` follows. The hook does not change: a hold remap left out of the plan is never claimed.
   - **Config and sync:** the member `worksWhenExcluded`, written only when false, on the hold remap and its sync item. It rides step 4's format raise.
   - **App:** the checkbox on the hold remap form with the ⓘ "Hold remaps never use the stroke button, so they can work in an app on Exclusions › Global. Untick to make the exclusion cover this hold remap too." Rename "Ignored" to "Exclusions" in the tab, sub-tab headers, dialogs, notes and the gallery. Code names (`IgnoredApp`, `IgnoreList`, `mapping.ignored`) stay, and the App README says the UI word differs.
   - **Tests:** the plan with and without the toggle over a Global entry; a disable-while-focused entry stops the hold remap either way.
   - **READMEs:** HoldRemaps (the plan section and the type table), Engine "Hold remaps" (the line that says the ignore bit never stops a hold), App.
7. **Joel's check, PC then Mac.** Install 0.10.0. Before testing, he turns off Eyeris's chord and, if its shortcut lacks the chord's tap and hold behaviour, has the Eyeris agent add it. He changes Global "magnifier" (today Middle + Right click, which taps the key) to the Button trigger with the Remap step, then goes through "Done when". Then the Mac build, so sync resumes.

## Not in this plan

A Button trigger whose set holds the stroke button; a button or wheel output from a Button trigger; latching inside Augram; letting an anchor's press through first (held off, requirements F1); local permissions over Exclusions › Global (decision 7); the magnifier in Blender; Eyeris's code.
