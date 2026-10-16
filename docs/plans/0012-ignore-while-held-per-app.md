# Plan 0012: Exclusions › While holding, an app's keys and buttons that make gestures pass through, per platform

Status: **PROPOSED (2026-10-16), open questions below for Joel.** Origin: Joel, 2026-10-16, on the PC. After Effects navigates its 3D viewport with Alt + middle drag, and Middle is his stroke button, so the drag becomes a stroke. The global ignore keys (Options › General, F1) would fix it, but Alt + stroke would then be ignored in every app. In Softimage XSI he navigated by holding S, so any key, not just a modifier: "an app could have one or multiple ignore keys on the keyboard. Not just modifiers." The list is laid out like a command's step list ("just copy the steps section"), per platform "the same way we do with commands", because After Effects on the Mac may not navigate with Alt. First placed on the app group; moved the same day to a third Exclusions list (Joel: "It's more clear. And easier to get an overview, it's cleaner").

## Decisions (Joel, 2026-10-16)

1. **A third list on the Exclusions tab: Global · Per command · While holding.** An entry is an app, identified as the other Exclusions entries are (the same matcher form, window finder, known-app guess and "Fields for: Windows · macOS" switch, plan 0010 decision 7), and its **"Ignore gestures while holding"** list. It needs no app group: an app with no commands gets its passthrough here.
2. **The list is laid out like a command's step list**: a card under the app's identification, "New…" adds an entry, rows can be removed and reordered, and copy, paste, delete and undo work as they do on steps. An entry is **one input: any keyboard key (S, Space, Alt, F13…) or a mouse button**, added by **recording** (press the key or click the button; the hotkey capture field's machinery, one key, no combination) or by **picking** from a drop-down (the mouse buttons and the modifiers, awkward to record alone).
3. **Holding any input on the list when a press starts over that app makes Augram pass the press through untouched**, as a held global ignore key does today. The app is the window under the pointer at the press (as for the other Exclusions lists and for anchors), not the focused window. The key or button itself is never touched: After Effects gets Alt's down and up as always.
4. **Only the held state matters, never what a command sends.** A command whose steps send Alt ("Alt+X") is unaffected: the check runs at the press, before any step. A command whose *trigger* holds a listed input ("while holding Alt") cannot fire over that app while it is held; the list wins and the editor does not warn (Joel: "shouldn't be an issue").
5. **Per platform, as a command's steps are (plan 0010).** "Ignore for: Windows · macOS" above the list, the same view switch: it opens on this platform, the other side is a read-only peek with "Edit on Windows" (or macOS) and **Copy from macOS / Copy from Windows**. A list made on the PC is the macOS side too, converted (inferred), until an edit on the Mac makes that side its own; from then on each machine changes only its own side.
6. **Added to the global ignore keys, never instead of them.** Over After Effects, a held global ignore key and a held listed input both ignore.

## Proposed details (to confirm with Joel)

7. **Same reach as the global ignore keys:** every press that would start something over that app passes through: the stroke button (gestures, wheel triggers, the Click kind) and a button another command anchors (button triggers, Right + wheel). Hold remaps too, as the global ignore keys stop them (F9). Question A.
8. **Several entries may match one window;** their lists for this platform add up. An app on Exclusions › Global as well needs nothing more (Augram is out of it anyway); the form says so.
9. **A key Augram swallowed never counts as held**: a hold remap's hold key (F9: Blender's Space) and a key pressed while a hotkey field captures are Augram's, not the app's, so they do not ignore. Only keys the OS saw go down count. A runtime rule, so no check across an entry's matcher and a group's.
10. **Conversion of the inferred side** uses the hotkey converter that converts a command's steps (F8), key by key: Alt ↔ Opt and Win ↔ Cmd as for hotkeys; letters, function keys and mouse buttons as they are. An input with no guess shows "needs a macOS version" like a step. Question C.
11. **The Global group and Options › General do not change:** the global ignore keys stay tick boxes there (F1, Joel 2026-10-09). Question B.

## Questions for Joel

- **A.** Should holding a listed input also pause hold remaps over that app, as the global ignore keys do? (Proposed: yes, one rule for both.)
- **B.** Keep the global ignore keys as tick boxes in Options › General (proposed), or later fold them into this list as an "everywhere" entry?
- **C.** For a macOS side made from a Windows list: should Ctrl become Cmd (the hotkey rule) or stay Ctrl? For navigation keys Ctrl → Ctrl seems more likely; the Mac can always make its own side.
- **D.** Names: the list "While holding" and the card "Ignore gestures while holding" (proposed)?

## How it works

**Reading "held" at the press, never remembering it** (Capture README, SP.net's stuck ignore mode). The hook already keeps a record of every key's press (`Engine/Input/KeySuppressionShadow`: up, passed or owed per key, 256 slots, a key with no event for 2 s counted as released) and of every button this machine saw down (`SuppressionShadow`, the Before buttons). At an anchor press the gate checks the window's inputs against those records: a key counts when its record is *passed* (decision 9), a modifier also through the event's mask as `IgnoreKey` is read today. No new OS call on the hook thread and no new tracking.
- Known edge: with key repeat off (macOS allows it), a non-modifier key held longer than 2 s without repeating counts as released, so gestures come back. It errs towards strokes, never towards a stuck ignore; noted in the Capture README.

**Per window, off the hook thread.** `IgnoreListWatch` already publishes the pointer's answer for the next press from the Exclusions entries (the exclusion bits) and the groups (anchor plan, drag distances, button outputs). The matching While holding entries' inputs for this platform join it as one more immutable value published beside `ButtonOutputs`, so the hook reads one reference at the press.

## What changes

**Core**
- `Mapping/IgnoredApp`: a third `IgnoreScope`, `WhileHolding`, and the entry's inputs with their two sides, held the way a command's steps are (decision 5). Step 1 decides, with the one-home rule in mind, whether a command's sides (`Command.Steps`/`OwnVersion`/`StepsFor`/Copy) become one small generic type both use, or the list reuses them another way; a second copy of the own/inferred rules is not an option. `MappingRules`: a While holding entry's inputs are distinct per side; `DisableEntirely` does not apply to it.
- `Config`: schema version up one (an older build refuses the file instead of dropping or misreading the entries). `Sync`: format version up one, the entries travel as the other Exclusions entries do. `Transfer` (F8 export) takes them with the Exclusions.
- `Capture`: no change to the state machine; the press's `IgnoreKeyHeld` now means "a global ignore key or one of this window's listed inputs was held".

**Engine** (`Hosting/InputGate`, `IgnoreListWatch`, `IgnoreLookup`): the inputs in the pointer answer; the check at an anchor press and at a hold remap's start (decision 7), beside today's `IgnoreKey` test, reading `KeySuppressionShadow` and `SuppressionShadow`.

**App**
- `ViewModels/Ignored` (`IgnoredViewModel`, `IgnoredEditViewModel`, beside `IgnoredViewModel.PerCommand.cs`): the third list and its entry's edits, one undo step each; the view switch state beside the selection as for steps.
- The tab's third segment and the entry's side panel: the matcher form as on the other lists, then the "Ignore gestures while holding" card built from the step list's parts (`Components/StepList`, the "Steps for" switch, the peek, Copy) and the capture field (`Components/Capture`) for recording, plus a drop-down for buttons and modifiers. Reuse first; whatever is generalised for it is named in the commit's Reuses list.
- `DevGallery/IgnoredGalleryPages`: a While holding entry, for UiShots.

**Docs**: requirements F1 (the list beside the global ignore keys) and F5 (Exclusions has three lists), Capture README (what `IgnoreKeyHeld` covers, the key-repeat edge), Engine README (the pointer answer's new member), `Core/Mapping` README.

## Tests (one per behaviour, beside the neighbours)

- Core: a While holding entry round-trips through config, sync and export; an older schema refuses the file; the inferred side follows the original converted; an edit on this platform makes its side own; Copy replaces a side, one undo restores.
- Engine (fakes, never real hooks): over a window matching an entry listing Alt, a stroke press with Alt held passes through and the same press without Alt suppresses; the same with S (a non-modifier, via the key record) and with a held Left; over another app, Alt held changes nothing; an S whose release was missed for over 2 s no longer ignores; a swallowed hold key does not ignore; two matching entries add up; a hold remap does not start while a listed input is held (if A is yes).
- App: the third list adds, renames and deletes entries like the others; recording a key adds an input; the drop-down adds a button; the other platform's side is read-only and Copy works from both sides.
- A UiShots render of a While holding entry, both sides, dark and light; no test for the look.

## Steps

1. **Core** (agent, effort high: it touches `Sync`). The scope, the inputs and their sides, the rules, config, sync and export.
2. **Engine** (agent, high; after step 1). The pointer answer and the checks.
3. **App** (agent, medium; may start on the view model once step 1's types exist). The list, the card, recording, the drop-down, the switch.
4. **Joel's check, PC then Mac.**

## Joel's check

- Exclusions › While holding: add After Effects (window finder) and Alt on its Windows side. Alt + middle drag navigates the 3D viewport; middle drag without Alt still draws gestures; Alt + middle drag in another app still draws (unless Alt is a global ignore key).
- Add S: holding S and middle-dragging in After Effects does not stroke; let go of S and gestures work at once.
- An After Effects command sending Alt+something still fires from its gesture when Alt is not held.
- On the Mac the entry's macOS side is inferred from the PC's list; an edit there makes it the Mac's own and the PC keeps its Alt.
