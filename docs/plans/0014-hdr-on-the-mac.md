# Plan 0014: the HDR step works on the Mac, through SkyLight's private HDR switch

Status: **PROPOSED (2026-11-02, Mac session), decisions to confirm with Joel.** Origin: Joel, 2026-11-02. On the PC his small-H gesture sends Win+Alt+B and he keeps it that way ("one shouldn't toggle the HDR through some hidden native flags on Windows. It's better to let the Win+B button control that switch"). macOS has no keyboard shortcut for HDR and no Shortcuts action, so the Mac needs Augram to flip the switch itself. He chose the native route over BetterDisplay.

Before this plan: the `hdr` step exists (`Core/Steps/Hdr`, learnings [0002](../learnings/0002-display-modes.md) §2–3). On the Mac it is skipped on purpose: `MacDisplayModes.CanSwitchHdr` is false, every display reads `Unsupported`, and a Windows HDR step converts to "no macOS equivalent". The reason was that no *public* API switches HDR. This plan uses the private one that System Settings and the HDR utilities use.

## What the probe found (2026-11-02, read-only)

macOS 27.0.1 (26A434), M5 Max, one display: the SONY TV, CGDirectDisplayID 5, the main display, 2304 × 1296 points at 120 Hz. A `python3 -I` ctypes script loaded SkyLight and only read; nothing was switched.

| Symbol in `/System/Library/PrivateFrameworks/SkyLight.framework/SkyLight` | Present | SONY TV |
|---|---|---|
| `bool SLSDisplaySupportsHDRMode(CGDirectDisplayID)` | yes | true |
| `bool SLSDisplayIsHDRModeEnabled(CGDirectDisplayID)` | yes | false (HDR off) |
| `CGError SLSDisplaySetHDRModeEnabled(CGDirectDisplayID, bool, int, int)` | yes | not called |
| `CoreDisplay_Display_SupportsHDRMode` / `_IsHDRModeEnabled` / `_SetHDRModeEnabled` (re-exported by SkyLight) | yes | same answers |

The signatures come from two open-source projects that call them: joevt/AllRez (`AppleMisc.h`, the four-argument setter, called with `0, 0`) and Vextil/DisplayFixer (`research/hdrfix.m`, which waits 2 s after the setter before reading back). lsjingyun/HDRSwitch reports that the setter is the same path as the System Settings toggle, so Settings stays in sync. It measured the switch at about 0.65 s, most of it the display re-syncing.

## Decisions (to confirm with Joel)

1. **The three `SLSDisplay…HDRMode…` calls, nothing else.** They read support, read the state and switch it, the same switch as System Settings › Displays › High Dynamic Range. The `CoreDisplay_` twins are the fallback names if a later macOS drops the `SLS` ones. No colour profile, no mode change and no "force 60 Hz to get HDR" (mac-utils' trick): if the current mode has no HDR, the step says so and does nothing.
2. **Resolved at run time, never linked.** `NativeLibrary.TryLoad` and `TryGetExport` run once, on first use. If a symbol is missing, `CanSwitchHdr` is false and the step is Skipped with "this macOS has no HDR switch Augram knows" (today's skip, with a truer reason). It never crashes, and the version and the missing name go in the log once. Private API use does not block signing or notarization, only the Mac App Store, which Augram is not in.
3. **The Mac reads HDR like Windows does.** `Displays()` fills `DisplayInfo.Hdr` from support and state: `Unsupported`, `Off` or `On`. The executor's existing table then applies unchanged: no HDR on the display → Skipped; already in the wanted state → Done with nothing applied; Toggle reads the state fresh each run.
4. **The step waits for the switch to land.** After the setter returns `kCGErrorSuccess`, `SetHdr` reads the state back every 50 ms for up to 3 s. When it matches, the result is Done. When it does not, the result is Failed with "HDR did not turn on on Display 1 within 3 s", which stops the command. A nonzero `CGError` is Failed with the error. Why: the step after it, or a second H drawn quickly, must see the new state. Toggling twice in a second then flips twice, never zero times. The wait is on the executor thread, never the hook's.
5. **The step is platform-neutral from now on.** `HdrStepType.IsPlatformNeutral` becomes true and the "no macOS equivalent" conversion goes, so an HDR step converts unchanged in both directions. **Joel's H command does not change on the PC**: its Windows side keeps the Win+Alt+B hotkey. On the Mac he gives its macOS side its own HDR Toggle step (plan 0010: an edit makes that side its own). The Windows HDR step stays as it is for anyone who wants it; this plan does not touch Windows.
6. **The step form's note says what is true on each OS.** "Windows' "Use HDR" switch; Win+Alt+B does the same from the keyboard. macOS lets no app switch HDR…" becomes a sentence naming both switches: Windows' "Use HDR", and macOS's System Settings › Displays › High Dynamic Range.
7. **Not in this plan:** Mac display names ("Display 1" stays; "SONY TV" would need IOKit or `NSScreen.localizedName`, a separate small change), colour profiles, and anything on Windows.

## What changes

**Platform.MacOS**
- `Interop/MacNative.Display.cs`: `SkyLightHdr`, the three function pointers resolved once (decision 2), plus `IsAvailable` and the reason.
- `Display/MacDisplayList` reads support and state per display into the reading. `Display/MacDisplayModes`: `CanSwitchHdr => SkyLightHdr.IsAvailable`, `Hdr` filled in, and `SetHdr` with the wait from decision 4.
- README: the `MacDisplayModes` row.

**Core**
- `Steps/Hdr/HdrStepType`: platform-neutral, the conversion removed. `Steps/Hdr/README.md` and the `Steps/README.md` row: the macOS column says SkyLight (private) instead of "none". `Abstractions/IDisplayModes` and `HdrState` doc comments drop "no HDR on macOS".

**App**
- `Components/Steps/Hdr/HdrStepForm`: the note (decision 6).

**Docs**
- Learnings 0002 §3, the HDR bullet: the probe table above, and that the step now switches HDR through SkyLight.

**Tests** (sized to the change)
- Core: the conversion test now expects Windows → macOS unchanged. The executor's existing tests already cover each outcome through fakes.
- The SkyLight calls themselves are not unit-tested: CI's macOS runner has no HDR display, and a test must not switch one. Joel's check covers them.

**Version** 0.21.0. Build: one agent at `high` (Platform.MacOS and Core), or the lead directly, since it is a few files.

## Joel's check (Mac)

1. Install 0.21.0 and restart Augram (quit from the menu bar first).
2. Commands: open the small-H command, switch "Steps for" to macOS, replace its steps with **HDR › Toggle** (display: under the gesture).
3. Draw H over the SONY TV: HDR turns on, and System Settings › Displays shows High Dynamic Range on. Draw it again: HDR off.
4. Diagnostics › Log: the `HDR` line shows `was=Off`, then `was=On`, with outcome Done.
5. On the PC nothing changes: H still sends Win+Alt+B.
