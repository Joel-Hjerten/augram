using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The "Also in" row of <see cref="CommandHeader"/> (Joel, 2026-10-10, plan 0005 decision 7), after the Not in row, on a command
/// whose trigger holds no stroke button (<see cref="CommandItem.CanSetAlsoIn"/>): the Exclusions › Global entries it still works
/// over, by name ("Blender", or "none"; <see cref="CommandItem.AlsoInText"/>), after <c>PART_AlsoInEdit</c> ("Change…"), which
/// asks the host to open the check list (<see cref="CommandTreeAction.EditAlsoIn"/>). The button sits before the names so a
/// longer list never moves it.
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>The ⓘ beside "Also in".</summary>
    public const string AlsoInHelp = "Over these excluded apps the command still works: only its own buttons are held back there; the stroke button stays the app's.";

    public static readonly StyledProperty<bool> CanSetAlsoInProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanSetAlsoIn));

    public static readonly StyledProperty<string> AlsoInTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(AlsoInText), string.Empty);

    /// <summary>Shows the Also in row: a command not under a hold remap whose trigger holds no stroke button.</summary>
    public bool CanSetAlsoIn
    {
        get => GetValue(CanSetAlsoInProperty);
        private set => SetValue(CanSetAlsoInProperty, value);
    }

    /// <summary>The Exclusions › Global entries the command still works over, by name, or "none".</summary>
    public string AlsoInText
    {
        get => GetValue(AlsoInTextProperty);
        private set => SetValue(AlsoInTextProperty, value);
    }

    /// <summary>What Change… does: asks the host to let the user tick the Exclusions › Global entries the command still works over.</summary>
    public void EditAlsoIn()
    {
        if (Item is { CanSetAlsoIn: true } item)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: item));
        }
    }

    private void FindAlsoInParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<Button>("PART_AlsoInEdit") is { } edit)
        {
            edit.Click += (_, _) => EditAlsoIn();
        }
    }

    private void ApplyAlsoIn()
    {
        var item = Item;
        CanSetAlsoIn = item is { CanSetAlsoIn: true };
        AlsoInText = item is { CanSetAlsoIn: true } ? item.AlsoInText : string.Empty;
    }
}
