# Steps/Delay

The **Delay** step (key `delay`, category Timing, platform-neutral): wait N milliseconds before the next step. Joel's SP.net config uses 24 of these inside keystroke sequences (Alt+Tab in games, "Save image": click, V, 700 ms, Enter).

| File | Role |
|---|---|
| `DelayStep` | record: `Milliseconds` · `Summary` "Wait 30 ms" |
| `DelayStepType` | `{ "milliseconds": 30 }`, 0..60000, default 30; out of range or not an integer is a `StepFormatException` naming `milliseconds` |
| `DelayExecutor` | 0 ms → Done at once; otherwise `Cancellation.WaitHandle.WaitOne(ms)`: woke by cancellation → Skipped "cancelled", timed out → Done |

This is **not** the A8 settle delay. The executor inserts that one itself, only after it had to move focus and only before the first Keyboard, Mouse or Text step; a user never has to author it, and this step never substitutes for it.

**May reference:** `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the importer (C1: `Delay(milliseconds)` and classic `{DELAY n}` tokens), the App's `Components/Steps/Delay/` form.
