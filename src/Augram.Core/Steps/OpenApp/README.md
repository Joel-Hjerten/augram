# Steps/OpenApp

The **Open app** step (key `openApp`, category Run; Joel, 2026-10-09): bring an app to the front, or start it when it is not running; or open Augram's own window ("This app (Augram)", the way to reach the settings from a gesture).

| File | Role |
|---|---|
| `OpenAppStep` | record: `IsAugram`, `WindowsApp` ("chrome.exe"), `MacApp` ("Google Chrome") · `AppFor(platform)`: this platform's name, else the known-app guess from the other's (`Mapping/KnownApps`), so no own version is needed (F8) · `Summary` "Open Augram", "Open chrome.exe", "Open app (none for macOS)", "Open app (no app set)" |
| `OpenAppStepType` | `{ "augram": true }` or `{ "windows": "…", "mac": "…" }`, empty members omitted; platform-neutral, `Convert` is Same |
| `OpenAppExecutor` | Augram → `IAppWindow.Open()` (posted to the UI thread; Skipped when there is none). An app → `IAppActivator.BringToFront(name)`: Activated → Done; Failed → Failed; NotRunning → `IProcessLauncher.Launch(name)` (Started → Done). No activation of the gesture's window (category Run). One Debug line. |

Platform behaviour: on Windows `Platform.Windows/Apps/Win32AppActivator` finds the app's topmost top-level window (visible or minimized, unowned, not a tool window) by executable file name, restores it when minimized and activates it with the foreground activator (B1); a launch goes through the Run step's launcher (shell execute, so `chrome.exe` resolves through App Paths). On macOS `NullAppActivator` answers "not running" and the launcher's `open -a <name>` both brings a running app forward (with its reopen event, so an app without windows makes one) and starts one that is not running.

**May reference:** `Abstractions`, `Diagnostics`, `Mapping` (KnownApps), `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the App's step form (`Components/Steps/OpenApp`).
