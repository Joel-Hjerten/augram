# Steps/MediaKey

The **Media key** step (key `mediaKey`, category System, platform-neutral): tap one volume or playback key. Joel's SP.net config fires these through bare `SendVKey` virtual keys (14 actions: volume, play/pause, next/previous).

| File | Role |
|---|---|
| `MediaKeyKind` | `VolumeUp`, `VolumeDown`, `VolumeMute`, `PlayPause`, `NextTrack`, `PreviousTrack`, `Stop` |
| `MediaKeyKindExtensions` | `ToKeyCode()`: the one mapping to `Abstractions.KeyCode` (`VolumeUp/Down/Mute`, `MediaPlay`, `MediaNext`, `MediaPrevious`, `MediaStop`) |
| `MediaKeyStep` | record: `Key` · `Summary` "Volume up", "Mute", "Play/pause"… |
| `MediaKeyStepType` | `{ "key": "VolumeUp" }`; an unknown name is a `StepFormatException` naming `key` |
| `MediaKeyExecutor` | `KeyPress(code)` then `KeyRelease(code)` on `IInputSimulator`; a non-success `SimulationResult` is Failed with "`<code> press: Unsupported`" (no release after a failed press). One Debug line per run |

Media keys are the same on both platforms, so there is nothing to convert (F8). Whether the OS honours the synthesized key is the Engine's simulator's business (`SharpHookInputSimulator`), reported through `SimulationResult`.

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the importer (C1: `SendVKey` 166..183 → kind; other keys → Hotkey), the App's `Components/Steps/MediaKey/` form.
