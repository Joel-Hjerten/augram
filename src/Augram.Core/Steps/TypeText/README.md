# Steps/TypeText

The **Type text** step (key `typeText`, category Text, platform-neutral): type a string into whatever has focus. Joel's StrokesPlus.net config has 18 `SendKeys` steps and one `SendString`, nearly all game console commands (open the console with the backtick, type `fov 70`, press Enter). Learnings 0001 measured why there are two methods: Unicode entry is fast and layout independent but arrives as `VK_PACKET`, which some games ignore; pressing keys is what those games see.

| File | Role |
|---|---|
| `TypeTextStep` | record: `Text`, `Method` (default Unicode) · `HasText` · `Lines()` (split at CR LF, CR or LF; CR LF counts once) · `Summary` `Type "fov 67.5"`, at most 40 characters of text with "…" when longer, line breaks shown as ⏎ (`Type "line one⏎line two"`), ` (by keys)` after it for the Keys method, "Type text (no text set)" when empty · `Empty`, the default a new step starts with |
| `TypeTextMethod` | `Unicode` (the characters themselves; any character, any layout, most apps) · `Keys` (the keys that produce them on a US layout, through `Abstractions/AsciiKeyLayout`; for games and consoles that read keys) |
| `TypeTextStepType` | metadata and JSON (below); `CreateDefault()` is `TypeTextStep.Empty`; `Convert` is always `Same` |
| `TypeTextExecutor` | the branches below; one Debug line (`steps` / "Type text": method, length, lines, outcome, reason) per run, **never the text** (it may be a password or an ID number) |

## Parameters

```json
{ "text": "fov 67.5", "method": "Unicode" }
```

- `text`: a JSON string, line breaks included; absent or null = empty (unset). Not a string is a `StepFormatException` naming `text`. Kept exactly as stored: no trimming, no line-ending normalisation (the App's multi-line field writes LF on every platform).
- `method`: `Unicode` or `Keys`, case-insensitive; absent = `Unicode`. Any other value, numbers included, is a `StepFormatException` naming `method`. `Write` emits both members, the method as `TypeTextMethod.ToString()` like every other step type's enum, so the round trip is byte-stable.

## Execution

| Case | Result |
|---|---|
| empty text | Skipped "no text set" (the command goes on), the simulator untouched |
| Keys, a character a US keyboard lacks (é, å, €) anywhere in the text | Failed "typing by keys: character N has no key on a US layout; …" before anything is typed (the command stops) |
| otherwise, per line | cancelled → Skipped "cancelled"; Enter (`IInputSimulator.Hotkey(None, Enter)`) before every line but the first; the line (when not empty) through `TypeText` (Unicode) or `TypeTextByKeys` (Keys); any answer but success → Failed "Unicode typing: Failed", "typing by keys: Unsupported", "Enter between lines: Failed" |
| every line typed | Done |

A line break is pressed as a real Enter key under both methods: a line feed entered as a Unicode character is not reliably an Enter key, since browsers and many apps act on the key press, not the character. Tabs are typed as characters (Unicode) or the Tab key (Keys).

The **settle delay (A8)** is not this step's: the Engine's `CommandRunner` activates the target and waits once before the first Keyboard or **Text** step (`NeedsFocus`), only when focus moved.

The reasons never quote the text, and the step's own log line carries only its length and line count. The `Summary` does show the text (it is what the command row displays), and the Engine's `CommandRunner` logs every step's summary ("Step ran" at Debug, "Command stopped" at Warning), so up to 40 characters of the text reach the log that way.

## Platform (F8)

`IsPlatformNeutral` is **true** and `Convert` returns the step unchanged: characters are the same on Windows and macOS, and the Keys method presses the same US-layout keys on both (the Engine's simulator maps `KeyCode` per platform).

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the App's `Components/Steps/TypeText/` form, the importer's `SendKeysSyntax` (SP.net `SendKeys` text runs).
