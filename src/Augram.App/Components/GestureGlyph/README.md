# GestureGlyph

A gesture drawn as an icon (F4) at any size: `GestureGlyph` (points in, `StrokePoints` out, `:inactive`, `:empty`) drawn by its template's `GlyphStroke` (gradient by arc length from `StartBrush` to `Stroke`, arrowhead at the end; the arrow length per size from the theme). `GlyphGeometry` is the recipe as pure functions (normalise, stroke points, progress, arrow wings, `BuildRaw` for the draw area); `GlyphColours` derives the start colour from the trail colour. Direction always shows (rotation-sensitive recognition).
