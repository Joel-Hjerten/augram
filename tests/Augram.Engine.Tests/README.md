# Augram.Engine.Tests

xunit tests for `Augram.Engine`: the hook-to-worker channel, the executor's task chain, hook health detection, and the `RecognitionLog`. Tests drive the engine through the Core ports with fakes; no real global hook is installed in a test (CI runners have no interactive desktop).

**May reference:** `Augram.Engine` (and `Augram.Core` through it), xunit.

Nothing here yet beyond the scaffold placeholder; the first real tests land in M1 step 4.
