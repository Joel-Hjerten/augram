# Augram.Engine

Wires Core to SharpHook: the hook adapter (`IInputSource`), input simulation (`IInputSimulator`), the bounded channel from the hook thread to the engine worker, the executor, the wheel trigger, hook health monitoring, and the diagnostics implementation (`Diagnostics/`).

**May reference:** `Augram.Core` and the SharpHook package. That is the only project allowed to reference SharpHook.

**Must never contain:** Avalonia or any UI type, a dispatcher, window lookup or window operations (those are Platform.*), or business rules (those are Core). Hook handlers do near-zero work: append a point, set suppress, return (CLAUDE.md invariants 1 and 2). Threads are documented in `docs/reference/threading.md` when they land.

## Diagnostics (N4, M1 step 4)

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

The hook thread should log at `Trace` only and guard value-typed properties with `IsEnabled` (they box at the call site); everything at Info and above is emitted by the worker. `IsEventSimulated` is true for input injected by any process, not only ours; the engine's guard therefore also ignores other utilities' synthetic input (learnings 0001).

### Threads introduced here

| Thread | Does | Must not |
|---|---|---|
| caller (hook thread, worker, UI) | `Log`: compare, `TryWrite` | wait |
| log drain (thread-pool task) | hand events to sinks, flush on idle, report drops | touch the hook, the worker, or a UI type |
| file flush timer (`System.Threading.Timer`) | `RollingFileSink.Flush` every 500 ms | throw |

`RingLog.Changed` on `InMemorySink` fires on the drain thread; a view model subscribing to it marshals to the UI and returns.

The rest of the Engine (hook adapter, worker, executor) lands in the remainder of M1 step 4.
