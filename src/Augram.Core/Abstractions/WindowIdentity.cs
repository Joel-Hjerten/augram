namespace Augram.Core.Abstractions;

/// <summary>
/// What the platform knows about one top-level window at the moment it was asked (F5 identification fields).
/// Immutable; a stale snapshot is fine because the engine resolves identity once per stroke.
/// </summary>
/// <param name="Handle">The window under the point as the OS reported it (may be a child control).</param>
/// <param name="RootHandle">The root owner of <paramref name="Handle"/>: the window activation targets and A20 compares.</param>
/// <param name="ProcessName">Executable file name, e.g. <c>chrome.exe</c>; the primary matching field. Never empty.</param>
/// <param name="ProcessPath">Full image path when the OS lets us read it; null for protected processes.</param>
/// <param name="Title">Root window caption, null when it has none.</param>
/// <param name="ClassChain">Window classes walking own, parent, root, owner (distinct, non-empty); the Windows-specific escape hatch.</param>
/// <param name="ProcessId">OS process id of the process that owns the window (the real app for UWP hosts).</param>
/// <param name="IsFullScreen">A21: the root window covers its monitor exactly and is not the desktop.</param>
/// <param name="IsDesktop">The root is the desktop or shell window; A20 never activates it.</param>
public sealed record WindowIdentity(
    nint Handle,
    nint RootHandle,
    string ProcessName,
    string? ProcessPath,
    string? Title,
    IReadOnlyList<string> ClassChain,
    int ProcessId,
    bool IsFullScreen,
    bool IsDesktop);
