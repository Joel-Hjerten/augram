# Steps/Run

The **Run** step (key `run`, category Run, **platform-bound**): start a program, document, folder or URI and go on at once (A4 names it; F5 "run a command line", "run program with arguments, elevated/hidden flags"). Joel's StrokesPlus.net config uses it as a `Run` step (`explorer`, `ms-settings:display`) and as `sp.RunProgram(…)` scripts (`taskkill.exe /f /im yuzu.exe` elevated and hidden; the display-changer scripts become the Display step instead). Requirement F8 sync: Run steps travel with the settings, so no passwords in them.

| File | Role |
|---|---|
| `RunStep` | record: `File`, `Arguments`, `WorkingDirectory` (empty = the launcher's default), `Elevated`, `Hidden` · `IsSet` (a non-blank file) · `IsUri` · `Target` "Run explorer", "Run dc64cmd.exe" (a path shows its last part), "Open ms-settings:display" · `Summary` = `Target` + arguments + "(as admin, hidden)", "(as admin)" or "(hidden)", or "Run (no program set)" · `ToLaunch()` the `ProcessLaunch` with blanks trimmed · `Unset`, the default a new step starts with |
| `RunStepType` | metadata and JSON (below); `CreateDefault()` is `RunStep.Unset` |
| `RunExecutor` | no program → Skipped "no program set" · cancellation already requested → Skipped "cancelled", nothing started · `IProcessLauncher.Launch` → `Started` Done, `Cancelled` Skipped "cancelled: the administrator prompt was declined", `NotSupported` Skipped with the reason, `Failed` Failed with the reason (the command stops). One Debug line (`steps` / "Run": file, elevated, hidden, outcome, reason) per run; **the arguments are never logged by the step** |
| `RunConversion` | F8, below |

## Parameters

```json
{ "file": "taskkill.exe", "arguments": "/f /im yuzu.exe", "workingDirectory": "", "elevated": true, "hidden": true }
```

- `file`, `arguments`, `workingDirectory`: strings, absent = empty. Stored as typed (no trimming, no expansion), so the round trip is byte-stable; `ToLaunch()` trims.
- `elevated`, `hidden`: JSON `true` / `false`, absent = false. A string such as `"true"` is a `StepFormatException` naming the member (`StepParameters.ReadBoolean`).
- `Write` emits all five members, always.

## Execution

- The executor thread calls `IProcessLauncher.Launch` and gets an answer within a bounded time: the launcher **never waits for the program to exit**, and an adapter that has to wait on the OS (the Windows UAC prompt) answers `Started` with a note after its wait and logs the late outcome itself.
- Run is not a Keyboard or Text step, so the executor never activates the target or waits the settle delay (A8) for it: a started program opens its own window.
- The step's log line names the file, not the arguments. The engine's own `Step ran` Debug line and `Command stopped` Warning carry the step's `Summary`, which shows the arguments: an open point for Joel (handoff), shared with TypeText.

## Platform (F8)

`IsPlatformNeutral` is **false**. `RunConversion`, computed for display and before every run, never stored:

| Authored on → run on | Rule | Examples |
|---|---|---|
| the same platform, or no program set | unchanged | |
| the other platform, a link every platform opens alike | unchanged: `http`, `https`, `mailto`, `ftp` | `https://example.com`, `mailto:joel@example.com` |
| the other platform, anything else | no guess: "Run explorer needs a macOS version: a program and its path belong to one platform"; "Open ms-settings:display needs a macOS version: ms-settings: links open on one platform only". The executor skips the step there; the command gets its own version on that platform (`Mapping/CommandVersion`) | `explorer`, `C:\Windows\notepad.exe`, `ms-settings:display`, `/Applications/Safari.app` |

The reason uses `Target`, never the arguments.

## What each adapter does

| | Windows (`Platform.Windows/Launch/Win32ProcessLauncher`) | macOS (`Platform.MacOS/Launch/MacProcessLauncher`) |
|---|---|---|
| how | `Process.Start` with `UseShellExecute = true` (ShellExecuteEx): a path, a bare name on the PATH or in App Paths (`explorer`, `mspaint.exe`), a document (its app), a folder (Explorer), a URI (its handler) | an executable file, or a bare name found on the PATH, runs itself with the arguments; an app (`Safari`, `/Applications/Safari.app`) through `open -a … --args …`; a document, folder or URI through `open` (arguments ignored) |
| file, Start in | `%VAR%` expanded (`Environment.ExpandEnvironmentVariables`); a URI is left as is | a leading `~` is the home folder, `%VAR%` expanded; a URI is left as is |
| Start in empty | the user's profile folder (as Win+R does) | the home folder for an executable; `open` launches through LaunchServices, which ignores it |
| elevated | `Verb = "runas"`: the UAC prompt; declined (Win32 error 1223) → Cancelled, not Failed | NotSupported "running as administrator is not supported on macOS" (the step skips) |
| hidden | `WindowStyle = Hidden` (SW_HIDE; a console program such as `taskkill` shows no window) | `open -g`: the app starts in the background, not in front; an executable has no window to hide |
| bounded wait | the start runs on its own STA thread; the executor waits at most 2 s (a UAC prompt can take longer), then answers Started with a note and the thread logs the late outcome | waits at most 2 s for `open` itself (not the app) to report; a non-zero exit is Failed with `open`'s message ("Unable to find application named 'Foo'") |
| failure | Win32 error → Failed "explorer2 was not found (2)", "… no app is associated with it (1155)", "… Start in folder is not valid (267)" | Failed with the reason |

**May reference:** `Abstractions`, `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn` (one line), the importer (`Augram.Import.StrokesPlus/RunMapping`, `RunProgramScript`; not yet wired into `StepReader`), the App's `Components/Steps/Run/` form.
