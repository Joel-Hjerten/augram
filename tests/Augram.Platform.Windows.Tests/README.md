# Augram.Platform.Windows.Tests

xunit tests for `Augram.Platform.Windows`. The window system is split into pure rules (`FullScreenRule`, `DesktopRule`, `UwpHostRule`, `ActivationPolicy`) and two classes that consume a native facade (`WindowIdentityReader`, `ForegroundActivator`); all of those are tested here against `FakeWin32`, a scripted in-memory window tree. One integration test (`Win32WindowSystemTests.WindowAt_Cursor_*`) calls real Win32 at the cursor position and only asserts "no exception, sane or null"; it returns early when `Environment.UserInteractive` is false. `Startup/RunKeyStartupRegistrationTests` writes a throwaway `AugramTest-<guid>` value under the real HKCU Run key and deletes it in `finally` (also skipped when not interactive); `Input/` covers the cursor probe and the `SystemEvents` mapping.

**May reference:** `Augram.Platform.Windows` (internals via `InternalsVisibleTo`), `Augram.Core`, xunit. Runs on the Windows CI runner.
