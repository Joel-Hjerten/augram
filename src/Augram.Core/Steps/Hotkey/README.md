# Steps/Hotkey

The **Hotkey** step (key `hotkey`, category Keyboard, **platform-bound**): press a modifier set and one key, release in reverse order. F5's model is "modifier set + one key"; a chord (Ctrl+K, Ctrl+C) is two Hotkey steps in a row. It is the most used step in Joel's StrokesPlus.net config (199 `SendHotKey` steps plus the non-media `SendVKey` ones).

| File | Role |
|---|---|
| `HotkeyStep` | record: `Modifiers` (`KeyModifiers` flags), `Key` (`KeyCode`) · `IsSet` (key is not `None`) · `Summary` "Ctrl+Shift+T", "Alt+F4", "Esc", or "Hotkey (no key set)" · `Unset`, the default a new step starts with |
| `HotkeyStepType` | metadata and JSON (below); `CreateDefault()` is `HotkeyStep.Unset` |
| `HotkeyExecutor` | no key set → Skipped "no key set" (the command goes on) · `IInputSimulator.Hotkey(modifiers, key)` → Done, or Failed "Ctrl+W: Unsupported" with the simulator's answer (the command stops). One Debug line (`steps` / "Hotkey": keys, outcome, reason) per run |
| `HotkeyText` | the one place a hotkey becomes text: Windows names Ctrl, Alt, Shift, Win in that order, joined by "+", then the key's short name (letters and digits as themselves, F-keys, "Esc", "Tab", "PgUp", "PgDn", "Ins", "Del", "PrtSc", arrows as "Left"/"Up"…, "Num 4", "Volume Up", punctuation as its character). The step summary, the App's capture field and key dropdown, and the importer share it |
| `HotkeyKeys` | which `KeyCode`s are modifier keys and which flag each holds (left and right fold together); `AllModifiers` |

## Parameters

```json
{ "modifiers": "Control, Shift", "key": "T" }
```

- `modifiers`: `KeyModifiers` flag names (`Control`, `Alt`, `Shift`, `Meta`), case-insensitive, comma-separated; `"None"` or `""` for none; absent = none. Any other name is a `StepFormatException` naming `modifiers`. `Write` emits `KeyModifiers.ToString()` ("None", "Control", "Control, Shift"), so the round trip is byte-stable.
- `key`: a `KeyCode` name (`T`, `Digit5`, `F5`, `PageUp`, `Escape`…), case-insensitive; absent = `None` (unset). Numbers are refused: the stored name is the contract, not the enum value.

## Execution rules

- The **settle delay (A8)** is not this step's: the executor activates the target and waits once before the first Keyboard or Text step, only when focus moved. This step just sends.
- The simulator presses the **left** modifier keys. Left and right are folded in the model; SP.net's `RAlt`/`RControl`/`RShift`/`RWin` import as the plain modifier (8 of Joel's 199 steps; on an AltGr layout RAlt alone means Ctrl+Alt, see the importer's notes).
- A key held by the user while the step runs is the user's business; the step never releases keys it did not press.

## Platform (F8)

`IsPlatformNeutral` is **false**: Ctrl on Windows is usually Cmd on macOS, Win is usually Ctrl or Cmd, and app shortcuts differ. The step stores what was authored; the command step's envelope records `authoredOn` (the App authors on `CommandsModule.CurrentPlatform`, the importer on Windows), and the step list marks the row "Windows". The **auto-conversion** (Ctrl ↔ Cmd, Alt ↔ Option, Win → Ctrl, keys unchanged) and the per-platform override editor are a later slice; until then a Windows-authored hotkey runs unconverted on macOS. Conversion will live in this folder (the type owns its rule and its "convertible?" answer) so adding it stays one folder.

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (`Augram.Import.StrokesPlus/HotkeyMapping`: `SendHotKey` and non-media `SendVKey`), the App's `Components/Steps/Hotkey/` form and `Components/HotkeyCapture/` field.
