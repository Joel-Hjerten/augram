# Core/Training

F3, A6: drawing a new gesture, or redrawing one, in the training popup. The state lives here (ADR-0002 §5a: a service, not a window); the popup is the App's (`App/Training/`), and the engine's strokes reach the session through `Engine/Hosting/TrainingRouting`.

| Type | Role |
|---|---|
| `TrainingSession` | The state: `Begin(TrainingRequest)` (the name prefilled "New gesture N" through `Mapping/FreeNames`, or the redrawn gesture's), one stroke that `ReplaceStroke` replaces (no averaging), the cleaned shape (`CleanUp`, on by default), the live best match (`Recognition/GestureMatcher.Rank`), `Accept()` through the library's rules (`GestureValidationException` carries the message) and `Cancel()`; `Changed` and `Ended` (`TrainingOutcome`). |
| `ITrainingSession` | What the engine asks after every completed stroke: `TryConsume(points, startX, startY)` takes a stroke that started inside the open draw area (`PublishCanvas`) and answers true. |
| `TrainingRequest`, `TrainingOutcome` | What to train (`NewGesture`, `Redraw(id)`) and how it ended. |
| `ScreenArea` | Where the draw area is on screen (physical pixels plus its scaling), immutable so the engine worker reads the latest without a lock; `ToLocal` maps a screen point into it. |

**Threads.** Everything runs on the UI thread except `TryConsume`, which the engine worker calls; it brings the stroke to the UI thread through the marshal the composition root passes (the App's `UiThread.RunOrPost`). Without one (tests, the gallery) it runs where it arrives.

**May reference:** Core's `Gestures`, `Recognition`, `Mapping`, `Abstractions`, `Diagnostics`. Never Avalonia or the Engine. **Referenced by:** the Engine (`TrainingRouting`) and the App (the popup, the Gestures tab, the gesture picker).
