# Steps/Hotkey

The **Hotkey** step (key `hotkey`, category Keyboard, **platform-bound**): press a modifier set and one key, release in reverse order. F5's model is "modifier set + one key"; a chord (Ctrl+K, Ctrl+C) is two Hotkey steps in a row. It is the most used step in Joel's StrokesPlus.net config (199 `SendHotKey` steps plus the non-media `SendVKey` ones).

| File | Role |
|---|---|
| `HotkeyStep` | record: `Modifiers` (`KeyModifiers` flags), `Key` (`KeyCode`), `RightHand` (which of `Modifiers` are the right-hand key; default none), `Layouts` (init-only, plan 0011: layout id → `HotkeyBinding`, the shortcut under that keyboard layout; `NoLayouts` by default, ids ordinal) · `IsSet` (key is not `None`) · `HasLayouts` · `Main` (the three fields as a `HotkeyBinding`) · `ForLayout(id)` (the layout's shortcut, else `Main`; null is `Main`) · `Summary` "Ctrl+Shift+T", "Alt+F4", "RAlt+F9", "Esc", or "Hotkey (no key set)"; with layout shortcuts the active layout's one (`ForLayout` of `HotkeyText.Layouts.Active`, named by that layout; the main one for a layout without a row or under the null port) plus " · per layout" (`PerLayoutSuffix`) · `Normalized()` (`RightHand` cut down to `Modifiers`, each layout shortcut the same way, and a layout entry without a key dropped: an empty row is no shortcut) · `Unset`, the default a new step starts with · equality is by value over the map too (two steps with the same shortcuts are equal whatever dictionary holds them) |
| `HotkeyBinding` | record: one shortcut, `Modifiers`, `Key`, `RightHand`, the shape the main shortcut and each layout's share · `IsSet`, `Summary`, `SummaryOn`, `IsNormalized`, `Normalized()` as on the step |
| `HotkeyStepType` | metadata and JSON (below); `CreateDefault()` is `HotkeyStep.Unset` |
| `HotkeyExecutor` | no key set → Skipped "no key set" (the command goes on) · `IInputSimulator.Hotkey(modifiers, key, rightHand & modifiers)` → Done, or Failed "Ctrl+W: Unsupported" with the simulator's answer (the command stops). With layout shortcuts: `context.Layouts.Active` once, then `ForLayout` ("Per keyboard layout" below). One Debug line (`steps` / "Hotkey": keys, outcome, reason; with layout shortcuts also `layout`, the active one's name or "unknown", and `shortcut`, "layout shortcut" or "fallback") per run |
| `HotkeyText` | the one place a hotkey becomes text: the modifiers in a fixed order under the platform's key names, Ctrl, Alt, Shift, Win on Windows and Ctrl, Opt, Shift, Cmd on macOS (Joel, 2026-10-07: words, not ⌘⌥ symbols; display only, Meta is the Win key there and the Command key here), a right-hand one as RCtrl, RAlt, ROpt, RCmd… in the same place ("Ctrl+RAlt+F9", "RCtrl+RShift+P"), joined by "+", then the key's short name (letters and digits as themselves, F-keys, "Esc", "Tab", "PgUp", "PgDn", "Ins", "Del", "PrtSc", arrows as "Left"/"Up"…, "Num 4", "Volume Up", punctuation as its character). `KeyName(key, layoutId: …)` names a letter, digit or punctuation key by what it prints under the active keyboard layout, or under the layout `layoutId` names (the App's per-layout rows; null is the active one), when the `IKeyboardLayouts` port can say ("Ö" for the key the US layout calls Semicolon, upper case as the keycap shows it; plan 0011 decision 6), the US name when it answers null; keys with names of their own (F-keys, Esc, arrows, Space, Num 4…) never ask. `Format` passes `layoutId` through the same way. The step summary, the App's capture field and key dropdown, the log and the importer share it. `HotkeyText.Names` picks the names for calls without an explicit platform: `RunningPlatform.Current` once something set it (the App's `Program.Main`, at startup), Windows until then; `HotkeyText.Layouts` is the port for calls without an explicit one the same way: `NullKeyboardLayouts` until the App sets the platform's adapter at startup (process-wide; a test that sets it resets it in a `finally`, in the never-parallel collection `RunningPlatformTests.Alone`). `ModifierList(conjunction)` names the four in a sentence from the same names ("Ctrl, Alt, Shift and Win", "Ctrl, Opt, Shift or Cmd"): the hold remap rules' refusals and the App's one-key capture field |
| `HotkeyKeys` | which `KeyCode`s are modifier keys and which flag each holds (left and right fold together); `IsRightHand` (the four Right* keys); `AllModifiers`; `Keys(modifiers, rightHand)`, the key of each modifier in press order (Ctrl, Alt, Shift, Meta), the right-hand one where `rightHand` asks; `LeftKeys(modifiers)` = `Keys(modifiers, None)` (what the Scroll step holds). **The one modifier-flag ↔ key rule** (plan 0007): the SharpHook simulator's hotkey and remap button, the engine's click relay and hold remap outputs, and the hook's key shadow (`ModifierOf`, a switch, no allocation) all ask here |
| `HotkeyConversion` (internal) | the F8 conversion below; `SwapModifiers(modifiers, from, out needs)` is the plain Ctrl ↔ Cmd swap alone, shared with the Scroll step's held keys |

## Parameters

```json
{ "modifiers": "Control, Alt", "rightHand": "Alt", "key": "F9" }
```

- `modifiers`: `KeyModifiers` flag names (`Control`, `Alt`, `Shift`, `Meta`), case-insensitive, comma-separated; `"None"` or `""` for none; absent = none. Any other name is a `StepFormatException` naming `modifiers`. Read by `StepParameters.ReadFlags`, as the Scroll step's `keys` and the Remap step's modifiers are. `Write` emits `KeyModifiers.ToString()` ("None", "Control", "Control, Shift"), so the round trip is byte-stable.
- `rightHand`: the same flag names for the modifiers pressed with the right-hand key. Written only when there are any (so a plain hotkey's JSON is unchanged and older files read as plain); read and written cut down to `modifiers`. A bad name is a `StepFormatException` naming `rightHand`.
- `key`: a `KeyCode` name (`T`, `Digit5`, `F5`, `PageUp`, `Escape`…), case-insensitive; absent = `None` (unset). Numbers are refused: the stored name is the contract, not the enum value.
- `layouts` (plan 0011, schema 8): an object keyed by keyboard layout id (`com.apple.keylayout.Swedish-Pro`, `0000041D`: the platform's own id, `KeyboardLayout.Id`), each value a hotkey in the three members above, read and normalised the same way. Written only when the step has at least one, after `key`, in ordinal id order (byte-stable); absent or null = none, so every file from before reads as before and a step without layout shortcuts writes exactly what it did. A value that is not an object, an empty (or blank) layout id, or a member a shortcut refuses is a `StepFormatException` naming `layouts` and the entry (`'layouts' entry 'com.apple.keylayout.ABC': 'key' must be one of …`). A layout entry without a key is dropped on read (`Normalized()`).

```json
{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "com.apple.keylayout.Swedish-Pro": { "modifiers": "Alt, Meta", "key": "Digit8" } } }
```

## Per keyboard layout (plan 0011, Joel 2026-10-13)

macOS resolves a menu shortcut by the character the active layout gives a key, so Chrome's Back is Cmd+[ under ABC and Cmd+Opt+8 under Swedish, and no transposition Augram could compute is safe (Cmd+Opt+8 is how Swedish types "[", and Chrome ignores it). A Hotkey step therefore holds one shortcut per layout, authored by the user, and the main shortcut is the fallback for every layout without its own.

- **The executor picks by the active layout at run time:** a step with layout shortcuts reads `StepExecutionContext.Layouts.Active` once per run and presses `ForLayout(active.Id)`, the layout's own shortcut when the map has it, else the main one (also when the platform cannot say which layout is active). A step without layout shortcuts never asks the port, and its log line says nothing of layouts.
- **Ids are the platform's own**, stored as text; the two platforms' ids never meet. A layout shortcut belongs to the side it was authored on (plan 0010).
- **Conversion (F8) converts the main shortcut only** and drops the map (decision 5): the other platform's inferred side has other layouts, authored there when needed. So a step whose main shortcut the rules leave alone (F5) still converts, to the same shortcut without layouts.
- **Key names follow the active layout** (decision 6): `HotkeyText.KeyName` above. Names are display only; the stored key stays the `KeyCode`.
- **Each row names its keys by its own layout** (Joel, 2026-10-13, first round: a layout switch renamed every row). `KeyName` and `Format` take an optional `layoutId` (a `KeyboardLayout.Id`) passed through to `IKeyboardLayouts.CharacterOf(key, layoutId)`; null is the active layout, as before. The App's per-layout rows pass their layout's id ("Cmd+Å" on the Swedish row, "Cmd+[" on the ABC row, whatever is active); the summaries, the log and the main field pass nothing.
- Which layouts exist and which is active is the `IKeyboardLayouts` port (`../../Abstractions/README.md`). **The App's form derives the main shortcut** (Joel, 2026-10-13, first round; Core unchanged): with "Per keyboard layout" on, the rows replace the one capture field, every installed layout starts with the main shortcut, and after a row edit the main shortcut is the active layout's row when set, else the first set row; so the other platform's conversion starts from the active layout's shortcut. The main shortcut stays the stored fallback for a layout without a row.
- **The summary shows the active layout's row** (Joel, 2026-10-13, third round: the stored main is the row of the layout active at the last edit, so after a switch the step list read it as a shortcut never captured). `Summary` and `SummaryOn` read `HotkeyText.Layouts.Active` once and format `ForLayout(active.Id)` with that id as `layoutId`: "Cmd+Ö · per layout" under Swedish, "Cmd+[ · per layout" under ABC; a layout without a row shows the main one, as the executor runs it; the null port shows the main one in US names, as before. The App's Commands view model re-projects its rows on `KeyboardLayoutChanged`, so the step list and the command row repaint.

## Right-hand modifiers (F5, Joel 2026-10-07)

Some apps treat plain Alt and Ctrl differently from the right-hand keys, which is why 8 of Joel's 199 SP.net hotkeys use RAlt or RControl (RAlt+F9, RAlt+F10, RControl+RShift+P, RControl+0). A hotkey records, shows, stores and sends the right-hand key when that is what was pressed or imported; it is never folded into the plain modifier.

- `RightHand` is a second `KeyModifiers` set, not new enum bits: `KeyModifiers` stays four bits: the ignore-key setting (`Config.GeneralSettings.IgnoreKey`) is a `KeyModifiers` too, written by four names.
- A positional record cannot validate in its constructor, so `RightHand` may carry bits outside `Modifiers` in memory. They mean nothing: `HotkeyText` and the executor ignore them, and `Read`, `Write`, the App's form and capture field and the importer normalise (`Normalized()`).
- Held on both sides at once, a modifier is plain. The importer does the same for `LAlt` and `RAlt` both true.
- AltGr layouts (Joel's Swedish one): Windows reports AltGr as a synthesised LeftControl plus RightAlt, so the capture field records "Ctrl+RAlt+…", and replaying it sends what Windows saw. An imported `RAlt` alone stays RAlt, the key SP.net sent.

## Execution rules

- The **settle delay (A8)** is not this step's: the executor activates the target and waits once before the first Keyboard, Mouse or Text step, only when focus moved. This step just sends.
- The simulator presses the **left** modifier keys, and the right-hand key (RightAlt, RightControl, RightShift, RightMeta) for each modifier in `RightHand`.
- A key held by the user while the step runs is the user's business; the step never releases keys it did not press.

## Platform (F8)

`IsPlatformNeutral` is **false**: the step stores what was authored and its envelope records `authoredOn` (the App authors on `RunningPlatform.Current`, the importer on Windows). On the other platform `HotkeyConversion` gives the best guess (F8, Joel 2026-10-07: both ways, best effort), computed for display and before every run, never stored:

| Authored on | Rule | Examples |
|---|---|---|
| Windows → macOS | the exception table first, then Ctrl → Cmd; Alt (Option), Shift and the key stay; a right-hand modifier keeps its side | Ctrl+W → Cmd+W · RCtrl+0 → RCmd+0 · Alt+Tab → Cmd+Tab · Alt+F4 → Cmd+W · Ctrl+Y → Shift+Cmd+Z · Ctrl+Tab stays |
| Windows → macOS, with the Windows key | no guess: "Win+D needs a macOS version: the Windows key has no Mac counterpart"; the executor skips the step and the command goes on | Win+D, Ctrl+Win+Left |
| macOS → Windows | the exception table first, then Cmd → Ctrl; Ctrl, Option (Alt), Shift and the key stay | Cmd+W → Ctrl+W · Cmd+Tab → Alt+Tab · Cmd+Q → Alt+F4 · Shift+Cmd+Z → Ctrl+Y · Ctrl+Tab stays |
| macOS → Windows, with Cmd and Ctrl together | no guess: "… needs a Windows version: Cmd and Ctrl together have no Windows counterpart" | Ctrl+Cmd+F |
| either way, the rules change nothing | unchanged | F5, Shift+F5, Alt+F9, Space |

The exception table is in `HotkeyConversion` and grows as real use shows more. `SummaryOn(platform)` reads a step in that platform's names, so a Windows "Win+D" reads "Win+D" on a Mac too. The layout shortcuts never convert: the converted step has none ("Per keyboard layout" above).

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), `Steps/Scroll` (`HotkeyText`, `HotkeyKeys`, `SwapModifiers`), the importer (`Augram.Import.StrokesPlus/HotkeyMapping`: `SendHotKey` and non-media `SendVKey`), the App's `Components/Steps/Hotkey/` form and `Components/Capture/` field, and the Engine (`HotkeyKeys`: `SharpHookInputSimulator`, `KeySuppressionShadow`, `InputGate`, `ClickRelay`, the worker's hold remap outputs).
