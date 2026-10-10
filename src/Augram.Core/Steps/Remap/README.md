# Steps/Remap

The **Remap** step (key `remap`, category Mouse, platform-neutral, **offered only for commands under a hold remap**): while the hold key is held, the command's input is played as this output, press for press and release for release (Left held → Middle held, so a drag stays a drag). Requirements F9, plan 0002; the model it lives in is `../../HoldRemaps/`.

| File | Role |
|---|---|
| `RemapStep` | record: `Output` · `Summary` "Remap to Ctrl + Middle", "Remap to G", "Remap to Ctrl + wheel up", "Remap (no key set)"; `SummaryOn(platform)` names the modifiers in that platform's words (Opt, Cmd) |
| `RemapOutput` | closed set with value equality: `Button(MouseButton, Modifiers)`, `Key(KeyCode, Modifiers, RightHand)`, `Wheel(ScrollDirection, Modifiers)`; `Kind` (`RemapOutputKind`), `IsSet` (false only for a key output with no key), `Describe(names)` |
| `RemapStepType` | metadata and JSON (below); `HoldRemapsOnly` true (`IStepType.HoldRemapsOnly`: the picker offers it under a hold remap and nowhere else); `CreateDefault()` is Middle with nothing held (Joel's orbit); `Convert` is always the same step; `Execute` skips (below) |

There is no executor: the **engine worker** plays the output (plan 0002 step 3, `Engine/Hosting/EngineWorker.HoldRemaps.cs`), not the command executor.

## What the modifiers mean

As Joel's AutoHotkey script has it (`+{MButton Down}`):

- **Button output**: the modifiers are pressed around the button's **down only** (Blender reads them when the drag starts; nothing stays held through it).
- **Key output**: the modifiers are held for the **whole press**, so the key's repeats keep them; `RightHand` says which are the right-hand key, as a Hotkey step's.
- **Wheel output**: the modifiers are held around the notch. For a wheel input only (`HoldRemaps/HoldRemapRules`).

## Parameters

```json
{ "output": "Button", "button": "Middle", "modifiers": "Shift" }
{ "output": "Key", "key": "R", "modifiers": "Control, Alt, Shift", "rightHand": "Alt" }
{ "output": "Wheel", "direction": "Up", "modifiers": "Control" }
```

- `output`: `Button`, `Key` or `Wheel` (`RemapOutputKind`), case-insensitive; absent = `Button`.
- `button`: a `MouseButton` name; absent = `Middle`. `key`: a `KeyCode` name; absent = `None` (no key set). `direction`: a `ScrollDirection` name (`Up`, `Down`, `Left`, `Right`); absent = `Up`. Only the member of the output's kind is written.
- `modifiers`: `KeyModifiers` names, comma-separated, read by the Hotkey step's reader (`HotkeyStepType.ReadModifiers`); written only when there are any. `rightHand`: the same, for a key output, cut down to `modifiers`; written only when there are any.
- A bad name or shape is a `StepFormatException` naming the member, like every step type's.

## Execution

`Execute` never runs it: it returns `Skipped` with `RemapStepType.NotRunHere` ("a Remap step is played by its hold remap while the hold key is held, never as a command step") and logs one Debug line (`steps` / "Remap": output, outcome, reason). The command executor would only reach it for a command run some other way, or one moved out of its hold remap.

## Platform (F8)

`IsPlatformNeutral` is **true** and `Convert` returns the step unchanged both ways (F9: remap outputs are not converted; Blender's Ctrl + Middle zoom is Ctrl on the Mac too). A platform that needs another output gets the command's own version (`Mapping/CommandVersion`), as for any step; `HoldRemapPlan` reads the platform's own steps.

**May reference:** `Abstractions`, `Capture` (`MouseButton`), `Diagnostics`, `Steps`, `Steps/Hotkey` (`HotkeyText`, `HotkeyKeys`, the modifier reader). **Referenced by:** `StepRegistry.BuiltIn` (one line), `HoldRemaps` (rules, plan, machine outcomes), the App's `Components/Steps/Remap/` form (output kind, button, key, wheel direction, modifiers; App README "Step forms").
