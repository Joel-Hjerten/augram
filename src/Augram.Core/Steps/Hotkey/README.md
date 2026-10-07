# Steps/Hotkey

The **Hotkey** step (key `hotkey`, category Keyboard, **platform-bound**): press a modifier set and one key, release in reverse order. F5's model is "modifier set + one key"; a chord (Ctrl+K, Ctrl+C) is two Hotkey steps in a row. It is the most used step in Joel's StrokesPlus.net config (199 `SendHotKey` steps plus the non-media `SendVKey` ones).

| File | Role |
|---|---|
| `HotkeyStep` | record: `Modifiers` (`KeyModifiers` flags), `Key` (`KeyCode`), `RightHand` (which of `Modifiers` are the right-hand key; default none) · `IsSet` (key is not `None`) · `Summary` "Ctrl+Shift+T", "Alt+F4", "RAlt+F9", "Esc", or "Hotkey (no key set)" · `Normalized()` (`RightHand` cut down to `Modifiers`) · `Unset`, the default a new step starts with |
| `HotkeyStepType` | metadata and JSON (below); `CreateDefault()` is `HotkeyStep.Unset` |
| `HotkeyExecutor` | no key set → Skipped "no key set" (the command goes on) · `IInputSimulator.Hotkey(modifiers, key, rightHand & modifiers)` → Done, or Failed "Ctrl+W: Unsupported" with the simulator's answer (the command stops). One Debug line (`steps` / "Hotkey": keys, outcome, reason) per run |
| `HotkeyText` | the one place a hotkey becomes text: the modifiers in a fixed order under the platform's key names, Ctrl, Alt, Shift, Win on Windows and Ctrl, Opt, Shift, Cmd on macOS (Joel, 2026-10-07: words, not ⌘⌥ symbols; display only, Meta is the Win key there and the Command key here), a right-hand one as RCtrl, RAlt, ROpt, RCmd… in the same place ("Ctrl+RAlt+F9", "RCtrl+RShift+P"), joined by "+", then the key's short name (letters and digits as themselves, F-keys, "Esc", "Tab", "PgUp", "PgDn", "Ins", "Del", "PrtSc", arrows as "Left"/"Up"…, "Num 4", "Volume Up", punctuation as its character). The step summary, the App's capture field and key dropdown, the log and the importer share it. `HotkeyText.Names` picks the names for calls without an explicit platform: Windows by default, set by the App's `Program.Main` to the platform it runs on |
| `HotkeyKeys` | which `KeyCode`s are modifier keys and which flag each holds (left and right fold together); `IsRightHand` (the four Right* keys); `AllModifiers` |

## Parameters

```json
{ "modifiers": "Control, Alt", "rightHand": "Alt", "key": "F9" }
```

- `modifiers`: `KeyModifiers` flag names (`Control`, `Alt`, `Shift`, `Meta`), case-insensitive, comma-separated; `"None"` or `""` for none; absent = none. Any other name is a `StepFormatException` naming `modifiers`. `Write` emits `KeyModifiers.ToString()` ("None", "Control", "Control, Shift"), so the round trip is byte-stable.
- `rightHand`: the same flag names for the modifiers pressed with the right-hand key. Written only when there are any (so a plain hotkey's JSON is unchanged and older files read as plain); read and written cut down to `modifiers`. A bad name is a `StepFormatException` naming `rightHand`.
- `key`: a `KeyCode` name (`T`, `Digit5`, `F5`, `PageUp`, `Escape`…), case-insensitive; absent = `None` (unset). Numbers are refused: the stored name is the contract, not the enum value.

## Right-hand modifiers (F5, Joel 2026-10-07)

Some apps treat plain Alt and Ctrl differently from the right-hand keys, which is why 8 of Joel's 199 SP.net hotkeys use RAlt or RControl (RAlt+F9, RAlt+F10, RControl+RShift+P, RControl+0). A hotkey records, shows, stores and sends the right-hand key when that is what was pressed or imported; it is never folded into the plain modifier.

- `RightHand` is a second `KeyModifiers` set, not new enum bits: `KeyModifiers` stays four bits because `Config.IgnoreKeys` shares it bit for bit.
- A positional record cannot validate in its constructor, so `RightHand` may carry bits outside `Modifiers` in memory. They mean nothing: `HotkeyText` and the executor ignore them, and `Read`, `Write`, the App's form and capture field and the importer normalise (`Normalized()`).
- Held on both sides at once, a modifier is plain. The importer does the same for `LAlt` and `RAlt` both true.
- AltGr layouts (Joel's Swedish one): Windows reports AltGr as a synthesised LeftControl plus RightAlt, so the capture field records "Ctrl+RAlt+…", and replaying it sends what Windows saw. An imported `RAlt` alone stays RAlt, the key SP.net sent.

## Execution rules

- The **settle delay (A8)** is not this step's: the executor activates the target and waits once before the first Keyboard or Text step, only when focus moved. This step just sends.
- The simulator presses the **left** modifier keys, and the right-hand key (RightAlt, RightControl, RightShift, RightMeta) for each modifier in `RightHand`.
- A key held by the user while the step runs is the user's business; the step never releases keys it did not press.

## Platform (F8)

`IsPlatformNeutral` is **false**: the step stores what was authored and its envelope records `authoredOn` (the App authors on `CommandsModule.CurrentPlatform`, the importer on Windows). On the other platform `HotkeyConversion` gives the best guess (F8, Joel 2026-10-07: both ways, best effort), computed for display and before every run, never stored:

| Authored on | Rule | Examples |
|---|---|---|
| Windows → macOS | the exception table first, then Ctrl → Cmd; Alt (Option), Shift and the key stay; a right-hand modifier keeps its side | Ctrl+W → Cmd+W · RCtrl+0 → RCmd+0 · Alt+Tab → Cmd+Tab · Alt+F4 → Cmd+W · Ctrl+Y → Shift+Cmd+Z · Ctrl+Tab stays |
| Windows → macOS, with the Windows key | no guess: "Win+D needs a macOS version: the Windows key has no Mac counterpart"; the executor skips the step and the command goes on | Win+D, Ctrl+Win+Left |
| macOS → Windows | the exception table first, then Cmd → Ctrl; Ctrl, Option (Alt), Shift and the key stay | Cmd+W → Ctrl+W · Cmd+Tab → Alt+Tab · Cmd+Q → Alt+F4 · Shift+Cmd+Z → Ctrl+Y · Ctrl+Tab stays |
| macOS → Windows, with Cmd and Ctrl together | no guess: "… needs a Windows version: Cmd and Ctrl together have no Windows counterpart" | Ctrl+Cmd+F |
| either way, the rules change nothing | unchanged | F5, Shift+F5, Alt+F9, Space |

The exception table is in `HotkeyConversion` and grows as real use shows more. `SummaryOn(platform)` reads a step in that platform's names, so a Windows "Win+D" reads "Win+D" on a Mac too.

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (`Augram.Import.StrokesPlus/HotkeyMapping`: `SendHotKey` and non-media `SendVKey`), the App's `Components/Steps/Hotkey/` form and `Components/HotkeyCapture/` field.
