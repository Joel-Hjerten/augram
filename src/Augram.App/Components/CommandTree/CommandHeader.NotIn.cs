using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The "Not in" row of <see cref="CommandHeader"/> (Joel, 2026-10-10, plan 0004), on every command not under a hold remap
/// (<see cref="CommandItem.CanSetNotIn"/>): the Ignored › Per command entries it is not used over, by name ("Eyeris, Spine",
/// or "none"; <see cref="CommandItem.NotInText"/>), after <c>PART_NotInEdit</c> ("Change…"), which asks the host to open the
/// check list (<see cref="CommandTreeAction.EditNotIn"/>). The button sits before the names so a longer list never moves it.
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>The ⓘ beside "Not in".</summary>
    public const string NotInHelp = "Over these apps the command does nothing and holds no button back. The apps are the ones on Ignored › Per command.";

    public static readonly StyledProperty<bool> CanSetNotInProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanSetNotIn));

    public static readonly StyledProperty<string> NotInTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(NotInText), string.Empty);

    /// <summary>Shows the Not in row: a command not under a hold remap.</summary>
    public bool CanSetNotIn
    {
        get => GetValue(CanSetNotInProperty);
        private set => SetValue(CanSetNotInProperty, value);
    }

    /// <summary>The Per command entries the command is not used over, by name, or "none".</summary>
    public string NotInText
    {
        get => GetValue(NotInTextProperty);
        private set => SetValue(NotInTextProperty, value);
    }

    /// <summary>What Change… does: asks the host to let the user tick the Per command entries the command is not used over.</summary>
    public void EditNotIn()
    {
        if (Item is { CanSetNotIn: true } item)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: item));
        }
    }

    private void FindNotInParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<Button>("PART_NotInEdit") is { } edit)
        {
            edit.Click += (_, _) => EditNotIn();
        }
    }

    private void ApplyNotIn()
    {
        var item = Item;
        CanSetNotIn = item is { CanSetNotIn: true };
        NotInText = item is { CanSetNotIn: true } ? item.NotInText : string.Empty;
    }
}
