# Plan 0010: a command's steps per platform, and read-only fields for the other platform

Status: **PROPOSED (2026-10-12, Mac session), waiting for Joel's go-ahead.** Origin: Joel, 2026-10-12, on the Mac, after the magnifier's own macOS version kept a Windows step that read "Alt+Shift+X · from Windows" while it ran Opt+Shift+X (fixed in 0.13.9). He asked whether steps should have a Windows list and a macOS list, unified while they are the same and both listed where they differ, and pointed at the "Fields for: Windows | macOS" switch that Exclusions and the Per command identification already have: "[Unified, Windows, macOS] perhaps?".

## Decisions (Joel and the lead, 2026-10-12)

1. **A "Steps for" switch above a command's steps: Unified · Windows · macOS.** The same segmented control as app identification's "Fields for" (`ButtonRadioField`), not a new one.
2. **Unified means one list that Augram converts between the platforms** (F8's best guess: Ctrl+W ↔ Cmd+W, the exception table, "needs a macOS version" where there is no guess). It is selected while the command has no own version. An edit in Unified changes that one list, so it reaches both platforms. The edited step is authored on the machine it was edited on (0.13.9's rule), and the other platform runs its conversion. The step markers ("Cmd+W → Ctrl+W", "from Windows · converted") keep that visible.
3. **This platform's segment (macOS on a Mac, Windows on the PC) edits this platform's steps only.** The first edit there splits the lists: this platform gets its own list and the other keeps exactly what it ran before. After a split the switch shows this platform, and the command row says "own macOS version" as today.
4. **The other platform's segment is a read-only peek** (Joel: "I don't think it makes a lot of sense for any of the Fields for toggles to allow you to edit the other platform"). It shows the steps as they run there, in that platform's key names, with a note "Edit on Windows" (or "Edit on macOS"). Why: nothing about the other platform can be captured, picked or tried from this machine, each machine writes only its own side so the two cannot both change one list, and "you change what runs here" is one rule for the whole app.
5. **Choosing a segment only changes the view; an edit moves it.** Peeking is free and never saved. A command opens on Unified when it has no own version, else on this platform. An edit in Unified stays in Unified; a split (decision 3) moves to this platform.
6. **Back to Unified after a split asks which list to keep**: "Keep the macOS steps for both" or "Keep the Windows steps for both". The kept list becomes the one list; the other is dropped. One undo step gives the split back. This replaces today's "Use the converted original" button (keeping the original's side is what that button did).
7. **The same read-only rule for every "Fields for" switch**: Exclusions › Global, Exclusions › Per command, and the Apps tab's app identification. Today they let the Mac edit the Windows fields; after this plan the other platform's side shows its values read-only with the same "Edit on Windows" note. The known-app guess (`Core/Mapping/KnownApps`) still fills the other side for common apps, as today. Creating an entry on one machine leaves the other side empty (or guessed) until that machine edits it.
8. **No format change.** A command already holds the original list (`Steps`, each step with its `authoredOn`) and at most one own version (`OwnVersion`, for the platform that is not the origin). Unified is "no own version", a split is "an own version", and the choices in decision 6 are `WithoutOwnVersion` and its mirror. Config schema 7 and sync format 15 stay; nothing new crosses machines.

## What changes

**Core (`Augram.Core/Mapping/Command`)**
- An edit in Unified writes the original list even on the platform that is not the origin (today `WithStepsFor` sends every edit there to the own version). A new method beside `WithStepsFor`, for example `WithUnifiedSteps(steps)`.
- A split made on the origin's platform: the own version goes to the *other* platform as `ConvertedFor(other)`, then the edit lands on the original. Today a split can only be made from the non-origin platform.
- `Origin` is read from the first step's `authoredOn` when there is no own version. Once Unified edits can mix where steps were authored, the origin must not flip under an existing own version. Fix the origin when a split is made, or derive it from the own version's platform only (it already does that when one exists); step 1 settles which and tests it.
- "Keep this side for both": with the original's side, `WithoutOwnVersion` (as today); with the own version's side, its steps become the original and the own version is dropped.

**App**
- `ViewModels/Commands` (steps): the switch's view state lives beside the selection, never in the store, like `AppMatcherFormView`. The step list binds to the chosen side. On the other platform's side, the list's add, edit, remove, reorder, paste, change-to and capture actions are off; the note says where to edit.
- `Components/CommandHeader`: the version text and "Use the converted original" give way to the switch and the keep-which-list question (decision 6). "Mark as checked" for a stale own version stays.
- `ViewModels/AppMatcherEditViewModel` and its form: the other platform's fields read-only, with the note (decision 7).
- Words and markers in `Components/StepList/StepPlatformMarker` stay; the read-only side reads each step with `SummaryOn(that platform)` of its plan there.

**Docs**: requirements F8 (the switch, Unified, read-only other side), `ViewModels/Commands/README.md` (own versions), the App README's rules of the road if the read-only rule needs a line, learnings only if something surprises.

## Tests (one per behaviour, beside the neighbours)

- Core: a Unified edit on the non-origin platform changes the original and makes no own version; a split from the origin's platform gives the other platform its converted list; the origin does not flip after a mixed Unified edit; keeping either side for both.
- App, Commands: the switch opens on Unified without an own version and on this platform with one; an edit on this platform's side splits and moves the switch; the other side is read-only (no edit reaches the store) and reads in that platform's names; Unified after a split asks, and undo restores the split.
- App, app identification: the other platform's fields are read-only on Exclusions › Global, › Per command and the Apps tab.
- A UiShots render of the switch in each state, dark and light; no new test for the look.

## Steps

1. **Core** (agent, effort medium: `Core/Mapping` is outside the high-effort folders). Decisions 2, 3, 6 and the origin rule, with their tests.
2. **App, steps** (agent, medium; after step 1). The switch, the read-only side, the keep-which-list question, the header's buttons. Version **0.14.0**.
3. **App, app identification** (same agent as step 2, or the lead if it stays small). Decision 7.
4. **Joel's check, Mac then PC.** Below.

## Joel's check

On the Mac, then on the PC:
- A command with the same steps on both opens on **Unified**. Editing a hotkey there shows the other platform's conversion in the step's marker, and the other machine runs it after sync.
- Choosing **macOS** and changing a step gives the Mac its own list; the row says "own macOS version"; the PC still runs what it ran before.
- Choosing **Windows** on the Mac shows the Windows steps in Windows key names, with "Edit on Windows", and nothing there can be changed.
- Choosing **Unified** on a split command asks which list to keep; the undo key gives the split back.
- Exclusions and the Apps tab: the Windows fields are read-only on the Mac, the macOS fields read-only on the PC.
- The magnifier and Open Explorer read clearly: the Windows side "Shift+Win+X" and "explorer", the macOS side Joel's own steps.

## Open questions for Joel

1. **The Unified segment's name.** "Unified", "Both" or "Same on both"? The plan says Unified, his word.
2. **The row marker after a split.** Keep "own macOS version", or say "differs on Windows"?
3. **A Unified edit on the non-origin machine re-authors only the steps it touches** (the rest keep where they were made). The alternative is to re-author the whole list on the machine that edits it. The plan takes the first: it changes only what the edit touched.
