# Plan 0010: a command's steps per platform, seen side by side, and read-only fields for the other platform

Status: **BUILT as 0.14.0 (2026-10-12, Mac session), awaiting Joel's check.** Proposed and revised the same day, wording settled. Origin: Joel, 2026-10-12, on the Mac, after the magnifier's own macOS version kept a Windows step that read "Alt+Shift+X · from Windows" while it ran Opt+Shift+X (fixed in 0.13.9). He first asked for a "Unified · Windows · macOS" switch, then rethought it: "Perhaps we only have Win and macOS, no unified tab. … the fields toggle is not setting a state but rather just switching views and allowing the copy."

## Decisions (Joel, 2026-10-12)

1. **A "Steps for: Windows · macOS" switch above a command's steps**, the same segmented control as app identification's "Fields for" (`ButtonRadioField`). **It only switches the view**; it never sets anything. It opens on the platform Augram runs on.
2. **The platform you are on is the one that runs, and it is drawn that way.** Its side is the normal, editable list, labelled with its platform's name as the one running here ("macOS · runs here" on the Mac, "Windows · runs here" on the PC; Joel: "platform specific"). The other side is a peek: read-only, drawn a step back (muted, no edit controls), labelled with where its steps come from (below), with a note "Edit on Windows".
3. **A side is either its own or inferred.** A command made on the Mac has macOS steps; the Windows side is inferred: the macOS steps converted (F8's best guess: Cmd+W → Ctrl+W, the exception table, "needs a Windows version" where there is no guess). An inferred side follows the other side's current steps: an edit on the Mac reaches the inferred Windows side through the conversion.
4. **An edit on your platform makes your side its own.** On the PC, the inferred Windows steps are there by default; the first edit there makes them Windows' own, and from then on nothing done on the PC changes the macOS side, and nothing done on the Mac changes the Windows side. Today's "own macOS version" marker and the stale flag ("· Windows original changed", Mark as checked) stay.
5. **Copy from the other platform**: one button that throws a side's own steps away and takes the other side's steps, converted (the side becomes inferred again, so it follows the other side from then on). It acts on **the side in view**: standing on Windows it reads "Copy from macOS"; with the macOS side open it reads "Copy from Windows" (Joel: the platform's name, clearer than "the other platform"). Shown only when the side in view has its own steps (an inferred side already is the other side's). One undo step.
6. **The other side is never edited field by field.** Copy (decision 5) is the one way to change it from here: a whole-list replace, deliberate and undoable. Why: nothing about the other platform can be captured, picked or tried from this machine, each machine writes only its own side's fields, and "you change what runs here" is one rule for the whole app.
7. **The same read-only rule for every "Fields for" switch** (Exclusions › Global, Exclusions › Per command, the Apps tab's app identification): the other platform's side shows its values read-only with the "Edit on Windows" note. Today they let the Mac edit the Windows fields. The known-app guess (`Core/Mapping/KnownApps`) still fills the other side for common apps. Copy is not offered there (an exe name or a window class does not convert).
8. **No format change.** It is the model a command already has: the original list (`Steps`, each step with its `authoredOn`) is the side it was made on; `OwnVersion` is the other side's own steps; no own version means that side is inferred. Copy into the own version's side is `WithoutOwnVersion`; copy into the original's side makes the own version's steps the original and drops the own version. Config schema 7 and sync format 15 stay.

## What changes

**Core (`Augram.Core/Mapping/Command`)**
- "Copy into the original's side": the own version's steps become the original (their `authoredOn` already says where they were made) and the own version goes. Beside `WithoutOwnVersion`, which is already "copy into the own version's side". Both one store call, so one undo step.

**App**
- `ViewModels/Commands` (steps): the switch's view state lives beside the selection, never in the store, like `AppMatcherFormView`; it resets to this platform when another command is selected. The step list binds to the side in view. On the other side, add, edit, remove, reorder, paste, change-to and capture are off.
- `Components/CommandHeader` and the step list's look: the running side's label, the peek's muted look and its "Edit on …" note, the Copy button (decision 5). The version line ("Own macOS steps; the Windows original runs on Windows.") and "Use the converted original" go (Joel, 2026-10-12): the side labels say where steps come from, and Copy from Windows on the macOS side is that button. Mark as checked stays, for a stale own version.
- `ViewModels/AppMatcherEditViewModel` and its form: the other platform's fields read-only, with the note (decision 7).
- `Components/StepList/StepPlatformMarker`: the peek reads each step as it runs on that platform, in its key names.

**Docs**: requirements F8 (the view switch, own and inferred sides, Copy, the read-only other side), `ViewModels/Commands/README.md` (own versions), the App README's rules of the road for the read-only rule.

## Tests (one per behaviour, beside the neighbours)

- Core: copying into the original's side makes the own version's steps the original and drops it; into the own version's side drops it (exists today, keep).
- App, Commands: the switch opens on this platform and resets on another selection; the other side is read-only (no edit reaches the store) and reads in that platform's key names; an edit on this platform's inferred side makes it own and leaves the other side's steps unchanged; Copy on either side replaces that side's steps with the other's, one undo restores them; Copy is hidden on an inferred side.
- App, app identification: the other platform's fields are read-only on Exclusions › Global, › Per command and the Apps tab.
- A UiShots render of both sides of a command with its own macOS steps, dark and light; no new test for the look.

## Steps

1. **Core** (lead or agent, effort medium: `Core/Mapping` is outside the high-effort folders). Decision 8's copy into the original's side, with its test.
2. **App, steps** (agent, medium; after step 1). Decisions 1–6. Version **0.14.0**.
3. **App, app identification** (same agent, or the lead if it stays small). Decision 7.
4. **Joel's check, Mac then PC.** Below.

## Joel's check

On the Mac, then on the PC:
- A command opens on this platform's side, labelled as running here. Switching to the other side shows its steps in that platform's key names, muted, with "Edit on …", and nothing there can be changed except by Copy.
- A command made on the PC shows its macOS side inferred (converted) on the Mac. Editing a step there makes the macOS side its own; the PC still runs what it ran before.
- On a command with its own macOS steps: on the macOS side, Copy from Windows brings back the converted Windows steps; on the Windows side, Copy from macOS makes the PC run the macOS steps (converted). The undo key gives the old steps back either time.
- Exclusions and the Apps tab: the Windows fields are read-only on the Mac, the macOS fields on the PC.
- The magnifier and Open Explorer read clearly: the Windows side "Shift+Win+X" and "explorer", the macOS side Joel's own steps.

## Settled wording (Joel, 2026-10-12)

1. The running side is labelled with its platform's name: "macOS · runs here".
2. The button names the platform: "Copy from macOS", "Copy from Windows".
