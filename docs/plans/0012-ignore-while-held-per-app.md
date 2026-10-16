# Plan 0012: Exclusions › While holding, an app's keys and buttons that make gestures pass through, per platform

Status: **BUILT as 0.20.0 (2026-10-16, PC session), awaiting Joel's check, PC then Mac.** Decided by Joel the same day. Built choices to look at: Move to stays between Global and Per command (a While holding entry has none); Copy to … shows on a peek row only when that side has its own list; an entry whose app is also on Global says its list then adds only the hold remaps; a listed mouse button whose release the hook missed counts as held until its next press or a hook reset (keys age out after 2 s). Origin: Joel, 2026-10-16, on the PC. After Effects navigates its 3D viewport with Alt + middle drag, and Middle is his stroke button, so the drag becomes a stroke. The global ignore keys (Options › General, F1) would fix it, but Alt + stroke would then be ignored in every app. In Softimage XSI he navigated by holding S, so any key, not just a modifier: "an app could have one or multiple ignore keys on the keyboard. Not just modifiers." The list is laid out like a command's step list ("just copy the steps section"), per platform "the same way we do with commands", because After Effects on the Mac may not navigate with Alt. First placed on the app group; moved the same day to a third Exclusions list (Joel: "It's more clear. And easier to get an overview, it's cleaner").

## Decisions (Joel, 2026-10-16)

1. **A third list on the Exclusions tab: Global · Per command · While holding.** An entry is an app, identified as the other Exclusions entries are (the same matcher form, window finder, known-app guess and "Fields for: Windows · macOS" switch, plan 0010 decision 7), and its **"Ignore gestures while holding"** list. It needs no app group: an app with no commands gets its passthrough here.
2. **The list is laid out like a command's step list**: a card under the app's identification, "New…" adds an entry, rows can be removed and reordered, and copy, paste, delete and undo work as they do on steps. An entry is **one input: any keyboard key (S, Space, Alt, F13…) or a mouse button**, added by **recording** (press the key or click the button; the hotkey capture field's machinery, one key, no combination) or by **picking** from a drop-down (the mouse buttons and the modifiers, awkward to record alone).
3. **Holding any input on the list when a press starts over that app makes Augram pass the press through untouched**, as a held global ignore key does today. The app is the window under the pointer at the press (as for the other Exclusions lists and for anchors), not the focused window. The key or button itself is never touched: After Effects gets Alt's down and up as always.
4. **Only the held state matters, never what a command sends.** A command whose steps send Alt ("Alt+X") is unaffected: the check runs at the press, before any step. A command whose *trigger* holds a listed input ("while holding Alt") cannot fire over that app while it is held; the list wins and the editor does not warn (Joel: "shouldn't be an issue").
5. **Per platform, as a command's steps are (plan 0010).** One "Ignore for: Windows · macOS" switch at the top of the entry's card, not one per input; the same view switch: it opens on this platform, the other side is a read-only peek drawn as a command's other side is (muted, "Edit on Windows" or macOS). A list made on the PC is the macOS side too, converted ahead of time (inferred), until an edit on the Mac makes that side its own; from then on each machine changes only its own side.
6. **Copy from the other platform, whole list or one input.** The whole list: **Copy from macOS / Copy from Windows** on the switch's line, as for steps (the side becomes inferred again). One input (Joel: "per key or button"): each row of the peek has **Copy to Windows** (or macOS), which adds that input, converted, to this platform's list (making it own if it was inferred). One undo step each. New compared with steps, which only copy whole lists.
7. **Added to the global ignore keys, never instead of them.** Over After Effects, a held global ignore key and a held listed input both ignore. **The global ignore keys stay tick boxes in Options › General** (F1, Joel 2026-10-09); the Global group gets no list.
8. **Same reach as the global ignore keys** (Joel: "let's go with your decision for A right now"): every press that would start something over that app passes through: the stroke button (gestures, wheel triggers, the Click kind), a button another command anchors (button triggers, Right + wheel), and a hold remap's hold key. Each is decided at its own press, so the order counts: in Blender, Alt held then Space pressed starts no hold remap; Space first (the hold remap running) then Alt leaves the hold remap running. A toggle per entry may come later if use shows the need.
9. **Several entries may match one window;** their lists for this platform add up. An app on Exclusions › Global as well needs nothing more (Augram is out of it anyway); the form says so.
10. **Hold keys conflict, and the editor says so** (Joel: "someone trying to put Space as an ignore button for Blender"). A hold remap's hold key is swallowed by Augram, so listing it for the same app cannot work. Two parts:
    - **Warned in the editor, both ways:** a listed input that is the hold key of a hold remap in an app group matching the same app shows a warning on its row ("Space is the hold key of Blender › Navigate; while Navigate runs it never ignores"), and the hold remap's hold key field shows the same from its side. "The same app" is decided from the matchers: an executable name (this platform's, case-insensitive, non-regex) in both. A warning, not a refusal, because matchers with regex or other fields cannot be compared reliably.
    - **At the press, a key Augram swallowed never counts as held** (a hold key, a key pressed while a hotkey field captures): only keys the OS saw go down count, so a conflict the editor could not see still behaves predictably.
11. **Conversion of the inferred side** uses the hotkey converter that converts a command's steps (F8), key by key, unchanged (Joel: "the same kind of conversion"): Ctrl ↔ Cmd, Alt ↔ Opt as for hotkeys; letters, function keys and mouse buttons as they are. An input with no guess shows "needs a macOS version" like a step. The other machine sets its own side by hand when the guess is wrong.
12. **Names:** the list **While holding**, the card **Ignore gestures while holding** (Joel agreed).

## How it works

**Reading "held" at the press, never remembering it** (Capture README, SP.net's stuck ignore mode). The hook already keeps a record of every key's press (`Engine/Input/KeySuppressionShadow`: up, passed or owed per key, 256 slots, a key with no event for 2 s counted as released) and of every button this machine saw down (`SuppressionShadow`, the Before buttons). At an anchor press the gate checks the window's inputs against those records: a key counts when its record is *passed* (decision 10), a modifier also through the event's mask as `IgnoreKey` is read today. No new OS call on the hook thread and no new tracking.
- Known edge: with key repeat off (macOS allows it), a non-modifier key held longer than 2 s without repeating counts as released, so gestures come back. It errs towards strokes, never towards a stuck ignore; noted in the Capture README.

**Per window, off the hook thread.** `IgnoreListWatch` already publishes the pointer's answer for the next press from the Exclusions entries (the exclusion bits) and the groups (anchor plan, drag distances, button outputs). The matching While holding entries' inputs for this platform join it as one more immutable value published beside `ButtonOutputs`, so the hook reads one reference at the press.

## What changes

**Core**
- `Mapping/IgnoredApp`: a third `IgnoreScope`, `WhileHolding`, and the entry's inputs with their two sides, held the way a command's steps are (decision 5). Step 1 decides, with the one-home rule in mind, whether a command's sides (`Command.Steps`/`OwnVersion`/`StepsFor`/Copy) become one small generic type both use, or the list reuses them another way; a second copy of the own/inferred rules is not an option. `MappingRules`: a While holding entry's inputs are distinct per side; `DisableEntirely` does not apply to it. The hold-key conflict (decision 10) is one query here, used by both forms: the entries and hold remaps whose matchers share an executable name on a platform and whose input is the hold key (a small matcher comparison beside `AppMatcher`; none exists today).
- `Config`: schema version up one (an older build refuses the file instead of dropping or misreading the entries). `Sync`: format version up one, the entries travel as the other Exclusions entries do. `Transfer` (F8 export) takes them with the Exclusions.
- `Capture`: no change to the state machine; the press's `IgnoreKeyHeld` now means "a global ignore key or one of this window's listed inputs was held".

**Engine** (`Hosting/InputGate`, `IgnoreListWatch`, `IgnoreLookup`): the inputs in the pointer answer; the check at an anchor press and at a hold remap's start (decision 8), beside today's `IgnoreKey` test, reading `KeySuppressionShadow` and `SuppressionShadow`.

**App**
- `ViewModels/Ignored` (`IgnoredViewModel`, `IgnoredEditViewModel`, beside `IgnoredViewModel.PerCommand.cs`): the third list and its entry's edits, one undo step each; the view switch state beside the selection as for steps.
- The tab's third segment and the entry's side panel: the matcher form as on the other lists, then the "Ignore gestures while holding" card built from the step list's parts (`Components/StepList`, the "Steps for" switch, the peek, Copy) and the capture field (`Components/Capture`) for recording, plus a drop-down for buttons and modifiers. Reuse first; whatever is generalised for it is named in the commit's Reuses list.
- `DevGallery/IgnoredGalleryPages`: a While holding entry, for UiShots.

**Docs**: requirements F1 (the list beside the global ignore keys) and F5 (Exclusions has three lists), Capture README (what `IgnoreKeyHeld` covers, the key-repeat edge), Engine README (the pointer answer's new member), `Core/Mapping` README.

## Tests (one per behaviour, beside the neighbours)

- Core: the hold-key conflict query finds a shared executable name and ignores a regex matcher; a While holding entry round-trips through config, sync and export; an older schema refuses the file; the inferred side follows the original converted; an edit on this platform makes its side own; Copy replaces a side, one undo restores.
- Engine (fakes, never real hooks): over a window matching an entry listing Alt, a stroke press with Alt held passes through and the same press without Alt suppresses; the same with S (a non-modifier, via the key record) and with a held Left; over another app, Alt held changes nothing; an S whose release was missed for over 2 s no longer ignores; a swallowed hold key does not ignore; two matching entries add up; a hold remap does not start while a listed input is held, and one already running is not stopped by a listed input pressed after its hold key (decision 8's order).
- App: the third list adds, renames and deletes entries like the others; recording a key adds an input; the drop-down adds a button; the other platform's side is read-only; Copy of the whole list works from both sides and Copy to … on one peek row adds that input converted, each one undo step; a listed hold key shows the warning on the row and on the hold remap's form.
- A UiShots render of a While holding entry, both sides, dark and light; no test for the look.

## Steps

1. **Core** (agent, effort high: it touches `Sync`). The scope, the inputs and their sides, the rules, config, sync and export.
2. **Engine** (agent, high; after step 1). The pointer answer and the checks.
3. **App** (agent, medium; may start on the view model once step 1's types exist). The list, the card, recording, the drop-down, the switch, both Copies, the conflict warnings.
4. **Joel's check, PC then Mac.**

## Joel's check

- Exclusions › While holding: add After Effects (window finder) and Alt on its Windows side. Alt + middle drag navigates the 3D viewport; middle drag without Alt still draws gestures; Alt + middle drag in another app still draws (unless Alt is a global ignore key).
- Add S: holding S and middle-dragging in After Effects does not stroke; let go of S and gestures work at once.
- An After Effects command sending Alt+something still fires from its gesture when Alt is not held.
- Exclusions › While holding: add Blender and Space. The row warns that Space is the hold key of Blender's hold remap, and that hold remap's form says the same.
- On the Mac the entry's macOS side is inferred from the PC's list; an edit there makes it the Mac's own and the PC keeps its Alt.
