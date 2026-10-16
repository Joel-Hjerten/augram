using Augram.App.Components.CommandTree;
using Augram.App.Components.Parts;
using Avalonia;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The "Steps for" row (plan 0010), drawn as the first row of the Steps card (Joel, 0.14.3: as "Fields for" is of the App
/// identification card, so it reads as tied to the steps): a <see cref="PlatformSideBar"/> (its look and switch) over the
/// selected command. All of it comes from <see cref="Item"/>'s <see cref="CommandHeaderItem.StepSide"/>; on this computer's side
/// the note is the row's F8 sentence ("From Windows · 1 needs a macOS version"; Joel, 0.19.7: moved here from the command row,
/// which keeps a badge), on the other "Edit on Windows". Its asks leave as the tree's actions (<see cref="ActionRequested"/>:
/// ShowStepsFor, CopyStepsFromOtherPlatform, MarkOwnVersionChecked) naming the command.
/// </summary>
public sealed class StepSideBar : PlatformSideBar
{
    /// <summary>The Steps for row's ⓘ.</summary>
    public const string StepsForHelp = "This computer runs its platform's steps, and only those are edited here. The other platform's show read-only: they are its own, or converted from these until it has its own. Copy from replaces the steps in view with the other platform's.";

    public static readonly StyledProperty<CommandHeaderItem?> ItemProperty =
        AvaloniaProperty.Register<StepSideBar, CommandHeaderItem?>(nameof(Item));

    public StepSideBar()
    {
        Label = "Steps for";
        Help = StepsForHelp;
        SideRequested += (_, platform) => Ask(CommandTreeAction.ShowStepsFor, platform);
        CopyRequested += (_, _) => Ask(CommandTreeAction.CopyStepsFromOtherPlatform);
        MarkCheckedRequested += (_, _) => Ask(CommandTreeAction.MarkOwnVersionChecked);
    }

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    /// <summary>The selected command; its <see cref="CommandHeaderItem.StepSide"/> is what the row shows, and it is the command each request names.</summary>
    public CommandHeaderItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(PlatformSideBar);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            Side = Item?.StepSide is { } side
                ? new PlatformSide(side.Shown, side.IsHere, side.IsHere ? Sentence(Item.PlatformMarker) : side.EditNote, side.CopyText, side.CanMarkChecked)
                : null;
        }
    }

    private static string? Sentence(string? marker) => string.IsNullOrEmpty(marker) ? null : char.ToUpperInvariant(marker[0]) + marker[1..];

    private void Ask(CommandTreeAction action, Augram.Core.Abstractions.HostPlatform? platform = null)
    {
        if (Item is { } item)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, command: item, platform: platform));
        }
    }
}
