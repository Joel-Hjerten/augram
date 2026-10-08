# Steps/Hdr

The **HDR** step (key `hdr`, category Display, platform-bound): switch a display's HDR, the system switch behind Windows Settings' "Use HDR". Joel toggles HDR with Win+Alt+B today and keeps that hotkey; this is the system-level alternative. Research and sources: [docs/learnings/0002-display-modes.md](../../../../docs/learnings/0002-display-modes.md) §2–3.

| File | Role |
|---|---|
| `HdrAction` | `Toggle`, `On`, `Off` |
| `HdrStep` | record: `Action` (default Toggle), `Target` (`DisplayTarget`, default `UnderGesture`) · `Summary` "Toggle HDR", "HDR on", "HDR off", with " (main display)" when targeted |
| `HdrStepType` | `{ "action": "Toggle", "display": "UnderGesture" }`; an unknown name is a `StepFormatException` naming the member |
| `HdrExecutor` | the adapter cannot switch HDR (`CanSwitchHdr` false) → Skipped "switching HDR is not supported on MacOS" · no display under the start → Skipped · the display has no HDR → Skipped "SONY TV does not support HDR" · already in the wanted state → Done, nothing applied · `SetHdr` → Done, or Failed with the adapter's reason. Toggle reads the state fresh each run. One Debug line (`steps` / "HDR": action, display, was, outcome, reason) |

## Conversion (F8)

`IsPlatformNeutral` is false. Windows → macOS: `StepConversion.None("HDR on has no macOS equivalent: macOS offers apps no way to switch HDR")`, so the step list marks it and the executor skips it there. macOS → Windows and same-platform: unchanged.

| | Windows (`Win32DisplayModes`) | macOS (`MacDisplayModes`) |
|---|---|---|
| read | CCD `DisplayConfigGetDeviceInfo`: `GET_ADVANCED_COLOR_INFO_2` (24H2+, HDR supported / user enabled bits) first, else `GET_ADVANCED_COLOR_INFO` | none: every display reads `Unsupported` |
| switch | `DisplayConfigSetDeviceInfo` with `SET_HDR_STATE` when `_INFO_2` answered, else `SET_ADVANCED_COLOR_STATE` | no public API; `CanSwitchHdr` is false |

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the App's `Components/Steps/Hdr/` form.
