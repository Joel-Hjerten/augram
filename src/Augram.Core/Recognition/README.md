# Core/Recognition

MIT port of the StrokesPlus classic / HighSign recognizer (`NOTICE.md` beside the code carries the licence and credits).

| Type | Role |
|---|---|
| `StrokeResampler` | raw points to at most P points evenly spaced by arc length (`GetInterpolatedPointArray`) |
| `AngleSequence` | resampled points to segment headings; angular delta; delta to 0–100 score |
| `SampleScorer` (internal) | one template vs one stroke under a `ScoringMode` |
| `TemplateCache` | per-sample angle sequences cached by gesture id, sample index, precision |
| `GestureMatcher` | `Rank` (every active gesture, best first, for the recognition log) and `Match` (best only if score > threshold) |
| `RecognitionOptions` | precision 100, threshold 75, `ScoringMode`, `SampleAggregation` |
| `MatchResult` | gesture id, name, score |

Invariants: rotation-SENSITIVE by design (up-flick is not down-flick); runs on button-up only (CLAUDE.md invariants 3 and 4); `ScoringMode.Legacy` keeps the original's divide-by-precision dilution and is the default. The comment at the top of `AngleSequence.cs` explains both. Read `docs/reference/strokesplus-classic-source.md` §1 before touching the math.

**May reference:** `Gestures`. **Referenced by:** Engine (worker), training session, recognition log.
