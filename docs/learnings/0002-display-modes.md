# Learnings 0002: display modes, refresh rates and HDR (the Display steps)

**Status: RESEARCHED 2026-10-08** (PC session, worktree agent), before building the `displayMode` and `hdr` steps that replace Joel's 12noon Display Changer commands. Windows facts were checked read-only on Joel's PC (enumeration only: `EnumDisplayDevices`, `EnumDisplaySettingsEx`, `QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`, DXGI `GetDisplayModeList`); nothing was changed and no setter was called. The macOS half is from Apple's documentation; the Mac session verifies it.

Joel's PC on 2026-10-08: one display `\\.\DISPLAY1`, CCD friendly name "SONY TV  *30", NVIDIA RTX 5090, 3840×2160 at 125 %, running at exactly 120 Hz (CCD target `vSyncFreq` 120/1). HDR supported and off.

## 1. 12noon Display Changer (what Joel's ten commands use)

Joel's commands run `dc64cmd.exe` (dated 2011-12-27) from `sp.RunProgram` scripts: `-refresh=` 23, 24, 25, 30, 50, 60, 100, 120 and `-width=1920 -height=1080`, `-width=3840 -height=2160`. The documentation is 12noon's Display Changer page (Joel saved a copy beside the program in 2023: `J:\My Drive\PROGRAMS\DISPLAY\12noon Display Changer\usage\`); the switches he uses have not changed since.

| Question | Answer (doc wording quoted) |
|---|---|
| A mode the monitor does not offer | Refused, nothing changes: "Normally, Display Changer prevents you from using a mode that is not supported by your video card and monitor. You can use the `-force` switch to use an unlisted video mode." `-test` "Does not apply new settings. Reports if the settings are valid or not." The whole mode is checked, so a preset fails as one piece. |
| `-refresh=N` alone | Keeps the resolution: for `-width`, `-height`, `-depth`, "If you don't specify a value, it uses the specified monitor's current value." The troubleshooting section adds the flip side: an unspecified value taken from the current mode can make an otherwise valid request fail (its example: the current "stretch" fixed output does not exist at the target resolution). |
| Which monitor | "`-monitor={name}` Specify which monitor to operate on. (If none is specified, the primary monitor is used.)" Joel's commands never pass it: they always changed the primary display. |
| Fractional rates | Display Changer only knows Windows' integer frequency (its `-listmodes` example lists "1280 720 32 59" next to "60"). `-refresh=N` picks the mode Windows lists as N. Windows lists a 1000/1001 rate rounded down (119.88 → 119, 59.94 → 59, 23.976 → 23, 29.97 → 29) and a whole rate as itself (section 2). So on Joel's TV today `-refresh=120` gives exactly 120 Hz, and 119.88 would be `-refresh=119`. His names "120hz (119.88)" and "60hz (59.940)" match Microsoft KB2006076: a display that offers only the TV timing (59.94 Hz and no 60 Hz) gets both 59 and 60 in the list and both are applied as 59.94, so on such a display `-refresh=60` lands on 59.94. |
| Permanent or temporary | A plain call is not written to the registry: only "`-more` … That also means the settings will be permanent—stored in the Registry". (The doc does not state the plain case outright; this is the reading of that sentence.) With a program on the command line the change is temporary on purpose and the old mode comes back when the program exits. |
| HDR | Not possible: 12noon says Display Changer X "cannot modify the Windows DPI (scaling factor) or HDR settings". |

## 2. Windows APIs

**Legacy API** (`EnumDisplaySettingsEx` / `ChangeDisplaySettingsEx`, `DEVMODEW`). `dmDisplayFrequency` is an integer. Measured on Joel's PC, at 3840×2160 the legacy list is 23 24 25 29 30 50 59 60 100 119 120 and DXGI's exact list is 23976/1000, 24, 25, 29970/1000, 30, 50, 60000/1001, 60, 100, 119880/1000, 120: one to one, a 1000/1001 rate is its rounded-down integer and every whole rate is itself. The integer therefore selects 119.88 or 120 exactly when the display has both, and on a display with only the TV timing Windows maps both integers to it (KB2006076). `CDS_TEST` checks a mode without applying it; `CDS_UPDATEREGISTRY` stores it (what Settings does); the result is `DISP_CHANGE_SUCCESSFUL`, `DISP_CHANGE_BADMODE` (not supported), `DISP_CHANGE_RESTART` (needs a restart), `DISP_CHANGE_FAILED` or a parameter error. The mode list repeats each mode per fixed-output variant (default, stretch, center), which is what tripped Display Changer's troubleshooting example.

**CCD API** (`QueryDisplayConfig` / `SetDisplayConfig`, `DISPLAYCONFIG_RATIONAL`). Rates are exact rationals, but `QueryDisplayConfig` returns only the *active* paths and modes: there is no list of what a display offers (DXGI `IDXGIOutput::GetDisplayModeList` is the public source of exact rates, through COM). `SetDisplayConfig` can request a rational rate through `DISPLAYCONFIG_PATH_TARGET_INFO.refreshRate` with the target mode left out, and then the target mode and scaling are Windows' choice.

**Decision: the legacy API lists and sets modes; CCD is read for names and used for HDR.** The legacy list is exactly what Settings and Display Changer offer, the integer already distinguishes 119.88 from 120, and a full mode is applied in one call, tested first. Augram turns Windows' integer into the exact rate with the rule above (`RefreshRate.FromLegacyHertz`: N is (N+1)/1.001 when N+1 is a TV rate, 24, 30, 48, 60, 120 or 240; otherwise N), so the step stores and shows "119.88 Hz" and the adapter sends 119 back. DXGI is not used: it adds nothing on Joel's machine and is COM; revisit if a monitor reports a rate the rule labels wrongly (a 143.86 Hz panel would read "143 Hz").

**Point → display.** The hook's coordinates are physical pixels (PerMonitorV2), and each display's current `DEVMODE` gives `dmPosition` and size in the same space, so Core picks the display whose bounds contain the gesture start; no `MonitorFromPoint` is needed. The CCD path of a display is the one whose `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` is the display's device name (`\\.\DISPLAY1`); `GET_TARGET_NAME` gives the monitor's friendly name.

**HDR.** `DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9)`: bits `advancedColorSupported`, `advancedColorEnabled`, `wideColorEnforced`, `advancedColorForceDisabled`; `DisplayConfigSetDeviceInfo(SET_ADVANCED_COLOR_STATE = 10)` with `enableAdvancedColor`. Windows 11 24H2 split "advanced color" into HDR and wide colour (auto colour management on SDR displays) and added `GET_ADVANCED_COLOR_INFO_2 = 15` (bits `advancedColorSupported`, `advancedColorActive`, reserved, `advancedColorLimitedByPolicy`, `highDynamicRangeSupported`, `highDynamicRangeUserEnabled`, `wideColorSupported`, `wideColorUserEnabled`, then `activeColorMode` SDR/WCG/HDR), `SET_HDR_STATE = 16` (`enableHdr`) and `SET_WCG_STATE = 17`. Joel's PC (build 26200) answers both: 0x1 (supported, off) and 0x51 (HDR supported, not enabled). **Augram asks `_INFO_2` first and then switches with `SET_HDR_STATE`; only a Windows that refuses `_INFO_2` gets the old pair**, because on 24H2 "advanced color on" can mean wide colour on an SDR display, not HDR. This is the switch Settings' "Use HDR" toggles, and it is stored like it. Win+Alt+B stays Joel's hotkey; the step is the system-level alternative.

## 3. macOS

- **Displays.** `CGGetActiveDisplayList`; `CGDisplayBounds` in global points with the origin at the top-left of the main display (the hook's space); `CGMainDisplayID`; `CGDisplayIsBuiltin`. `CGGetDisplaysWithPoint` exists, but Core's containment test over the bounds gives the same answer without another call.
- **Modes.** `CGDisplayCopyAllDisplayModes(display, options)`; with `kCGDisplayShowDuplicateLowResolutionModes` = true it also lists the 1x twins of HiDPI sizes (System Settings' "Show all resolutions"), which on a 4K TV carry the 1080p TV rates (23.976, 59.94…) that the HiDPI 1920×1080 lacks. `CGDisplayModeGetWidth`/`Height` are points (the "looks like" size), `GetPixelWidth`/`Height` pixels; `CGDisplayModeIsUsableForDesktopGUI`. `CGDisplayModeGetRefreshRate` is Hz as a double, and Apple says: "Some displays may not use conventional video vertical and horizontal sweep in painting the screen; for these displays, the return value is 0" (older built-in panels).
- **Setting.** `CGBeginDisplayConfiguration` → `CGConfigureDisplayWithDisplayMode` → `CGCompleteDisplayConfiguration(config, option)`, `CGCancelDisplayConfiguration` on failure. Options: `forAppOnly` ("After the application terminates, the display configuration settings revert"), `forSession` (reverts at logout to "the last saved permanent configuration"), `permanently` (what System Settings does). `CGDisplaySetDisplayMode` is the app-only shortcut and is not used.
- **HDR.** No public API switches it: System Settings › Displays › High Dynamic Range is the only switch, and the utilities that flip it (BetterDisplay) use private SkyLight calls. AppKit only reports EDR headroom. **The HDR step has no macOS equivalent**: it converts to "no equivalent" with that reason and the macOS adapter declines it.

**Resolution means what each OS's display settings mean by it:** pixels on Windows (Settings › Display resolution), points on macOS (the "looks like" size). When one size and rate exist as both a HiDPI and a 1x mode, the macOS adapter picks the HiDPI one, as System Settings does by default.

## 4. Decision table (Core `Steps/DisplayMode/DisplayModeResolver`)

The step stores Resolution (Auto, or W×H) and Refresh (Auto, or a rate in Hz rounded to 3 decimals, so 119.88 and 120 stay apart). At run time the target display (under the gesture start, or the main display) and what it offers are read fresh; nothing is cached.

| Situation | Result |
|---|---|
| Resolution and refresh both Auto | Skipped "nothing to change" |
| Resolution W×H not offered by the display | Skipped "SONY TV has no 2560×1080; it offers 3840×2160, 2560×1440, …" (largest first) |
| Refresh R offered exactly at the target size | that mode |
| R not offered exactly, but a rate within 0.2 % is (120 ↔ 119.88, 60 ↔ 59.94, 24 ↔ 23.976) | the closest such rate; on a display with both, the exact one always wins |
| R not offered at the target size at all | Skipped "SONY TV has no 144 Hz at 3840×2160; it offers 120, 119.88, …, 23.976 Hz" |
| Refresh Auto, resolution changes | the current rate if offered at the new size (exact, then within 0.2 %); else the offered rate closest to the current one, the lower on a tie: a 120 Hz desktop going to a size that tops out at 60 lands on 60, not 24, so the rate stays as close to what the user had as the display allows |
| Refresh Auto and the current rate unknown (macOS reports 0) | the highest known rate offered at the new size, else the size's only mode |
| R given, but the display reports no rate (0) for every mode at the size | Skipped with the offer list |
| The resolved mode is the current one | Done without touching the display ("already 3840×2160 at 120 Hz") |
| The adapter refuses or fails (BADMODE, RESTART, a CoreGraphics error) | Failed with the adapter's reason; the command stops |

**Never half-applied:** the full mode (size and rate) is resolved first and applied in one call (`ChangeDisplaySettingsEx` after `CDS_TEST`; one CoreGraphics configuration transaction), so a step that cannot have both changes neither.

**Permanence: like the OS's own settings page** (`CDS_UPDATEREGISTRY`; `kCGConfigurePermanently`), not like Display Changer's plain call. A mode that is not stored lasts until sign-out and can be replaced by the stored one whenever Windows re-applies the display configuration (sleep and wake, a TV switched off and on), which would undo a "24 Hz for this film" step at the worst moment. Open question for Joel: does he want an "until sign-out" option.

**Display Changer import** (`Import.StrokesPlus/DisplayChangerMapping`): `-refresh=N` is Windows' integer, so it goes through the same rule (23 → 23.976, 59 → 59.94, 120 → 120); `-width` and `-height` both or neither; the target is the step default (the display under the gesture), which on a one-display PC is the primary display Display Changer used. Anything the step cannot express (`max`, `-depth`, `-monitor`, `-force`, `-test`, a program to run) leaves the command a Run step.

## Sources

- 12noon Display Changer documentation (saved copy, section 1); news posts: https://12noon.com/?cat=8 ; Display Changer X (HDR and DPI not changeable): https://12noon.com/?page_id=4793
- Microsoft KB2006076, "Screen refresh rate in Windows does not apply the user selected settings on monitors & TVs (that report specific TV compatible timings)": https://support.microsoft.com/en-in/help/2006076/screen-refresh-rate-in-windows-does-not-apply-the-user-selected-settin
- `DISPLAYCONFIG_PATH_TARGET_INFO` (refreshRate, target mode index): https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info
- `DISPLAYCONFIG_DEVICE_INFO_TYPE`: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_device_info_type ; the 24H2 additions (`_INFO_2` bit layout, `SET_HDR_STATE`, `SET_WCG_STATE`) as published in mingw-w64's `wingdi.h`: https://mingw.googlesource.com/mingw-w64/+/e33940a2ad6501a29f59642692e29cf2fe770e55%5E!/
- Advanced Color overview (HDR, and WCG on SDR displays): https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range
- `ChangeDisplaySettingsEx`: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw ; `QueryDisplayConfig`: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig
- Apple: `CGConfigureOption` https://developer.apple.com/documentation/coregraphics/cgconfigureoption ; `CGDisplayMode.refreshRate` https://developer.apple.com/documentation/coregraphics/cgdisplaymode/refreshrate ; `kCGDisplayShowDuplicateLowResolutionModes` https://developer.apple.com/documentation/coregraphics/kcgdisplayshowduplicatelowresolutionmodes
