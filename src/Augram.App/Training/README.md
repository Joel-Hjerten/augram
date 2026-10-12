# Training

F3: drawing a new gesture or another sample of one, in a small window. The session that holds the state is Core's (`../../Augram.Core/Training/README.md`); the view model (`ViewModels/TrainingViewModel`) only projects it. This folder is the popup.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| The training state (request, stroke, best match, Accept, Cancel) | `Core/Training/TrainingSession` | Also the engine's `ITrainingSession`: a stroke-button stroke that starts over the open draw area is a training stroke, handed over from the engine worker and brought to the UI thread with the marshal `GesturesModule` passes (`Hosting/UiThread.RunOrPost`). |
| What to train | `Core/Training/TrainingRequest` (`NewGesture`, `Redraw(id)`) | The outcome is a `TrainingOutcome`. |
| Route the engine's strokes to training first | `Engine/Hosting/TrainingRouting` | The one line `EngineModule` wires as `EnginePorts.Intercept`; wheel ticks are never training input. |
| Open the window | `ITrainingPresenter` / `TrainingPresenter` | One window at a time (`Dialogs/DialogSlot`); closing cancels the session. |
| The window and its content | `TrainingWindow` + `TrainingView` | A `DialogWindow` (code-only, `Dialog.Training` sizes) around the view (XAML) in the `DialogShell` (`Dialogs/README.md`). |

## Training (F3, A6)

**Shape cleanup (plan 0001 M2 step 10, 2026-10-09).** The popup has a **Clean up shape** check box, ticked by default (`TrainingSession.CleanUp`, kept for the rest of the run once changed): the cleaned shape (`CleanedStroke`) shows in gold over the stroke (a second `GestureDrawArea` with class `cleaned`, not hit-testable), the best match compares what Accept will store, and Accept stores the cleaned shape with the stroke as its original (`GestureCleanup.FromStroke`); unticked, the stroke as drawn. A gesture tile's menu offers **Clean up shape** on a gesture as drawn and **Restore original** on a cleaned one (`GestureGridAction.CleanUp` / `RestoreOriginal`, one library update each, so one undo step). The Gallery's Shape cleanup page previews the cleanup on any stroke and on this machine's set.

`TrainingSession` owns the state: `Begin(TrainingRequest)` (a new gesture, prefilled "New gesture N", or a redraw of an existing one), one stroke that each `ReplaceStroke` replaces (no averaging), the live best match from `GestureMatcher.Rank` against the active library, `Accept()` (adds the gesture, or replaces the redrawn gesture's samples with the stroke, through the library's rules; the exception message is shown inline) and `Cancel()`. The Commands tab's gesture picker opens the same session for New Gesture… and selects what it stored. `TrainingPresenter` shows it as `TrainingWindow` (owned by the main window, centred, Cancel and Accept; Escape cancels, closing the window cancels). `GestureDrawArea` accepts left-button strokes and publishes its `ScreenArea` (physical screen pixels plus render scaling) whenever it moves; `TrainingSession.PublishCanvas` keeps the latest.

Stroke-button routing: the hook suppresses the stroke button, so the window never sees it. The engine wiring calls `ITrainingSession.TryConsume(points, startX, startY)` (or the `TrainingRouting.TryConsume(EngineEvent)` extension) for every completed stroke; when a session is open and the start point is inside the canvas, the points are converted to canvas coordinates, the stroke replaces the current one on the UI thread, and the call returns true. Safe from the engine worker thread. In M1 the engine still ranks and logs the stroke first; that is accepted.
