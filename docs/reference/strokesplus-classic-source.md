# StrokesPlus (classic, C++) source: porting notes

**Source:** `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\StrokesPlus (original strokesPlusNet is based on)\StrokesPlus-master` = https://github.com/minyoad/StrokesPlus. **MIT, (c) 2014 Rob Larkin**; recognition code credited to HighSign by Dylan Vester. This is the code Augram's recognizer is ported from, and the behavioural ancestor of StrokesPlus.net (closed source). Read 2026-10-05.

Everything lives in `StrokesPlusHook/StrokesPlusHook.cpp` (17,164 lines). Regions that matter, by line:

| Region | Lines | Use |
|---|---|---|
| Gesture Match Math | 5907–6061 | **Port verbatim** (resample + angles + probability) |
| `GetGestureName` (in Gesture Match and Execute) | 6603–6668 | **Port verbatim** (scoring across patterns and gestures) |
| `clearCaptureVars` | 6154–6190 | the full list of capture state |
| `TimerProc` (cancel delay) | 7282–7337 | timeout semantics |
| `MouseProc` (low-level mouse hook) | 7565–8151 | the capture state machine |
| Graphic Functions `ScaleGesture` | 1153–1239 | glyph normalisation (bounding box, scale on longest axis) |

Do not try to build it (VS2010, vendored Boost/Lua/Scintilla). Read it.

## 1. Recognizer, exactly as implemented

```
GetGestureName(stroke):
  best = threshold (75); bestName = ""
  for each gesture:
    probs = []
    for each PointPattern (training sample) of the gesture:
      a = angles(resample(template, P))      // P = iPrecision = 100
      b = angles(resample(stroke,   P))      // recomputed per pattern in the original; cache it in the port
      deltas = array of P zeros
      for k in 0 .. len(a)-1: deltas[k] = AngularDelta(a[k], b[k])
      probs.add( Probability( sum(deltas) / P ) )     // ← divides by P, not by len(a)
    avg = mean(probs)
    if avg > best and avg > 0: best = avg; bestName = gesture.name   // strictly greater
  return bestName
```

- `AngularGradient(p1,p2) = atan2(dy, dx)`.
- `AngularDelta(a1,a2) = |a1-a2|; if > π then 2π - that`.
- `Probability(d) = |d * 31.830988618379067 - 100|` (31.83 = 100/π, so a mean delta of π gives 0 and 0 gives 100; the `abs` makes negative never happen).
- `resample(points, P)`: walk the polyline, emit a point every `totalLength / P`; first point always included; stops at P points. Produces **P points at most, so P−1 angles**.

**Quirk to preserve for score parity:** `deltas` is sized `P` but only `len(a) ≤ P−1` entries are filled, and the average divides by `deltas.size()` = P. So every score is diluted by at least one zero entry, and short strokes that resample to fewer than P points are diluted more (a zero delta counts as a perfect match for that slot). Joel's threshold of 75 and his trained patterns were tuned against this behaviour. The port keeps it as the default scoring mode (`Legacy`), with a corrected mode (`divide by actual angle count`) available for later experiments. Changing the mode changes what 75 means; the recognition log should show scores from the active mode.

**Multi-pattern scoring:** this source averages the per-pattern probabilities of a gesture. Rob described SP.net on the forum as "highest match probability wins" across patterns ([strokesplus-net-config.md §9](strokesplus-net-config.md)). SP.net is closed, so which it does is unknown. The port exposes both (`Average` / `Best`) and the choice is tested against Joel's three multi-pattern gestures.

**Rotation sensitivity** is inherent: angles are compared index by index with no normalisation. Scale and position invariance come from resampling by arc length. Do not add rotation normalisation (CLAUDE.md invariant 3).

**Cost:** per gesture, per pattern: two resamples + P deltas. With 90 gestures and ~1.03 patterns each that is ~9,300 angle comparisons per stroke; microseconds. Caching the stroke's angle array once (the original recomputes it per pattern) is the only optimisation worth making. Templates' angle arrays can be cached per precision too.

**Bounding box** of the stroke is computed during the stroke's resample (`bActualGesture`) and used elsewhere for regions; irrelevant to matching.

## 2. Capture state machine (classic MouseProc), compared with what Augram decided

Classic SP uses a `WH_MOUSE_LL` hook. Injected events it created carry `dwExtraInfo == 123456` and are passed through (SharpHook's `IsEventSimulated` plays this role in Augram).

| Event | Classic SP behaviour | Augram |
|---|---|---|
| Stroke button down | Pass through if over one of SP's own dialogs (other than the draw window) or over an ignored window (checked by `WindowFromPoint`). Otherwise consume, record start point, capture modifier state (`CaptureModifiersOnMouseDown`), start the cancel timer. | Same, incl. "own windows are ignored" (checklist A6). |
| Move | Point stored only if > `iMinSegmentLength` (6 px) from the last stored point; each stored point restarts the cancel timer; segment drawn with GDI `LineTo` on the overlay. | Same decimation. SP.net added `MinGestureStartDistance` (Joel: 30 px) as the click/gesture decision; classic has only the 6 px rule. |
| Stroke button up, **no points added** and no modifiers/wheel/other buttons | **Click passthrough:** inject button down + up at the *current* cursor position via `mouse_event` (honours `SM_SWAPBUTTON`), with a guard flag so the hook ignores the injected pair. | Same. |
| Stroke button up, points added | Fire `gGestureComplete` on a new thread (match + execute); the up event is consumed. | Same, via channel to the engine worker. |
| **Cancel timer fires** (held still for `CancelDelay`, default 500 ms; reset on movement, modifier, wheel) | Cancel the gesture, erase the trail, **move the cursor back to the start point and inject the stroke-button DOWN there**, then move the cursor to where it is now: the hold becomes a real press/drag for the app underneath. Comment in source: "right-click dragging from a file in Explorer, then letting the gesture timeout threshold pass will cause the mouse to now have the file attached to it". | Joel decided **cancel only** (A12/A13). SP.net exposes this as `DisableRelayOnGestureTimeout`, which Joel set to true. The classic behaviour is the natural later option ("hold still to drag"). |
| Wheel while stroke button held (`FireOnMouseWheel`) | **At any time during the hold, before or after movement:** consume the wheel event, kill the cancel timer, set `bMouseWheelFiring`, fire the gesture-complete path immediately with the wheel direction as the trigger. Every tick fires again (volume repeats). While `bMouseWheelFiring`, further movement is not recorded and the trail stops. On button up: nothing else fires, state is cleared, the up is consumed. | Same semantics. GestureSign only accepts the wheel before movement; the SP behaviour is what Joel's 73 k volume fires expect. |
| Other mouse button while stroke button held | Consumed and recorded as a **rocker modifier** (`bLeftMouseDown` etc.); its release is consumed too; restarts the cancel timer. Not a cancel. | Joel decided other-button = **cancel** (A12). Rocker deferred. |
| Keyboard modifier while held | Recorded (`bControlDown` …), restarts the cancel timer. Also the "Ignore Key": holding it means the stroke button passes through untouched. | Ignore Key kept (Joel: Control). Modifier chords deferred. |
| Cursor hidden (`CURSORINFO.flags == 0`, option `CheckCursorFlags`) or off-screen | Pass through. Hidden cursor = many games, Input Director. | Worth keeping as an option; Joel has it off. |
| Wheel with no button held (`MouseWheelRelay`) | Optionally re-posts `WM_MOUSEWHEEL` to the window under the cursor, with current key/button states, so unfocused windows scroll. | Not needed on Windows 10+ (OS scrolls unfocused windows). Skip. |

Capture state is ~25 booleans plus the point list; `clearCaptureVars` lists them all and is the checklist for what Augram's `CaptureStateMachine` must reset.

## 3. Trail

Classic SP draws on a full-virtual-screen window using GDI `LineTo` per stored point and erases by retracing the points with a background pen (`ClearGesture`). `iMaxDrawPoints` gives a "streamer" that erases the oldest segment as new ones are added. SP.net later added `UseAlternativeDrawingMethod` and `Enable2DTextRendering` options for machines where this flickered. Augram's overlay (F6) is a different implementation; the only thing to take is the streamer as a possible style option.

## 4. Glyph normalisation (`ScaleGesture`)

Bounding box of the points, scale by the longer axis into a 125×125 box, translate to origin. GestureSign and SP.net do the same with an arrowhead added. This is F4's algorithm.

## 5. What else is in the file (skip)

Lua engine and bindings, actions library (`Action Functions`, 3,500 lines: window ops, clipboard, volume mixer via `Mixer`, SendKeys interpreter), Synaptics touchpad, touch/pen, config save/load as XML property tree, all the dialogs. The `SendKeys` region (8595–9162) documents the classic key-syntax the importer may meet in old scripts; SP.net's own `SendKeys` steps use .NET `SendKeys` syntax instead.
