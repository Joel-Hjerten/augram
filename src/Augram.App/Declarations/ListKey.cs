namespace Augram.App.Declarations;

/// <summary>A key gesture (Avalonia syntax, e.g. <c>Ctrl+C</c>, <c>Delete</c>) bound to a list action.</summary>
public sealed record ListKey(string Gesture, ListAction Action);
