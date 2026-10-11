# Core/Recognition

MIT port of the StrokesPlus classic / HighSign recognizer (`NOTICE.md` beside the code carries the licence and credits).

| Type | Role |
|---|---|
| `StrokeResampler` | raw points to at most P points evenly spaced by arc length (`GetInterpolatedPointArray`); `PathLength` (`GetPointArrayLength`, also shape cleanup's); the distance is `Gestures/GesturePoint.DistanceTo` |
| `AngleSequence` | resampled points to segment headings; angular delta; delta to 0–100 score |
| `SampleScorer` (internal) | one template vs one stroke under a `ScoringMode` |
| `TemplateCache` | per-sample angle sequences cached by gesture id, sample index, precision; each `GestureMatcher` owns one |
| `GestureMatcher` | `Rank` (every active gesture, best first, for the recognition log), `Best(ranked, options)` (the threshold rule, its one home: the first if its score is > threshold and > 0, else null; the Engine's `StrokeRecognizer` ranks once and asks it) and `Match` (`Best` of `Rank`) |
| `RecognitionOptions` | precision 100, threshold 75, `ScoringMode`, `SampleAggregation` |
| `MatchResult` | gesture id, name, score |

Invariants: rotation-SENSITIVE by design (up-flick is not down-flick); runs on button-up only (CLAUDE.md invariants 3 and 4); `ScoringMode.Corrected` (divide by the deltas compared, as StrokesPlus.net scores) is the default (Joel, 2026-10-09); `ScoringMode.Legacy` keeps the original's divide-by-precision dilution, for comparison. The comment at the top of `AngleSequence.cs` explains both. Read `docs/reference/strokesplus-classic-source.md` §1 before touching the math.

**May reference:** `Gestures`. **Referenced by:** Engine (worker), training session, recognition log.
