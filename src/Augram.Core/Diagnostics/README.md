# Core/Diagnostics

The data side of N4 (observability): what a log event is, how to emit one cheaply, the health summary, and the recognition log store (A15). No I/O, no threads, no timers: the Engine's `Diagnostics/` folder does the queueing and writing. The two ports live in `Abstractions/` (`IEventLog`, `IHealthSource`).

## Types

| Type | Role |
|---|---|
| `EventLevel` | `Trace`, `Debug`, `Info`, `Warning`, `Error`; ordered, so "minimum level" is one comparison |
| `LogEvent` | `(Timestamp, Level, Source, Message, Properties?, Exception?)`, immutable record |
| `LogProperty` | `(Key, Value)`; written as a tuple at the call site, `("generation", 2)` |
| `EventLogExtensions` | `log.Trace/Debug/Info/Warning/Error(source, message, params properties)`; `Error` has an overload taking the exception |
| `NullEventLog` | `Instance`; never enabled, discards everything; the default for tests |
| `HealthSnapshot` | `HookAliveSince`, `HookReinstallCount`, `EventsLastMinute`, `LastStrokeLatencyMs`, `LastActivationOutcome`, `OverlayFirstFrameMs`, `UptimeSeconds`, `WorkingSetBytes`; every field nullable, null = unknown |
| `HealthRegistry` | the `IHealthSource`; `Register(Func<HealthSnapshot, HealthSnapshot>)` returns an `IDisposable` to unregister; `Current()` folds contributors over `HealthSnapshot.Empty` |
| `RingLog<T>` | fixed-capacity ring: `Add`, `Snapshot()` (oldest first), `Count`, `Version`, `Changed`, `Clear()` |
| `RecognitionCandidate` | `(Name, Score)` |
| `RecognitionLogEntry` | per stroke: timestamp, point count, duration, top matches (trimmed to 3, best first), matched group, fired command or why nothing fired |
| `RecognitionLog` | `RingLog<RecognitionLogEntry>`, capacity 200 |

## Contract

- **Sources** are short lowercase component names the Diagnostics tab filters on: `hook`, `capture`, `recognition`, `activate`, `steps`, `config`, `overlay`, `log` (the log's own drop reports). Add to this list when a component gains a source; do not invent per-call variants.
- **Properties carry the numbers, the message carries the sentence.** `log.Info("hook", "Hook installed", ("generation", 2), ("firstEventMs", 3.2))`, not string interpolation. The sinks render `key=value` invariantly, so an agent can grep for `firstEventMs=`.
- **`Error(source, message, exception, props...)`** for anything caught; the sinks print the exception on indented lines after the event line.
- **`RecognitionLog` is the store for the A15 panel; the same facts also go to `IEventLog` at Info** so the file and the panel agree. The Engine worker writes both; nothing else adds entries.
- **`HealthRegistry` contributors read, never compute.** A contributor is `s => s with { HookAliveSince = _aliveSince }` over a volatile field the component already maintains; it runs on the reader's thread when the Diagnostics tab refreshes.

## What is hot-path safe

Invariant 1 says hook handlers do near-zero work. These are the costs, so the hook thread can be audited:

| Call | When disabled | When enabled |
|---|---|---|
| `log.IsEnabled(level)` | one interface call | same |
| `log.Trace(src, msg)` | one interface call, no allocation | one `LogEvent`, one `DateTimeOffset.Now`, one `TryWrite` |
| `log.Trace(src, msg, ("k", v))` | as above, plus boxing of `v` if it is a value type (happens at the call site, before the level check) | as above, plus one `LogProperty[]` |
| `ring.Add(item)` | one lock, no allocation (the slot is reused); `Changed` handlers run inline on the caller's thread | |
| `registry.Current()` | one record per contributor; meant for a UI refresh, not a loop | |

Rules that follow: the hook thread logs at `Trace` only and guards value-typed properties with `IsEnabled`; the worker logs everything else; `RingLog.Changed` subscribers marshal to the UI themselves and return immediately; `HealthSnapshot` is read by the Diagnostics tab, never by the Engine.

## Threading

`RingLog<T>` is one writer, many readers, lock-based. `HealthRegistry` is copy-on-write for registration and lock-free for reads. `LogEvent`, `LogProperty`, `HealthSnapshot` and `RecognitionLogEntry` are immutable and may cross threads freely. `IEventLog.Log` is the Engine's problem to make non-blocking (see `src/Augram.Engine/README.md`).

**May reference:** nothing in Core but `Abstractions`. **Referenced by:** every subsystem that logs, Engine (sinks, worker), App (Diagnostics tab).
