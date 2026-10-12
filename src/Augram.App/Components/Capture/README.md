# Capture

Controls that take input from the whole system through a port, falling back to their own window without the engine (`--no-engine`). The ports come from `ComponentPorts` unless the control's own property is set (tests, gallery); the engine adapters (`EngineKeyCapture`, `EngineButtonCapture`) live in `Hosting/`. Gallery page: "Capture fields".

| Type | Job |
|---|---|
| `HotkeyCaptureBox` (+ `.SingleKey`) | The hotkey field (F5): Capture or a click arms `IKeyCapture` (every key comes here, Win+L included); commit is mouse-only (Accept, a press outside, the window losing activation); Clear commits no key; every exit gives the keyboard back first; the engine's own release (10 s idle, hook reset) keeps what was pressed and says why. One-key mode (`SingleKey`): a hold key or an input; modifiers refused, with the host's reason from `KeyProblem` (Core's rule). |
| `HotkeyRecorder` | Pure "modifier set + one key" logic, right-hand modifiers kept. |
| `CaptureWindowWatch`, `AvaloniaKeyMap` | What a capturing field watches on its window (presses outside, deactivation, and the window's keys in the fallback), and Avalonia keys to Core's. |
| `ModifierToggle` | A modifier's check box with its "R" (right-hand) toggle. |
| `HotkeyChoices` | The editors step forms put beside the field: `Keys` (every key by its name here), `ModifierToggle(flag, …)`, `HeldKeys(…)` (a row of modifier boxes), `Modifiers` in press order. The Hotkey, Remap and Scroll forms use it; no form builds these itself. |
| `IKeyCapture`, `IButtonCapture` | The ports; `Begin` returns null when nothing can capture. |
| `ButtonDetector` | Detect-to-assign for a mouse button (a hold remap command's Add button…): the next press anywhere (the engine) or on its window (fallback, the press marked handled); gives up after 5 s and says so. |
