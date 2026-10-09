# 0004 — Gesture shape cleanup: first measurements (2026-10-09, Mac session)

Plan 0001 M2 step 10, requirements F3 "Shape cleanup". The algorithm is `Core/Gestures/Cleanup/ShapeCleanup`; this note holds what was measured before trusting it.

## What the algorithm does

1. Resample the stroke evenly, spacing = bounding-box diagonal / 40 (ShortStraw's choice).
2. Find corners on a lightly smoothed copy: ShortStraw's straw (distance between the points 3 steps either side) below 0.95 × median, **and** a real turn of at least 35° at that point. Without the turn test and the smoothing, a 2.5 px hand wobble on a straight line produced nine pieces.
3. ShortStraw's refinements: add a corner where a piece is neither straight nor an arc; drop a corner whose neighbours make one straight line.
4. Fit each piece, on the points as drawn: **Line** when the points stray from the chord by at most 3 % of its length on average (a path-length ratio was thrown off by the wobble itself); **Arc** when a least-squares circle (Kåsa) fits within 8 % of its radius and the piece sweeps at least 30°; **Circle** when the whole stroke is one such piece that closes (ends within 35 % of the radius, ≥ 315° swept); otherwise **Smooth** (two passes of a 3-point average, ends kept).
5. Lines keep their drawn ends, so their angle is never snapped; arcs and circles run in the drawn direction from the drawn start.

## Joel's set (92 gestures, 95 samples, from the Mac's augram.json)

Piece breakdown, by eye: flicks, L's, V's, Z, the multi-segment ↕/↔/\/ strokes, Square, T, 7, A and the triangles' reverse came out as clean lines with corners where drawn; C, C Reverse, the loops, J Reverse and U reverse as arcs; O and O Reverse as circles. Suspect results to look at in the preview: Triangle (Arc Smooth Line), \Up \Down (Arc Arc), x (three arcs), Right Up and Up Right Left (a stray arc between lines).

Recognition (Corrected scoring, default options), each drawn sample matched against the raw and against the cleaned library:

| | raw templates | cleaned templates |
|---|---|---|
| samples recognised as their own gesture (top 1) | 95 / 95 | 95 / 95 |
| mean score of the right gesture | 99.7 | 97.0 |
| mean lead over the runner-up | 16.5 | 13.9 |
| confusion pairs (`ConfusionCheck`, cut-off 90) | 3 | 6 |

**Caveat:** this is biased towards raw: each raw template is scored against the very stroke it was made from. The fair test is fresh strokes. Cleanup also pulls gestures built from straight segments closer together (more pairs above the cut-off), so the per-gesture choice (Clean up shape / Restore original) matters.

## Next measurements

- Record the strokes Joel draws in daily use (the Recognition log holds them in memory only) and replay them against raw and cleaned templates.
- Look at the suspect gestures in the Gallery's cleanup preview, and tune the line deviation (3 %), arc tolerance (8 %) and corner turn (35°) if they misjudge Joel's drawing (requirements: "a strength setting if the line/arc/corner choices misjudge").
