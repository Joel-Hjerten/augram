using Augram.Core.Abstractions;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The header's "Steps for: Windows · macOS" line (plan 0010): the side of the command's steps in view (<see cref="Shown"/>; the
/// switch only changes the view, and this computer's platform is outlined), on the other platform's side the note
/// <see cref="EditNote"/> ("Edit on Windows"), the Copy button's text ("Copy from Windows") when both sides have steps of their
/// own, and whether Mark as checked shows (this platform's own steps, made before the original last changed). One line, Copy
/// first after the switch so it never moves (Joel, 0.14.2). Built by the view model (<c>CommandsViewModel.HeaderItem</c>).
/// </summary>
public sealed record StepSide(HostPlatform Shown, bool IsHere, string? EditNote, string? CopyText, bool CanMarkChecked);
