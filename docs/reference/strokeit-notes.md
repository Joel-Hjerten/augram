# StrokeIt: what to learn from it

**Sources:** https://tcbmi.com/strokeit/ (Jeff Doozan, 2001–2009, v0.9.7; development stagnant), plugin SDK at `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\StrokeIt\StrokeIt_SDK`, plugin sources at https://github.com/doozan/si_plugins (MIT). The recognition engine itself is **closed source**; only the plugins and SDK are open. Reviewed 2026-10-05.

## Why it matters

StrokeIt was the first really good Windows gesture app and set the bar for leanness: a 140 KB installer, a **15 KB recognition engine**, and **90–300 KB of RAM**, no dependencies. Augram will not hit those numbers on .NET, but they are the reminder that the engine is small and everything else is weight we choose to carry.

## Behaviours worth copying

- **Left-click cancels an in-progress gesture.** While holding the stroke button and drawing, clicking another button aborts the stroke with no action and no replay. Cheap, discoverable, and a better escape than waiting for a timeout. Candidate for F1.
- **Control key temporarily disables StrokeIt.** Same as SP.net's Ignore Key, which Joel has set to Control. Three apps agree; keep it.
- **Command chain with fall-through control.** The host offers plugins `StopProcessing(SP_STOP_ALL | SP_NEXT_APP)`: a gesture is resolved through the chain of matching apps (most specific first, then global), and a command can either stop the chain or let it continue to the next app's command for the same gesture. Augram's model is override-only (app command shadows global), which matches Joel's config; the fall-through variant is noted as a possible later option, not v1.
- **Plugin command = exec + show + save + clear.** The SDK contract per command is: `exec(s_execParams)` with the target `HWND`, the gesture (name, raw points, bounding box) and the saved parameter string; `show(container)` to build the config dialog; `save()` to serialize the dialog back to a string; `clear()` to drop cached data. That is the third app in a row (StrokeIt 2001, GestureSign 2015, StrokesPlus.net 2020) converging on "an action type owns its executor, its settings UI, and its serialization". Augram's step-type slice (ADR-0002 §4) is the same shape.
- **`SetTarget(HWND)`:** a plugin can redirect which window later commands in the sequence act on. Niche; Augram passes the target in the step execution context instead.

## Keystroke plugin internals (MIT, readable)

- **Window activation uses the classic trick:** `AttachThreadInput(foregroundThread, ourThread, TRUE)` → `SetForegroundWindow(target)` → detach. This confirms the approach for Augram's activation risk (requirements checklist item on foreground activation). Platform.Windows should implement exactly this, with a fallback of the Alt-key nudge if it still fails.
- **Hotkey capture is a thread-local `WH_KEYBOARD` hook** active while the capture box has focus, reading modifier state with `GetAsyncKeyState` and trapping Win keys. Being thread-local it cannot stop the OS from acting on Win+L or PrintScreen; only a low-level hook can. So StrokesPlus.net's system-wide suppression remains the model for Augram's hotkey field (requirements F5).
- **Key sending** goes through `SendInput` with a `keybd_event` fallback, and the plugin has a separate "keys" command that types a string with a small escape syntax for special keys and key-down/key-up markers. Augram's "type text" step plus explicit hotkey steps cover the same ground without a custom syntax.

## What to skip

- Lua scripting, the "Pro" plugin API, `.sxp` export files (binary, closed), anything about Windows 95–7 compatibility.
- The closed recognition engine: nothing to learn from it beyond "80+ gestures, 15 KB".
