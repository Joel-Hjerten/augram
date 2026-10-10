using System.Globalization;
using Augram.Core.Mapping;

namespace Augram.Core.Transfer;

/// <summary>
/// What a <see cref="TransferFile"/> holds, counted for the export dialog and the import review (plan 0003, decisions 16, 18).
/// <see cref="AppGroups"/> leaves Global out; <see cref="HasGlobal"/> is true when the file's Global has commands or
/// categories (an empty one is the shell every file has). <see cref="PrivateTextSteps"/> counts the steps, own versions
/// included, whose type may hold private text (<see cref="Steps.IStepType.MayHoldPrivateText"/>): they travel as written.
/// </summary>
public sealed record TransferContents(
    int Gestures,
    int AppGroups,
    bool HasGlobal,
    int Categories,
    int HoldRemaps,
    int Commands,
    int IgnoredApps,
    bool HasSettings,
    int PrivateTextSteps)
{
    public static TransferContents Of(TransferFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var mapping = file.MappingOrEmpty;
        var commands = mapping.Groups.SelectMany(group => group.Commands).ToArray();
        int privateText = commands
            .SelectMany(command => command.Steps.Concat(command.OwnVersion?.Steps ?? []))
            .Count(step => step.Step.Type.MayHoldPrivateText);
        return new TransferContents(
            file.Gestures.Count,
            mapping.Groups.Count(group => !group.IsGlobal),
            mapping.Groups.Any(group => group.IsGlobal && !IsShell(group)),
            mapping.Groups.Sum(group => group.Categories.Count),
            mapping.Groups.Sum(group => group.HoldRemaps.Count),
            commands.Length,
            mapping.Ignored.Count,
            file.Settings is not null,
            privateText);
    }

    /// <summary>True for a group with nothing in it but its header: the Global shell of a file that did not export Global.</summary>
    public static bool IsShell(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Commands.Count == 0 && group.Categories.Count == 0 && group.HoldRemaps.Count == 0;
    }

    /// <summary>"options, 90 gestures, Global, 19 app groups, 212 commands, 3 excluded apps": the parts that are there, for a dialog line and the log.</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (HasSettings)
        {
            parts.Add("options");
        }

        Add(parts, Gestures, "gesture");
        if (HasGlobal)
        {
            parts.Add(AppGroup.GlobalName);
        }

        Add(parts, AppGroups, "app group");
        Add(parts, HoldRemaps, "hold remap");
        Add(parts, Commands, "command");
        Add(parts, IgnoredApps, "excluded app");
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    private static void Add(List<string> parts, int count, string noun)
    {
        if (count > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}"));
        }
    }
}
