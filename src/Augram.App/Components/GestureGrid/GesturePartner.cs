using Augram.Core.Gestures;

namespace Augram.App.Components.GestureGrid;

/// <summary>Another gesture the tile's gesture is likely to be confused with (A7), and how closely they score.</summary>
public sealed record GesturePartner(GestureId Id, string Name, double Score);
