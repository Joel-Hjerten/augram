using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Controls;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The F8 "Use on" half of <see cref="CommandHeader"/> (<c>PART_UseOnWindows</c>, <c>PART_UseOnMac</c>): each box asks for
/// the command's own platforms with one flipped; a platform its group or category leaves out shows unchecked and disabled.
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>
    /// What a Use on box does (F8): asks the host for the command's own platforms with this one flipped; the boxes then show
    /// the answer. A platform its group or category leaves out (<see cref="CommandItem.UseOnLimit"/>) asks nothing: its box is
    /// disabled, and the command's own value for it is kept for when the category or group takes the platform back.
    /// </summary>
    public void ChooseUseOn(HostPlatform platform, bool used)
    {
        if (Item is { } item && item.UseOnLimit.Includes(platform) && item.UseOn.Includes(platform) != used)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, command: item, useOn: item.UseOn.With(platform, used)));
        }

        Apply();
    }

    private static void ShowUseOn(CheckBox? box, CommandItem? item, HostPlatform platform)
    {
        if (box is null)
        {
            return;
        }

        var allowed = item?.UseOnLimit.Includes(platform) ?? true;
        box.IsEnabled = allowed;
        box.IsChecked = allowed && (item?.UseOn.Includes(platform) ?? false);
    }

    private void WireUseOn(CheckBox? box, HostPlatform platform)
    {
        if (box is not null)
        {
            box.IsCheckedChanged += (_, _) =>
            {
                if (!_applying)
                {
                    ChooseUseOn(platform, box.IsChecked == true);
                }
            };
        }
    }
}
