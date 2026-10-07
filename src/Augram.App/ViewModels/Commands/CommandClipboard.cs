using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The in-memory clipboard of the Commands tab (F5a Copy/Paste), one for both sub-tabs so a Global
/// command copied on one pastes into an app group on the other: one command and one step, kept
/// separately so copying a step never loses a copied command. Records are immutable, so a paste
/// reuses them as they are. Putting the item on the system clipboard as Augram JSON (F5a leaning)
/// is a later slice.
/// </summary>
public sealed class CommandClipboard
{
    public Command? Command { get; set; }

    public CommandStep? Step { get; set; }
}
