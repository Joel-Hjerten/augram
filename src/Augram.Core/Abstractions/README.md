# Core/Abstractions

The ports (ADR-0002 §2): `IInputSource`, `IInputSimulator`, `IWindowSystem`, `IWindowOperations`, `IProcessLauncher`, `IOverlay`, `IClock`, `IConfigStore`, plus the two diagnostics ports below. Small interfaces, 1 to 5 members each, implemented by Engine, Platform.* and App. Core owns the contract; it never sees an implementation.

## Present

| Port | Members | Implemented by | Notes |
|---|---|---|---|
| `IEventLog` | `IsEnabled(level)`, `Log(event)` | Engine `ChannelEventLog`; `NullEventLog` in Core for tests | N4. `Log` must never block the caller. Use the extension methods in `Diagnostics/EventLogExtensions` rather than building a `LogEvent` by hand; they skip the allocation when the level is off. |
| `IHealthSource` | `Current()` | Core `HealthRegistry` | N4 health summary. Read on Diagnostics tab refresh only. |

The rest land with their first consumer.
