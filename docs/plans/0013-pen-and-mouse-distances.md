# Plan 0013: the pen's own distances — Augram tells the Wacom pen from the mouse and uses pen values for the start and drag distances

Status: **PROPOSED (2026-10-21, PC session), decisions to confirm with Joel.** Origin: Joel, 2026-10-21. At work on the Mac and at home on the PC he draws with a Wacom Intuos Pro L and Pro Pen 2 instead of the mouse, switching between them. The pen's side buttons are Right click (upper) and Middle click (lower, his stroke button); pen button mode is Hover click, so a side button clicks while the pen hovers over the tablet, which is shakier than a tip-down press. He usually runs the pen in Wacom's **mouse mode** (relative tracking, like a mouse, with Wacom's mouse acceleration on) with Windows Ink off; a fresh driver install defaults to pen mode with Ink on. In Krita the popup palette opens on Right; with the pen the press wobbled past Augram's **button drag distance** (10 px; Right is held back there because Zoom In/Out are Right + wheel), so Krita got a right-drag and no palette. The same wobble counts against the **gesture start distance** (30 px) when the lower button starts a stroke. His ask: "detect which device was last in use to drive the mouse", "separate values for that device", and the distance "for non-gesture recognition, when it's just about pressing the right mouse button". He also floated per-app stroke sensitivities. Both distances already exist in Options › Strokes › Capture; what is missing is a pen value for each and a way to know which device pressed.

Before this plan: [0.20.4](completed/0005-button-triggers.md) (decision 10's status note) made the Mac take the input the Wacom daemon posts; 0.20.5 (in progress) does the same on Windows, where Wacom's pen mode with Ink on injects every pen click and Augram dropped them (log 2026-10-21 07:27–07:42, `Button ignored … reason=simulated` then `released elsewhere` for tip, Right and Middle; in mouse mode the pen's clicks arrive as ordinary input and already work). This plan assumes 0.20.5 is in.

## Decisions (to confirm with Joel)

1. **One pair of pen values, machine-wide, not per app.** Options › Strokes › Capture gets a **Pen** value beside the mouse's for the two distances: **Start distance** and **Button drag distance**. Nothing else gets a pen value now (cancel delay, min segment, recognition threshold stay shared). Per-app sensitivities are **held off**: the problem is the pen, not Krita, so one pair fixes every app at once; an app-group override can come later if use shows one app still needs its own, and nothing here closes that door.
2. **The device is decided at the press and kept for the press**, as the window under the pointer is (plan 0005). A press is a **pen press** when the platform says the pen is the device in use at that moment; otherwise it is a mouse press. A pen press uses the pen's distances for its whole life; moving the mouse during it changes nothing.
3. **"In use" means near or recently moved.** The pen counts as the device in use when the tablet reports it **in proximity** (hovering or touching), or when the pen's own input **moved within the last 500 ms** where no proximity signal exists. Which of the two each platform and Wacom mode can provide is what step 1 finds out; the rule is written so that either signal is enough. The mouse is the fallback: no pen signal, mouse values, which is today's behaviour byte for byte.
4. **A command's own drag distance keeps its rule, per device.** Today the largest of the commands holding a button over a window wins, a command without one counting as the Options value (plan 0004). For a pen press the same rule runs with the **pen** Options value in place of the mouse's. A command set to 40 px beats a pen value of 30; a command set to 5 does not. No per-command pen value.
5. **Defaults.** Pen start distance **50 px**, pen button drag distance **30 px** (mouse: 30 and 10). Joel tunes them on both machines once he feels them; the numbers are starting points, not findings.
6. **Settings stay per machine.** Settings never sync (`Core/Sync` README), so the Mac and the PC each hold their own pair. Config schema goes up one, as every settings addition has (plan 0004 decision 4).
7. **Every Wacom mode is supported**, on both platforms: pen mode and mouse mode, Windows Ink on and off. Wacom sets these per application too (Tablet Properties' Application row), so Augram never reads a machine-wide driver setting; it decides from the input itself. Where a mode gives no usable signal at all, the fallback is decision 9, never "use pen mode".
8. **Step 1 is a probe, run before anything is designed around a signal.** A listen-only tool (`tools/PenProbe`) that installs **no suppressing hook and no overlay**, logs for every mouse event which signals are present, and is run by the lead on the PC with Joel at the machine, and by Joel on the Mac (agents never run it: CLAUDE.md's hook rule). Findings go to `docs/learnings/0009-pen-and-mouse.md`; the tool is removed at the end of the plan unless Joel wants to keep it for the next tablet.
9. **Fallback if a mode has no signal:** a manual **"Pen"** toggle on the tray menu (and the same in Options), so Joel can switch Augram to the pen's distances by hand in that mode. Built only if step 1 shows a mode without a signal on a machine he uses it in.
10. **Not in this plan:** Wacom ExpressKeys and the touch ring (keys the driver posts; the hook still drops simulated keys), pen pressure (Augram never reads it), the earlier "ignore movement in the first 80 ms after a press" idea (dropped: it delays every real drag for a jitter the pen values already absorb).

## What step 1 must find out

The probe logs, per event, which of these say "pen", with the Wacom mode noted by hand at the top of each run. Three runs on the PC (mouse mode + Ink off, his usual; pen mode + Ink off; pen mode + Ink on, Wacom's default) and two on the Mac (pen mode, mouse mode), each with: pen moves, pen tip, pen Right and Middle, then the same with the mouse, then both alternating.

**Windows** (`Augram.Platform.Windows`; the probe is its own console app and may use any of these without touching the platform project):
- **Raw Input** (`RegisterRawInputDevices` with `RIDEV_INPUTSINK` on a hidden message window, so it reports while another app is in front): for each `WM_INPUT`, the device handle and name (`GetRawInputDeviceInfo`; a Wacom name has `VID_056A`), its type (mouse, or HID with the pen usage page 0x0D), and for a HID pen the in-range and tip bits. Questions: in mouse mode, does the pen's input arrive as a Wacom-named mouse device (it arrives non-injected at the hook, so it most likely comes through the HID stack, not `SendInput`)? In pen mode with Ink on, does the pen also appear as a HID pen device with an in-range bit, which would be the proximity signal? Does injected input (`SendInput`) show with a null device, as documented?
- **The low-level hook's extra info** (the probe's own `WH_MOUSE_LL`, listen-only, `CallNextHookEx` always): `LLMHF_INJECTED` and `dwExtraInfo`; with Ink on, pen-made mouse input carries the `0xFF515700` signature (`MI_WP_SIGNATURE`). SharpHook does not pass `dwExtraInfo` on, so using it would need a second hook of our own; recorded so the choice is informed, not as the first option.
- **Wintab** (`Wintab32.dll`, present on the PC: `WTOpen` with a system context, `WT_PROXIMITY` and `WT_PACKET`): does a background window get proximity in every mode? Wintab is the one channel that still carries pen data in mouse mode with Ink off (Photoshop's WinTab preference proved it on 2026-10-21), so it may be the only proximity signal there. Cost: a dependency on a Wacom DLL; a machine without it must behave as today.

**macOS** (`Augram.Platform.MacOS`): a listen-only `CGEventTap` (`kCGEventTapOptionListenOnly`, session level; Accessibility is already granted for SharpHook) logging `kCGEventTabletProximity` (enter and leave), and for every mouse event its `kCGMouseEventSubtype` (`kCGEventMouseSubtypeTabletPoint` 1, `kCGEventMouseSubtypeTabletProximity` 2) and `kCGEventSourceUnixProcessID` (the Wacom daemon's pid; the 0.20.4 finding). Questions: in mouse mode, does the driver still mark events with the tablet subtype, and still post proximity? Which run loop thread the tap needs (its own; the Mac's `MacSystemEvents` needs the main thread, this must not).

**Both:** latency between a pen move and its signal (the signal must be there before the press it should classify; a signal that arrives after the press is useless), and whether the Wacom driver's own injected clicks (pen mode, Ink on) carry the device at all. The result of step 1 is a table per platform and mode: proximity available yes/no, device identity available yes/no, the chosen rule.

## How it works

**The device is one value the hook thread reads at the press, published off it**, the way the pointer answer is (`Engine/Hosting/InputGate.PublishPointer`, Engine README "The watch"): no OS call on the hook thread, no allocation. A platform adapter implements a small Core abstraction, `IPointingDeviceSource` (`Core/Abstractions`), whose `Current` is a volatile read of `PointingDevice { Mouse, Pen }` (`Core/Capture`); the adapter sets it from its own thread (Raw Input's message window thread on Windows, the tap's run-loop thread on the Mac) by decision 3's rule, with a clock for the 500 ms. No adapter (a test host, a machine without the signal) reads Mouse.

**The press carries its device** as it carries its drag distances: `CaptureEvent.ButtonDown` gains `Device`, set by the gate from the source at the press. `CaptureStateMachine` keeps it with `_drags` and `_ownerIsStroke`, and the one line that picks the distance (`CaptureStateMachine.Motion.cs`, Held) asks `Thresholds.StartDistanceFor(device)` and `_drags.For(owner, Thresholds.ButtonDragDistanceFor(device))`. `CaptureThresholds` stays the one home for the numbers (`PenStartDistancePx`, `PenButtonDragDistancePx`, with the two `For` accessors); `SettingsRules` bounds them as it does the mouse's. `AnchorDragDistances` does not change: it packs the commands' own values; the Options default is passed in at the call.

**Logging:** the press's device is one more field on the lines that already describe a press (`Stroke` / `Button trigger` / `Gesture recognized`: `device=pen`), so a wrong classification is visible in Diagnostics › Log without a new line.

## What changes

**Core**
- `Capture/PointingDevice`, `Abstractions/IPointingDeviceSource`; `CaptureEvent.ButtonDown.Device`; `CaptureThresholds` pen values and the two `For` accessors; `SettingsRules`; `SettingsStore.SetCapture` unchanged (one record). `Config`: schema version up one, the two values written beside the others, defaults when absent.

**Engine**
- `Hosting/InputGate`: reads the source at the press and stamps the event; `EngineHostOptions` takes the source as it takes the cursor probe; the log fields.

**Platform** (one adapter each, chosen by step 1; the folder READMEs get a row and a section)
- `Augram.Platform.Windows/Input/Win32PointingDevice`: Raw Input on a hidden message window on its own thread (the pattern of `Win32SystemEvents`' hidden-window case), or Wintab, per step 1. A machine without the signal publishes Mouse and logs once at start what it found.
- `Augram.Platform.MacOS/Input/MacPointingDevice`: the listen-only tap on its own run-loop thread; without Accessibility it publishes Mouse and logs once.
- `EngineModule` wires the adapter; `--no-engine` installs none.

**App**
- `ViewModels/AppSettingsViewModel`: `PenStartDistancePx`, `PenButtonDragDistancePx`. `Screens/OptionsScreen` Capture section: the two pen fields under the mouse ones ("Start distance, pen (px)", "Button drag distance, pen (px)"), help text saying when a press counts as the pen's. The F1 inspector names them as it names the others. If decision 9 is built: the tray toggle (`Tray/`) and a tick box in the same section.
- `DevGallery`: nothing new (the fields are NumberFields); a UiShots render of Options › Strokes for the look.

**Tools and docs**
- `tools/PenProbe` (step 1; removed at the end). `docs/learnings/0009-pen-and-mouse.md` (the table of signals per platform and mode, what Wacom's modes do to the input, the chosen rule, and the Krita diagnosis). Requirements F1 (the pen pair beside the thresholds). Capture README (the thresholds row, the Held rows), Engine README (the source in the hosting table and the press's device), both platform READMEs. CLAUDE.md read-first row for the two adapters once they have invariants.

**Version:** 0.21.0.

## Tests (one per behaviour, beside the neighbours; fakes, never real hooks)

- Core: a pen press uses the pen start distance and a mouse press the mouse's, from the same machine; a pen press keeps its distances after a mouse move; a pen press's drag distance is the largest of the commands' own and the pen Options value (decision 4); `SettingsRules` refuses a pen value out of range; config round-trips the two values and an older file reads the defaults.
- Engine: the gate stamps the device the source reports at the press, not at the move; no source means Mouse; the log line carries `device=pen`.
- Platform: the pure parts only (the decision-3 rule over a fake clock and fake signals: proximity sets Pen, leaving it sets Mouse, a move keeps Pen for 500 ms; a Wacom device name is recognised; a Raw Input or tablet event is decoded) on every OS; the native readers are Joel's check, as the Mac's TIS calls are.
- App: the two fields read and write the settings; one UiShots render.

## Steps

1. **The probe** (lead builds `tools/PenProbe`, effort high; small, one file per platform plus a shared log format). Run on the PC with Joel present (the lead says so first, listen-only, killed afterwards), then Joel runs it on the Mac (zsh: `~/.dotnet/dotnet run --project tools/PenProbe`). The lead writes learnings 0009 from the logs and fills in the table. **Gate:** the decisions above are confirmed or revised against it before step 2; decision 9 is decided here.
2. **Core and Engine** (one agent, effort high: `Capture` and `Engine`). The device on the press, the pen values, the config, the log fields, the tests.
3. **Platform adapters** (one agent per platform, effort high; after step 1, parallel with step 2 once `IPointingDeviceSource` exists). The Windows one is checked by the lead on the PC with Joel present; the Mac one by Joel.
4. **App** (lead or an agent at medium; after step 2). The two fields; decision 9's toggle if needed.
5. **Joel's check,** PC then Mac, both Wacom modes on each.

## Joel's check

1. **Krita, pen, mouse mode + Ink off (the case that started this):** a pen right-click opens the popup palette every time; the mouse's right-click still opens it; Right + wheel still zooms with both.
2. **Strokes with the pen's lower button** draw and fire with the 50 px start distance, and the mouse's with 30 px: a short mouse flick that fires today still fires; the pen's wobble on a plain middle click no longer draws.
3. **Switching devices:** mouse right after pen and pen right after mouse take their own distances at once (the 500 ms rule, where it applies); a press started with the pen keeps the pen's distances if the mouse is nudged during it.
4. **Wacom's other modes** (pen mode with Ink on and off): the same, plus that the log says `device=pen` for pen presses and `device=mouse` for mouse presses.
5. **Blender:** Space + pen buttons unchanged (hold remaps do not use these distances).
6. **Diagnostics › Log** names the device on each press line; Options › Strokes › Capture shows the two pen fields, with the help text making sense.
7. **The Mac,** the same list, in the modes he uses there.

## Open

- Whether the **Mac** needs the probe at all if the first check of `kCGEventTabletProximity` in mouse mode answers it in five minutes; the probe is cheap, so it runs anyway and the learnings doc gets a complete table.
- Whether a pen **cancel delay** is wanted later (the pen is slower to settle, so 1000 ms may cancel real pen strokes drawn slowly). Not now; one line in requirements F1 marks it.
- Whether to keep `tools/PenProbe` (decision 8).
- The parked UIAccess plan ([project memory](../../README.md)) takes the next number, 0014.
