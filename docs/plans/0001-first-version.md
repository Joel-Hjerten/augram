# Plan 0001: First version (M0–M3)

**Status: ACTIVE** — written 2026-10-05 after ADR-0001 and ADR-0002 were accepted. Read [requirements.md](../requirements.md) and both ADRs first. This plan says *what in what order*; it does not restate the rules.

## Milestones

| | Goal | Done when |
|---|---|---|
| **M0** | Scaffold + risk spike 2 | Solution builds on CI with architecture tests; the four B-risks have a measured answer and a fallback decision |
| **M1** | Joel's "first version" (A5): window, tray, recognition testable | Wireframe window opens from the tray; chosen button captured with a trail; strokes matched against trained or imported gestures; recognition log shows the result; training popup works |
| **M2** | Commands and steps | Commands page (groups › commands › steps) edits real config; Hotkey, Type text, Run, Media key, Delay steps execute; per-app override and ignore list work; SP.net import brings in Joel's whole config |
| **M3** | Daily-driver | Joel runs Augram instead of SP.net for a full day; polish list from that day becomes plan 0002 |

Each milestone ends with a commit tagged `m0`…`m3` and a short entry in `docs/learnings/` for anything that surprised us.

## M0: scaffold and spike 2

1. **Solution layout** per ADR-0002 §1: `Augram.Core`, `Augram.Engine`, `Augram.Platform.Windows`, `Augram.Platform.MacOS` (stub), `Augram.App`, `Augram.Import.StrokesPlus`, and the three test projects. `Directory.Build.props` (net10.0, nullable, warnings as errors, analyzers), `Directory.Packages.props`, `.editorconfig`, GitHub Actions on `windows-latest` running build, format check, tests.
2. **Architecture tests** first: Core references nothing but the BCL; no literal colours under `App/Components/`. These fail until the projects exist; that is the point.
3. **Spike 2** in `src/Augram.Spike2/` (throwaway, deleted at M1), one console switch per risk:
   - B1 activation: `AttachThreadInput` + `SetForegroundWindow` from a worker thread against Chrome, Explorer, a borderless game; log success rate.
   - B2 overlay: an Avalonia transparent, topmost, non-activating, hit-test-disabled window pre-created at startup; measure time from first point to first visible segment; test over a borderless-fullscreen game and across two monitors at different DPI. If it fails any of these, Platform.Windows gets the layered-window implementation from the GestureSign notes and the Avalonia overlay is dropped.
   - B3 hook resilience: run the SharpHook hook for hours through sleep, lock, unlock; detect a dead hook (no events for N seconds while input is happening, or SharpHook's stop event) and reinstall.
   - B4 keyboard: SharpHook keyboard suppression including Win key; media key simulation; text entry into Notepad and into a game console.
4. Record what the agent could self-verify in `docs/learnings/0001-spike2.md`. **Changed 2026-10-05 (N4):** the probes are not run as separate tests by Joel; their measurements (hook health, overlay timings, activation technique, key simulation results) move into the engine as always-on diagnostics, and B1–B4 are closed by reading the Diagnostics tab and log files during normal use. Spike2 stays in the repo until M1 step 4 has absorbed its code, then is deleted.

## M1: window, tray, recognition

Order matters: each step is testable without the next.

1. **Core/Recognition**: port from the classic source exactly as [strokesplus-classic-source.md §1](../reference/strokesplus-classic-source.md) describes, with `ScoringMode { Legacy, Corrected }` and `PatternAggregation { Average, Best }`. NOTICE file with the MIT text and HighSign credit beside it. Tests: scale/position invariance, rotation sensitivity (up-flick ≠ down-flick), and a regression that classifies the spike's recorded strokes against Joel's 90 templates (fixture = his gestures exported to JSON, with permission).
2. **Core/Gestures + Config**: `Gesture` (id, name, active, samples as raw points), `GestureLibrary` store, JSON document with `schemaVersion`, backup-before-write, load fallbacks. Round-trip tests.
3. **Core/Capture**: `CaptureStateMachine` as a pure object fed `CaptureEvent`s and a clock; outcomes per the table in [strokesplus-classic-source.md §2](../reference/strokesplus-classic-source.md) with Joel's decisions (another button cancels; hold-still cancels; wheel fires immediately and repeatedly; no relay on no-match; Ignore Key). Tests are scripted event sequences, including the stuck-button invariant (A19): every consumed down has a consumed up.
4. **Diagnostics first (N4)**: `IEventLog` port in Core (structured events, levels, never blocks the caller); Engine/App implementation with Microsoft.Extensions.Logging + a rolling file sink under `logs/` beside the config; a `HealthSnapshot` service. Then **Engine**: SharpHook adapter implementing `IInputSource`/`IInputSimulator`; hook thread → bounded channel → engine worker; `IsEventSimulated` guard; hook health monitor (watchdog + reinstall, from Spike2 `HookMode`) logging every transition; capture timings and recognition results logged per stroke (`RecognitionLog` ring buffer feeds the Diagnostics tab).
5. **Platform.Windows**: `IWindowSystem` (window under point, root owner, process name/path/title/class chain, foreground root) and `IOverlay` per the B2 decision.
6. **App shell**: composition root with DI; tray icon (single-click toggle, double-click open, menu: Open, Enable/Disable, Start at login, Quit); single instance; wireframe theme; navigation registry with Gestures / Commands / Ignored / Options tabs (Commands and Ignored are empty placeholders in M1); `SectionForm` and `ItemList` renderers with the first field kinds (Toggle, Dropdown, Text, Number, ButtonRadio, Note, Custom); dev gallery; F1 inspector with click-to-copy.
7. **Gestures tab**: `GestureGrid` of `GestureGlyph` tiles (active/inactive styling), Add new gesture → training popup (canvas, Cancel, Accept, redraw replaces, name field, live "looks like X, N%"), rename (F2/Return), delete with undo, toggle active.
8. **Options tab (M1 subset)**: stroke button with detect-to-assign, Ignore Key, start distance, cancel delay, no-match behaviour, **trail colour, width and opacity** (needs the `Color` field kind), threshold and precision, config folder, start at login.
9. **Diagnostics tab**: health summary, live log tail with level/source filter, recognition log view, "open log folder", "copy last N lines".
10. **Gesture import only** from SP.net JSON (`Gestures[]` → library) so Joel's 90 gestures are available on day one. Full import is M2.

M1 acceptance: Joel draws his top ten gestures over any app and the log names them correctly; a wrong one can be retrained from the Gestures tab; nothing fires yet.

## M2: commands, steps, scoping, import

1. **Core/Mapping**: `AppGroup` (identity fields per F5, active, suppress-globals), `Command` (id, name, gesture id or trigger, active, steps), `CommandResolver` (group under gesture start → command for gesture/trigger → "override to nothing" honoured → global fallback), `MappingStore` with undo.
2. **Core/Steps** as vertical slices: `Hotkey`, `TypeText`, `Run`, `MediaKey`, `Delay`, each with record + type metadata (category, convertible?, conversion rule) + executor. Window operations (`CloseWindow`, `Minimize`, `MaximizeRestore`, `ToggleTopmost`, `Center`, `SetSize`, `SnapHalf`) as one `WindowOp` step type with a sub-kind, executed through `IWindowOperations`.
3. **Engine executor**: serialized task chain; activation rule A20; settle delay A8 only when focus moved; per-step platform override resolution (F8).
4. **Platform.Windows**: `IWindowOperations`, `IProcessLauncher`, activation per B1.
5. **Commands tab**: `CommandTree` (groups collapsible, Global pinned, alphabetical), command rows (glyph/trigger icon, name, step summary, active, platform marker), inline `StepList` with drag reorder, step editor from the step type's declared form, `StepTypePicker` grouped by category, gesture picker popup with New Gesture… and No Gesture, context menu + keymap (Ctrl+N/C/V/D, Del, F2/Return), confirmations for group/command delete, Test button. **Used by** (Joel, 2026-10-07): right-click a gesture tile › "Used by…" opens a small popup listing every app group › command that uses the gesture (SP.net never had this); clicking a row navigates to that command on the Commands tab with it selected. Also wired the other way: deleting a gesture warns with the same list (F3).
6. **App identification page** per group (exe name primary, path, process, title, regex toggles, crosshair picker, ignore-if-fullscreen, suppress globals) and the **Ignored tab** (same form + disable-on-focus).
7. **Hotkey capture field**: engine keyboard suppression while capturing, Accept and click-outside commit, Clear, watchdog, mouse never suppressed.
8. **Full SP.net import** per the mapping table below, with a report of what was skipped.
9. **Export/import of Augram JSON** with scopes and merge.
10. **Gesture shape cleanup** (built 2026-10-09; requirements F3 "Shape cleanup", Joel 2026-10-08), after the remaining step types (TypeText, Run). Core: a pure `Recognition/` (or `Gestures/`) cleanup function, corner finding (ShortStraw-style) → per-piece line / arc / circle / smooth fit → a raw point list in the drawn direction; the gesture keeps its original samples beside the cleaned ones, and Restore swaps back (one undo step each). App: **Clean up shape** checkbox in the draw area, ticked by default (Joel), with a preview, **Clean up shape** / **Restore original** on the tile menu. Sync: the original travels with the gesture; raise `SyncFile.CurrentFormatVersion`. Before calling it done, replay the Recognition log's drawn strokes against raw vs cleaned templates of Joel's set and write the numbers to `docs/learnings/`.

## M3: daily driver

Switch Joel's stroke button to Middle, retire SP.net for a day, collect the friction list, fix what blocks daily use, write the next plan from the rest (macOS adapter, cross-platform conversion UI, hold-modifier wheel step, exclusion zones if ever needed).

## C1: SP.net import mapping

| SP.net step (`Steps[].Method`) | Augram |
|---|---|
| `SendHotKey(hotkey{modifiers, Key})` | Hotkey step |
| `SendVKey(virtualKey)` for media/browser keys (166–183) | MediaKey step; other keys → Hotkey with no modifiers |
| `SendKeys(string)` | parse .NET SendKeys syntax: plain runs → TypeText; `^+%` groups and `{KEY}` tokens → Hotkey steps; `{DELAY n}`-style classic tokens → Delay |
| `Delay(milliseconds)` | Delay step |
| `Run(command)` / `sp.RunProgram(file, args, verb, style, …)` in scripts | Run step (file, args, elevated = verb "runas", hidden = style) |
| `CloseWindow`, `MinimizeWindow`, `MaximizeOrRestoreWindow`, `ToggleWindowAlwaysOnTop`, `SetWindowSize`, `InvokeObjectMethodByName(Center)` | WindowOp step |
| `MouseClick(StartPoint, button)` | MouseClick step (M3 if not needed sooner) |
| `SendAltDown`/`SendAltUp`, `SendWinDown`/`SendWinUp`, `ConsumePhysicalInput` | import as disabled with a note; candidates for a hold-modifier step later |
| Script-only actions | import disabled; the script text kept as the command's note |
| Action with `WheelUp`/`WheelDown` and no gesture | trigger-only command |
| Action with `Shift`/rocker modifiers | import disabled with a note (deferred feature) |
| `Applications[].{FileName, FilePath, Owner*/Root*/Parent*/Control*}` | group identity fields; regex flags carried over |
| `IgnoredApplications[]` with `DisableOnFocus` | Ignored list entries, mode by flag |
| Gesture references by name that do not resolve | warning in the import report; command imported without a gesture |

Config keys: `StrokeButton` (WinForms enum → button), `MatchProbabilityThreshold`, `MatchPrecision`, `MinGestureStartDistance`, `CancelDelay`, `ResetCancelDelayOnMovement`, `IgnoreKeyList`, `PenWidth/PenOpacity/PenColor`.

## C3: starter gesture set for a fresh install

Eight flicks (↑ ↓ ← → and the four diagonals), four out-and-backs (↑↓ ↓↑ ←→ →←), four L-shapes (↓→ ↓← ↑→ ↑←), and C, S, Z, circle. Stored as raw point lists drawn once at 100 px scale, like SP.net's stock gestures. Joel's own set replaces these via import.

## Not in this plan

Hold remaps (requirements F9): [plan 0002](completed/0002-hold-remaps.md), which Joel put ahead of the rest of M2 on 2026-10-10 (done on both machines the same day); macOS adapter and permissions onboarding; signing and notarization (both started anyway on Joel's Mac, 2026-10-07/08: session handoff §8a, docs/release.md); cross-platform conversion UI beyond the data model; hold-modifier-across-wheel step; exclusion zones; rocker/modifier chords; config-file schema migrations beyond version 1.
