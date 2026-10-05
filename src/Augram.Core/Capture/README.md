# Core/Capture

`CaptureStateMachine`, `CaptureThresholds`, `CaptureEvent`, `CaptureOutcome`. A pure object: feed it events and a clock, it returns outcomes (suppress / replay click / stroke complete / wheel trigger / cancelled). It never touches a hook, a timer, or a window, which is what keeps the hook handler at "append and return" (CLAUDE.md invariant 1).

Lands in M1 step 3. Nothing here yet.
