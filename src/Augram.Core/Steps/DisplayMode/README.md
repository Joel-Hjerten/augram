# Steps/DisplayMode

The **Display mode** step (key `displayMode`, category Display, platform-neutral): set a display's resolution and/or refresh rate. It replaces Joel's 12noon Display Changer presets (`dc64cmd.exe -refresh=24`, `-width=1920 -height=1080`); research, sources and the reasoning behind every rule below: [docs/learnings/0002-display-modes.md](../../../../docs/learnings/0002-display-modes.md).

## Files

| File | Role |
|---|---|
| `DisplayModeStep` | record: `Resolution` (`DisplayResolution?`, null = Auto, keep the current one), `Refresh` (`RefreshRate?`, null = Auto), `Target` (`DisplayTarget`: `UnderGesture` default, or `Main`) · `Summary` "Display 1920×1080 at 119.88 Hz", "Display refresh 24 Hz", "Display 3840×2160", "Display (no change)", each with " (main display)" when targeted |
| `DisplayModeStepType` | `{ "width": 1920, "height": 1080, "refreshHz": 119.88, "display": "UnderGesture" }`. `width`/`height` 1..32767, both or neither (absent = Auto); `refreshHz` a JSON number above 0 and at most 1000, kept to three decimals (millihertz), so 119.88 and 120 stay apart (absent = Auto); `display` a `DisplayTarget` name. Written as a double, so the file reads 119.88, 120, 23.976. A numeric string, a lone width or height, a bad rate or target name is a `StepFormatException` naming the member. `Convert` is always `Same` |
| `DisplayModeResolver` | the decision table below, pure; also `LargestFirst` (resolutions) and `HighestFirst` (rates), the orders the reasons and the App form list them in |
| `DisplayModeResolution` | the resolver's answer: a mode to apply, the same mode flagged current, or the reason the step is skipped |
| `DisplayModeExecutor` | both Auto → Skipped "nothing to change…" · no displays → Skipped "no displays reported on Windows" · no display under the start → Skipped · resolver says unsupported → Skipped with its reason · already current → Done, nothing applied · `SetMode` → Done, or Failed with the adapter's reason. One Debug line (`steps` / "Display mode": requested, display, from, to, outcome, reason) |

## Decision table (`DisplayModeResolver`)

| Situation | Result |
|---|---|
| resolution W×H the display lacks | unsupported: "SONY TV has no 2560×1080; it offers 3840×2160, 2560×1440, … and N more" (eight, largest first) |
| refresh R offered at the target size | that rate |
| R not offered, a rate within 0.2 % is (`RefreshRate.IsNear`: 120 ↔ 119.88, 60 ↔ 59.94, 24 ↔ 23.976) | the closest such rate; the exact one always wins when both exist |
| R not offered at all | unsupported: "SONY TV has no 144 Hz at 3840×2160; it offers 120, 119.88, …, 23.976 Hz" |
| refresh Auto | the current rate if the target size has it (exact, then near); else the offered rate closest to it, the lower on a tie; with no known current rate (macOS 0), the highest |
| the resolved mode is the current one | current: Done without a change |

The whole mode is resolved before anything is applied and the adapter applies it in one call, so a step never half-changes a display. Rates the display reports as 0 (some macOS panels) are offered as unknown: they serve Auto and a pure resolution change, never a stated rate.

## Which display, and the units

`StepExecutionContext.Displays` (`IDisplayModes`) is read fresh on every run. `DisplayLookup` (Abstractions) picks the display whose bounds contain `StepExecutionContext.Start` (both in the hook's coordinates), or the main one. A resolution is what that platform's display settings call one: physical pixels on Windows, points (the "looks like" size) on macOS; the adapter maps it to a native mode. Applied changes are stored like the OS's own settings page does (Windows `CDS_UPDATEREGISTRY`, macOS `kCGConfigurePermanently`).

| | Windows (`Platform.Windows/Display/Win32DisplayModes`) | macOS (`Platform.MacOS/Display/MacDisplayModes`) |
|---|---|---|
| list | `EnumDisplayDevices` (attached, not mirroring) + `EnumDisplaySettingsEx`; whole hertz through `RefreshRate.FromLegacyHertz` (119 → 119.88) | `CGGetActiveDisplayList` + `CGDisplayCopyAllDisplayModes` with the duplicate low-resolution modes; points and rates rounded |
| apply | the listed setting carrying the mode, `CDS_TEST` then `CDS_UPDATEREGISTRY` | the HiDPI twin when there is one, in a `CGBeginDisplayConfiguration` transaction, permanently |

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the App's `Components/Steps/DisplayMode/` form (which also lists choices with `LargestFirst`/`HighestFirst`), `Import.StrokesPlus/DisplayChangerMapping` (not yet called by the step reader).
