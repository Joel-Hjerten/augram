# Plan 0004: a command's own drag distance, and "Not in" for Global commands

**Status: IN PROGRESS (2026-10-10, PC session).** Step 1 built (Core and engine, `eadb13d`). Step 2 built by a worktree agent and merged (`804c5ae`, merge `b27876b`). Step 3 (App) with a worktree agent. Origin: Joel, 2026-10-10. His Global › Media › Zoom In/Out are Right + wheel on the PC, so every right press is held back. Right-drags then started only after 30 px, which lags in Spine and in Eyeris, which pan with Right. He weighed letting the press through and closing the context menu afterwards, then **held it off** (requirements F1). He asked for these two instead. The Options › Capture **Button drag distance** (default 10 px, `c1b7ac0`) came first and becomes the default the per-command value overrides.

## Done when

On the PC (Joel):
- With Zoom In/Out set to their own drag distance (say 3 px), a right-drag in Chrome starts at once and Right + wheel still zooms.
- With Spine and Eyeris ticked in the zoom commands' "Not in", Right pans there with no delay and Right + wheel does nothing special there.
- Menus, gestures and the Mac are unchanged.
- Both machines run the new build and sync resumes.

## Decisions (Joel, 2026-10-10, and the lead's calls he can overrule)

1. **The drag distance belongs to the trigger's "while holding" set** (`TriggerHold.DragDistancePx`, null = the Options value). It means something only for a set without the stroke button, whose anchors are held back and handed back (`HandsBackDrags`). The normaliser drops it everywhere else: a gesture, a click, Stroke + Right (the Mac's zoom). On the stroke button, movement starts a gesture: that is the start distance, shared by every gesture, never per command (Joel meant Zoom, not Volume). Range 1 to 200 px. It sits in the trigger, so a platform's own version can have its own.
2. **Over one window the largest distance wins** (lead's call). A press is decided before the wheel says which command it is, so it waits as long as any command holding that button there could still want it. A command without its own counts as the Options value. Packed per anchor in `Capture/AnchorDragDistances` next to the `AnchorPlan`, published by the watch, carried by each press. The hook decides nothing from it.
3. **"Not in" is a Global command's list of app groups** (`Command.NotIn`). Over a window of a listed group the command is as if absent: it fires nothing (resolver reason `'Zoom In' is not used in 'Spine'`) and holds no button back (`AnchorPlanner`). It lists app groups, not ignored apps: an ignored app has Augram off already. An app with no commands still needs a group to be recognised. The group's own Use on covers the platforms, so the list is not per platform. Command level only; a Global category's "Not in" can come later if Joel wants it. Normalised, never refused: only Global commands keep it, unknown and deleted groups drop out, sorted by id.
4. **Formats:** config schema 5 and sync format 12, raised together (step 2). An older build would drop both on save or publish. As with 0.7.0, both machines update before sync resumes.

## Steps

1. **Core and engine (built, lead).** `TriggerHold.DragDistancePx`, `HandsBackDrags` and `MaxDragDistancePx`; `Command.NotIn` and `IsNotIn`; `MappingRules` (range, `NotIn` normalisation); `CommandResolver`; `AnchorPlanner.AnswerForGroup`; `Capture/AnchorDragDistances`; `CaptureEvent.ButtonDown.Drags`; the machine's hand-back distance; `IgnoreLookup` / `IgnoreListWatch` / `InputGate` publishing and attaching the distances. Tests: `Core.Tests/Mapping/NotInTests`, `Core.Tests/Capture/DragDistanceTests`, two `ChordEngineTests`. READMEs: Mapping (rule 11, resolver rule 5), Capture, Engine.
2. **Config, sync, export/import (agent).**
   - Config schema 5 (a no-op migration with its comment, as 3 → 4): `hold.dragDistancePx` (omitted when null) and a command's `notIn` (group id strings, omitted when empty).
   - Sync format 12 with its comment line: command items carry both; the merge's repairs leave nothing naming a group that is gone (normalisation does it when the merged document is validated: check).
   - Transfer (plan 0003): a command's `notIn` remapped where the import maps a group id to a local one; dropped where the group is not there.
   - Round trip, defaults, format guard and merge tests; Config, Sync and Transfer READMEs.
   - **Built:** an out-of-range `dragDistancePx` in a hand-edited file is dropped with a notice (the Options value is used), never the whole file; a `notIn` entry that is no id is dropped with a notice; a merge drops a "Not in" naming a group the other machine deleted, silently (no repair line). `ConfigSession` puts a Global command's "Not in" back after a piecemeal load (Global loads before its app groups). **Known edge:** exporting Global without the groups its commands name drops those entries from the file (the export is validated like any mapping), so importing it with Take theirs clears them here; select the groups with Global to keep them (Transfer README).
3. **App (agent).**
   - In the command's trigger area, for a trigger whose set `HandsBackDrags` on the platform shown: "Drag distance". Either the Options value ("Options value, 10 px") or its own (1–200 px), with an ⓘ.
   - On a Global command: "Not in", the app groups as a check list (existing check-list field kind), with an ⓘ ("Over these apps the command does nothing and holds no button back").
   - The command row shows nothing new. Dev gallery entries. App README.
4. **Joel's check (PC, then Mac).** Install the build (Joel runs Setup.exe). Create app groups for Spine and Eyeris (an executable each). Tick both in Zoom In's and Zoom Out's "Not in". Set the zoom commands' drag distance and try Chrome. Then the Mac build, so sync resumes.

## Not in this plan

Letting an anchor's press through (held off, requirements F1); a per-category "Not in"; a per-app-group drag distance; anything for the stroke button's start distance.
