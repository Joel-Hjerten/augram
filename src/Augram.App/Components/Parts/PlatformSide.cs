using Augram.Core.Abstractions;

namespace Augram.App.Components.Parts;

/// <summary>
/// What a <see cref="PlatformSideBar"/> shows (plan 0010; plan 0012): the side in view (<see cref="Shown"/>; the switch only
/// changes the view, and this computer's platform is outlined), whether it is this computer's (<see cref="IsHere"/>), the note
/// after the buttons ("Edit on Windows" on the other side, where the list comes from on this one; null for none), the Copy
/// button's text ("Copy from Windows"; null hides it) and whether Mark as checked shows.
/// </summary>
public sealed record PlatformSide(HostPlatform Shown, bool IsHere, string? Note, string? CopyText, bool CanMarkChecked = false);
