# Augram.Core.Tests

xunit tests for `Augram.Core` and `Augram.Import.StrokesPlus`: recognizer fixtures (Joel's templates), scripted capture state-machine sequences, config round-trips, and the `Architecture/` rules that fail the build when a project references something ADR-0002 forbids.

**May reference:** `Augram.Core`, `Augram.Import.StrokesPlus`, `Augram.Sync.Git` (for its architecture rule only; its behaviour is tested in `Augram.Sync.Git.Tests`), xunit. No Avalonia, no SharpHook: if a test needs them it belongs in another test project.

Tests are plain xunit with reflection; no NetArchTest or similar dependency.
