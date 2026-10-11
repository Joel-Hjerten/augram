using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Builds the <see cref="CommandTree"/>'s one flat list: each section's <see cref="SectionRow"/>, then a
/// <see cref="CommandRow"/> per command while the section is expanded. A nested section (a hold remap, listed right after its
/// app group's section) shows only while the section it is nested in is shown and expanded. Every row's events go to
/// <c>raise</c> with the section and command they belong to (a header's New command button as NewCommand for its section);
/// finding a row by identity is here too.
/// </summary>
internal static class CommandTreeRows
{
    public delegate void Raise(CommandTreeAction action, SectionItem section, CommandItem? command, string? name);

    public static List<Control> Build(IReadOnlyList<SectionItem> sections, Raise raise, EventHandler<PointerPressedEventArgs> pressed)
    {
        var rows = new List<Control>();
        var open = new HashSet<SectionId>();
        foreach (var section in sections)
        {
            if (section.Id.Parent is { } parent && !open.Contains(parent))
            {
                continue;
            }

            var header = new SectionRow { Item = section };
            header.ExpandToggled += (_, _) => raise(CommandTreeAction.ToggleExpanded, section, null, null);
            header.RenameCommitted += (_, name) => raise(CommandTreeAction.Rename, section, null, name);
            header.ActiveToggled += (_, _) => raise(CommandTreeAction.ToggleActive, section, null, null);
            header.NewCommandRequested += (_, _) => raise(CommandTreeAction.NewCommand, section, null, null);
            header.PointerPressed += pressed;
            rows.Add(header);
            if (!section.IsExpanded)
            {
                continue;
            }

            open.Add(section.Id);
            foreach (var command in section.Commands)
            {
                var row = new CommandRow { Item = command };
                row.RenameCommitted += (_, name) => raise(CommandTreeAction.Rename, section, command, name);
                row.ActiveToggled += (_, _) => raise(CommandTreeAction.ToggleActive, section, command, null);
                row.PointerPressed += pressed;
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>The command's row when <paramref name="command"/> is set, else the section's header; null when neither is shown.</summary>
    public static Control? Find(IReadOnlyList<Control> rows, SectionId? section, CommandId? command)
        => command is { } commandId ? Command(rows, commandId)
            : section is { } sectionId ? Section(rows, sectionId)
            : null;

    public static CommandRow? Command(IReadOnlyList<Control> rows, CommandId id)
        => rows.OfType<CommandRow>().FirstOrDefault(row => row.Item?.Id == id);

    public static SectionRow? Section(IReadOnlyList<Control> rows, SectionId id)
        => rows.OfType<SectionRow>().FirstOrDefault(row => row.Item?.Id == id);
}
