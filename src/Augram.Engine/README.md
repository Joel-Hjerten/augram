# Augram.Engine

Wires Core to SharpHook: the hook adapter (`IInputSource`), input simulation (`IInputSimulator`), the bounded channel from the hook thread to the engine worker, hook health monitoring, the engine host that composes them, the command executor (`Execution/`, M2 step 3) and the diagnostics implementation (`Diagnostics/`). The engine captures, recognizes, logs and reports; when the App wires a mapping it also resolves and runs commands.

**May reference:** `Augram.Core` and the SharpHook package. That is the only project allowed to reference SharpHook.

**Must never contain:** Avalonia or any UI type, a dispatcher, window lookup or window operations (those are Platform.*), or business rules (those are Core). Hook handlers do near-zero work: decide, enqueue, return (CLAUDE.md invariants 1 and 2).

## Threads

| Thread | Name | Does | Must not |
|---|---|---|---|
| hook | `augram-hook-gN` (one per install, N = generation) | SharpHook `Run()`; per event: translate to `RawInput`, `InputGate.Handle`: read four volatiles (state, stroke button, enabled, the ignore list's answer), ask `SuppressionShadow`, `TryWrite` one message, hand a move's or press's position to the ignore-list watch (one volatile write, one wake per batch while it watches), measure itself, return the suppress decision. A key event reads the capture flag, asks `KeySuppressionShadow`, and posts one message only while a hotkey capture is armed. Logs at Trace only. | run the state machine, allocate beyond one `CaptureEvent`, log above Trace, touch the simulator, wait on anything |
| engine worker | `augram-engine-worker` | drain the channel; the **only** caller of `CaptureStateMachine.Handle`; publish state for the hook thread; trail calls; click replay (the only click injector, A19); recognition; `EngineHost.EventRaised`; ask `EnginePorts.Intercept`; enqueue an `ExecutionRequest` (or, with no executor, complete the recognition log entry itself); hand captured key events and release notices to the hotkey capture's callback (restarting its watchdog); all Info/Debug/Warning logging for capture and recognition | touch a UI type; block on the App (`IStrokeTrail`, event handlers and the intercept must return at once); run a step |
| command executor | `augram-command-executor` (only when `EnginePorts.Mapping` is wired) | drain its own bounded queue (8, drop-oldest); per request: `IWindowSystem.WindowAt`, `CommandResolver.Resolve`, complete and add the recognition log entry, every `IStepType.Execute` in order with `IWindowSystem.Activate` and the settle delay (A8) before the first Keyboard or Text step (the only caller of `IWindowOperations`, `IProcessLauncher`, of `IDisplayModes.SetMode`/`SetHdr` through `EnginePorts.DisplayModes` and `StepExecutionContext.Displays`, and of the simulator's key and text members); all `exec` logging; the activation outcome for health | touch the state machine, the hook, the hook-to-worker channel, or a UI type; let a step's exception escape (one Error line, the next request runs) |
| ignore-list watch | `augram-ignore-watch` (only when `EnginePorts.Mapping` is wired) | keep the ignore list's answer current for the hook (below, "Ignore list"): on a pointer batch from the hook (at most one pass per 10 ms), a mapping change, resume / unlock / display change, and every 200 ms while an active ignored app can match; `IWindowSystem.WindowKeyAt` / `ForegroundKey` every pass they are due, `WindowAt` / `Foreground` only when a key changed; publish the answer to `InputGate`; log pointer enter/leave (Debug) and pause start/stop (Info); raise `EngineHost.PauseChanged` | touch the hook, the machine, the worker channel or a UI type; run a step |
| tick timer | `System.Threading.Timer` | every 25 ms **while a button is held** (armed by the worker on Held/Drawing, disarmed otherwise): enqueue `CaptureEvent.Tick(clock.MonotonicMs)` | anything else |
| health poll | `System.Threading.Timer`, 1 s | `HookHealthMonitor.Poll`: first-event report, events-per-minute ring, reinstall when the source reported Lost or the cursor watchdog fires, log every transition, raise `ResetRequested` | run inside a source callback (a loss reported from the hook thread is only flagged there and acted on at the next poll) |
| log drain | thread-pool task | hand events to sinks, flush on idle, report drops (`Diagnostics/`) | touch the hook, the worker, or a UI type |
| file flush timer | `System.Threading.Timer`, 500 ms | `RollingFileSink.Flush` | throw |
| key capture watchdog | `System.Threading.Timer`, one-shot per armed capture | after the caller's idle timeout with no key event: clear the capture flag, log, post the release notice | touch the hook or the machine |
| UI / settings | the App's | `EngineHost.Enabled`, `IgnoreKey` (volatile writes read by the hook thread); `MappingChanged` (wakes the ignore-list watch); `StrokeButton`, `SetThresholds` (messages on the same channel, applied by the worker in order with the input around them); `CaptureNextButtonPress` (arms a one-shot flag the hook thread turns into a `ButtonObserved` message); `CaptureKeys` (arms the hotkey-capture flag; disposing clears it at once) | call `Handle` or anything on the machine |

No other thread hops. `docs/reference/threading.md` is this table; it lives here because the Engine is the only place with threads.

## Why the hook thread can decide without running the machine

The machine is owned by the worker and lags the hook by the queue depth, so the hook thread cannot run it and cannot trust its state for the press/release pairing. `Input/SuppressionShadow` derives the decision from the Capture README table with one local fact, *which button's consumed release is still owed*:

- stroke-button press: suppress iff capture is allowed (`Enabled`, and the ignore list's last answer says neither "over an ignored app" nor "paused by a focused one") and the ignore key is up; the machine says the same from every state because a press while not Idle restarts the capture;
- stroke-button release: suppress iff the press was suppressed (A19);
- "the stroke button" is the owed button while one is owed, else the configured one, which is the machine's `_activeButton`: a button change mid-capture keeps consuming the old button until its release;
- other buttons: never suppressed;
- wheel: suppress iff the machine's published state is Held, Drawing or WheelFiring. This one reads the worker's snapshot; a wheel tick arriving in the tick interval after a hold-still deadline can be suppressed although the machine then cancels first. The worker logs that at Debug (`Suppression decision mismatch`); anything else at that message is a Warning and a bug;
- moves: never suppressed;
- keys: suppressed only while a hotkey capture is armed, and then per press (see Key capture below; `Input/KeySuppressionShadow`).

`tests/Augram.Engine.Tests/Input/SuppressionShadowTests` drives shadow and machine over 2,000 random sequences (button changes, ignore key, disabled presses, hold-still cancels) and asserts equality for every state and event kind. The worker cross-checks every live decision too.

## Input

| Type | Role |
|---|---|
| `SharpHookInputSource` | the `IInputSource`: `SimpleGlobalHook(All)` per `Start`, own thread; drops `IsEventSimulated` events (true for input injected by *any* process, so other utilities' synthetic input is ignored too, learnings 0001); maps buttons, wheel (SharpHook rotation > 0 is up; horizontal ignored), keys, modifier mask; key events go through the handler like the rest, so the gate alone decides key suppression. Thin and untested: needs a desktop |
| `SharpHookInputSimulator` | the `IInputSimulator`: click at point, key press/release, hotkey (left modifier keys, the right-hand key for each modifier in the right-hand set: "RAlt+F9" presses RightAlt), Unicode text entry, per-key text via Core's `Abstractions/AsciiKeyLayout` (US layout). Untested for the same reason, except `ModifierKeys`, the left/right choice |
| `MouseButtonMap`, `KeyCodeMap` | the only places that know SharpHook's numbering; `KeyCodeMap` maps by name and a test proves every `Core.KeyCode` has a counterpart |
| `SuppressionShadow` | above |
| `KeySuppressionShadow` | per key: up, passed (the OS saw the press) or owed (the press was suppressed); a press is suppressed iff a hotkey capture is armed when it starts, its repeats and release follow it; a record older than 2 s means the release was missed and the next press starts fresh |
| `HookHealthMonitor` | owns the source lifecycle: install, reinstall on `Lost` or on the watchdog (no event for 15 s while `ICursorProbe` saw the cursor move in 4 polls), exponential retry when a reinstall throws, system events logged and the silence clock restarted, `ResetRequested` after reinstall / resume / unlock, health contributor (`HookAliveSince`, `HookReinstallCount`, `EventsLastMinute`) |

## Hosting

| Type | Role |
|---|---|
| `InputGate` (internal) | everything that runs on the hook thread: the four volatiles the hook reads, the `SuppressionShadow`, the one `TryWrite`, the hand-over of positions to the ignore-list watch, the worst-handler stopwatch and the drop counters |
| `IgnoreListWatch` (internal), `IgnoreLookup` (internal) | the ignore list's thread and one pass of it (below, "Ignore list"); `EngineHost.PausedBy`, `IgnoredUnderPointer`, `PauseChanged`, `MappingChanged` are its public face (`EngineHost.IgnoreList.cs`) |
| `EngineHost` | composition point for the engine (the App's composition root creates one): ports in `EnginePorts`, tunables in `EngineHostOptions`, gestures and `RecognitionOptions` as delegates. `Start`/`Stop`/`Dispose`, `Enabled`, `StrokeButton`, `IgnoreKey`, `SetThresholds`, `State`, `Health`, `IsRunning`, `EventRaised`, `CaptureNextButtonPress` (F1 detect-to-assign: the next physical press is reported once, on the worker, without changing how it is handled), `CaptureKeys` and `IsCapturingKeys` (F5 hotkey capture, below) |
| `KeyCaptureController` (internal), `KeyCaptureEvent`, `KeyCaptureEventKind` | hotkey capture (F5), below |
| `EngineWorker` (internal) | the worker loop above; queue depth warning at half capacity; dropped moves/ticks reported as a Warning, a dropped button/wheel event as an Error plus a capture reset |
| `StrokeRecognizer` (internal) | button-up only: `GestureMatcher.Rank`, top 3, threshold, one Info line; a no-match entry goes into the `RecognitionLog` at once, a recognised gesture's entry comes back as a draft in `RecognitionResult` for the executor (or the worker) to complete with group, command or reason |
| `EngineEvent` | `GestureRecognized`, `NoMatch`, `WheelTriggered`; raised on the worker thread with the raw points so training can keep them |
| `WorkerMessage` (internal) | the channel item: input with the hook's decision, or a setting change, or a reset |
| `LogSources` | `hook`, `capture`, `recognition`, `engine`, `exec` |

## Key capture (F5 hotkey field)

`EngineHost.CaptureKeys(callback, idleTimeout)` is what the App's hotkey field arms while it records a combination. Joel's requirement: every key goes to the field, so Win+L, Alt+Tab, Esc and PrintScreen are recorded instead of acted on; commit is mouse-only; the keyboard can never stay dead.

- **Hook thread:** one volatile flag in `InputGate`. While it is set, a key press is suppressed system-wide and its event (key, reported modifier mask, down or up) is posted to the worker; the mouse path does not read the flag at all.
- **Per-press pairing (A19 for keys):** `KeySuppressionShadow` decides per press, not per event. A key pressed during the capture stays swallowed, repeats and release included, even after the capture ends (the user still holding Ctrl when clicking Accept): Windows sees whole presses or nothing, never a press without its release, which is a stuck key. A key held from before the capture reaches the OS to its release, because the OS already saw it go down.
- **Worker:** delivers each event to the callback and restarts the watchdog. The caller marshals to its own thread.
- **Release (the safety invariant):** disposing the handle (not reported back), the watchdog after `idleTimeout` with no key event (`DefaultKeyCaptureIdleTimeout` 10 s), another `CaptureKeys` call (one capture at a time), a hook reset (reinstall after loss, resume, unlock; the key record is forgotten too) and `Stop`. Each clears the flag first, then logs `Key capture released` with the reason, then posts a `Released` notice to the worker so it reaches the callback after the key events queued before it. A lost hook suppresses nothing, so a loss needs no separate path: the reinstall releases.
- **Independent of `Enabled`:** the tray toggle is about gestures; recording a hotkey works with gestures off.

`tests/Augram.Engine.Tests/Hosting/KeyCaptureTests` and `Input/KeySuppressionShadowTests` cover each rule through `FakeInputSource`; no test installs a real hook.

## Execution (`Execution/`, M2 step 3)

| Type | Role |
|---|---|
| `CommandExecutor` (internal) | the executor thread and its queue (`Channel<ExecutionRequest>`, capacity 8, drop-oldest with one `Execution queue full` Warning naming the dropped trigger); `Enqueue` from the worker never blocks; `Stop` cancels the running command, completes the queue (what is still queued is drained, not run) and joins the thread (5 s cap). Built by `EngineHost` only when `EnginePorts.Mapping` is present; the host stops it after the worker |
| `CommandRunner` (internal) | one resolved command: activation, the one `StepExecutionContext`, the steps, the per-step and per-command lines, the last activation outcome for `HealthSnapshot.LastActivationOutcome` |
| `ExecutionRequest` (internal) | trigger, stroke start, the recognition log draft (null for a wheel tick), enqueue timestamp |

Per request, in this order:

1. `target = Windows.WindowAt(start)`: the window under the gesture start, not the focused one (F5).
2. `CommandResolver.Resolve(Mapping(), target, trigger)`; the draft entry is completed (`MatchedGroup`, `FiredCommand` or `NothingFiredReason` = the resolver's reason) and added; one `Trigger resolved` Info line.
3. Not `Fires` (override to nothing, ignored app, globals suppressed, no command): done. A command with no active steps: `Command has no active steps`, done.
4. Steps in order, inactive ones skipped, each resolved for the adapter's platform (`CommandStep.ResolveFor`, F8).
5. **Lazy activation:** right before the first step whose category is Keyboard or Text, `Windows.Activate(target)` when there is a target; the adapter applies A20 (`not-needed` when the root already has focus or is the desktop). `focusMoved` = succeeded and not `not-needed`. A failure is a Warning and the command still runs on whatever is in front. Only injected keys need focus: a window operation acts on the handle and a media key is global, so a command without Keyboard or Text steps never activates (2026-10-07: an Alt-tap activation put 310 ms in front of a minimize over Chrome). **Settle delay (A8):** waited once, right after that activation, only when `focusMoved`; `EngineHostOptions.SettleDelayMs`, default 30, 0 allowed. `Failed` stops the command (`Command stopped`), `Skipped` continues it, cancellation (the executor stopping) stops it after the step that saw it.
6. `Command fired` with steps run and skipped and the elapsed time from enqueue. Log lines name the gesture (`gesture '/Down'`) rather than the trigger kind.

Before any of this the worker raises `EventRaised`, then asks `EnginePorts.Intercept`; true means the training popup took the stroke (`Stroke consumed`, entry reason `consumed by training`) and nothing is enqueued. Without a Mapping port the worker completes the entry with `no mapping configured` (M1 behaviour).

The resolver's `Ignored` outcome stays as a backstop: a stroke captured over an ignored app because the watch's answer was stale is recognised, resolved to `Ignored` and dropped, so it never fires a command. Normally the button never gets that far (next section).

Queue: bounded, 4,096, `Wait` mode so `TryWrite` reports full instead of silently dropping. Moves and ticks are dropped when full; a press that cannot be enqueued is passed through and the shadow restored; a release is still consumed (A19) and the worker resets the machine when it sees the drop.

## Ignore list (F5; SP.net's Ignore List)

Two modes per entry (`Core/Mapping/IgnoreList`): over a window of any active ignored app the stroke button passes through untouched (no capture, no trail, no suppression; the app gets its down, drag and up, and the wheel while the button is held passes too, since the machine stays Idle); a "disable while focused" app (`IgnoredApp.DisableEntirely`, Joel's VMware) also pauses Augram entirely while it has focus, exactly as if disabled.

**The hook never looks a window up.** The `IgnoreListWatch` thread keeps the answer current and publishes it to `InputGate` as two bits (`OverIgnoredApp`, `PausedByFocus`); at a stroke-button press the hook reads that one volatile and `CaptureAllowed` becomes `Enabled && answer == 0`, the same flag the tray toggle sets, so the press goes down the existing pass-through path of the shadow and the machine. Lookups are deduplicated by window (`IgnoreLookup`): `IWindowSystem.WindowKeyAt` (Windows: `WindowFromPoint`; macOS: a hit test on a window-list copy at most 250 ms old) and `ForegroundKey` (Windows: `GetForegroundWindow`; macOS: the frontmost app's pid) are cheap; `WindowAt` and `Foreground` run only when a key changed, and `Foreground` only while a "disable while focused" app is active. Nothing is watched, and the hook does not wake the watch, while no active ignored app can match on this platform. A mapping change (the App calls `MappingChanged` from `EngineSettingsLink`) re-judges the last pointer position and the focus without waiting for a move; resume, unlock and display changes drop every key.

**Staleness window.** The answer the hook reads is the watch's last pass:
- the pointer is judged within one pass interval after it moves (10 ms; about 16 ms with the Windows default timer granularity), plus the lookup (microseconds on Windows, a window-server read on macOS when the window changed); a press that quick after entering an ignored app's window can still be captured;
- focus is asked every 200 ms (and on every mapping change), so a pause starts or ends up to 200 ms after focus moves; a focus change also re-checks a motionless pointer, which covers a window brought to the front under it (alt-tab, an app opening);
- a window that appears under a motionless pointer without taking focus, or a title that changes under it, is seen on the next move of the pointer (one pixel is enough); on macOS a window that opened or moved within the last 250 ms may be missed until the list copy is refreshed.
This is acceptable because the cost of a stale answer is one press handled the old way: a stale "not ignored" captures that one stroke (the app misses that right-drag, as before this feature) and the resolver's backstop still fires nothing over the ignored app; a stale "ignored" passes one press through to the window now under the pointer, as the ignore key would. Moving the pointer onto a window and pressing within 10 ms is rare; the pass interval bounds the watch's cost while the pointer moves.

**A19 holds** because the answer is read at the press only: the shadow's release decision depends on nothing but whether that press was consumed, so a press passed through over an ignored app gets a passed-through release wherever the pointer is by then, and a consumed press gets a consumed release even if it ends over an ignored app or a pause started meanwhile (`Input/IgnorePairingTests`, `Hosting/IgnoreListWatchTests`).

**Pause.** `EngineHost.PausedBy` is the focused "disable while focused" app; `PauseChanged` is raised on the watch thread when it starts, stops or its app is renamed (the App's `EnginePauseState` shows it in the tray tooltip), and once with null at `Stop`. Hotkey capture and detect-to-assign are not affected, as with `Enabled`.

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
| `engine` | Info | `Engine starting`, `Engine stopped`, `Engine enabled` / `Engine disabled`, `Stroke button changed`, `Capture thresholds changed`, `Key capture armed`, `Key capture released` | `strokeButton`, `enabled`, `tickMs`, `button`, threshold fields, `idleTimeoutMs`, `reason` |
| `engine` | Error | `Key capture callback threw` (the capture holds) | `kind`, `key` |
| `capture` | Debug | `Stroke consumed` (the intercept took it) | `trigger` |
| `exec` | Info | `Trigger resolved`, `Window activated`, `Command fired`, `Command has no active steps`, `Command cancelled` | `trigger`, `outcome`, `reason`, `group`, `command`, `process`, `technique`, `elapsedMs`, `focusMoved`, `stepsRun`, `stepsSkipped`, `step` |
| `exec` | Debug | `Step ran`, one per step; `Execution request dropped: executor stopped` | `index`, `type`, `summary`, `outcome`, `reason`, `ms`, `trigger` |
| `ignore` | Info | `Ignore list watched` / `Ignore list not watched` (an active entry can match here, or none can), `Paused while an ignored app has focus`, `Resumed: the ignored app lost focus` | `focus`, `app` |
| `ignore` | Debug | `Pointer over an ignored app; the stroke button passes through`, `Pointer left an ignored app` | `app` |
| `ignore` | Error | `Ignore list lookup failed; keeping the last answer` (once until a pass succeeds; the exception is attached) | |
| `exec` | Warning / Error | `Activation failed`, `Command stopped` (a Failed step), `Execution queue full`, `Command execution failed` (a step type threw; the exception is attached) | `technique`, `group`, `command`, `step`, `type`, `summary`, `reason`, `dropped`, `capacity`, `trigger` |

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
