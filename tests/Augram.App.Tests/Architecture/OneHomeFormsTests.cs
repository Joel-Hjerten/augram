using System.Text.RegularExpressions;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// One home per mapping rule and form mechanism (plan 0007 S13; docs/reference/one-home-guards.md): names joined for display,
/// gesture retargeting, an app matcher's summary, the Global rename rule, the side panel's form lifecycle, "Use on", checked
/// sets, their removable entries and an Exclusions entry's users, form dialogs, button and committed-text lines, and the nameless relay.
/// </summary>
public sealed partial class OneHomeFormsTests
{
    private const string App = "src/Augram.App/";

    private static IEnumerable<string> AppCode => Code("src/Augram.App");

    [Fact]
    public void NamesAreJoinedOnlyByCommandNames()
        => AssertOnlyIn(
            NameJoin(),
            Code("src"),
            "Use Core/Mapping/CommandNames.Label or .Parent instead of joining names with \" › \" (plan 0007 S13).",
            new Allowed("src/Augram.Core/Mapping/CommandNames.cs", "the home"),
            new Allowed("src/Augram.App/Components/SectionForm/SectionForm.cs", "a form's region path for the F1 inspector (screen › section › field), not a command's name"),
            new Allowed("src/Augram.App/Components/Shell/Shell.cs", "a tab's path (tab › sub-tab), not a command's name"));

    [Fact]
    public void GesturesAreRetargetedOnlyByTheMappingStore()
        => AssertOnlyIn(
            Retarget(),
            Code("src"),
            "Use MappingStore.UnbindGesture or RetargetGesture (Core/Mapping) instead of rewriting commands one by one (plan 0007 S13).",
            new Allowed("src/Augram.Core/Mapping/", "the home"));

    [Fact]
    public void ThePlatformSideRulesLiveInPlatformSides()
        => AssertOnlyIn(
            SideRule(),
            Code("src"),
            "Use Core/Mapping/PlatformSides (Command.IsInferredOn, EditableStepsFor; PlatformList<T>) instead of comparing an origin with a platform by hand (plan 0012).",
            new Allowed("src/Augram.Core/Mapping/Command.Trigger.cs", "the trigger half: the original's trigger converts from the origin (TriggerConversion), not a list's side"));

    [Fact]
    public void TheAppProjectsAMatcherOnlyThroughItsSummary()
        => AssertOnlyIn(
            MatcherSummary(),
            AppCode,
            "Use AppMatcher.Summary (Core) for what an app matcher matches on this platform (plan 0007 S13).");

    [Fact]
    public void TheGlobalRenameRuleIsTheMappingRules()
        => AssertOnlyIn(
            GlobalRename(),
            AppCode,
            "Use MappingRules.EnsureNotRenamingGlobal (Core/Mapping) and show its refusal (plan 0007 S13).");

    [Fact]
    public void SidePanelFormsFollowThroughPanelEdit()
        => AssertOnlyIn(
            HandWiredPanel(),
            Code(App + "ViewModels/Commands", App + "ViewModels/Ignored").Where(file => Path.GetFileName(file).StartsWith("CommandsViewModel", StringComparison.Ordinal) || Path.GetFileName(file).StartsWith("IgnoredViewModel", StringComparison.Ordinal) || Path.GetFileName(file) == "SectionPanel.cs"),
            "A side panel's form follows its item through ViewModels/PanelEdit (Commands: ViewModels/Commands/SectionPanel) (plan 0007 S13/S15).");

    [Fact]
    public void UseOnIsEditedOnlyByUseOnEdit()
        => AssertOnlyIn(
            UseOn(),
            AppCode,
            "Use ViewModels/UseOnEdit (its field and its PlatformSet) instead (plan 0007 S13).",
            new Allowed(App + "ViewModels/UseOnEdit.cs", "the home"));

    [Fact]
    public void CheckListsKeepTheirTicksInCheckedSet()
        => AssertOnlyIn(
            CheckedField(),
            Code(App + "ViewModels"),
            "Use ViewModels/CheckedSet<TId> (and its Items) instead of a HashSet of ticks or CheckListItems built by hand (plan 0007 S13).",
            new Allowed(App + "ViewModels/CheckedSet.cs", "the home"));

    [Fact]
    public void RemovableEntriesOfACheckListLiveInRemovableEntries()
        => AssertOnlyIn(
            RemovableBookkeeping(),
            Code(App + "ViewModels"),
            "Use ViewModels/Commands/RemovableEntries for a check list's \"unused\" entries and their ✕ instead of building a CheckListRemove or keeping removed ids by hand (Joel, 2026-10-12).",
            new Allowed(App + "ViewModels/Commands/RemovableEntries.cs", "the home"));

    [Fact]
    public void AnExclusionEntrysUsersAreLookedUpOnlyThroughUsersOf()
        => AssertOnlyIn(
            EntryUsers(),
            AppCode,
            "Use MappingDocument.UsersOf (Core) for the commands naming an Exclusions entry, Per command through their Not in and Global through their Also in, instead of scanning the commands' lists (Joel, 2026-10-12).");

    [Fact]
    public void FormDialogsAreRequestedThroughFormDialogs()
        => AssertOnlyIn(
            NewFormDialogRequest(),
            AppCode,
            "Use Dialogs/FormDialogs (with an IDialogForm) instead of building a FormDialogRequest (plan 0007 S13).",
            new Allowed(App + "Dialogs/", "the home"),
            new Allowed(App + "Sync/SyncJoinPresenter.cs", "the join dialog's request carries its own buttons"));

    [Fact]
    public void ScreensDeclareFieldsAndBuildNoControls()
        => AssertOnlyIn(
            ControlInScreen(),
            Code(App + "Screens"),
            "A screen declares fields: Declarations/ButtonsField for buttons on a line, CommittedTextField for a text committed on leaving it; a new kind of line is a field and its renderer in Components/Fields/ (plan 0007 S13).");

    [Fact]
    public void FieldLinesAreStyledOnlyByTheButtonsRenderer()
        => AssertOnlyIn(
            FieldLineClass(),
            Code("src/Augram.App").Concat(Markup("src/Augram.App")),
            "Use Declarations/ButtonsField (rendered by Components/Fields/Buttons) instead of the field-line / field-buttons classes (plan 0007 S13).",
            new Allowed(App + "Components/Fields/Buttons/", "the home"),
            new Allowed(App + "Themes/Default/Forms.axaml", "the classes' styles"),
            new Allowed(App + "Components/Steps/Run/RunStepForm.cs", "leftover, fold later: the Browse… line beside the program's text field is built by hand, not a ButtonsField"));

    [Fact]
    public void OnlyFormChangesRelaysChangesAsNameless()
        => AssertOnlyIn(
            NamelessRelay(),
            AppCode,
            "Use Declarations/FormChanges to raise one nameless change from several sources (plan 0007 S13).",
            new Allowed(App + "Declarations/FormChanges.cs", "the home"));

    // A list's side rule written by hand: an origin taken apart and compared with a platform ("Origin is { } origin && origin != platform").
    [GeneratedRegex(@"\bOrigin is (?:not )?\{ \} \w+ (?:&&|\|\|) \w+ [!=]= ")]
    private static partial Regex SideRule();

    // "a" + " › " + "b", $"{a} › {b}"; a fixed UI path ("Options › Sync") is not a join.
    [GeneratedRegex(@"""\s*›\s*""|\}\s*›\s*\{")]
    private static partial Regex NameJoin();

    // A command rewritten for a gesture: WithGestureReplaced, or an UpdateCommand loop over UsedBy.
    [GeneratedRegex(@"\.WithGestureReplaced\(|\bforeach\s*\([^\n]*\bUsedBy\([^\n]*\n(?:[^\n]*\n){0,4}?[^\n]*\bUpdateCommand\(")]
    private static partial Regex Retarget();

    [GeneratedRegex(@"\bEffectiveProcessNames\(|\bIsGuessedOn\(|\(guessed\)|matches nothing yet|""nothing on ")]
    private static partial Regex MatcherSummary();

    [GeneratedRegex(@"cannot be renamed")]
    private static partial Regex GlobalRename();

    [GeneratedRegex(@"\bbool\s+_(?:applying|syncing)\w*|\.PropertyChanged\s*\+=")]
    private static partial Regex HandWiredPanel();

    [GeneratedRegex(@"\bPlatformSet\.None\.With\(|""Use on""")]
    private static partial Regex UseOn();

    [GeneratedRegex(@"\bHashSet<[^>]+>\s+_checked\w*|\bnew\s+CheckListItem\s*\(")]
    private static partial Regex CheckedField();

    [GeneratedRegex(@"\bnew\s+CheckListRemove\s*\(|\bList<GroupId>\s+_removed\w*")]
    private static partial Regex RemovableBookkeeping();

    [GeneratedRegex(@"\.(?:NotIn|AlsoIn)\.Contains\(")]
    private static partial Regex EntryUsers();

    [GeneratedRegex(@"\bnew\s+FormDialogRequest\s*\(")]
    private static partial Regex NewFormDialogRequest();

    [GeneratedRegex(@"\bCustomField\b|\bnew\s+(?:Button|TextBox|StackPanel|DockPanel|Grid|TextBlock)\s*[({]")]
    private static partial Regex ControlInScreen();

    [GeneratedRegex(@"\bfield-(?:line|buttons)\b")]
    private static partial Regex FieldLineClass();

    // Subscribing to a source and raising a nameless change on every change it makes.
    [GeneratedRegex(@"PropertyChanged\s*\+=[^;\n]*Invoke\([^;\n]*(?:string\.Empty|""""|\bAny\b)|PropertyChanged\s*\+=[^;\n]*OnPropertyChanged\(\s*(?:string\.Empty|"""")")]
    private static partial Regex NamelessRelay();
}
