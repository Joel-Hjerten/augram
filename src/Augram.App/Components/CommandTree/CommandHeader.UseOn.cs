using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The F8 "Use on" half of <see cref="CommandHeader"/> (<c>PART_UseOnWindows</c>, <c>PART_UseOnMac</c>): each box asks for
/// the command's own platforms with one flipped; a platform its group or category leaves out shows unchecked and disabled.
/// </summary>
public sealed partial class CommandHeader
{
    private CheckBox? _useOnWindows;
    private CheckBox? _useOnMac;

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

    private void FindUseOnParts(TemplateAppliedEventArgs e)
    {
        _useOnWindows = e.NameScope.Find<CheckBox>("PART_UseOnWindows");
        _useOnMac = e.NameScope.Find<CheckBox>("PART_UseOnMac");
        WireUseOn(_useOnWindows, HostPlatform.Windows);
        WireUseOn(_useOnMac, HostPlatform.MacOS);
    }

    /// <summary>Puts the boxes on the command's real values: checked where it is used, disabled (unchecked) where its group or category leaves the platform out.</summary>
    private void ApplyUseOn(CommandItem? item)
    {
        _applying = true;
        try
        {
            ShowUseOn(_useOnWindows, item, HostPlatform.Windows);
            ShowUseOn(_useOnMac, item, HostPlatform.MacOS);
        }
        finally
        {
            _applying = false;
        }
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
