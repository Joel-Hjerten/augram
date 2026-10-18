# 0005: Hold remaps on macOS (plan 0002 step 0, 2026-10-10)

**Question:** on macOS, with the physical button swallowed by our event tap and a Middle press posted instead, does Blender navigate? Do posted Shift and Ctrl reach Blender together with the posted button?

**Setup:** Joel's Mac (Apple silicon, macOS 26), Blender 5.2 (Homebrew), the default keymap (orbit Middle, pan Shift + Middle, zoom Ctrl + Middle). A throwaway console spike on SharpHook 7.1.3 (`spikes/hold-remap-mac/`, commits `cb27e23` and `e7533a6`, deleted after this note): Space swallowed while Blender is frontmost; Space + Left → Middle, + Right → Shift + Middle, + Middle → Ctrl + Middle, + Left + Right → Ctrl + Middle, rolling between them; a Space tap within 180 ms re-posted. Joel ran it from VS Code's terminal.

## Results

| Run | Left (orbit) | Right (pan) | Middle (zoom) | Left + Right (zoom) | Tap / long Space |
|---|---|---|---|---|---|
| A: drags pass through; buttons and modifiers through SharpHook (as Augram posts today) | orbits | no pan | no zoom | | not tried |
| A: drags pass through; buttons and modifiers through CoreGraphics | orbits | **no pan** | zooms | zooms | taps of 116 and 120 ms typed a space; a 2474 ms Space sent nothing, no playback |
| B: drags swallowed and re-posted as Middle drags; CoreGraphics | orbits | pans | zooms | zooms, rolling works | Joel: "felt basically perfect" (latency lines not captured) |

The spike's switch times (hook event to the last post) were 0.1–1.3 ms after the first, 5–6 ms for the first of a run.

## What it means

1. **Swallowing Space works** through SharpHook's hook on macOS, and so does the tap: the hold key part needs nothing Mac-specific.
2. **Modifiers must be posted natively on macOS.** Shift and Ctrl pressed through SharpHook's `SimulateKeyPress` do not reach Blender with a posted Middle (run 1: Middle orbited instead of zooming). What works: a `kCGEventFlagsChanged` event for the modifier (virtual key 56 Shift / 59 Control; flags = the mask, the left-key device bit, and 0x100) before the button, the same mask set on the `kCGEventOtherMouseDown` with `CGEventSetFlags`, and a flagsChanged without it after the down; posted at `kCGHIDEventTap`.
3. **Right-drags do not move a posted Middle drag; left-drags do.** With Right swallowed and Shift + Middle posted, Blender started nothing visible (run 2), while Left + Right (where macOS reports left-drags) zoomed. Shift itself is not the cause: it is encoded exactly like Ctrl, which works. So on macOS, while a hold remap holds a button output, the engine must swallow the physical drag and move events and re-post each as a `kCGEventOtherMouseDragged` (button center) at the same position, with `kCGMouseEventDeltaX`/`DeltaY` (fields 4 and 5) from the previous physical position and the current modifier flags. Done for every input button, not only Right: one rule, and Left feels the same either way.
4. Windows needs none of this: the AutoHotkey script posts through `SendInput` and Blender navigates.

## Synthetic modifiers and passing moves (2026-10-18)

**Found with Iris's loupe:** Joel's button trigger "Right + Left" over Iris holds ⌥⇧X from the chord's fire to its end. Iris cannot read the letter without Input Monitoring, so it polls `CGEventSource.flagsState(.combinedSessionState)` for ⌥ and ⇧ every 25 ms and closes the loupe when they read up. Holding the chord still kept the loupe; the first mouse move closed it. With the real keyboard, and on Windows, it stays.

**Cause:** every hardware mouse event that passes the tap carries the hardware's modifier flags, none, and the session's modifier state is reset from it, so modifiers posted as synthetic key events read as released. While a chord holds them, physical moves passed through (the press is frozen, moves are the app's).

**What works:** the pattern of §3 above. While a chord holds a key output with modifiers, the hook swallows each physical move and the worker re-posts it as a `kCGEventMouseMoved` at the same position, `kCGMouseEventDeltaX`/`DeltaY` from the previous physical position and `CGEventSetFlags(CGEventSourceFlagsState(combined session))` (`MacRemapButtonSimulator.RepostMove`; Engine README, "Button triggers"). A swallowed move never reaches the session state, so nothing resets the flags. Not yet covered: wheel ticks during such a chord, and a hold remap's key output with modifiers held while the mouse moves.

## Launch gotcha

A built .NET apphost (`bin/…/HoldRemapSpike`) cannot find an SDK installed in the home folder (`~/.dotnet`): "You must install .NET to run this application". Run the dll through it instead: `~/.dotnet/dotnet bin/…/HoldRemapSpike.dll`. Augram itself is unaffected (it is launched the same way, and its installer bundles the runtime).
