using System.Reflection;
using System.Text.RegularExpressions;
using Augram.App.ViewModels.Commands;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// The Commands tab's view model delegates (plan 0007 S15; docs/reference/one-home-guards.md): the trigger draft to
/// <c>TriggerEditor</c>, the side panel to <c>SectionPanel</c>, steps to <c>StepEditor</c>, lookups to <c>CommandLookups</c>;
/// <c>ICommandsHost</c> is implemented only explicitly, and the collaborators stay internal to the view models.
/// </summary>
public sealed partial class OneHomeCommandsTests
{
    private const string Commands = "src/Augram.App/ViewModels/Commands/";

    private static readonly Dictionary<string, string> Homes = new(StringComparer.Ordinal)
    {
        ["TriggerDraft"] = "TriggerEditor",
        ["_draft"] = "TriggerEditor",
        ["DraftedTrigger"] = "TriggerEditor",
        ["WithDraft"] = "TriggerEditor",
        ["RefusalOf"] = "TriggerEditor",
        ["KeepDraft"] = "TriggerEditor",
        ["TrySaveTrigger"] = "TriggerEditor",
        ["SaveTrigger"] = "TriggerEditor",
        ["WithTriggerHere"] = "TriggerEditor",
        ["SettleConflict"] = "TriggerEditor",
        ["Exchanged"] = "TriggerEditor",
        ["Accepts"] = "TriggerEditor",
        ["CheckChange"] = "StepEditor",
        ["NewStep"] = "StepEditor",
        ["WithStepsFor"] = "StepEditor",
        ["RequireCommand"] = "CommandLookups",
        ["RequireGroup"] = "CommandLookups",
        ["RequireHoldRemap"] = "CommandLookups",
    };

    private static IEnumerable<string> ViewModelFiles
        => Code(Commands).Where(file => Path.GetFileName(file).StartsWith("CommandsViewModel", StringComparison.Ordinal));

    [Fact]
    public void CommandsViewModelDeclaresNoneOfItsCollaboratorsJobs()
    {
        var offenders = typeof(CommandsViewModel)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(member => Homes.ContainsKey(member.Name) || (member is FieldInfo field && Homes.ContainsKey(field.Name.TrimStart('_'))))
            .Select(member => $"{member.Name} (home: ViewModels/Commands/{Homes.GetValueOrDefault(member.Name) ?? Homes[member.Name.TrimStart('_')]})")
            .Distinct()
            .ToList();
        offenders.AddRange(typeof(CommandsViewModel).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType.Name.StartsWith("PanelEdit", StringComparison.Ordinal))
            .Select(field => $"{field.Name} (home: ViewModels/Commands/SectionPanel)"));

        Assert.True(offenders.Count == 0, "CommandsViewModel declares a collaborator's job; call the collaborator instead (plan 0007 S15):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void CommandsViewModelDelegatesStepsAndLookups()
        => AssertOnlyIn(
            CollaboratorWork(),
            ViewModelFiles,
            "Steps go through ViewModels/Commands/StepEditor (StepOffer.Check, the StepListAction switch), lookups through CommandLookups (\"no longer exists\") (plan 0007 S15).",
            new Allowed(Commands + "CommandsViewModel.Categories.cs", "leftover, fold later: a category has no RequireCategory in CommandLookups yet", "no longer exists"),
            new Allowed(Commands + "CommandsViewModel.HoldRemaps.cs", "leftover, fold later: the delete asks before Guard, so it writes the lookup's words itself", "no longer exists"));

    [Fact]
    public void CommandsHostIsImplementedOnlyExplicitly()
    {
        // The view binds these two anyway (the header's stroke button, the tree's selection); they satisfy the host as they are.
        string[] publicAnyway = ["get_StrokeButton", "get_SelectedCommandId"];
        var map = typeof(CommandsViewModel).GetInterfaceMap(typeof(ICommandsHost));
        var implicitMembers = map.TargetMethods.Where(method => !method.Name.Contains('.', StringComparison.Ordinal)).Select(method => method.Name).ToList();

        Assert.False(implicitMembers.Except(publicAnyway).Any(), $"ICommandsHost members implemented implicitly on CommandsViewModel: {string.Join(", ", implicitMembers.Except(publicAnyway))}. Implement them explicitly, so the collaborators' host stays off the view model's surface (plan 0007 S15).");
        Assert.Equal(publicAnyway, implicitMembers.Intersect(publicAnyway));
    }

    [Fact]
    public void TheCollaboratorsStayInsideTheViewModels()
    {
        Type[] collaborators = [typeof(TriggerEditor), typeof(SectionPanel), typeof(StepEditor), typeof(CommandLookups)];
        Assert.All(collaborators, type => Assert.False(type.IsPublic, $"{type.Name} is public; keep it internal (plan 0007 S15)."));
        AssertOnlyIn(
            CollaboratorName(),
            Code("src/Augram.App/Components", "src/Augram.App/Screens"),
            "Components and screens talk to CommandsViewModel, never to its collaborators (TriggerEditor, SectionPanel, StepEditor, CommandLookups) (plan 0007 S15).");
    }

    [GeneratedRegex(@"\bStepOffer\.Check\b|\bcase\s+StepListAction\.|\bStepListAction\.\w+\s*=>|no longer exists")]
    private static partial Regex CollaboratorWork();

    [GeneratedRegex(@"\b(?:TriggerEditor|SectionPanel|StepEditor|CommandLookups)\b")]
    private static partial Regex CollaboratorName();
}
