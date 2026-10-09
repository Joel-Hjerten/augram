# Plan 0002: Hold remaps (Blender first)

**Status: ACTIVE** — written 2026-10-10. Joel put this ahead of the rest of M2 (plan 0001 M2 steps 8–9 and M3 wait). The *what* is [requirements F9](../requirements.md) (read it first, all of it); this plan says *in what order* and *where the code goes*, and records the design decisions an implementer needs. Structure rules: [ADR-0002](../adr/0002-code-and-repo-structure.md). Origin and the behaviour to beat: [reference/autohotkey-navigation-for-blender/](../reference/autohotkey-navigation-for-blender/README.md).

## Done when

Joel uninstalls AutoHotkey on the PC. Concretely, on both machines, in Blender: Space + Left orbits, Space + Right pans, Space + Middle and Space + Left + Right zoom, rolling between them mid-drag works; a Space tap types a space and a long Space never starts playback; "a b" typed fast in a Blender text field stays "a b"; W, E, R, F do their Space versions; nothing is ever left held; Space everywhere else, and gestures everywhere, behave as before.

## Decisions taken for this plan (D12 and design)

Joel can overturn any of these; each is cheap to change before step 3.

1. **Editor:** a **Hold remaps** section on the app group's page (Commands › Apps › a group), under the identification fields. Global has none for now (requirements D12).
2. **No hold-time threshold for letter hold keys** yet; the tap time is the only timer. A letter hold key (S) is allowed, with the rollover rule protecting typing.
3. **Hold keys may not be Ctrl, Alt, Shift, Win/Cmd or Fn.** Those are already "while holding" keys of triggers (F1), and the After-key logic owns them.
4. **A "Run command" row points at an existing command** of the same app group or of Global, by id (F8 stable ids), rather than holding its own step list. The command keeps every feature it has (step types, own platform versions, Use on); Joel's "hub for launching other commands" is literally that. A command used by a row is listed in its "Used by" and warned about on delete, as a gesture is. It runs on the input's press, once per press (key repeats ignored), once per wheel notch; target window: the foreground window, as for the hold key itself.
5. **One hold remap active at a time.** Another hold key pressed while one is held is an ordinary input of the active one (a row or not).
6. **Any mouse button or wheel turn cancels the tap**, row or not (Joel: "if a mouse button is pressed while space is held then no space will be sent"). Moves never do. Ctrl/Alt/Shift/Win pressed during the hold pass through and do not cancel it, so Shift held across a Space tap gives Shift + Space.
7. **Rows match the held set exactly.** The remap's output is the row whose button set equals the row buttons held right now; no row for that set → no output held. On a change: release the old output first, then press the new one.
8. **The hold key belongs to the foreground app**, as published by the watch (below); inputs pressed during the hold use that decision even over another app's window on a second display.

## Design

### Core: `Core/HoldRemaps/` (new folder, pure, with a README)

| Type | Role |
|---|---|
| `HoldRemapId`, `HoldRowId` | Guids (F8) |
| `HoldRemap` | `Id`, `HoldKey` (`KeyCode`), `TapTimeMs` (default 180), `IsActive`, `UseOn` (`PlatformSet`), `Rows` |
| `HoldRow` | `Id`, `Input`, `Action`, `IsActive` |
| `HoldInput` | closed set: `Buttons(HeldButtons set)`, `Wheel(WheelDirection)`, `Key(KeyCode)` |
| `HoldAction` | closed set: `Remap(RemapOutput, RemapOutput? MacOutput)` (outputs are **never converted**; the optional macOS output overrides), `RunCommand(CommandId)` |
| `RemapOutput` | `Button(MouseButton, KeyModifiers)` (modifiers around the down only), `Key(KeyCode, KeyModifiers, rightHand)` (modifiers held for the whole press), `Wheel(ScrollDirection, KeyModifiers)` (one notch per input notch; wheel inputs only) |
| `HoldRemapRules` | called by `MappingRules`: hold key not a modifier; hold keys unique per group; inputs unique per hold remap; a button set non-empty; a wheel input takes a Wheel or Key output or RunCommand; a RunCommand target that is gone is kept and reported ("command gone"), never refused, as a missing gesture is |
| `HoldRemapPlan` | what the hook needs for one app group on this platform, built off the hook thread: per hold key, its tap time and its row inputs as bit sets (buttons, wheel directions, a 256-bit key set). Immutable; the hook reads it through one volatile reference |
| `HoldRemapMachine` | the behaviour, pure and table-tested (the worker runs it, like `CaptureStateMachine`): events `HoldDown`, `HoldUp`, `Button`, `Wheel`, `Key`, `Rollover`, `Reset`, each with the event's timestamp; outcomes `TapHoldKey`, `PressOutput`, `ReleaseOutput`, `WheelOutput`, `ReplayKey`, `RunCommand`. Owns: the tap decision (released within `TapTimeMs`, phase still Pending), the rollover, the rolling between button sets (rule 7), and the pairing of every output down with its up, also across `HoldUp` before the buttons and across `Reset` |

`AppGroup` gains `HoldRemaps` (sorted by hold key name, empty by default). `MappingStore` gets `UpdateHoldRemaps(groupId, …)` or goes through `UpdateGroup`; one undo step either way. The store gains a "used by" for commands (the hold remap rows that run one), beside the existing one for gestures. `KnownApps` learns `Blender.exe` ↔ `Blender` (Platform.MacOS README, ignore-list check 5).

### Config and sync

`holdRemaps` on a group in the config file (omitted when empty); the Config README documents the members. Sync: a new item kind `holdRemap:<id>`, content `{ "group": <id>, "holdRemap": { … } }` like a command, so a Space remap edited on the Mac and a command edited on the PC merge without a conflict. Raise `SyncFile.CurrentFormatVersion` to 11 (a format 10 build would drop the hold remaps and publish the group without them) and, if the reader needs it, the config schema, with the comment lines those constants carry.

### Engine: the hook thread (`Engine/Input/HoldRemapShadow`, called from `InputGate.Handle`)

The hook's copy of the facts it needs to answer "suppress?" for hold remaps, as `SuppressionShadow` is for gestures; single writer, no allocation beyond the one message, no window lookup (invariant 1). It is asked **before** the gesture shadow, so a row button never reaches gesture capture even when it is the stroke button (F9 "Hold remaps before gestures").

- **Hold key press:** claimed (suppressed, owed with its repeats and release in `KeySuppressionShadow`, which gets a general "claim this press" input besides its modifier claim) iff the published plan has this key, the engine is enabled, not paused by a focused app, the ignore key is up, no hotkey capture is armed, no gesture press is owned, no hold is active, and this is a fresh press (not a repeat of a press the OS saw). The "over an ignored app" bit does **not** stop it: Joel's Blender is on the ignore list in that mode (plain Middle keeps navigating there) and must keep its hold remap.
- **While held:** a row button's down is suppressed and owed (its up suppressed whatever the hold does meanwhile); a row wheel turn is suppressed; a row key's press is claimed. A non-row key within the tap time and before anything was used is the rollover: claimed, and the worker sends the hold key's tap and then **replays** that key's press; its repeats and release are suppressed and replayed too (a `replayed` key set in the shadow). Everything else follows the existing paths.
- **Messages:** one `WorkerMessage.Hold(...)` per decision, critical like a button. A message that cannot be enqueued undoes the decision (the shadow's `Save`/`Restore` pattern), except a release, which is still consumed.
- **The hold key's release** while a key-repeat-less hold outlives `KeySuppressionShadow.LostReleaseAfterMs` (2 s): the hold must still own it. Decide in step 3 with a test (macOS can have key repeat off; Space is held through long orbits).

### Engine: the watch and the worker

- **Foreground answer.** `IgnoreListWatch` (and `IgnoreLookup`) also watch focus while any active hold remap can match on this platform, find the foreground's app group on a change of `ForegroundKey`, and publish its `HoldRemapPlan` (`InputGate.PublishForeground`). Staleness is the 200 ms focus poll: Space pressed within 200 ms of switching into Blender may pass through once. Platform foreground notifications (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, `NSWorkspaceDidActivateApplicationNotification`) can remove that later if Joel ever notices.
- **Worker.** `EngineWorker` runs the `HoldRemapMachine` and cross-checks every hook decision against it (Warning on a mismatch, as for capture). It injects the outputs itself (low latency, in order with the input): a button output as Ctrl/Shift… down, `Press(button, x, y)` at the event's position, modifiers up; a key output with its modifiers held through the press; a wheel output through `Scroll` (claimed by `OwnWheelInjections`). `RunCommand` goes to the `CommandExecutor` as an `ExecutionRequest` for that command. `ResetMachine` and engine stop release every held output (A19).
- **Log source `hold`:** Info when hold remaps start or stop being watched; Debug per hold (`Hold`, key, app, then `Tap sent` / `Tap not sent` with the held time and why, `Rollover` with the key, `Output` changes); Warning for a failed injection. Engine README: the thread table, a "Hold remaps" section, the log table. `docs/reference/threading.md` if it lists the hook's work.

### App

The **Hold remaps** section on the app group page (wireframe first, ADR-0002 §5b; declared, §5c): a list of hold remaps (hold key, tap time, active, Use on) and, for the selected one, its rows: **Input** (buttons by detect-to-assign, several for a set; wheel up/down; a key by the hotkey capture field in one-key mode), **Action** (Remap: output kind, button/key/wheel, modifier toggles, optional macOS output; or Run command: a picker of this group's and Global's commands), Active. Header text: "Hold remaps — remap inputs or run commands while a key is held". Joel shapes the details when he sees it running.

## Steps

0. **macOS spike (Joel runs it; nothing else waits on Windows).** A throwaway console app under `spikes/hold-remap-mac/` on SharpHook 7.1.3, Blender's rows hard-coded, active only while Space is held, exits by itself after three minutes. It answers: (a) with the physical Left suppressed and a Middle down posted, does Blender orbit on the hardware's left-drag events; same for Shift + Middle and Ctrl + Middle (do posted modifiers reach Blender with the button); (b) does rolling (release Middle, press Ctrl + Middle mid-drag) work; (c) is Space suppressed (no playback) and a posted Space tap typed. If (a) fails, variant B in the same spike: while an output is held, suppress the physical drag events and post `otherMouseDragged` at the same positions from a worker; measure the lag. The result goes to `docs/learnings/0005-hold-remaps-mac.md`, then the spike is deleted. Windows needs no spike: the AutoHotkey script is the proof for (a) and (c); rolling is checked in step 5.
1. **Core** (`Core/HoldRemaps/`, README, `MappingRules` hook-up, `AppGroup.HoldRemaps`, the command "used by", `KnownApps` pair). Tests: rules; the machine against tables written from Joel's words: tap at 179 / 181 ms, a click inside a short Space (no tap), a long Space with nothing (no tap), rollover "a b", Shift across a tap, rolling Left → Left + Right → Right → Left + Right → Left with the exact output sequence, Space released before the buttons, Reset with outputs held.
2. **Config and sync.** Reader/writer, round-trip, an older file reads as no hold remaps, the format-version guard, a three-way merge of hold remap items.
3. **Engine.** `HoldRemapShadow`, `KeySuppressionShadow` claim, `InputGate` order, watch publishes the foreground plan, worker runs and injects, executor for RunCommand, logging, READMEs. Tests (fakes only, never a real hook): a randomized pairing test in the style of `ChordPairingTests` (hold keys, row and non-row buttons, the stroke button, keys, wheel; models the OS from passed events plus injections; asserts nothing is ever held at the end and every hook decision equals the machine's), and `EngineHarness` end-to-end cases for each Joel scenario.
4. **App.** The section and its rows, wired to the store; "Used by" for commands; a dev-gallery entry.
5. **Joel's Blender, Mac then PC.** The lead creates the Blender app group (`blender.exe` / `Blender`) and its Space remap (rows: Left → Middle; Right → Shift + Middle; Middle → Ctrl + Middle; Left + Right → Ctrl + Middle; W → G; E → S; R → Ctrl + Shift + Alt + R; F → Numpad .) through the UI or in `augram.json` (app stopped, file backed up), tells Joel, he runs the "Done when" list. The lead runs the app only after saying so (CLAUDE.md). Then the session handoff, and the AutoHotkey script retires.

## Not in this plan

Global hold remaps; a hold-time threshold for letter keys; leader-key sequences (tap Space, then W); marking-menu style radial menus; platform foreground notifications; hold keys that are modifiers.
