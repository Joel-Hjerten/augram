# Steps/Scroll

The **Scroll** step (key `scroll`, category Mouse, platform-neutral): turn the mouse wheel at the gesture start, up, down, or sideways (left, right), one or more notches, optionally while holding keys. Ctrl + scroll is zoom in most apps; Joel's SP.net Explorer zoom commands posted exactly that (`WM_MOUSEWHEEL` with `MK_CONTROL` to the window under `action.Start`), and the importer maps them here.

| File | Role |
|---|---|
| `ScrollStep` | record: `Direction` (`Abstractions.ScrollDirection`: `Up`, `Down`, `Left`, `Right`), `Notches` (1..20, default 1), `Keys` (`KeyModifiers`, default none) · `HeldKeys` (`Keys` cut down to the four modifiers) · `NotchCount` (`Notches` clamped to 1..20) · `Summary` "Scroll up", "Ctrl + scroll down ×3", "Scroll right", the keys named by `HotkeyText` (`SummaryOn(MacOS)` reads "Cmd + scroll down") |
| `ScrollStepType` | metadata and JSON (below); `CreateDefault()` scrolls down one notch; `Convert` (below) |
| `ScrollExecutor` | presses the left-hand key of each held modifier (Ctrl, Alt, Shift, Meta order), `IInputSimulator.Scroll(direction, notches, start.X, start.Y)`, releases the keys in reverse in a `finally`. A failed press → Failed "LeftControl press: Unsupported" with no scroll; a failed scroll → Failed "Ctrl + scroll down: Failed"; a failed release → Failed after every pressed key was released. Only keys this step pressed are released. One Debug line (`steps` / "Scroll": direction, notches, keys, outcome, reason) per run |

## Parameters

```json
{ "direction": "Down", "notches": 3, "keys": "Control" }
```

- `direction`: a `ScrollDirection` name, case-insensitive; absent = `Down`. Any other name is a `StepFormatException` naming `direction`.
- `notches`: an integer 1..20; absent = 1. Outside the range, or not an integer, names `notches`.
- `keys`: `KeyModifiers` flag names (`Control`, `Alt`, `Shift`, `Meta`), comma-separated, read by the Hotkey step's reader (`HotkeyStepType.ReadModifiers`); absent, `""` or `"None"` = none. Written only when there are any, so a plain scroll's JSON is `{ "direction": "Up", "notches": 1 }`.

## Execution

- **Where:** the pointer moves to the gesture start (`StepExecutionContext.Start`) and the wheel turns there, so the window under the gesture start scrolls even when the stroke ended over another one. The pointer stays at the start point afterwards. A wheel trigger's start is where the pointer already is.
- **Focus:** category Mouse, so the executor activates the target (A20) and waits the settle delay (A8) before the first Mouse, Keyboard or Text step, exactly as for keys: held keys land in the foreground window, and the wheel then goes to the same window whatever the "scroll inactive windows" setting.
- **One notch** is what a mouse wheel's click sends: 120 (`WHEEL_DELTA`) on Windows, one line on macOS (`SharpHookInputSimulator`). Positive is up or left.
- Every key down gets its up (A19), even when the simulator throws.

## Platform (F8)

`IsPlatformNeutral` is **true**: a direction and a notch count mean the same on both platforms, and a scroll without held keys runs unchanged everywhere (no marker). Held keys convert like a hotkey's modifiers, through `HotkeyConversion.SwapModifiers`:

| Authored on | Held keys | On the other platform |
|---|---|---|
| Windows | none, Alt, Shift | unchanged |
| Windows | Ctrl (with or without Alt, Shift) | Cmd in its place: "Ctrl + scroll up" → "Cmd + scroll up" |
| Windows | Win | none: "Win + scroll down needs a macOS version: the Windows key has no Mac counterpart"; the executor skips the step |
| macOS | Cmd | Ctrl in its place |
| macOS | Cmd and Ctrl together | none: "… needs a Windows version: Cmd and Ctrl together have no Windows counterpart" |

**May reference:** `Abstractions`, `Diagnostics`, `Steps`, `Steps/Hotkey` (`HotkeyText`, `HotkeyKeys`, the modifier reader and swap). **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (`ScriptMapping`: SP.net's `WM_MOUSEWHEEL` + `MK_CONTROL` post), the App's `Components/Steps/Scroll/` form.
