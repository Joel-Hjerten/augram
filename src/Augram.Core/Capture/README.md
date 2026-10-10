# Core/Capture

The suppress-then-replay loop (handoff §5, F1) as a pure object. `CaptureStateMachine` is fed `CaptureEvent`s and returns `CaptureOutcome`s; it never touches a hook, a timer, a clock or a window. That is what keeps the hook handler at "append and return" (CLAUDE.md invariant 1) and lets every behaviour be a scripted test in `tests/Augram.Core.Tests/Capture/`.

Since 2026-10-09 a press can hold keys and other buttons (F1 "Triggers as combinations"; `docs/learnings/0003-trigger-modifiers.md` §3–4, with Joel's decisions of that day): the machine records what a press held and owns presses of buttons other than the stroke button where a command holds them (anchors, decided per app).

## Types

| Type | Role |
|---|---|
| `CapturePoint` | `(X, Y, TimestampMs)`; timestamps are ms on one monotonic clock the Engine stamps |
| `MouseButton`, `WheelDirection` | Core's own enums; the Engine maps SharpHook's numbering at the boundary |
| `HeldButtons`, `HeldButtonsExtensions` | buttons as a set: `Stroke` (whichever button is the stroke button on the machine at hand) and the physical `Left`…`X2`; `Flag(button)`, `Has`, `Buttons()` (display order Left, Right, Middle, X1, X2), `ForStrokeButton(button)` (a set naming this machine's stroke button explicitly means the stroke button) |
| `AnchorPlan` | one `long` the hook reads per press: which buttons are held back as anchors over the window under the pointer (besides the stroke button, always one), and for each anchor (and the stroke button) which other buttons join its press. Worked out by `Mapping/AnchorPlanner` off the hook thread |
| `AnchorDragDistances` | one `long` beside the plan (plan 0004): per anchor the largest own drag distance of the commands holding it over that window (8 bits) and whether one of them uses the Options value (1 bit); `For(anchor, optionsPx)` is the distance a press of that anchor is handed back at; `None` is the Options value |
| `PressHold` | what a press held: its anchor (`Stroke` or a physical button), the stroke button then, Before (keys and buttons already held when the anchor went down) and After (pressed while held, and swallowed); `IsEmpty`, `TrackedKeys` (Ctrl, Alt, Shift, Win) |
| `CaptureEvent` | closed set: `ButtonDown(button, x, y, t, captureAllowed, ignoreKeyHeld, modifiers, plan, drags)`, `ButtonUp`, `Move`, `Wheel`, `Tick(t)`, `Key(modifier, consumed, t)`. The Engine sets `captureAllowed` false when Augram is disabled, paused by a focused "disable while focused" app, or the pointer is over an ignored app (F5): the press passes through like one with the ignore key held. `modifiers` are the keys held at the press; `plan` is the anchor plan the hook decided this press with, `drags` that window's drag distances (only the hand-back distance of an anchor's press reads them). `Key` is a Ctrl/Alt/Shift/Win press while a press is held, with the hook's decision (keys are paired by the Engine's key shadow). `ButtonReleasedElsewhere(button, x, y, t)`: another program posted the button's release (plan 0005 decision 10; Eyeris's loupe chord swallows the real one and posts its own), so the OS has it up and the real release may never come |
| `CaptureOutcome` | closed set: `Suppress`, `PassThrough` (the synchronous input decision), `BeginStroke`, `StrokeProgress`, `EndStroke` (trail), `ReplayClick` (with `AfterKeys` to press around it), `ClickTrigger` (a stroke-button click holding something: resolve it, relay the click when nothing fires), `StrokeComplete` (with `Hold`), `WheelTrigger` (with `Hold` and `AfterDrawing`), `HandBack` (inject an anchor's down at the start point, put the pointer back), `ReleaseHandedBack` (inject its up), `Cancelled(reason)` (worker) |
| `CaptureThresholds` | `StartDistancePx 30`, `MinSegmentPx 6`, `CancelDelayMs 1000`, `ResetCancelDelayOnMovement true`, `ButtonDragDistancePx 10` (a press owned by an anchor other than the stroke button never draws, so it is handed back at this distance instead of the start distance; Joel, 2026-10-10: Spine and Eyeris pan with Right); swappable at any time |
| `CaptureState` | `Idle`, `Held`, `Drawing`, `WheelFiring`, `Cancelled`, `HandedBack` |
| `CaptureStateMachine` | `Handle(event)`, `State`, `StrokeButton`, `ActiveButton`, `HandedBackButton`, `OwedButtons`, `Thresholds`, `Reset()`; split in `CaptureStateMachine.cs` (buttons) and `CaptureStateMachine.Motion.cs` (moves, wheel, ticks, keys) |

## Contract

Rows follow the classic-source table ([reference §2](../../../docs/reference/strokesplus-classic-source.md)) with Joel's decisions A12, A13, A19 applied, extended by learnings 0003 §4.2 for combinations. "Anchor" is the stroke button or a button the press's plan holds back; "owner" is the anchor that started the press.

| Event, state | Outcomes | Next state |
|---|---|---|
| ButtonDown (anchor), Idle, ignore key held or capture not allowed | PassThrough | Idle |
| ButtonDown (anchor), Idle | Suppress; owner, plan, Before (keys from the event, buttons this machine saw down), start point and cancel deadline (t + CancelDelayMs) remembered | Held |
| ButtonDown (not an anchor here), Idle | PassThrough (tracked as down, a later press's Before button) | Idle |
| ButtonDown (another button), Held or Drawing, the press's plan claims it for the owner | Suppress; an After button; the deadline is pushed | unchanged |
| ButtonDown (another button), Held, owner is not the stroke button | PassThrough, HandBack(owner, start, here) | HandedBack |
| ButtonDown (another button), Drawing | PassThrough, EndStroke, Cancelled(OtherButton) | Cancelled |
| ButtonDown (another button), Held or WheelFiring (stroke owner) | PassThrough, Cancelled(OtherButton) | Cancelled |
| ButtonDown (another button), Cancelled or HandedBack | PassThrough | unchanged |
| ButtonDown (the owner again: its release was missed) | as from Idle; first EndStroke (if Drawing) or ReleaseHandedBack (if HandedBack) | Held or Idle |
| Key (consumed), Held or Drawing, not already held | an After key; the deadline is pushed. Nothing otherwise; never a decision | unchanged |
| Move, Held, under the press's distance from start (StartDistancePx for a stroke owner; for another, `drags.For(owner, ButtonDragDistancePx)`) | nothing (point recorded if at least MinSegmentPx from the last recorded one; each recorded point pushes the deadline when ResetCancelDelayOnMovement) | Held |
| Move, Held, at least StartDistancePx from start, stroke owner | BeginStroke(start), StrokeProgress for each recorded point after start | Drawing |
| Move, Held, at least that distance from start, another owner | HandBack(owner, start, here): a drag or a selection starts at once | HandedBack |
| Move, Drawing | StrokeProgress(point) if recorded (same decimation), else nothing | Drawing |
| Tick, Held or Drawing, t at or past deadline, stroke owner | EndStroke (if Drawing), Cancelled(HoldStill) | Cancelled |
| Tick, Held, t at or past deadline, another owner | HandBack(owner, start, last position): a long press reaches the app | HandedBack |
| ButtonUp (owner), Held, nothing held besides it | Suppress, ReplayClick(button, release position) | Idle |
| ButtonUp (owner), Held, stroke owner holding keys or buttons | Suppress, ClickTrigger(button, release position, start, hold) | Idle |
| ButtonUp (owner), Held, another owner | Suppress, ReplayClick with its After keys; just Suppress when an After button took part | Idle |
| ButtonUp (owner), Drawing | Suppress, EndStroke, StrokeComplete(points start..release, start, button, hold) | Idle |
| ButtonUp (owner), WheelFiring or Cancelled | Suppress | Idle |
| ButtonUp (owner), HandedBack | Suppress, ReleaseHandedBack(button) | Idle |
| ButtonUp (any other button, any state) | Suppress if and only if its down was suppressed, else PassThrough | unchanged |
| ButtonReleasedElsewhere (not the owner of a press in progress) | nothing, no decision; the button is no longer down here, so no later press holds it Before | unchanged |
| ButtonReleasedElsewhere (the owner) | EndStroke (if Drawing), Cancelled(ReleasedElsewhere); no click, trigger, recognition, or ReleaseHandedBack (the OS got the other program's release); the release stays owed | Idle |
| Wheel, Held | Suppress, WheelTrigger(direction, start, hold); the sets freeze, the deadline is abandoned | WheelFiring |
| Wheel, Drawing | Suppress, EndStroke, WheelTrigger marked AfterDrawing (a drawn gesture + wheel: no wheel command fires) | WheelFiring |
| Wheel, WheelFiring | Suppress, WheelTrigger again (every tick fires) | WheelFiring |
| Wheel, Idle, Cancelled or HandedBack | PassThrough | unchanged |
| Move or Tick, WheelFiring, Cancelled or HandedBack | nothing | unchanged |
| Move, Idle | PassThrough (the Engine need not forward idle moves at all) | Idle |

Recognition is **not** done here: the Engine runs the matcher when it receives `StrokeComplete` (invariant 4). `StrokeComplete.Points` is handed over; the machine allocates a fresh list on the next press and never touches the old one again, so the worker may read it without copying.

## Invariants

- **Pairing (A19), per button.** Every button down that returned `Suppress` is followed by that button's up returning `Suppress`; a passed-through down gets a passed-through up; this holds for the owner, for After buttons released after their owner, across a wheel tick, a cancel, a hand-back and a stroke-button change. A hand-back's injected down always gets its injected release (`ReleaseHandedBack`, also before a restart). `tests/Augram.Core.Tests/Capture/PairingInvariantTests.cs` checks this over 1,000 random sequences without combinations and 1,000 with random anchor plans; `tests/Augram.Engine.Tests/Input/ChordPairingTests` drives the hook and the machine together over 3,000 with keys and checks what the OS ends up holding. Changing `StrokeButton` mid-capture cancels a stroke press but keeps consuming the *old* button until its release; `Reset()` is the hard reset for when the hook itself was reinstalled and the OS state is unknown anyway (the caller releases `HandedBackButton` first).
- **Decide at the down, never afterwards** (learnings 0003 §4.3): a button whose down passed is only ever a Before member; a held-back anchor that turns out unused is replayed or handed back, never "un-suppressed".
- **Ignore key is read per press**, never remembered (SP.net's "stuck in ignore mode" bug, reference §9). The Engine passes the current modifier state on each `ButtonDown`.
- **Button and wheel events always yield exactly one of `Suppress` / `PassThrough`;** moves yield `PassThrough` only while Idle; ticks, keys and a release elsewhere never yield a decision.
- **Another program's release (plan 0005 decision 10)** ends what the machine thinks is held, never what it owes: a real release that still comes after it keeps its press's decision, a handed-back press is not released again, and only a release the other program swallowed stays owed. Both pairing tests mix such releases in, half of them swallowing the real one, and `ChordPairingTests`' model of the OS takes the other program's release.
- **O(1) per event** apart from appending one point. Single-outcome results are cached static arrays; a press allocates one point list.

## Threading

Called from **one thread only**: the Engine's worker calls `Handle`; the hook thread applies its own shadow's decision synchronously and posts the event through its channel (`../../Augram.Engine/README.md`). The Engine serialises everything else that touches the machine (`Tick` from its timer, `Thresholds` and `StrokeButton` from live-save) onto that same thread; the machine has no locks.
