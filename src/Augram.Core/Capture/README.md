# Core/Capture

The suppress-then-replay loop (handoff §5, F1) as a pure object. `CaptureStateMachine` is fed `CaptureEvent`s and returns `CaptureOutcome`s; it never touches a hook, a timer, a clock or a window. That is what keeps the hook handler at "append and return" (CLAUDE.md invariant 1) and lets every behaviour be a scripted test in `tests/Augram.Core.Tests/Capture/`.

## Types

| Type | Role |
|---|---|
| `CapturePoint` | `(X, Y, TimestampMs)`; timestamps are ms on one monotonic clock the Engine stamps |
| `MouseButton`, `WheelDirection` | Core's own enums; the Engine maps SharpHook's numbering at the boundary |
| `CaptureEvent` | closed set: `ButtonDown(button, x, y, t, captureAllowed, ignoreKeyHeld)`, `ButtonUp`, `Move`, `Wheel`, `Tick(t)`. The Engine sets `captureAllowed` false when Augram is disabled, paused by a focused "disable while focused" app, or the pointer is over an ignored app (F5): the press passes through like one with the ignore key held |
| `CaptureOutcome` | closed set: `Suppress`, `PassThrough` (the synchronous input decision), `BeginStroke`, `StrokeProgress`, `EndStroke` (trail), `ReplayClick`, `StrokeComplete`, `WheelTrigger`, `Cancelled(reason)` (worker) |
| `CaptureThresholds` | `StartDistancePx 30`, `MinSegmentPx 6`, `CancelDelayMs 1000`, `ResetCancelDelayOnMovement true`; swappable at any time |
| `CaptureState` | `Idle`, `Held`, `Drawing`, `WheelFiring`, `Cancelled` |
| `CaptureStateMachine` | `Handle(event)`, `State`, `StrokeButton`, `Thresholds`, `Reset()` |

## Contract

Rows follow the classic-source table ([reference §2](../../../docs/reference/strokesplus-classic-source.md)) with Joel's decisions A12, A13, A19 applied.

| Event, state | Outcomes | Next state |
|---|---|---|
| ButtonDown (stroke button), Idle, ignore key held or capture not allowed | PassThrough | Idle |
| ButtonDown (stroke button), Idle | Suppress; start point and cancel deadline (t + CancelDelayMs) remembered | Held |
| Move, Held, under StartDistancePx from start | nothing (point recorded if at least MinSegmentPx from the last recorded one; each recorded point pushes the deadline when ResetCancelDelayOnMovement) | Held |
| Move, Held, at least StartDistancePx from start | BeginStroke(start), StrokeProgress for each recorded point after start | Drawing |
| Move, Drawing | StrokeProgress(point) if recorded (same decimation), else nothing | Drawing |
| Tick, Held or Drawing, t at or past deadline | EndStroke (if Drawing), Cancelled(HoldStill) | Cancelled |
| ButtonUp (stroke button), Held | Suppress, ReplayClick(button, release position) | Idle |
| ButtonUp (stroke button), Drawing | Suppress, EndStroke, StrokeComplete(points start..release, start, button) | Idle |
| ButtonUp (stroke button), WheelFiring or Cancelled | Suppress | Idle |
| ButtonUp (stroke button), Idle | PassThrough | Idle |
| Wheel, Held | Suppress, WheelTrigger(direction, start); deadline abandoned | WheelFiring |
| Wheel, Drawing | Suppress, EndStroke, WheelTrigger(direction, start) | WheelFiring |
| Wheel, WheelFiring | Suppress, WheelTrigger again (every tick fires) | WheelFiring |
| Move or Tick, WheelFiring or Cancelled | nothing | unchanged |
| ButtonDown (other button), Held or WheelFiring | PassThrough, Cancelled(OtherButton) | Cancelled |
| ButtonDown (other button), Drawing | PassThrough, EndStroke, Cancelled(OtherButton) | Cancelled |
| ButtonDown (other button), Idle or Cancelled; ButtonUp (other button), any state | PassThrough | unchanged |
| Wheel, Idle or Cancelled | PassThrough | unchanged |
| Move, Idle | PassThrough (the Engine need not forward idle moves at all) | Idle |
| ButtonDown (stroke button), not Idle (release was missed: hook reinstall, sleep) | EndStroke (if Drawing), Suppress; capture restarts from this press | Held |

Recognition is **not** done here: the Engine runs the matcher when it receives `StrokeComplete` (invariant 4). `StrokeComplete.Points` is handed over; the machine allocates a fresh list on the next press and never touches the old one again, so the worker may read it without copying.

## Invariants

- **Pairing (A19).** Every stroke-button down that returned `Suppress` is followed by a stroke-button up that returns `Suppress`; a passed-through down gets a passed-through up. Other buttons are never consumed. `tests/Augram.Core.Tests/Capture/PairingInvariantTests.cs` checks this over 1,000 random sequences. Changing `StrokeButton` mid-capture cancels the capture but keeps consuming the *old* button until its release; `Reset()` is the hard reset for when the hook itself was reinstalled and the OS state is unknown anyway.
- **Ignore key is read per press**, never remembered (SP.net's "stuck in ignore mode" bug, reference §9). The Engine passes the current modifier state on each `ButtonDown`.
- **Button and wheel events always yield exactly one of `Suppress` / `PassThrough`;** moves yield `PassThrough` only while Idle; ticks never yield a decision.
- **O(1) per event** apart from appending one point. Single-outcome results are cached static arrays; a press allocates one point list.

## Threading

Called from **one thread only**: the Engine's hook handler calls `Handle`, applies `Suppress`/`PassThrough` to the hook event synchronously, and posts the remaining outcomes through its channel to the worker. The Engine serialises everything else that touches the machine (`Tick` from its timer, `Thresholds` and `StrokeButton` from live-save) onto that same thread or under its own lock; the machine has no locks. See `docs/reference/threading.md` once Engine lands.
