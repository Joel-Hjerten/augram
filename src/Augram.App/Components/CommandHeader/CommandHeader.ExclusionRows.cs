using Augram.App.Components.CommandTree;
using Augram.App.Components.Parts;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The header's two rows over the Exclusions tab's entries, one parameterised row (<see cref="ExclusionRow"/>) each shown with a
/// "Change…" button before the names, so a longer list never moves it, which asks the host to open the check list:
/// <list type="bullet">
/// <item>"Not in" (Joel, 2026-10-10, plan 0004), on every command not under a hold remap (<see cref="CommandItem.CanSetNotIn"/>):
/// the Exclusions › Per command entries it is not used over, by name ("Eyeris, Spine", or "none"; <see cref="CommandItem.NotInText"/>),
/// after <c>PART_NotInEdit</c> (<see cref="CommandTreeAction.EditNotIn"/>).</item>
/// <item>"Also in" (Joel, 2026-10-10, plan 0005 decision 7), after it, on a command whose trigger holds no stroke button
/// (<see cref="CommandItem.CanSetAlsoIn"/>): the Exclusions › Global entries it still works over ("Blender", or "none";
/// <see cref="CommandItem.AlsoInText"/>), after <c>PART_AlsoInEdit</c> (<see cref="CommandTreeAction.EditAlsoIn"/>).</item>
/// </list>
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>The ⓘ beside "Not in".</summary>
    public const string NotInHelp = "Over these apps the command does nothing and holds no button back. The apps are the ones on Exclusions › Per command.";

    /// <summary>The ⓘ beside "Also in".</summary>
    public const string AlsoInHelp = "Over these excluded apps the command still works: only its own buttons are held back there; the stroke button stays the app's.";

    public static readonly StyledProperty<bool> CanSetNotInProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanSetNotIn));

    public static readonly StyledProperty<string> NotInTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(NotInText), string.Empty);

    public static readonly StyledProperty<bool> CanSetAlsoInProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanSetAlsoIn));

    public static readonly StyledProperty<string> AlsoInTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(AlsoInText), string.Empty);

    // After the properties they name: static initialisers run in the order they are written.
    private static readonly ExclusionRow NotInRow =
        new("PART_NotInEdit", CommandTreeAction.EditNotIn, item => item.CanSetNotIn, item => item.NotInText, CanSetNotInProperty, NotInTextProperty);

    private static readonly ExclusionRow AlsoInRow =
        new("PART_AlsoInEdit", CommandTreeAction.EditAlsoIn, item => item.CanSetAlsoIn, item => item.AlsoInText, CanSetAlsoInProperty, AlsoInTextProperty);

    private static readonly ExclusionRow[] ExclusionRows = [NotInRow, AlsoInRow];

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

    /// <summary>What Not in's Change… does: asks the host to let the user tick the Per command entries the command is not used over.</summary>
    public void EditNotIn() => Edit(NotInRow);

    /// <summary>What Also in's Change… does: asks the host to let the user tick the Exclusions › Global entries the command still works over.</summary>
    public void EditAlsoIn() => Edit(AlsoInRow);

    private void Edit(ExclusionRow row)
    {
        if (Item is { } item && row.Shows(item))
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(row.Action, command: item));
        }
    }

    private void FindExclusionRowParts(TemplateAppliedEventArgs e)
    {
        foreach (var row in ExclusionRows)
        {
            e.OnClick(row.EditPart, () => Edit(row));
        }
    }

    private void ApplyExclusionRows()
    {
        var item = Item;
        foreach (var row in ExclusionRows)
        {
            var shows = item is not null && row.Shows(item);
            SetValue(row.ShowsProperty, shows);
            SetValue(row.TextProperty, shows ? row.Text(item!) : string.Empty);
        }
    }

    /// <summary>One row over an Exclusions list: its Change… part, the action it asks for, when the item shows it, its names, and the header's two properties the template binds.</summary>
    private sealed record ExclusionRow(
        string EditPart,
        CommandTreeAction Action,
        Func<CommandItem, bool> Shows,
        Func<CommandItem, string> Text,
        StyledProperty<bool> ShowsProperty,
        StyledProperty<string> TextProperty);
}
