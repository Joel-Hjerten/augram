using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The in-memory clipboard of the Commands tab (F5a Copy/Paste), one for both sub-tabs so a Global
/// command copied on one pastes into an app group on the other: one tree item (a command, or a hold remap with the commands
/// under it, plan 0002; copying one replaces the other, so the tree's Paste pastes what was copied last) and one step, kept
/// separately so copying a step never loses a copied command. Records are immutable, so a paste
/// reuses them as they are. Putting the item on the system clipboard as Augram JSON (F5a leaning)
/// is a later slice.
/// </summary>
public sealed class CommandClipboard
{
    public Command? Command
    {
        get;
        set
        {
            field = value;
            if (value is not null)
            {
                HoldRemap = null;
            }
        }
    }

    /// <summary>A copied hold remap and the commands under it.</summary>
    public HoldRemapCopy? HoldRemap
    {
        get;
        set
        {
            field = value;
            if (value is not null)
            {
                Command = null;
            }
        }
    }

    public CommandStep? Step { get; set; }
}
