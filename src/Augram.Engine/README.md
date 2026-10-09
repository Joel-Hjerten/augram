# Augram.Engine

Wires Core to SharpHook: the hook adapter (`IInputSource`), input simulation (`IInputSimulator`), the bounded channel from the hook thread to the engine worker, hook health monitoring, the engine host that composes them, the command executor (`Execution/`, M2 step 3) and the diagnostics implementation (`Diagnostics/`). The engine captures, recognizes, logs and reports; when the App wires a mapping it also resolves and runs commands.

**May reference:** `Augram.Core` and the SharpHook package. That is the only project allowed to reference SharpHook.

**Must never contain:** Avalonia or any UI type, a dispatcher, window lookup or window operations (those are Platform.*), or business rules (those are Core). Hook handlers do near-zero work: decide, enqueue, return (CLAUDE.md invariants 1 and 2).

## Threads

| Thread | Name | Does | Must not |
|---|---|---|---|
| hook | `augram-hook-gN` (one per install, N = generation) | SharpHook `Run()`; per event: translate to `RawInput`, `InputGate.Handle`: read four volatiles (state, stroke button, enabled, the pointer's answer: the ignore list's bits and the window's `AnchorPlan`), ask `SuppressionShadow`, `TryWrite` one message, hand a move's or press's position to the ignore-list watch (one volatile write, one wake per batch while it watches), measure itself, return the suppress decision. A key event reads the capture flag, asks `KeySuppressionShadow` (a held press may claim a Ctrl/Alt/Shift/Win press, below), and posts one message while a hotkey capture is armed and one for a modifier press while a press is held. Logs at Trace only. | run the state machine, allocate beyond one `CaptureEvent`, log above Trace, touch the simulator, wait on anything |
| engine worker | `augram-engine-worker` | drain the channel; the **only** caller of `CaptureStateMachine.Handle`; publish state for the hook thread; trail calls; click replay, hand-back and its release (the only injector of a press's own mouse input, A19), the Ctrl tap that masks a Win or Alt held before a press; recognition; `EngineHost.EventRaised`; ask `EnginePorts.Intercept`; enqueue an `ExecutionRequest` (or, with no executor, complete the recognition log entry itself and relay a click trigger's click); hand captured key events and release notices to the hotkey capture's callback (restarting its watchdog); wake the watch when the stroke button changes; all Info/Debug/Warning logging for capture and recognition | touch a UI type; block on the App (`IStrokeTrail`, event handlers and the intercept must return at once); run a step |
| command executor | `augram-command-executor` (only when `EnginePorts.Mapping` is wired) | drain its own bounded queue (8, drop-oldest); per request: `IWindowSystem.WindowAt`, `CommandResolver.Resolve`, complete and add the recognition log entry, every `IStepType.Execute` in order with `IWindowSystem.Activate` and the settle delay (A8) before the first Keyboard, Mouse or Text step (the only caller of `IWindowOperations`, `IProcessLauncher`, `IClipboard` through `EnginePorts.Clipboard` and `StepExecutionContext.Clipboard`, of `IDisplayModes.SetMode`/`SetHdr` through `EnginePorts.DisplayModes` and `StepExecutionContext.Displays`, and of the simulator's key, wheel and text members); relay a click trigger's click that fired nothing (`ClickRelay`: keys down, click, keys up); all `exec` logging; the activation outcome for health | touch the state machine, the hook, the hook-to-worker channel, or a UI type; let a step's exception escape (one Error line, the next request runs) |
| ignore-list watch | `augram-ignore-watch` (only when `EnginePorts.Mapping` is wired) | keep the pointer's answer current for the hook (below, "Ignore list" and "Anchors per app"): on a pointer batch from the hook (at most one pass per 10 ms), a mapping or stroke-button change, resume / unlock / display change, and every 200 ms while an active ignored app can match or a command holds a button besides the stroke button; `IWindowSystem.WindowKeyAt` / `ForegroundKey` every pass they are due, `WindowAt` / `Foreground` only when a key changed; the anchor plan per app group, worked out again only when the window, the mapping or the stroke button changed; publish the answer to `InputGate`; log pointer enter/leave (Debug) and pause start/stop (Info); raise `EngineHost.PauseChanged` | touch the hook, the machine, the worker channel or a UI type; run a step |
| tick timer | `System.Threading.Timer` | every 25 ms **while a button is held** (armed by the worker on Held/Drawing, disarmed otherwise): enqueue `CaptureEvent.Tick(clock.MonotonicMs)` | anything else |
| health poll | `System.Threading.Timer`, 1 s | `HookHealthMonitor.Poll`: first-event report, events-per-minute ring, reinstall when the source reported Lost or the cursor watchdog fires, log every transition, raise `ResetRequested` | run inside a source callback (a loss reported from the hook thread is only flagged there and acted on at the next poll) |
| log drain | thread-pool task | hand events to sinks, flush on idle, report drops (`Diagnostics/`) | touch the hook, the worker, or a UI type |
| file flush timer | `System.Threading.Timer`, 500 ms | `RollingFileSink.Flush` | throw |
| key capture watchdog | `System.Threading.Timer`, one-shot per armed capture | after the caller's idle timeout with no key event: clear the capture flag, log, post the release notice | touch the hook or the machine |
| UI / settings | the App's | `EngineHost.Enabled`, `IgnoreKey` (volatile writes read by the hook thread); `MappingChanged` (wakes the ignore-list watch); `StrokeButton`, `SetThresholds` (messages on the same channel, applied by the worker in order with the input around them); `CaptureNextButtonPress` (arms a one-shot flag the hook thread turns into a `ButtonObserved` message); `CaptureKeys` (arms the hotkey-capture flag; disposing clears it at once) | call `Handle` or anything on the machine |

No other thread hops. `docs/reference/threading.md` is this table; it lives here because the Engine is the only place with threads.

## Why the hook thread can decide without running the machine

The machine is owned by the worker and lags the hook by the queue depth, so the hook thread cannot run it and cannot trust its state for the press/release pairing. `Input/SuppressionShadow` derives the decision from the Capture README table with its own record of *which anchor owns the press in progress and which buttons' consumed releases are still owed*:

- a press with no press in progress (or of the owner again, a missed release): suppress iff the button is an anchor (the stroke button, or one the window's plan holds back), capture is allowed (`Enabled`, and the watch's last answer says neither "over an ignored app" nor "paused by a focused one") and the ignore key is up; it then owns the press, with the plan and the keys of that moment;
- another button while a press is owned and not frozen (no wheel tick suppressed in it, and the published state is not WheelFiring, Cancelled or HandedBack): suppress iff the press's plan claims it for the owner (an After button);
- any release: suppress iff its press was suppressed (A19), whatever happened in between: a release of an After button after its owner's, a hand-back (the physical release is consumed and the worker injects the app's), a stroke-button change;
- wheel: suppress iff a press is owned and the published state is not Cancelled or HandedBack. A wheel tick, or a button joining a press, arriving in the tick interval after a hold-still deadline can be suppressed although the machine then cancels (or hands back) first: one tick or one click eaten, never a stuck button, since the release follows the press. The worker logs that at Debug (`Suppression decision mismatch`); a mismatch on the owner's own press or release is a Warning and a bug;
- moves: never suppressed;
- keys: a press is suppressed while a hotkey capture is armed (Key capture below), or when it is a Ctrl/Alt/Shift/Win press the held press claims (`SuppressionShadow.KeyClaim`: owned, not frozen, and the key was not held when the anchor went down); its repeats and release follow it (`Input/KeySuppressionShadow`). The worker hears of each modifier press during a held press with that decision (`CaptureEvent.Key`), so the machine's After keys are exactly the keys the app did not see.

`tests/Augram.Engine.Tests/Input/SuppressionShadowTests` drives shadow and machine over 2,000 random sequences (button changes, ignore key, disabled presses, hold-still cancels) and asserts equality for every state and event kind; `Input/ChordPairingTests` drives the real gate and a machine over 3,000 sequences with random anchor plans per window, keys and stroke-button changes, asserts every decision equals the machine's and every release its press's, and models the OS (passed events plus the worker's injections) to prove it never gets an up for a button or key it does not hold and holds nothing at the end. The worker cross-checks every live decision too.

## Input

| Type | Role |
|---|---|
| `SharpHookInputSource` | the `IInputSource`: `SimpleGlobalHook(All)` per `Start`, own thread; drops `IsEventSimulated` events (true for input injected by *any* process, so other utilities' synthetic input is ignored too, learnings 0001); maps buttons, wheel (SharpHook rotation > 0 is up; horizontal ignored), keys, modifier mask; key events go through the handler like the rest, so the gate alone decides key suppression. Thin and untested: needs a desktop |
| `SharpHookInputSimulator` | the `IInputSimulator`: click at point, press at point and release (a hand-back), move the pointer, scroll at a point (move there, then one `SimulateMouseWheel` per notch: `Notch` is 120 per notch on Windows, one line, `BlockScroll`, on macOS; positive is up or left; horizontal for left and right), key press/release, hotkey (left modifier keys, the right-hand key for each modifier in the right-hand set: "RAlt+F9" presses RightAlt), Unicode text entry, per-key text via Core's `Abstractions/AsciiKeyLayout` (US layout). Untested against a desktop for the same reason; `ModifierKeys` (the left/right choice) and `Notch` are tested, and `Scroll` over SharpHook's `TestGlobalHook`, which records and posts nothing |
| `MouseButtonMap`, `KeyCodeMap` | the only places that know SharpHook's numbering; `KeyCodeMap` maps by name and a test proves every `Core.KeyCode` has a counterpart |
| `SuppressionShadow` | above |
| `KeySuppressionShadow` | per key: up, passed (the OS saw the press) or owed (the press was suppressed); a press is suppressed iff a hotkey capture is armed when it starts or a held mouse press claims it (a Ctrl/Alt/Shift/Win press, `SuppressionShadow.KeyClaim`), its repeats and release follow it; a record older than 2 s means the release was missed and the next press starts fresh |
| `HookHealthMonitor` | owns the source lifecycle: install, reinstall on `Lost` or on the watchdog (no event for 15 s while `ICursorProbe` saw the cursor move in 4 polls), exponential retry when a reinstall throws, system events logged and the silence clock restarted, `ResetRequested` after reinstall / resume / unlock, health contributor (`HookAliveSince`, `HookReinstallCount`, `EventsLastMinute`) |

## Hosting

| Type | Role |
|---|---|
| `InputGate` (internal) | everything that runs on the hook thread: the four volatiles the hook reads, the `SuppressionShadow`, the one `TryWrite`, the hand-over of positions to the ignore-list watch, the worst-handler stopwatch and the drop counters |
| `IgnoreListWatch` (internal), `IgnoreLookup` (internal) | the pointer watch's thread and one pass of it: the ignore list's answer and the anchor plan for the window under the pointer (below, "Ignore list" and "Anchors per app"); `EngineHost.PausedBy`, `IgnoredUnderPointer`, `PauseChanged`, `MappingChanged` are its public face (`EngineHost.IgnoreList.cs`) |
| `EngineWorker.Outcomes.cs` | what each machine outcome makes the worker do (trail, replay, hand-back, recognition, firing, relaying a click with no executor) |
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
| `ExecutionRequest` (internal) | what fired (`PressedTrigger`: the kind and what the press held), stroke start, the recognition log draft (null for a wheel tick or a click), the click to relay when a click trigger fires nothing (`Relay`), enqueue timestamp |
| `ClickRelay` (internal) | a click trigger's click handed to the app after all (Joel, 2026-10-09: an unbound click with keys passes through with them held): the After keys down, the click, a Ctrl tap when Alt or Win is among them, the keys up. None when a button joined the press (that is a chord attempt, like a cancel) |

Per request, in this order:

1. `target = Windows.WindowAt(start)`: the window under the gesture start, not the focused one (F5).
2. `CommandResolver.Resolve(Mapping(), target, trigger)`: a command fires when its trigger on this platform matches the press exactly (learnings 0003 §3.4); the draft entry is completed (`MatchedGroup`, `FiredCommand` or `NothingFiredReason` = the resolver's reason) and added; one `Trigger resolved` Info line.
3. Not `Fires` (override to nothing, ignored app, globals suppressed, no command): a click trigger's click is relayed (`Click relayed`, Debug); done. A command with no active steps: `Command has no active steps`, done.
4. Steps in order, inactive ones skipped, each resolved for the adapter's platform (`CommandStep.ResolveFor`, F8).
5. **Lazy activation:** right before the first step whose category is Keyboard, Mouse or Text, `Windows.Activate(target)` when there is a target; the adapter applies A20 (`not-needed` when the root already has focus or is the desktop). `focusMoved` = succeeded and not `not-needed`. A failure is a Warning and the command still runs on whatever is in front. Only injected keys and wheel need focus (a Scroll's held keys go to the foreground window, and its wheel must reach the same one): a window operation acts on the handle, a media key is global and the clipboard is the system's, so a command without Keyboard, Mouse or Text steps never activates (2026-10-07: an Alt-tap activation put 310 ms in front of a minimize over Chrome). **Settle delay (A8):** waited once, right after that activation, only when `focusMoved`; `EngineHostOptions.SettleDelayMs`, default 30, 0 allowed. `Failed` stops the command (`Command stopped`), `Skipped` continues it, cancellation (the executor stopping) stops it after the step that saw it.
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

## Anchors per app and trigger combinations (F1, Joel 2026-10-09)

A trigger may hold keys and buttons besides the stroke button (`Core/Mapping/TriggerHold`), and a wheel trigger may hold another button instead of it ("Right + wheel up"). Such a button is an **anchor**: its press is held back like the stroke button's. **Only where a command uses it:** the watch works out, for the window under the pointer, an `AnchorPlan` (`Core/Mapping/AnchorPlanner`: that window's app group's active commands used on this platform, plus Global's unless the group suppresses globals; an app command shadows a Global one with an overlapping trigger, and an override to nothing holds nothing back) and publishes it with the ignore bits as one volatile (`InputGate.PublishPointer`). At a press the hook reads that answer once; it never looks a window up. The plan also lists, per anchor, the buttons that join its press (stroke + Left: Left's click is swallowed while the stroke button is held). The press carries the plan it was decided with (`CaptureEvent.ButtonDown.Plan`), so the machine decides from the same facts. Nothing is watched for anchors while no active command holds a button besides the stroke button.

**What happens to a held-back anchor** (learnings 0003 §4.2): a click is replayed at release; a move past the start distance, the hold-still time, or another button going down hands it back at once (`HandBack`: the worker injects its down at the start point and puts the pointer back, so a drag or a text selection starts where it began); a wheel tick fires the wheel trigger and the button's click never reaches the app. A handed-back press's physical release is consumed and the worker injects the app's release (`ReleaseHandedBack`), so the shadow's rule stays "a release follows its press" and the app's down and up stay a pair; the worker releases a handed-back button before a hard reset too. (Deviation from learnings 0003 §4.3 point 4, which let the physical release pass.)

**Staleness**, the same as the ignore list's (above): the plan is the watch's last pass, so a press within about one pass interval (10 ms, plus the lookup) of the pointer entering a window is decided with the previous window's plan. A stale "anchor here" holds one press back that the app then gets at release or on the first move; a stale "not an anchor" lets one press through untouched, so that one chord does nothing. Focus does not matter: the plan follows the window under the pointer, where the command would resolve.

**Keys.** Ctrl, Alt, Shift and Win held when the anchor goes down are the press's Before keys (the app saw them): the event's modifier mask, limited to keys the hook saw go down since its last reset (`KeySuppressionShadow.HeldModifiers`), so a mask the input library left stale (Win's release missed while Win+L locked the desktop) cannot make every press hold a phantom Win and stop every plain gesture matching; unlock and resume reset that record. A key already held when Augram started is therefore not a Before key. Pressed while it is held and before the first wheel tick, they are claimed and swallowed with their repeats and release (After keys). A click whose stroke button held something is a click trigger: resolved like any trigger, and relayed to the app when nothing fires there, with the After keys pressed around it (`ClickRelay`), so Shift + right-click opens Explorer's extended menu without SP.net's workaround command. **Win or Alt held before a press** would reach Windows as a lone key (the press between was swallowed) and open Start or the menu bar: the worker taps Ctrl once when such a press starts (Windows only), as SP.net does for its hotkeys.

**Wheel after drawing** (SP.net): once the stroke passed the start distance, a tick is "this gesture + wheel", which no command is; the worker logs `Wheel after drawing; no command` and fires nothing.

## Logging (N4)

| Source | Level | When | Properties |
|---|---|---|---|
| `hook` | Info | `Hook installing`, `Hook installed`, `Hook alive` (first event after install), `Hook reinstalled`, `System event`, `Hook stopped` | `generation`, `reason`, `firstEventMs`, `reinstalls`, `tookMs`, `kind` |
| `hook` | Warning / Error | `Hook lost`, `Hook lost, reinstalling`, `Hook reinstall failed` | `generation`, `detail`, `reason`, `retryInMs` |
| `hook` | Trace | `Input` per non-move event | `kind`, `button`, `key`, `x`, `y`, `suppress`, `state` |
| `capture` | Info | `Wheel trigger`, `Capture reset` | `direction`, `x`, `y`, `reason` |
| `capture` | Debug | `Stroke began`, `Click replayed`, `Press handed back`, `Handed-back press released`, `Wheel after drawing; no command`, `Gesture cancelled`, a raced `Suppression decision mismatch` (a wheel tick or a button joining a press) | `button`, `x`, `y`, `keys`, `result`, `lagMs`, `reason`, `direction` |
| `capture` | Warning / Error | `Input queue deep`, `Input queue full: …`, button `Suppression decision mismatch` | `depth`, `capacity`, `dropped`, `event`, `hook`, `machine` |
| `recognition` | Info | `Gesture recognized` / `No match`, one per stroke | `gesture`, `score`, `points`, `durationMs`, `matchMs`, `worstHandlerUs`, `top` (`name=score;…`), `reason` |
| `engine` | Info | `Engine starting`, `Engine stopped`, `Engine enabled` / `Engine disabled`, `Stroke button changed`, `Capture thresholds changed`, `Key capture armed`, `Key capture released` | `strokeButton`, `enabled`, `tickMs`, `button`, threshold fields, `idleTimeoutMs`, `reason` |
| `engine` | Error | `Key capture callback threw` (the capture holds) | `kind`, `key` |
| `capture` | Debug | `Stroke consumed` (the intercept took it) | `trigger` |
| `exec` | Info | `Trigger resolved`, `Window activated`, `Command fired`, `Command has no active steps`, `Command cancelled` | `trigger`, `outcome`, `reason`, `group`, `command`, `process`, `technique`, `elapsedMs`, `focusMoved`, `stepsRun`, `stepsSkipped`, `step` |
| `exec` | Debug | `Step ran`, one per step; `Click relayed` (a click trigger fired nothing); `Execution request dropped: executor stopped` | `index`, `type`, `summary`, `outcome`, `reason`, `ms`, `trigger`, `button`, `keys` |
| `ignore` | Info | `Ignore list watched` / `Ignore list not watched` (an active entry can match here or a command holds a button besides the stroke button, or neither), `Paused while an ignored app has focus`, `Resumed: the ignored app lost focus` | `focus`, `anchors`, `app` |
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
