# Plan 0012: an app group's "Ignore gestures while holding" list, per platform

Status: **PROPOSED (2026-10-16), open questions below for Joel.** Origin: Joel, 2026-10-16, on the PC. After Effects navigates its 3D viewport with Alt + middle drag, and Middle is his stroke button, so the drag becomes a stroke. The global ignore keys (Options › General, F1) would fix it, but Alt + stroke would then be ignored in every app. In Softimage XSI he navigated by holding S, so any key, not just a modifier: "an app could have one or multiple ignore keys on the keyboard. Not just modifiers." Then: "we could I think just copy the steps section and add it under the app group and just have it as an ignore gesture list", per platform "the same way we do with commands", because After Effects on the Mac may not navigate with Alt.

## Decisions (Joel, 2026-10-16)

1. **An app group has an "Ignore gestures while holding" list**, laid out like a command's step list: a card in the group's side panel (under App identification), "New…" adds an entry, entries are rows that can be removed and reordered like steps, and copy, paste, delete and undo work as they do on steps. The list may be empty (the default) or hold any number of entries.
2. **An entry is one input: a keyboard key (any key: S, Space, Alt, F13…) or a mouse button.** It is added either by **recording** (press the key or click the button; the hotkey capture field's machinery, one key, no modifiers combined) or by **picking** from a drop-down (the mouse buttons, and the modifiers, which are awkward to record alone).
3. **Holding any entry when a press starts over that app makes Augram pass the press through untouched**, exactly as a held global ignore key does today. The app is the window under the pointer at the press (as for exclusions and anchors), not the focused window. The key or button itself is never touched: After Effects gets Alt's down and up as always.
4. **Only the held state matters, never what a command sends.** A command whose steps send Alt (a hotkey step "Alt+X") is unaffected: the check runs at the press, before any step. A command in the group whose *trigger* holds an entry ("while holding Alt") cannot fire there while it is held; the ignore list wins and the editor does not warn (Joel: "shouldn't be an issue").
5. **Per platform, as a command's steps are (plan 0010).** "Ignore for: Windows · macOS" above the list, the same view switch: it opens on this platform, the other side is a read-only peek with "Edit on Windows" (or macOS) and **Copy from macOS / Copy from Windows**. A list made on the PC is the macOS side too, converted (inferred), until an edit on the Mac makes the macOS side its own; from then on each machine changes only its own side.
6. **Added to the global ignore keys, never instead of them.** Over After Effects, a held global ignore key and a held After Effects entry both ignore.

## Proposed details (to confirm with Joel)

7. **Same reach as the global ignore keys:** every press that would start something over that app is passed through: the stroke button (gestures, wheel triggers, the Click kind) and a button another command anchors (button triggers, Right + wheel). Hold remaps too, as the global ignore keys stop them (requirements F9). Question A.
8. **The Global group gets no list in this plan;** Options › General keeps the global ignore keys as they are. Folding them into a list on the Global group (one home for "ignore while holding") would move a setting Joel placed there on 2026-10-09 and needs a config migration. Question B.
9. **A group's hold remap hold key cannot also be on its ignore list** (MappingRules refuses it, with the reason shown): the hold key is swallowed by Augram, so "held" would mean two things at one press.
10. **Conversion of the inferred side** uses the hotkey converter that converts a command's steps (F8), key by key, so Alt ↔ Opt and Win ↔ Cmd convert as they do for hotkeys; letters, function keys and mouse buttons stay as they are. An entry the converter has no guess for shows "needs a macOS version" like a step. Question C (whether Ctrl should become Cmd here, as in hotkeys).

## Questions for Joel

- **A.** Should holding an entry also pause the group's hold remaps, as the global ignore keys do? (Proposed: yes, one rule for both lists.)
- **B.** Keep the global ignore keys as tick boxes in Options › General (proposed), or move them into the same list on the Global group?
- **C.** For the macOS side made from a Windows list: should Ctrl become Cmd (the hotkey rule) or stay Ctrl? For navigation keys Ctrl → Ctrl seems more likely; the Mac can always make its own side.
- **D.** The card's title: "Ignore gestures while holding" (proposed), or something shorter such as "Ignore while held"?

## How it works

**Reading "held" at the press, never remembering it** (Capture README, SP.net's stuck ignore mode). The hook already keeps a record of every key's press (`Engine/Input/KeySuppressionShadow`, 256 slots, a key with no event for 2 s counted as released) and of every button this machine saw down (`SuppressionShadow`, the Before buttons). At an anchor press the gate checks the window's entries against those records, the modifiers against the event's mask limited to keys seen down (as `IgnoreKey` is today). No new OS call on the hook thread, and no new tracking: only a read of what is already tracked.
- Known edge: with key repeat off (macOS allows it), a non-modifier key held longer than 2 s without repeating counts as released, so gestures come back. It errs towards strokes, never towards a stuck ignore; noted in the Capture README.

**Per window, off the hook thread.** `IgnoreListWatch` already publishes the pointer's answer for the next press (the exclusion bits, the anchor plan, drag distances, button outputs). The matched group's entries for this platform join it as one more immutable value published beside `ButtonOutputs`, so the hook reads one reference at the press.

## What changes

**Core**
- `Mapping`: the list on `AppGroup` (empty by default; never on the Global group, decision 8), its two sides held the way a command's steps are (decision 5). Step 1 decides, with the one-home rule in mind, whether a command's sides (`Command.Steps`/`OwnVersion`/`StepsFor`/Copy) become one small generic type both use, or the list reuses them another way; a second copy of the own/inferred rules is not an option. `MappingRules`: decision 9.
- `Config`: schema version up one (an older build refuses the file instead of dropping the list). `Sync`: format version up one, the list travels with its group in the merge.
- `Capture`: no change to the state machine; the press's `IgnoreKeyHeld` now means "a global ignore key or one of this window's entries was held".

**Engine** (`Hosting/InputGate`, `IgnoreListWatch`): the entries in the pointer answer; the check at an anchor press and at a hold remap's start (decision 7), beside today's `IgnoreKey` test, both reading `KeySuppressionShadow` and `SuppressionShadow`.

**App**
- The group's side panel: the "Ignore gestures while holding" card, built from the step list's parts (`Components/StepList`, the "Steps for" switch, the peek, Copy) and the capture field (`Components/Capture`) for recording, plus a drop-down for buttons and modifiers. Reuse first; whatever is generalised for it is named in the commit's Reuses list.
- `ViewModels/Commands` (or the group's view model): the list's edits, one undo step each, the view switch state beside the selection as for steps.

**Docs**: requirements F1 (the per-app list beside the global ignore keys), F5 (an app group's parts), Capture README (what `IgnoreKeyHeld` covers, the key-repeat edge), Engine README (the pointer answer's new member), `ViewModels/Commands/README.md`.

## Tests (one per behaviour, beside the neighbours)

- Core: a group's list round-trips through config and sync; an older schema refuses the file; the inferred side follows the original converted; an edit on this platform makes its side own; Copy replaces a side, one undo restores; a hold key on the list is refused.
- Engine (fakes, never real hooks): over a window whose group lists Alt, a stroke press with Alt held passes through and the same press without Alt suppresses; the same with S (a non-modifier, via the key record) and with a held Left; over a window of another group, Alt held changes nothing; an S whose release was missed for over 2 s no longer ignores; a hold remap does not start while an entry is held (if A is yes).
- App: recording a key adds an entry; the drop-down adds a button; the other platform's side is read-only and Copy works from both sides.
- A UiShots render of the card, both sides, dark and light; no test for the look.

## Steps

1. **Core** (agent, effort high: it touches `Sync`). The model, the sides decision, the rules, config and sync versions.
2. **Engine** (agent, high; after step 1). The pointer answer and the checks.
3. **App** (agent, medium; may start on the view model once step 1's types exist). The card, recording, the drop-down, the switch.
4. **Joel's check, PC then Mac.**

## Joel's check

- After Effects' group lists Alt (Windows side). Alt + middle drag navigates the 3D viewport; middle drag without Alt still draws gestures; Alt + middle drag in another app still draws (or is ignored only if Alt is a global ignore key).
- An entry for S: holding S and middle-dragging in that app does not stroke; let go of S and gestures work at once.
- A command in that group sending Alt+something still fires from its gesture when Alt is not held.
- On the Mac the list shows its macOS side inferred from the PC's; an edit there makes it the Mac's own and the PC keeps its Alt.
