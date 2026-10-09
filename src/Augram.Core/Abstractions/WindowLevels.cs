namespace Augram.Core.Abstractions;

/// <summary>
/// The caption and class of each window StrokesPlus.net's app definition matches on (Joel, 2026-10-09: every field, each
/// with Use Regex): the control under the point, its parent, the root of the parent chain, and the root owner (the window
/// <see cref="WindowIdentity.Title"/> and activation use). Windows only; on macOS every member is null (<see cref="None"/>).
/// A member is null when that window has no caption or class, or when there is no such window.
/// </summary>
public sealed record WindowLevels(
    string? ControlTitle,
    string? ControlClass,
    string? ParentTitle,
    string? ParentClass,
    string? RootTitle,
    string? RootClass,
    string? OwnerTitle,
    string? OwnerClass)
{
    public static WindowLevels None { get; } = new(null, null, null, null, null, null, null, null);
}
