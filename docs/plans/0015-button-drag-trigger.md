# Plan 0015: a Drag trigger: a held-back button's drag played as a Remap (Photoshop's Right-drag pan)

Status: **PROPOSED (2026-11-13, Mac session), decisions below to confirm with Joel.** Origin: Joel, 2026-11-13: "in photoshop, i would like the RMB drag = photoshops viewport pan -> Space+LMB while RMB is held, and then on RMB release if not drag happened allow the RMB menu to pop up."

## Why nothing built does it today

- **A hold remap** needs a keyboard hold key (`HoldRemap.HoldKey` is a `KeyCode`, rule 2). Right can't be one. Its Remap step's button output presses modifiers around the down only, and Space isn't a modifier.
- **A Button trigger** ("Right + Left", plan 0005) fires only at a second button's press. A plain drag of Right fires nothing, and the second button is swallowed.
- **The nearest piece is the anchor hand-back** (Engine README "What happens to a held-back anchor", learnings 0003 §4.2). Joel's stroke button is Middle, so Right can be an anchor in Photoshop, as it is for Spine and Eyeris. A click is replayed at release, so the context menu opens ✓. A move past the button drag distance hands back **Right's own** down ✗. This plan changes only what is handed back.

## Decisions (to confirm with Joel)

1. **A new trigger kind, Drag.** The trigger dropdown gets **Drag** beside Gesture / Wheel / Button / Click / No trigger. A Drag trigger's "while holding" set names exactly one button that is not the stroke button (Right), plus keys if wanted. It reads "Right drag". It is a new `Trigger.DragTrigger` record with `NeedsStroke` false, like `ButtonTrigger`. Two Drag triggers on the same button and keys clash (A7). *Rejected alternative:* a hold remap with a mouse button as hold key. Its tap time, rollover and following rules are built around a key. The anchor path already has the click replay, the drag distance and Also in.
2. **It fires where today's hand-back would, at the drag distance only.** Moving past the button drag distance fires it: the command's own `TriggerHold.DragDistancePx`, else Options' 10 px. The press is held back as any anchor's (`AnchorPlanner`), only over windows where an active Drag command uses it.
   - **The hold-still time does not hand back** a press whose anchor has a Drag command. A slow click must still open the menu, and a pause before panning must still pan.
   - **Another button going down** hands back exactly as today (Right's own down). The Drag command does not fire.
   - **A click** (released before the distance) is replayed as today, so the menu opens.
3. **Its steps: one Remap step, or ordinary steps run once at the drag's start,** like a Button trigger. The Remap step is the case this plan is for. Its output is held from the drag's start until the button's release, never through the command executor (the same reason as `ButtonOutputs`: the executor's queue may drop a release).
4. **A Remap button output may hold one key through the whole press: "Left with Space held".** `RemapOutput.Button` gains `HeldKey` (a `KeyCode`, `None` by default; not a modifier, since modifiers keep their meaning). At the press: the held key down, the modifiers down, the button down, the modifiers up. At the release: the button up, then the held key up. The held key is never auto-repeated. It is allowed on any button output, under a hold remap too: Blender has no use for it, but one rule is simpler than two. Summary: "Remap to Left with Space held". JSON: `"heldKey": "Space"`, written only when set (config schema and sync format bump).
5. **Where the output plays.** At the start point, as a hand-back plays today: the worker puts the pointer back and presses the output there, so Photoshop's pan starts where the drag started. On **Windows** the physical moves drag the posted Left (as for a hold remap). On **macOS** physical right-drags do not move a posted Left (learnings 0005). The worker re-posts the swallowed moves as Left drags, the hold remaps' `RepostsRemapDrags` path (Engine README "Re-posted drags").
6. **Pairing (A19).** Every output press gets its release: at the anchor's release, at another program's release of it (`ButtonReleasedElsewhere`), at a reset, at engine stop. The held key is up last. Right's own release stays swallowed, since its down was. Nothing is ever left held once everything is physically up.
7. **Anchor plan room.** `AnchorPlan` is full (bit 60 plus the hook's two ignore bits), and the hook does not need the answer: the hand-back is decided on the worker. "This anchor has a Drag command" travels with the press in `AnchorDragDistances` (already per press, read only at the hand-back). The output travels in `ButtonOutputs` beside the Button triggers' (`AnchorPlanner.Answer`).
8. **Also in, Not in, Exclusions** apply as to a Button trigger (plan 0005 decision 7): no stroke button, so a Drag command may list a Global exclusion in its Also in. Exclusions › While holding makes the press pass through (plan 0012).
9. **Platforms.** A Drag trigger converts like a Button trigger (buttons the same, keys as hotkeys). A Remap output is not converted (F9), so Left with Space held is the same on both. Photoshop's group matches `Photoshop.exe` and "Adobe Photoshop 2026".
10. **Not in this plan:** Drag on the stroke button (its movement draws), a drag that runs steps per move, a gesture-or-drag choice on one button, and Space's auto-repeat. A known quirk, not fixed: while Photoshop's text tool is editing, Space types a space, the same as pressing Space by hand.

## Steps

1. **Core model** (`high`: Mapping, Steps/Remap, Config, Sync). `Trigger.DragTrigger`, its rules in `MappingRules` (one non-stroke button; clashes), `TriggerConversion`, `StepOffer`/`HoldRemapRules.FittedTo` (a Remap step on a Drag trigger: a button output, Left by default), `RemapOutput.Button.HeldKey` with JSON and summary, the config schema and sync format bumps, and `AnchorPlanner` (the anchor held back, its drag distance, the drag-bound flag in `AnchorDragDistances`, the output in `ButtonOutputs`). READMEs: Mapping, Steps/Remap, Config, Sync.
2. **Capture** (`high`). `CaptureStateMachine`: from Held, a move past the distance with the drag bound → `DragTrigger(owner, start, here)` instead of `HandBack`, state `DragFiring` (frozen until the owner's release, like `ButtonFiring`). The hold-still tick does nothing when bound. The owner's release → `DragTriggerEnded`. Another button → today's hand-back. `PairingInvariantTests` with drag-bound plans. Capture README table rows.
3. **Engine** (`high`). `EngineWorker.ButtonTriggers.cs` (or a `.DragTriggers.cs` beside it): press the output at the start point with its held key, re-post moves on macOS, release in order. Ordinary steps → enqueue like a Button trigger. `SharpHookInputSimulator.PressRemapButton` and the Mac `MacRemapButtonSimulator` take the held key. Extend `ChordPairingTests` and the hold remaps' pairing tests for the held key. Engine README "Button triggers" and the log table.
4. **App** (`medium`). The trigger dropdown's Drag entry with its button picker and drag distance. The Remap step form's "Hold key through the press" one-key field (refusing modifiers, as `HoldRemapRules.InputKeyProblem` words it). UiShots of both. Commands README.
5. **Docs.** requirements.md (the Triggers bullet), this plan's status, learnings 0003 §4.2 (the hand-back now has a Drag variant).

Steps 1 → 2 → 3 depend on each other; 4 can follow 1. **Version** 0.22.0. Full suite and `dotnet format` once before the commit.

## Joel's check (both machines)

1. Install 0.22.0 and restart Augram.
2. Commands › New app group **Photoshop** (window finder on Photoshop). New command **Pan**: trigger **Drag**, button **Right**. Step **Remap**: output **Left**, Hold key through the press **Space**.
3. In Photoshop, zoom in, then Right-drag: the canvas pans with the hand cursor. Release: the previous tool is back.
4. Right-click without moving: the context menu opens. A slow right-click (held a second, no move) also opens it.
5. Right-drag, then press Left mid-drag, then release both: nothing stays held (Space typed nowhere, no stuck button). Diagnostics › Log shows `Output pressed` / `Output released` paired.
6. Outside Photoshop, Right behaves as before.
