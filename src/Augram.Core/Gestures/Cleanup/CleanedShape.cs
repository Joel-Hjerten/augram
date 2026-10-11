namespace Augram.Core.Gestures.Cleanup;

/// <summary>A cleaned stroke: the new raw points, the corners found (where the pieces meet) and each piece's kind, in order.</summary>
public sealed record CleanedShape(IReadOnlyList<GesturePoint> Points, IReadOnlyList<GesturePoint> Corners, IReadOnlyList<PieceKind> Pieces);
