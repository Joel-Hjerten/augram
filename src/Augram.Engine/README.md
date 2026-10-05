# Augram.Engine

Wires Core to SharpHook: the hook adapter (`IInputSource`), input simulation (`IInputSimulator`), the bounded channel from the hook thread to the engine worker, hook health monitoring, the engine host that composes them, and the diagnostics implementation (`Diagnostics/`). M1 step 4: the engine captures, recognizes, logs and reports; it executes nothing yet (M2).

**May reference:** `Augram.Core` and the SharpHook package. That is the only project allowed to reference SharpHook.

**Must never contain:** Avalonia or any UI type, a dispatcher, window lookup or window operations (those are Platform.*), or business rules (those are Core). Hook handlers do near-zero work: decide, enqueue, return (CLAUDE.md invariants 1 and 2).

## Threads

| Thread | Name | Does | Must not |
|---|---|---|---|
| hook | `augram-hook-gN` (one per install, N = generation) | SharpHook `Run()`; per event: translate to `RawInput`, `InputGate.Handle`: read three volatiles, ask `SuppressionShadow`, `TryWrite` one message, measure itself, return the suppress decision. Logs at Trace only. | run the state machine, allocate beyond one `CaptureEvent`, log above Trace, touch the simulator, wait on anything |
| engine worker | `augram-engine-worker` | drain the channel; the **only** caller of `CaptureStateMachine.Handle`; publish state for the hook thread; trail calls; click replay (the only injector, A19); recognition; `RecognitionLog`; `EngineHost.EventRaised`; all Info/Debug/Warning logging for capture and recognition | touch a UI type; block on the App (`IStrokeTrail` and event handlers must return at once) |
| tick timer | `System.Threading.Timer` | every 25 ms **while a button is held** (armed by the worker on Held/Drawing, disarmed otherwise): enqueue `CaptureEvent.Tick(clock.MonotonicMs)` | anything else |
| health poll | `System.Threading.Timer`, 1 s | `HookHealthMonitor.Poll`: first-event report, events-per-minute ring, reinstall when the source reported Lost or the cursor watchdog fires, log every transition, raise `ResetRequested` | run inside a source callback (a loss reported from the hook thread is only flagged there and acted on at the next poll) |
| log drain | thread-pool task | hand events to sinks, flush on idle, report drops (`Diagnostics/`) | touch the hook, the worker, or a UI type |
| file flush timer | `System.Threading.Timer`, 500 ms | `RollingFileSink.Flush` | throw |
| UI / settings | the App's | `EngineHost.Enabled`, `IgnoreKey` (volatile writes read by the hook thread); `StrokeButton`, `SetThresholds` (messages on the same channel, applied by the worker in order with the input around them) | call `Handle` or anything on the machine |

No other thread hops. `docs/reference/threading.md` is this table; it lives here because the Engine is the only place with threads.

## Why the hook thread can decide without running the machine

The machine is owned by the worker and lags the hook by the queue depth, so the hook thread cannot run it and cannot trust its state for the press/release pairing. `Input/SuppressionShadow` derives the decision from the Capture README table with one local fact, *which button's consumed release is still owed*:

- stroke-button press: suppress iff capture is allowed (`Enabled`) and the ignore key is up; the machine says the same from every state because a press while not Idle restarts the capture;
- stroke-button release: suppress iff the press was suppressed (A19);
- "the stroke button" is the owed button while one is owed, else the configured one, which is the machine's `_activeButton`: a button change mid-capture keeps consuming the old button until its release;
- other buttons: never suppressed;
- wheel: suppress iff the machine's published state is Held, Drawing or WheelFiring. This one reads the worker's snapshot; a wheel tick arriving in the tick interval after a hold-still deadline can be suppressed although the machine then cancels first. The worker logs that at Debug (`Suppression decision mismatch`); anything else at that message is a Warning and a bug;
- moves and keys: never suppressed.

`tests/Augram.Engine.Tests/Input/SuppressionShadowTests` drives shadow and machine over 2,000 random sequences (button changes, ignore key, disabled presses, hold-still cancels) and asserts equality for every state and event kind. The worker cross-checks every live decision too.

## Input

| Type | Role |
|---|---|
| `SharpHookInputSource` | the `IInputSource`: `SimpleGlobalHook(All)` per `Start`, own thread; drops `IsEventSimulated` events (true for input injected by *any* process, so other utilities' synthetic input is ignored too, learnings 0001); maps buttons, wheel (SharpHook rotation > 0 is up; horizontal ignored), keys, modifier mask; `SuppressAllKeys` for the later hotkey-capture flow, default off. Thin and untested: needs a desktop |
| `SharpHookInputSimulator` | the `IInputSimulator`: click at point, key press/release, hotkey, Unicode text entry, per-key text via `AsciiKeyLayout` (US layout). Untested for the same reason |
| `MouseButtonMap`, `KeyCodeMap`, `AsciiKeyLayout` | the only places that know SharpHook's numbering; `KeyCodeMap` maps by name and a test proves every `Core.KeyCode` has a counterpart |
| `SuppressionShadow` | above |
| `HookHealthMonitor` | owns the source lifecycle: install, reinstall on `Lost` or on the watchdog (no event for 15 s while `ICursorProbe` saw the cursor move in 4 polls), exponential retry when a reinstall throws, system events logged and the silence clock restarted, `ResetRequested` after reinstall / resume / unlock, health contributor (`HookAliveSince`, `HookReinstallCount`, `EventsLastMinute`) |

## Hosting

| Type | Role |
|---|---|
| `InputGate` (internal) | everything that runs on the hook thread: the three volatiles the hook reads, the `SuppressionShadow`, the one `TryWrite`, the worst-handler stopwatch and the drop counters |
| `EngineHost` | composition point for the engine (the App's composition root creates one): ports in `EnginePorts`, tunables in `EngineHostOptions`, gestures and `RecognitionOptions` as delegates. `Start`/`Stop`/`Dispose`, `Enabled`, `StrokeButton`, `IgnoreKey`, `SetThresholds`, `State`, `Health`, `EventRaised` |
| `EngineWorker` (internal) | the worker loop above; queue depth warning at half capacity; dropped moves/ticks reported as a Warning, a dropped button/wheel event as an Error plus a capture reset |
| `StrokeRecognizer` (internal) | button-up only: `GestureMatcher.Rank`, top 3, threshold, one `RecognitionLogEntry` and one Info line with the same facts |
| `EngineEvent` | `GestureRecognized`, `NoMatch`, `WheelTriggered`; raised on the worker thread with the raw points so training can keep them |
| `WorkerMessage` (internal) | the channel item: input with the hook's decision, or a setting change, or a reset |
| `LogSources` | `hook`, `capture`, `recognition`, `engine` |

Queue: bounded, 4,096, `Wait` mode so `TryWrite` reports full instead of silently dropping. Moves and ticks are dropped when full; a press that cannot be enqueued is passed through and the shadow restored; a release is still consumed (A19) and the worker resets the machine when it sees the drop.

## Logging (N4)

| Source | Level | When | Properties |
|---|---|---|---|
| `hook` | Info | `Hook installing`, `Hook installed`, `Hook alive` (first event after install), `Hook reinstalled`, `System event`, `Hook stopped` | `generation`, `reason`, `firstEventMs`, `reinstalls`, `tookMs`, `kind` |
| `hook` | Warning / Error | `Hook lost`, `Hook lost, reinstalling`, `Hook reinstall failed` | `generation`, `detail`, `reason`, `retryInMs` |
| `hook` | Trace | `Input` per non-move event | `kind`, `button`, `key`, `x`, `y`, `suppress`, `state` |
| `capture` | Info | `Wheel trigger`, `Capture reset` | `direction`, `x`, `y`, `reason` |
| `capture` | Debug | `Stroke began`, `Click replayed`, `Gesture cancelled`, wheel-race `Suppression decision mismatch` | `button`, `x`, `y`, `result`, `lagMs`, `reason` |
| `capture` | Warning / Error | `Input queue deep`, `Input queue full: …`, button `Suppression decision mismatch` | `depth`, `capacity`, `dropped`, `event`, `hook`, `machine` |
| `recognition` | Info | `Gesture recognized` / `No match`, one per stroke | `gesture`, `score`, `points`, `durationMs`, `matchMs`, `worstHandlerUs`, `top` (`name=score;…`), `reason` |
| `engine` | Info | `Engine starting`, `Engine stopped`, `Engine enabled` / `Engine disabled`, `Stroke button changed`, `Capture thresholds changed` | `strokeButton`, `enabled`, `tickMs`, `button`, threshold fields |

## Diagnostics (N4, M1 step 4a)

The `IEventLog` implementation and its sinks. BCL only: `System.Threading.Channels` plus file I/O. Microsoft.Extensions.Logging and Serilog were deliberately not used; the hot path must be ours and dependency-free. If a library ever needs an `ILogger`, bridge MEL onto `IEventLog`, never the other way round.

| Type | Role |
|---|---|
| `ChannelEventLog` | the `IEventLog`: level check, `TryWrite` onto a bounded channel (4,096, `DropWrite`), return. One drain task delivers to every sink in order and flushes them when the queue empties. `MinimumLevel` is a runtime property (Diagnostics tab). `Dispose` completes the channel, drains what is queued (10 s cap), then disposes the sinks it owns. |
| `ILogSink` | `Write(event)`, `Flush()`, `Dispose()`; called from the drain task only |
| `RollingFileSink` | `augram-yyyyMMdd.log` in a directory (`logs/` beside the config), UTF-8 without BOM, one line per event, exception on indented lines, file chosen by each event's own date, files older than 7 days deleted at startup and at rotation, buffered with a 500 ms flush timer, opened with read-write sharing so a viewer can tail it |
| `InMemorySink` | `RingLog<LogEvent>` of the last 2,000 events for the Diagnostics tab's live tail; poll `Version` or subscribe to `Changed` |
| `LogLineFormatter` | the one line shape, also for "copy last N lines": `2026-10-05T21:14:03.123+02:00 INFO  hook      Hook installed {generation=2}` |

### Rule: no logging call may block the hook thread

`ChannelEventLog.Log` is a level comparison and one non-blocking `TryWrite`. It never waits for the channel, a sink, or a lock. Consequences, each covered by a test in `tests/Augram.Engine.Tests/Diagnostics/`:

- **Full queue drops the newest event** (`DropWrite`), because the events already queued are the ones that explain the stall. Drops are counted (`DroppedCount`) and the drain task writes one `WARN log Events dropped` per 1,000 drops and one when the burst ends.
- **A slow or throwing sink cannot reach the caller.** Sink exceptions are counted (`SinkFailureCount`) and the event is skipped for that sink only.
- **Level filtering happens before the queue**, so `Trace` hook traces cost one comparison while off.
- **Shutdown drains.** `Dispose` is the only call that waits, and it is made from the composition root at exit, never from the hook thread.

The hook thread logs at `Trace` only and guards value-typed properties with `IsEnabled` (they box at the call site); everything at Info and above is emitted by the worker.
