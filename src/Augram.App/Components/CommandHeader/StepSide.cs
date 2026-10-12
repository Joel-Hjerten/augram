using Augram.Core.Abstractions;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The header's "Steps for: Windows · macOS" row (plan 0010): the side of the command's steps in view (<see cref="Shown"/>;
/// the switch only changes the view), its label ("macOS · runs here", "Windows · converted from macOS"), on the other
/// platform's side the note "Edit on Windows", the Copy button's text ("Copy from Windows") when both sides have steps of
/// their own, and whether Mark as checked shows (this platform's own steps, made before the original last changed). Built by
/// the view model (<c>CommandsViewModel.HeaderItem</c>).
/// </summary>
public sealed record StepSide(HostPlatform Shown, bool IsHere, string Label, string? EditNote, string? CopyText, bool CanMarkChecked);
