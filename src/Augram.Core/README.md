# Augram.Core

Pure .NET domain: gestures, recognition, capture state machine, mapping, hold remaps (`HoldRemaps/`, F9), step types, config, machine-to-machine sync (`Sync/`: the item merge; git is behind a port), and the ports (interfaces) everything outside implements. Organised by subsystem, one folder each, every folder with its own README (ADR-0002 §3).

**May reference:** nothing but the BCL. No project references, no package references.

**Must never contain:** Avalonia, SharpHook, `System.Windows.*`, `System.Drawing`, OS APIs, timers, threads that touch a hook or a window, or any UI type. The architecture test in `tests/Augram.Core.Tests/Architecture` fails the build if any of these appear.

Business rules have exactly one home and it is here (ADR-0002 §5a). Data is immutable `record` types; state changes go through one store per aggregate.
