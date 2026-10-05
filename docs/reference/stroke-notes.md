# Stroke (poerin/Stroke): what to learn from it

**Source:** `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\Stroke (simple like strokeit - open source)\Stroke-main` = https://github.com/poerin/Stroke. .NET Framework 4.8, WinForms, ~3,000 lines of engine. **License: the LICENSE file is GPL-3.0** although the README badge says MIT; treat as GPL, read-only reference, no code copied. Reviewed 2026-10-05.

## Why it matters

It is the smallest working gesture engine in the set: one windowless `Stroke.exe` with the hook, overlay and executor, plus a separate `Stroke.Configure.exe` that can be deleted after setup. That is the cleanest demonstration of "engine has no UI dependency" (ADR-0002 §1) among the reference apps.

## Recognizer (different algorithm, same family)

- Resamples the stroke into **128 unit direction vectors** stored as signed bytes scaled to ±127 (`Gesture.GenerateVectors`). Similarity is the mean over the 128 indices of `1 − squaredDistance/32258`, giving a 0–128 score; the engine fires at `> 80` (62.5%).
- It is rotation-sensitive like HighSign's angle sequence (direction vectors are compared index by index) and scale/position invariant through arc-length resampling. Same family, different metric. Not a reason to change Augram's port; it is a second independent confirmation that the family works.
- **Template blending instead of multi-sample:** retraining a gesture blends the new drawing into the stored vectors at **10% new / 90% old**, then renormalises each vector (`GestureCanvas`, FAQ). Templates are therefore stored as vectors, not raw points. Augram keeps raw points (F2), but a "blend into one template" mode is a cheap later experiment next to Average / Best in checklist A22.
- Glyph rendering integrates the vectors back into a polyline, scales on the longest axis into a square with padding, and draws with an arrow end cap. Same F4 recipe as everyone else.

## Capture (minimal state machine, no timers)

- Button down: target = **root window under the start point**, process identity via `QueryFullProcessImageName` (full path, so regex on path works). "Filtrations" = ignore list of path regexes; a match passes the button through untouched.
- Click vs gesture: moved more than ~22.6 px (`dx²+dy² > 512`) from the start = gesture; otherwise the button-up replays a click via `SendInput` with a `dwExtraInfo` marker the hook recognises as its own.
- **Special gestures:** a click of another button or a wheel tick while holding, *without having moved*, is a trigger in its own right (`#5` = wheel up, `#6` = wheel down, `#0`–`#4` = clicks). Once one fires, the rest of the hold is "abolished" and the up is swallowed. This is the GestureSign/SP.net wheel-while-holding model again.
- **Key marks:** other buttons held *while drawing* become a 4-bit modifier mask handed to the action, so one gesture can have up to 16 variants. Same idea as SP.net's rocker modifiers; Joel deferred it.
- No cancel delay, no hold-to-drag, no modifier keys. Deliberately simple.
- **Action packages** are tried bottom-up; the first package whose path regex matches *and* that has an action for the gesture wins. An app package without that gesture therefore falls through to the global package. This is the same override model Augram uses (F5a), with "override to nothing" needing an explicit empty action.

## Overlay

A WinForms form the size of the virtual screen with `TransparencyKey` black and `WS_EX_TRANSPARENT | LAYERED | NOACTIVATE`, drawn with GDI `LineTo`. It is **shown and made topmost only for the duration of the stroke, then hidden and un-topmost**, which avoids the "hook window counts as visible and blocks fullscreen games" problem SP.net hit (reference §9). Augram's overlay should be invisible to the window manager when idle the same way.

## Learn canvas

Full-screen draw with the stroke button, cursor hidden while drawing; the stroke is blended into the selected gesture on release. Confirms that training with the real stroke button through the real hook is the natural design (F3 implementation note).

## What to skip

C# scripting via runtime compilation, the thread pool for scripts, BinaryFormatter settings file, key marks, blending as the only training model.
