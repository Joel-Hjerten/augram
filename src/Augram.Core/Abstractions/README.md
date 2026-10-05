# Core/Abstractions

The ports (ADR-0002 §2): `IInputSource`, `IInputSimulator`, `IWindowSystem`, `IWindowOperations`, `IProcessLauncher`, `IOverlay`, `IClock`, `IConfigStore`. Small interfaces, 1 to 5 members each, implemented by Engine, Platform.* and App. Core owns the contract; it never sees an implementation.

Lands with the first consumer in M1. Nothing here yet.
