# Augram.Engine

Wires Core to SharpHook: the hook adapter (`IInputSource`), input simulation (`IInputSimulator`), the bounded channel from the hook thread to the engine worker, the executor, the wheel trigger, hook health monitoring, and the `RecognitionLog`.

**May reference:** `Augram.Core` and the SharpHook package. That is the only project allowed to reference SharpHook.

**Must never contain:** Avalonia or any UI type, a dispatcher, window lookup or window operations (those are Platform.*), or business rules (those are Core). Hook handlers do near-zero work: append a point, set suppress, return (CLAUDE.md invariants 1 and 2). Threads are documented in `docs/reference/threading.md` when they land.

Lands in M1 step 4. Empty for M0.
