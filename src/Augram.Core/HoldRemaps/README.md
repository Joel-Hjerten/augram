# Core/HoldRemaps

Hold remaps (requirements F9, plan 0002 steps 1–3): a key that, while held, changes what other inputs do ("remap inputs or run commands while a key is held"). Joel's AutoHotkey script for Blender is the origin (`docs/reference/autohotkey-navigation-for-blender/`). A hold remap belongs to one app group and is a parent in that group's command tree, beside its categories; the commands under it are ordinary commands whose trigger is an **input** (`Mapping/Trigger.InputTrigger`) and whose steps are either one **Remap** step (`../Steps/Remap/`, the input held through as an output) or ordinary steps run at the press. This folder holds the model, its rules, what the hook reads, and the behaviour as a pure machine; the engine wiring (plan 0002 step 3) is in `src/Augram.Engine/README.md`, "Hold remaps".

## Types

| Type | Role |
|---|---|
| `HoldRemapId` | strongly typed Guid, unique within its group (as a category's is) |
| `HoldRemap` | `Id`, `Name` (the hold key's name by default, `DefaultName`; "Hold remap" while no key is chosen), `HoldKey` (`KeyCode`; `None` = not chosen yet, does nothing), `TapTimeMs` (default 180, `DefaultTapTimeMs`), `IsActive`, `UseOn` (F8; ANDed into its commands' like a category's). `For(key)` makes a new one. On `AppGroup.HoldRemaps`, sorted by name, empty by default |
| `HoldInput` | closed set with value equality: `Buttons(HeldButtons Set)` (one button or several held together; physical buttons only, `Normalised` drops `Stroke`), `Wheel(WheelDirection)`, `Key(KeyCode)`; `Of(button)`, `Of(buttons…)`; `Describe` ("Left + Right", "wheel up", "W") |
| `HoldRemapRules` | the rules below; `Normalised(group)`, `EnsureValid(group)`, `EnsureValid(holdRemap, group, others)`, `EnsureValid(command, group)` (in `HoldRemapRules.Commands.cs`), `FindByHoldKey`, `Moved(command, from, to)` (a move or paste: the target's hold remap on the same hold key, else `Detached`), `Detached(command)` (out of its hold remap: input cleared, steps kept), `HoldKeyProblem(key)` and `InputKeyProblem(key, holdRemap)` (the sentence a key is refused with as a hold key or as an input, null when it is taken; the modifiers named as `HotkeyText.Names`' platform names them, "Ctrl, Opt, Shift and Cmd" on macOS, through `HotkeyText.ModifierList`; an overload takes the platform; the App's one-key fields show it before anything is stored, plan 0002 step 4), `FittedTo(output, input)` (rule 7 read the other way: a button output on a wheel input becomes a notch the same way, a wheel output on another input Middle, modifiers kept; the App gives it to a new Remap step and applies it when an input changes, in the same edit). Called by `Mapping/MappingRules` for every group |
| `HoldRemapPlan`, `HoldRemapEntry`, `HoldBinding`, `KeySet` | what the hook reads (below) |
| `HoldRemapMachine`, `HoldRemapEvent`, `HoldRemapOutcome`, `KeyPhase`, `HoldRemapState` | the behaviour (below) |

## Rules (`HoldRemapRules`, via `MappingRules.ValidDocument`)

1. Names are trimmed; an empty name becomes the hold key's name. Names are unique within the group, case-insensitively ("A hold remap named 'Space' already exists in 'Blender'."); ids are unique within the group.
2. A hold key is never a modifier (Ctrl, Alt, Shift, Win/Cmd; plan 0002 decision 2: they are already "while holding" keys of triggers). Hold keys are unique within the group; `KeyCode.None` (not chosen yet) any number of times. Two groups may both hold Space.
3. Tap time 0..2000 ms (0: the hold key is never sent). Used on at least one platform. Never on the Global group (decision 1; "add 'Space' to an app group").
4. A command under a hold remap has an input or no trigger yet, never a gesture, wheel or click trigger; an input only on a command under a hold remap. Checked on both platforms (an own version's trigger too).
5. A button input names at least one button; a key input is a key, not a modifier (they pass through a hold, decision 4) and not the hold key.
6. Inputs are unique per hold remap (A7 in `MappingRules.Overlap`, which never matches commands under different hold remaps): two hold remaps may both use Left. Button sets match exactly, so Left and Left + Right are different inputs.
7. A Remap step is a command's only step (anywhere). A wheel input takes a key or a wheel output, never a button; a wheel output needs a wheel input.
8. Normalised, never refused: a command whose `HoldRemapId` names no hold remap of its group becomes an ordinary command with its input cleared (`Detached`; a hand-edited file, a deleted hold remap, a sync repair); a command under a hold remap has no category (cleared).

The resolver (`Mapping/CommandResolver`) and the anchor planner skip commands under a hold remap, and `PressedTrigger.Matches` never matches an input: a gesture or a wheel press never fires them. A command's "Use on" is group AND hold remap AND command (`AppGroup.IsCommandUsedOn`).

## What the hook reads (`HoldRemapPlan`)

`HoldRemapPlan.ForGroup(group, platform)` (or `For(mapping, foreground window, platform)`, which finds the group as the resolver does) builds, off the hook thread, one `HoldRemapEntry` per active hold remap used on the platform with a hold key chosen; `Empty` for no group, Global, or a group inactive or not used there. An entry has the hold key, the tap time, and its inputs as bit sets: `Buttons` (every button some input names, alone or in a set), the wheel directions, and `Keys`, a 256-bit `KeySet`; `IsInput(button | direction | key)` is one mask each and allocates nothing. Its `Bindings` are the active commands under it used on the platform with an input there, each a `HoldBinding`: `Output` (the Remap step's, from `Command.PlanFor(platform)`, so a platform version's output is used where it has one) or `RunsSteps`, or neither (no steps, its only step inactive, a key output with no key: the input is claimed and does nothing). `ForButtons(held)` is the exact-set lookup, `ForWheel`, `ForKey`; `HoldsButtonOutput(held)` is one bit per button set (the set's command is a Remap with a button output), read by the hook when the owed set changes to know whether moves are re-posted as drags (macOS, plan 0002 step 3a). The plan is immutable: the hook reads it through one volatile reference and `Find(holdKey)` walks a short array. `HoldRemapPlan.WatchesFocus(mapping, platform)` says whether any plan could be non-empty here (an active group whose matcher can match on this platform, with an active hold remap used here and a hold key chosen): the engine's watch follows the foreground for hold remaps only then.

## The behaviour (`HoldRemapMachine`)

Pure, like `../Capture/CaptureStateMachine`: `Handle(event)` returns outcomes; no hook, timer, clock or window; timestamps arrive on the events; one thread calls it (the engine worker, `Engine/Hosting/EngineWorker.HoldRemaps.cs`; the hook's mirror of its decisions is `Engine/Input/HoldRemapShadow`). It classifies every event against the hold remap of the hold in progress itself, so its decision is what the hook's shadow should have decided and the worker can compare.

Events: `HoldDown(entry)` (the hook claimed a hold key's press for that hold remap), `HoldUp(key)`, `Button(button, down/up, x, y)`, `Move(x, y)` (fed only where the simulator re-posts drags, macOS), `Wheel(direction, x, y)`, `Key(key, Down | Repeat | Up, x, y)` (any key the hook forwards; x, y is the pointer, for a button output pressed by a key), `FocusMoved` (the app in front is no longer the hold's), `Reset`. Outcomes: `Suppress` / `PassThrough` (exactly one, first, for every event but `FocusMoved` and `Reset`), `HoldEnded(key)`, `TapHoldKey(key)`, `PressOutput(command, output, x, y)`, `RepeatOutput(command, output)` (a key output's auto-repeat), `ReleaseOutput(command, output)`, `DragOutput(command, output, x, y)` (a swallowed move re-posted as a drag of the button output held), `WheelOutput(command, output, x, y)`, `ReplayKey(key, phase)`, `RunSteps(command, x, y)`.

| Event, state | Outcomes | Next state |
|---|---|---|
| HoldDown, Idle | Suppress; the hold starts (time noted, nothing used) | Holding |
| HoldDown, Following, the same hold remap | Suppress; the hold resumes, counted as used (buttons or keys are still in it) | Holding |
| HoldDown, Following, another hold remap | PassThrough (the hook should not have claimed it) | Following |
| HoldDown or Key (the hold key, Down or Repeat), Holding or RolledOver | Suppress | unchanged |
| HoldDown of another hold key, Holding | as a Key down of that key (decision 6: an ordinary key of this hold) | |
| HoldUp, Holding | Suppress; TapHoldKey when released within the tap time (≤) with nothing used | Idle, or Following while inputs are down |
| HoldUp, RolledOver | Suppress (the tap went at the rollover) | Idle |
| HoldUp, not holding | Suppress | unchanged |
| Button down, Holding | counts as used; an input button: Suppress, owed, and the set is followed (below); another: PassThrough | |
| Button down, Following with buttons owed | an input button: Suppress, owed, followed; another: PassThrough | Following |
| Button down, otherwise (RolledOver, Idle, Following with only keys) | PassThrough | unchanged |
| Button up, owed | Suppress; the set is followed | Idle once nothing is down and the hold key is up |
| Button up, not owed | PassThrough | unchanged |
| Following the set (decision 7) | the command whose set equals the owed buttons now: on a change, ReleaseOutput of the old output first, then PressOutput of the new at the event's position, or RunSteps for a Steps command; none: nothing held | |
| Move, the set's output is a button (fed only where the simulator re-posts drags: macOS, learnings 0005) | Suppress, DragOutput of that output at the move's position (the worker adds the delta); never counts as used, changes nothing | unchanged |
| Move, otherwise (nothing held, a set with no command, a key output or a Steps command) | PassThrough | unchanged |
| Wheel, Holding | counts as used; an input direction: Suppress, then WheelOutput (wheel output), PressOutput + ReleaseOutput (key output), RunSteps (Steps), once per notch; another: PassThrough | Holding |
| Wheel, otherwise | PassThrough | unchanged |
| Key down, an input, Holding | counts as used; Suppress, claimed; PressOutput (held until its up) or RunSteps | Holding |
| Key repeat, claimed | Suppress; RepeatOutput for a key output (a Steps command ignores repeats) | unchanged |
| Key up, claimed (also after the hold key's release) | Suppress; ReleaseOutput | Idle once nothing is down and the hold key is up |
| Key down, a modifier, Holding | PassThrough; never counts as used (Shift held across a tap gives Shift + Space) | Holding |
| Key down, another key, Holding, within the tap time (≤), nothing used | the rollover: Suppress, TapHoldKey, ReplayKey(key, Down) | RolledOver |
| Key down, another key, Holding, past the tap time or something used | PassThrough | Holding |
| Key repeat or up, replayed | Suppress, ReplayKey(key, Repeat or Up) | unchanged |
| Key, otherwise | PassThrough | unchanged |
| FocusMoved, Holding or RolledOver with nothing owed (no input button, no input key) | HoldEnded; no tap; the hold key is remembered as ended (Joel, 2026-10-10) | Idle |
| FocusMoved, otherwise (inputs owed: the hold keeps following them and ends as usual; or no hold) | nothing | unchanged |
| HoldDown, Key or HoldUp of an ended hold's key | Suppress (its press was swallowed); its release forgets it | unchanged |
| Reset | ReleaseOutput for the set's output and every claimed key's (newest first), ReplayKey(key, Up) for every replayed key still down; forgets everything, an ended hold's key too | Idle |

## Invariants

- **Pairing (A19).** Every `PressOutput` is followed by exactly one `ReleaseOutput` of the same output, and every `DragOutput` falls between the two (a wheel output and a Steps command hold nothing); every replayed key's down by its up; at the input's release, whatever the hold key did meanwhile, or at `Reset`. A swallowed press gets a swallowed release, a passed one a passed one. `tests/Augram.Core.Tests/HoldRemaps/HoldRemapPairingTests` checks all of it over 2,000 random sequences (hold key, input and other buttons, input and other keys with repeats, Shift, both wheel directions, resets) and that nothing is held and the machine is idle once everything is physically up.
- **Decide at the press.** An input belongs to the hold remap if its down was claimed; its repeats and release follow that decision, never the hold key's state at the time.
- **Buttons are followed after the hold key, keys and the wheel are not:** after the hold key is up, a newly pressed input button joins the set while one is still owed; keys and the wheel pass.
- **One hold at a time** (decision 6); a replayed key does not stop a new hold from starting.

## Persistence and sync

Config file (schema 4, `../Config/README.md`): a group's `holdRemaps` (omitted when empty), a command's `holdRemap` (omitted for an ordinary command), the input trigger `{ "input": { "buttons" | "wheel" | "key": … } }`, the Remap step's `params` (`../Steps/Remap/README.md`); an older file reads as no hold remaps, a bad hold remap entry is dropped with a notice. Sync (format 11, `../Sync/README.md`): a hold remap's header is an item of its own, `holdRemap:<groupId>/<id>`, merged like a category; the commands under it are command items carrying `holdRemap`; the merge repairs name and hold key clashes, a hold remap whose group is gone, a command whose hold remap is gone, and a command that breaks these rules across items.

## Threading

`HoldRemapPlan` and its entries are immutable and safe from any thread; the hook reads them. `HoldRemapMachine` is called from one thread only (the engine worker) and has no locks. Nothing here injects, waits or reads a clock.

**May reference:** `Abstractions` (`KeyCode`, `KeyModifiers`, `HostPlatform`), `Capture` (`MouseButton`, `HeldButtons`, `WheelDirection`), `Mapping` (`AppGroup`, `Command`, `Trigger`, `MappingRules`), `Steps/Remap`, `Steps/Hotkey` (`HotkeyText`, `HotkeyKeys`). **Referenced by:** `Mapping` (the model and rules), `Config` and `Sync` (persistence), the engine (plan, machine and `WatchesFocus`, step 3), the App (step 4).
