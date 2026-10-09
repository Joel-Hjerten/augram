# Steps/ClearClipboard

The **Clear clipboard** step (key `clearClipboard`, category System, platform-neutral, no parameters): empty the system clipboard so nothing is left to paste. Joel's SP.net "Clear Clipboard" command ran `clip.Clear();`; the importer maps that script here.

| File | Role |
|---|---|
| `ClearClipboardStep` | record without members (every instance is equal) · `Summary` "Clear clipboard" |
| `ClearClipboardStepType` | parameters `{}`; `Read` ignores any member, `Write` emits none; `CreateDefault()` is the one step |
| `ClearClipboardExecutor` | `StepExecutionContext.Clipboard.Clear()` → Done · not supported (`NullClipboard`, a platform without an adapter) → Skipped with the reason, the command goes on · failed → Failed with the adapter's reason, the command stops. One Debug line (`steps` / "Clear clipboard": outcome, reason) per run |

Runs on the command executor thread and needs no UI thread. No activation, no settle delay (A8): the clipboard belongs to the system, not to the window under the gesture.

## Adapters (`Abstractions.IClipboard`)

| | Windows (`Platform.Windows/Clipboard/Win32Clipboard`) | macOS (`Platform.MacOS/Clipboard/MacClipboard`) |
|---|---|---|
| Clear | `OpenClipboard(NULL)` → `EmptyClipboard` → `CloseClipboard` (always, in a `finally`) | `[[NSPasteboard generalPasteboard] clearContents]` inside an autorelease pool |
| Busy | another app holding the clipboard makes `OpenClipboard` fail: retried up to 10 times, 20 ms apart (≈ 200 ms at most), then Failed "the clipboard is held by another app (error N)" | the pasteboard server serialises writers; no retry |
| Not available | | AppKit not loaded (no `NSPasteboard` class) → not supported, the step skips |

The step means the same on both platforms, so there is nothing to convert (F8).

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (`ScriptMapping`: `clip.Clear();` alone), the App's `Components/Steps/ClearClipboard/` form.
