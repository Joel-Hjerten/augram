# Steps/Hdr

The **HDR** step (key `hdr`, category Display, platform-neutral): switch a display's HDR, the system switch behind Windows Settings' "Use HDR" and macOS's System Settings › Displays › High Dynamic Range. On the PC Joel keeps Win+Alt+B for it; on the Mac, which has no HDR shortcut, his H gesture runs this step (plan 0014). Research and sources: [docs/learnings/0002-display-modes.md](../../../../docs/learnings/0002-display-modes.md) §2–3, [plan 0014](../../../../docs/plans/0014-hdr-on-the-mac.md).

| File | Role |
|---|---|
| `HdrAction` | `Toggle`, `On`, `Off` |
| `HdrStep` | record: `Action` (default Toggle), `Target` (`DisplayTarget`, default `UnderGesture`) · `Summary` "Toggle HDR", "HDR on", "HDR off", with " (main display)" when targeted |
| `HdrStepType` | `{ "action": "Toggle", "display": "UnderGesture" }`; an unknown name is a `StepFormatException` naming the member |
| `HdrExecutor` | the adapter cannot switch HDR (`CanSwitchHdr` false: a macOS without SkyLight's HDR calls) → Skipped "switching HDR is not supported on MacOS" · no display under the start → Skipped · the display has no HDR → Skipped "SONY TV does not support HDR" · already in the wanted state → Done, nothing applied · `SetHdr` → Done, or Failed with the adapter's reason. Toggle reads the state fresh each run. One Debug line (`steps` / "HDR": action, display, was, outcome, reason) |

## Conversion (F8)

`IsPlatformNeutral` is true since plan 0014: the step converts unchanged both ways.

| | Windows (`Win32DisplayModes`) | macOS (`MacDisplayModes`) |
|---|---|---|
| read | CCD `DisplayConfigGetDeviceInfo`: `GET_ADVANCED_COLOR_INFO_2` (24H2+, HDR supported / user enabled bits) first, else `GET_ADVANCED_COLOR_INFO` | SkyLight (private, resolved at run time): `SLSDisplaySupportsHDRMode` (the current mode) and `SLSDisplayIsHDRModeEnabled`; missing calls read `Unsupported` |
| switch | `DisplayConfigSetDeviceInfo` with `SET_HDR_STATE` when `_INFO_2` answered, else `SET_ADVANCED_COLOR_STATE` | `SLSDisplaySetHDRModeEnabled(id, on, 0, 0)`, then the state is read back every 50 ms for up to 3 s (the display re-syncs in about 0.65 s); not there in time → Failed |

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the App's `Components/Steps/Hdr/` form.
