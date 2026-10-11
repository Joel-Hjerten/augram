# Training

F3: drawing a new gesture or another sample of one, in a small window. The session that holds the state is Core's (`../../Augram.Core/Training/README.md`); the view model (`ViewModels/TrainingViewModel`) only projects it. This folder is the popup.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| The training state (request, stroke, best match, Accept, Cancel) | `Core/Training/TrainingSession` | Also the engine's `ITrainingSession`: a stroke-button stroke that starts over the open draw area is a training stroke, handed over from the engine worker and brought to the UI thread with the marshal `GesturesModule` passes (`Hosting/UiThread.RunOrPost`). |
| What to train | `Core/Training/TrainingRequest` (`NewGesture`, `Redraw(id)`) | The outcome is a `TrainingOutcome`. |
| Route the engine's strokes to training first | `Engine/Hosting/TrainingRouting` | The one line `EngineModule` wires as `EnginePorts.Intercept`; wheel ticks are never training input. |
| Open the window | `ITrainingPresenter` / `TrainingPresenter` | One window at a time (`Dialogs/DialogSlot`); closing cancels the session. |
| The window and its content | `TrainingWindow` + `TrainingView` | XAML; not on the dialog shell. |
