# Augram.Platform.MacOS.Tests

xunit tests for `Augram.Platform.MacOS`. The adapter is split like the Windows one: pure rules over plain records (`MacWindowPick` for hit-testing the window server's list and the desktop and full-screen flags, `MacRect` for the Cocoa ↔ top-left flip, `MacMaximize` for what "filled" means and which screen a window is on, `MacModePick` for which display modes are offered and which twin is applied) are tested here on every OS, the Windows CI runner included. The native readers and the Accessibility calls have one live test, `MacWindowOperationsLiveTests`, which opens a throwaway TextEdit document and maximizes, restores, minimizes and closes it through the real adapter. It runs only with `AUGRAM_MAC_LIVE=1` on a Mac whose test runner holds the Accessibility permission, and returns at once everywhere else:

```
AUGRAM_MAC_LIVE=1 dotnet test tests/Augram.Platform.MacOS.Tests
```

**May reference:** `Augram.Platform.MacOS` (internals via `InternalsVisibleTo`), `Augram.Core`, xunit.
