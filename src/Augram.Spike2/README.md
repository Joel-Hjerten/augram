# Augram.Spike2 — risk spike for B1–B4

Throwaway probe for the four open risks in [requirements §6 part B](../../docs/requirements.md) (plan [0001, M0 step 3](../../docs/plans/0001-first-version.md)). One program, one command-line switch per risk. Deleted at M1; the answers go to `docs/learnings/0001-spike2.md`.

Stack: .NET 10, SharpHook 7.1.3, Avalonia 11.3.22 (the versions pinned in `Directory.Packages.props`; the csproj carries its own copies so it builds standalone). Everything is logged to the console **and** to `spike2-<mode>.log` next to the exe (`bin/Release/net10.0/` or `bin/Debug/net10.0/`), so a run that lasts hours can be sent back as a file. Every mode prints an instruction paragraph at start and exits on Ctrl+C or closing the console (hooks are disposed, suppression released; if the process is killed, Windows removes the hooks anyway).

```
dotnet build src/Augram.Spike2 -c Release
dotnet run --project src/Augram.Spike2 -c Release -- activate
dotnet run --project src/Augram.Spike2 -c Release -- overlay [keep] [layered] [selftest]
dotnet run --project src/Augram.Spike2 -c Release -- hook
dotnet run --project src/Augram.Spike2 -c Release -- keys [a|b|c|ab|bc|...]
```

## `activate` — B1 foreground activation from a background process

**What it does.** Every 3 s, on a worker thread (not the UI thread, not a hook thread): window under the cursor → `GetAncestor(GA_ROOTOWNER)`. If that root is not already the foreground root and not the desktop/shell/taskbar (rule A20), it tries in order: plain `SetForegroundWindow`; `AttachThreadInput(foreground thread → us)` + `SetForegroundWindow`; the same with the target's thread attached too; Alt tap via `SendInput` + `SetForegroundWindow`. Each attempt is judged by `GetForegroundWindow` after 50 ms. A tally per technique is printed at exit.

**What to do.** Click something else so the console is in the background, then hover (no clicking) over Chrome, an Explorer window, and a borderless-fullscreen game for a few seconds each.

**Pass/fail.** Pass: technique 0, 1 or 2 activates all three targets. Partial: only the Alt tap works (usable, but watch whether it opens a menu bar in the target; the real app would need to send the Alt tap only when the attach trick fails). Fail: nothing activates a target, or the target only flashes in the taskbar. Fallback for fail: `Platform.Windows` does what SP.net ended up doing: offer the opt-in `SPI_SETFOREGROUNDLOCKTIMEOUT = 0` system setting, and keep a hidden window of our own so `AllowSetForegroundWindow` conditions can be met; a decision for Joel, not something to do silently.

## `overlay` — B2 Avalonia transparent click-through trail

**What it does.** Pre-creates at startup an Avalonia window covering the virtual screen: `TransparencyLevelHint = Transparent`, no decorations, not in the taskbar, `ShowActivated = false`, topmost, `IsHitTestVisible = false`; on the Win32 handle it adds `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (plus `WS_EX_LAYERED` with `layered`) and `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`. The window is hidden while idle (`keep` leaves it shown but empty, which is what SP.net got wrong with exclusive-fullscreen games). A SharpHook `SimpleGlobalHook` captures the **right** button (SP.net owns Middle on Joel's machine): the press and release are suppressed, points go hook thread → channel → worker → one batched UI flush per UI turn, and a 5 px round-capped green 50 % stroke is drawn, pen width scaled by the DPI of the monitor under the start point. On release, a stroke that moved < 30 px is replayed as a right click through `EventSimulator` (so context menus still work); anything longer just clears.

Per stroke it logs: `Show()` cost (hidden variant), time from button-down and from first move to the first `Render()` that drew a segment, time to the next animation-frame callback (≈ when that frame was handed to the compositor), number of UI batches and renders, average/max render time, worst hook-handler time, and whether `GetForegroundWindow()` ever returned the overlay.

`selftest` draws three synthetic strokes through SharpHook's simulator (accepted as real input only in this mode) and exits; it moves the cursor briefly. Measured on the dev machine (single 3840×2160 at 125 %): hidden-when-idle variant — `Show()` 2.5–4 ms, first segment rendered 3–11 ms after button-down (first stroke of a run is the slow one), next frame 11–20 ms; `keep` variant — first segment 0.3–6 ms, next frame 7–11 ms; render 0.01–0.1 ms avg, hook handler ≤ 0.4 ms; foreground never taken.

**What to do.** Draw with the right button over a normal window, over a borderless-fullscreen game, and across both monitors (the pen should look the same width on each; the trail must follow the cursor on the second monitor without offset). Right-click without moving: the context menu must still open.

**Pass/fail.** Pass: trail visible within one frame (≈ 16 ms) everywhere, correct position and width on both monitors, visible over the game, `overlay took foreground: no` on every stroke, context menus intact. Fail on any of those → per plan 0001, `Platform.Windows` gets the layered-window implementation from the GestureSign notes (`UpdateLayeredWindow` with dirty rects) behind `IOverlay`, and the Avalonia overlay is dropped. If only the `keep` variant passes the latency bar, keep the window shown and verify it does not block exclusive-fullscreen games.

## `hook` — B3 hook resilience across sleep, lock, display changes

**What it does.** Installs the SharpHook mouse hook on its own thread and counts events. One log line per minute with counts per event type. Subscribes to `SystemEvents.SessionSwitch`, `PowerModeChanged`, `DisplaySettingsChanged`, `SessionEnding`. Watchdog every 500 ms: if the cursor has moved (`GetCursorPos`) in at least four polls but no hook event arrived for 15 s, logs `HOOK DEAD`, disposes the hook, installs a new generation, and logs the first event after the reinstall (or, if silence continues, another `HOOK DEAD`). SharpHook's `HookEnabled`/`HookDisabled` and any `HookException` are logged too.

**What to do.** Leave it running for hours. Sleep and wake, Win+L and unlock, change resolution, let the screen saver run. After each, move the mouse for a few seconds.

**Pass/fail.** Pass: no `HOOK DEAD` across all transitions, or `HOOK DEAD` followed by `alive: first event N ms after install` every time (the health-check-and-reinstall design from plan 0001 M1 step 4 is then enough). Fail: a reinstall does not bring events back → the engine needs a stronger recovery (re-create the hook on a fresh thread after a session-unlock event, or restart the process) and that becomes a Platform concern to design in M1.

## `keys` — B4 keyboard suppression, media keys, text entry

**What it does.** A SharpHook keyboard hook runs throughout and logs every simulated event it sees (what the OS actually received).

- **(a)** After a 5 s countdown, for 10 s **every** key event is suppressed system-wide and printed as a combination (modifiers + key, Win included). A watchdog releases suppression after 10 s no matter what (Ctrl+C is suppressed too during the window). `SessionSwitch` is watched: if Win+L was seen and the session locked, the verdict line says suppression failed.
- **(b)** Sends VolumeUp ×2, VolumeDown ×2, MediaPlay via `EventSimulator` and logs each result code.
- **(c)** After a 5 s countdown types `augram test 123` with `SimulateTextEntry`, then after another 5 s types it again as individual key presses.

`keys bc` skips the suppression window (used for the agent's own run against Notepad).

**What to do.** During (a) press: Win alone, Win+L, Win+E, Esc, PrintScreen, Ctrl+Shift+S, Alt+F4, a media key. Watch the Start menu, Explorer, screenshot flash, and the lock screen. For (c) click into Notepad before the first countdown; run again with a game console focused.

**Pass/fail.** (a) Pass: every combination is printed and nothing happens system-wide, in particular no lock on Win+L. Fail: Win+L locks → the hotkey field cannot capture Win+L through the hook; the field documents that and GestureSign's special-case (store Win+L, execute via `LockWorkStation`) is the fallback. (b) Pass: volume OSD moves and play/pause toggles, result codes `Success`. Fail → `IInputSimulator` media keys go to a Platform call (`SendInput` with `VK_VOLUME_*`). (c) Pass: both lines read exactly `augram test 123` in Notepad and in the game console. Partial: `SimulateTextEntry` works in Notepad but not in the game (games read scan codes) while key presses work → the TypeText step uses key presses for ASCII and text entry for the rest. Fail for both in the game → Platform `SendInput` with `KEYEVENTF_SCANCODE`.
