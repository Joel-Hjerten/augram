# Training

F3: drawing a new gesture or another sample of one, in a small window. The session holds the state; the view model (`ViewModels/TrainingViewModel`) only projects it.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| The training state (request, stroke, best match, Accept, Cancel) | `TrainingSession` | Also the engine's `ITrainingSession`: a stroke-button stroke that starts over the open draw area is a training stroke, handed over from the engine worker and brought to the UI thread with `Hosting/UiThread.RunOrPost`. |
| What to train | `TrainingRequest` (`NewGesture`, `Redraw(id)`) | The outcome is a `TrainingOutcome`. |
| Route the engine's strokes to training first | `TrainingRouting` | The one line the engine wiring needs; wheel ticks are never training input. |
| Open the window | `ITrainingPresenter` / `TrainingPresenter` | One window at a time (`Dialogs/DialogSlot`); closing cancels the session. |
| The window and its content | `TrainingWindow` + `TrainingView` | XAML; not on the dialog shell. |
