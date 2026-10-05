# GestureSign: what to learn from it (and what not to copy)

**Source:** `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\GestureSign (StrokesPlusNet alternative)\GestureSign-master` (TransposonY/GestureSign, C# / .NET Framework 4.6, WinForms daemon + WPF control panel). Reviewed 2026-10-05.

**License: GPL-2.0.** Read it for architecture and behaviour; **do not copy code into Augram.** The recognizer in `GestureSign.PointPatterns` is the HighSign algorithm in its original C# (same `31.8309…` scale constant, same resample-then-compare-angles pipeline as handoff §3), but Augram's port must come from the **MIT** StrokesPlus C++ source, not from here. Reading it to check our port against is fine.

## Why it matters

It is the closest existing thing to ADR-0001's proposal: a .NET gesture engine in a tray daemon with a separate settings UI, proven on real Windows machines (it is on the Microsoft Store). It answers several "how would we even do that" questions concretely.

## Architecture (two processes)

```
GestureSign.Daemon (WinForms, tray, hooks, overlay, plugin execution)
   ├─ Input/PointCapture        capture state machine (the core)
   ├─ Input/InputProvider       low-level mouse hook + raw-input (touch/pen) message window
   ├─ Surface/SurfaceForm       trail overlay (layered window)
   ├─ Triggers/MouseTrigger     wheel / other button while holding the stroke button
   └─ Plugins (CorePlugins)     one folder per action type: logic + settings UI + icon
GestureSign.ControlPanel (WPF + MahApps.Metro)  settings UI; talks to the daemon over a named pipe
GestureSign.Common                              model, config, plugin contracts, IPC
```

Augram (ADR-0001) is single-process, so the IPC layer is irrelevant, but the *separation* (engine with no UI dependency, UI with no hook dependency, shared model) is the right shape and maps to separate projects.

## Capture state machine (compare with our spike)

States: `Ready → CapturingInvalid → Capturing → Ready`, plus `TriggerFired` and `Disabled`.

- **Button down** → `CapturingInvalid` (held, not yet moved). Event is suppressed. Process priority is bumped to High for the duration of the capture (cheap latency trick; worth stealing as an idea).
- **Move** → points are decimated: a point is only stored if ≥ `MinimumPointDistance` (default **20 px**) from the last stored point. The first stored point beyond that flips the state to `Capturing`. (SP.net: `MinSegmentLength` 6 for storage and `MinGestureStartDistance` 30 for "is this a gesture". GestureSign uses one number for both.)
- **Up in `CapturingInvalid`** (never moved) → synthesize a click of the stroke button at the cursor = click passthrough. Done on a worker task, state `Disabled` while replaying so the hook ignores the synthetic click.
- **Up in `Capturing`** → `EndCapture`: fire `BeforePointsCaptured` (recognition happens in a subscriber), then `GestureRecognized` → actions. Recognition is off the hook thread.
- **`InitialTimeout`** (held still too long in `CapturingInvalid`) → synthesize **button down** and go `Ready`: the hold becomes a real drag/hold for the app underneath. This is the "I meant to hold middle for autoscroll" escape hatch. SP.net instead cancels (`CancelDelay`). Both are reasonable; make it an option with Joel's SP.net behaviour as default.
- **Wheel or other button while in `CapturingInvalid`** → `MouseTrigger` looks up actions with a matching `MouseHotkey` (WheelForward/WheelBackward/Left/Right/…) for the recognised app, fires them, sets `TriggerFired`, and suppresses the subsequent button-up. This is exactly SP.net's wheel-while-holding that Joel uses for volume. Note it only works while **not yet moved**; once drawing, the wheel is ignored.

## App resolution

- At capture start: window under the **first point** via `WindowFromPoint`, then match apps by `MatchUsing` = window class / window title / executable file name, plain or regex, case-insensitive. UWP hosts (`ApplicationFrameWindow`) are unwrapped to the real `Windows.UI.Core.CoreWindow` child.
- Ignored app matched → capture is cancelled (event passes through). Optional `IgnoreFullScreen` cancels over any full-screen window that is not the desktop/shell (useful for games).
- Per-app `MatchActivated` flag: match against the **foreground** window instead of the window under the cursor.
- Action lookup: app-specific actions for the gesture; if none, fall back to global. An app action therefore shadows the global one for the same gesture (same model SP.net and Joel's config rely on). No explicit "override to nothing" exists; Joel's config needs one.

## Action execution

- An `Action` = gesture name + optional `MouseHotkey` / `Hotkey` / `Condition` (JS expression) + ordered `Commands`. A `Command` = plugin class name + serialized settings string + enabled flag. Actions run on a **serialized task chain** (never concurrently), each command waits for the target window to be idle (200 ms cap).
- **Activate target window first** if the plugin's `ActivateWindowDefault` says so (keyboard plugins: yes; run-command/volume: no), overridable per action. Matches SP.net's `AlwaysActivateWindowOnGestureComplete` behaviour that Joel relies on.
- **Plugin contract** (`IPlugin`): `Name, Category, Description, Icon, GUI (settings control), ActivateWindowDefault, Gestured(PointInfo), Serialize(), Deserialize(string)`. `PointInfo` carries the gesture points, the start point, the target window, and a UI sync context. One folder per plugin holding logic + settings control + settings POCO. **This is the shape N3 asks for:** adding an action type is one self-registering folder. Adopt the shape, not the code.
- Core plugin set: Hotkey, Send keystrokes (string, via `SendKeys` or `SendInput`), Key down/up, Mouse actions, Run command (via `cmd /C`), Open file / launch app, Activate/next/previous window, Minimize, Maximize/restore, Toggle topmost, Volume (synthesized `VOLUME_UP` key presses in a loop), Screen brightness, Touch keyboard, Send window message, Delay, Toggle gestures, Notifier. Joel's minimum set (F5) is a strict subset.

## Trail overlay

`SurfaceForm`: one WinForms window with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, sized to the union of all monitors (minus a 1 px margin, which avoids triggering Focus Assist), hidden until the first segment. Drawing is GDI+ into a DIB section with round caps/joins, pen width scaled by the DPI of the monitor at the start point, then pushed with `UpdateLayeredWindowIndirect` using a **dirty rect of only the new segments** per batch. Cleared with a clip of the widened path. Defaults: width 9, opacity 0.35.

Takeaways: per-monitor DPI matters for pen width; incremental dirty-rect updates keep per-frame cost tiny; the window must be `NOACTIVATE` and click-through. In Avalonia the equivalent is a transparent, topmost, non-activating window with hit-test disabled; if that proves janky the platform adapter can drop to exactly this Win32 layered-window technique.

## Training UI

`GestureSelector`: the control panel tells the daemon to enter `Training` mode; the user draws **anywhere on screen** with the stroke button (the whole desktop is the canvas, with the normal trail); the daemon sends the points back; the control shows the rendered thumbnail, a Redraw button, and a warning "this looks like existing gesture X" computed with the same recognizer (threshold 80). Double-clicking the thumbnail lets you append a second stroke (GestureSign's `PointPatterns[]` are *sequential strokes*, not training samples; SP.net's are samples, and that is the model Augram keeps).

Draw-anywhere training is better than a canvas inside the window: it captures exactly what the hook will see (same mouse, same speed, same decimation). Keep this for F3.

## Hotkey capture (the part not to copy)

`CorePlugins/HotKey/HotKey.xaml.cs` captures with WPF `PreviewKeyDown` / `PreviewKeyUp` on a focused text box and `e.Handled = true`. That only stops the key inside the window: the OS still sees it, so Win+L locks the session and PrintScreen fires. GestureSign works around it with an "extra keys" combo box for media, browser, PrintScreen, Home/End keys, and a special case in `HotKeyPlugin.Gestured` that calls `LockWorkStation()` when the stored hotkey is Win+L. Modifiers are checkboxes; the key part is a *list* of non-modifier keys. Reset is a button; there is no explicit commit (the control's value is read when the dialog is saved).

Augram instead captures through the engine's low-level hook with system-wide suppression while the field is active (requirements F5), which is how StrokesPlus.net does it. Keep GestureSign's `GetKeyName` idea (OS-provided key names via `GetKeyNameText`, with fallbacks for keys that return nothing) for display.

## Gesture icons

`GestureImage.CreateImage`: bounding-box-normalize the points (scale on the longest axis), center in the tile with a margin, stroke with round caps, add an arrowhead at the last segment. Pure vector `DrawingImage`, cached per gesture. Confirms F4; the Avalonia equivalent is a `Geometry` built the same way.

## Config

JSON via Newtonsoft with `TypeNameHandling` for the polymorphic app/action lists; writes are debounced (200 ms), a backup copy is taken before each save, and load falls back to backup → legacy format → shipped defaults. The backup-before-write and defaults-fallback are worth copying as behaviour (F8).

## What to skip

- All touch / pen / touchpad / raw-input HID machinery (roughly half the daemon): out of scope.
- IPC, named pipes, two-process plumbing: single process in Augram.
- UWP / Store / UIAccess specifics.
- GPL code itself.
